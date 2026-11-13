using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Alcateia.Servidor.Banco;

/// <summary>Erro que o PostgreSQL devolveu: o código de cinco letras
/// (23505 = chave repetida) e a mensagem.</summary>
public sealed class PgException : Exception
{
	public string Code { get; }
	public PgException(string code, string message) : base(message) => Code = code;

	public bool IsUniqueViolation => Code == "23505";
}

/// <summary>O resultado de um comando: colunas, linhas (tudo texto, como
/// o banco manda; null é null) e quantas linhas ele mexeu.</summary>
public sealed class PgResult
{
	public string[] Columns = Array.Empty<string>();
	public readonly List<string[]> Rows = new();
	public int Affected;

	public bool Empty => Rows.Count == 0;
	public string[] First => Rows.Count > 0 ? Rows[0] : null;

	public string Text(int row, string column) => Rows[row][Index(column)];
	public long Long(int row, string column) => long.Parse(Text(row, column), CultureInfo.InvariantCulture);
	public int Int(int row, string column) => int.Parse(Text(row, column), CultureInfo.InvariantCulture);
	public bool Bool(int row, string column) => Text(row, column) == "t";

	private int Index(string column)
	{
		int i = Array.IndexOf(Columns, column);
		if (i < 0) throw new ArgumentException($"coluna '{column}' não veio na consulta");
		return i;
	}
}

/// <summary>
/// Uma conexão com o PostgreSQL, falando o protocolo dele (versão 3)
/// direto no soquete: entra com senha (SCRAM-SHA-256, a do Postgres 14
/// em diante, ou MD5), manda comandos com parâmetros separados do SQL
/// (protocolo estendido — o valor nunca é colado no texto do comando) e
/// lê as linhas como texto.
///
/// É o mínimo que o servidor precisa, e nada mais: sem TLS (o banco fica
/// na rede interna do Docker), sem tipos binários, sem COPY, sem
/// notificações. Uma conexão atende um comando por vez; quem cuida de
/// repartir é o <see cref="PgPool"/>.
/// </summary>
public sealed class PgConnection : IAsyncDisposable
{
	private TcpClient _tcp;
	private Stream _stream;
	private readonly byte[] _header = new byte[5];

	public bool Broken { get; private set; }

	public static async Task<PgConnection> Open(PgOptions o, CancellationToken cancel = default)
	{
		var c = new PgConnection { _tcp = new TcpClient { NoDelay = true } };
		try
		{
			await c._tcp.ConnectAsync(o.Host, o.Port, cancel);
			c._stream = new BufferedStream(c._tcp.GetStream(), 16 * 1024);
			await c.Startup(o, cancel);
			return c;
		}
		catch
		{
			await c.DisposeAsync();
			throw;
		}
	}

	/// <summary>Um comando com parâmetros: <c>$1</c>, <c>$2</c>... no SQL,
	/// os valores à parte. Devolve as linhas, se houver.</summary>
	public async Task<PgResult> Query(string sql, params object[] args)
	{
		try
		{
			var m = new MemoryStream();
			Message(m, 'P', w => { CString(w, ""); CString(w, sql); Int16(w, 0); });
			Message(m, 'B', w =>
			{
				CString(w, ""); CString(w, "");
				Int16(w, 0);
				Int16(w, (short)args.Length);
				foreach (object arg in args)
				{
					string text = ToText(arg);
					if (text == null) { Int32(w, -1); continue; }
					byte[] bytes = Encoding.UTF8.GetBytes(text);
					Int32(w, bytes.Length);
					w.Write(bytes);
				}
				Int16(w, 0);
			});
			Message(m, 'D', w => { w.WriteByte((byte)'P'); CString(w, ""); });
			Message(m, 'E', w => { CString(w, ""); Int32(w, 0); });
			Message(m, 'S', _ => { });
			await Send(m);
			return await ReadUntilReady();
		}
		catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException)
		{
			Broken = true;
			throw;
		}
	}

	/// <summary>Vários comandos num texto só, sem parâmetros: as migrações.</summary>
	public async Task Script(string sql)
	{
		try
		{
			var m = new MemoryStream();
			Message(m, 'Q', w => CString(w, sql));
			await Send(m);
			await ReadUntilReady();
		}
		catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException)
		{
			Broken = true;
			throw;
		}
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			if (_stream != null && !Broken)
			{
				var m = new MemoryStream();
				Message(m, 'X', _ => { });
				await Send(m);
			}
		}
		catch (Exception)
		{
			// Fechando: se o outro lado já foi embora, tanto faz.
		}

		_stream?.Dispose();
		_tcp?.Dispose();
		Broken = true;
	}

	// ------------------------------------------------------------ entrada

	private async Task Startup(PgOptions o, CancellationToken cancel)
	{
		var body = new MemoryStream();
		Int32(body, 196608);
		foreach (string s in new[] { "user", o.User, "database", o.Database, "client_encoding", "UTF8", "application_name", "alcateia" })
			CString(body, s);
		body.WriteByte(0);

		var packet = new MemoryStream();
		Int32(packet, (int)body.Length + 4);
		body.WriteTo(packet);
		await Send(packet);

		Scram scram = null;
		while (true)
		{
			(char type, byte[] data) = await Read();
			switch (type)
			{
				case 'R':
					int code = BinaryPrimitives.ReadInt32BigEndian(data);
					if (code == 0) break;
					if (code == 3) await Password(o.Password);
					else if (code == 5) await Password(Md5Password(o.User, o.Password, data.AsSpan(4, 4)));
					else if (code == 10)
					{
						if (!Encoding.ASCII.GetString(data, 4, data.Length - 4).Contains("SCRAM-SHA-256"))
							throw new PgException("28000", "o banco pediu um tipo de senha que este cliente não fala");
						scram = new Scram(o.Password);
						byte[] first = Encoding.UTF8.GetBytes(scram.ClientFirst());
						var m = new MemoryStream();
						Message(m, 'p', w => { CString(w, "SCRAM-SHA-256"); Int32(w, first.Length); w.Write(first); });
						await Send(m);
					}
					else if (code == 11)
					{
						byte[] final = Encoding.UTF8.GetBytes(scram.ClientFinal(Encoding.UTF8.GetString(data, 4, data.Length - 4)));
						var m = new MemoryStream();
						Message(m, 'p', w => w.Write(final));
						await Send(m);
					}
					else if (code == 12) scram.Verify(Encoding.UTF8.GetString(data, 4, data.Length - 4));
					else throw new PgException("28000", $"tipo de autenticação {code} não suportado");
					break;

				case 'E':
					throw Error(data);

				case 'Z':
					return;

				// 'S' (parâmetros do servidor), 'K' (chave de cancelamento), 'N' (aviso): não usamos.
			}

			cancel.ThrowIfCancellationRequested();
		}
	}

	private async Task Password(string password)
	{
		var m = new MemoryStream();
		Message(m, 'p', w => CString(w, password));
		await Send(m);
	}

	private static string Md5Password(string user, string password, ReadOnlySpan<byte> salt)
	{
		string inner = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password + user))).ToLowerInvariant();
		byte[] outer = new byte[inner.Length + 4];
		Encoding.ASCII.GetBytes(inner, outer);
		salt.CopyTo(outer.AsSpan(inner.Length));
		return "md5" + Convert.ToHexString(MD5.HashData(outer)).ToLowerInvariant();
	}

	// ------------------------------------------------------------ leitura

	private async Task<PgResult> ReadUntilReady()
	{
		var result = new PgResult();
		PgException error = null;

		while (true)
		{
			(char type, byte[] data) = await Read();
			switch (type)
			{
				case 'T':
					result.Columns = Columns(data);
					break;
				case 'D':
					result.Rows.Add(Row(data));
					break;
				case 'C':
					result.Affected = Affected(data);
					break;
				case 'E':
					error ??= Error(data);
					break;
				case 'Z':
					if (error != null) throw error;
					return result;
			}
		}
	}

	private async Task<(char, byte[])> Read()
	{
		await _stream.ReadExactlyAsync(_header);
		int length = BinaryPrimitives.ReadInt32BigEndian(_header.AsSpan(1)) - 4;
		if (length < 0 || length > 64 * 1024 * 1024) throw new IOException("mensagem do banco com tamanho absurdo");
		byte[] data = new byte[length];
		await _stream.ReadExactlyAsync(data);
		return ((char)_header[0], data);
	}

	private static string[] Columns(byte[] data)
	{
		int count = BinaryPrimitives.ReadInt16BigEndian(data);
		var names = new string[count];
		int at = 2;
		for (int i = 0; i < count; i++)
		{
			int end = Array.IndexOf(data, (byte)0, at);
			names[i] = Encoding.UTF8.GetString(data, at, end - at);
			at = end + 1 + 18;      // tabela, coluna, tipo, tamanho, modificador, formato
		}
		return names;
	}

	private static string[] Row(byte[] data)
	{
		int count = BinaryPrimitives.ReadInt16BigEndian(data);
		var values = new string[count];
		int at = 2;
		for (int i = 0; i < count; i++)
		{
			int length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(at));
			at += 4;
			if (length < 0) continue;
			values[i] = Encoding.UTF8.GetString(data, at, length);
			at += length;
		}
		return values;
	}

	/// <summary>"INSERT 0 3", "UPDATE 2", "SELECT 5": o último número.</summary>
	private static int Affected(byte[] data)
	{
		string tag = Encoding.ASCII.GetString(data, 0, Math.Max(data.Length - 1, 0));
		int space = tag.LastIndexOf(' ');
		return space >= 0 && int.TryParse(tag.AsSpan(space + 1), out int n) ? n : 0;
	}

	private static PgException Error(byte[] data)
	{
		string code = "", message = "erro do banco";
		int at = 0;
		while (at < data.Length && data[at] != 0)
		{
			char field = (char)data[at++];
			int end = Array.IndexOf(data, (byte)0, at);
			string value = Encoding.UTF8.GetString(data, at, end - at);
			at = end + 1;
			if (field == 'C') code = value;
			else if (field == 'M') message = value;
		}
		return new PgException(code, message);
	}

	// ------------------------------------------------------------ escrita

	private async Task Send(MemoryStream m)
	{
		m.Position = 0;
		await m.CopyToAsync(_stream);
		await _stream.FlushAsync();
	}

	private static void Message(MemoryStream to, char type, Action<MemoryStream> write)
	{
		var body = new MemoryStream();
		write(body);
		to.WriteByte((byte)type);
		Int32(to, (int)body.Length + 4);
		body.WriteTo(to);
	}

	private static void Int32(Stream s, int v)
	{
		Span<byte> b = stackalloc byte[4];
		BinaryPrimitives.WriteInt32BigEndian(b, v);
		s.Write(b);
	}

	private static void Int16(Stream s, short v)
	{
		Span<byte> b = stackalloc byte[2];
		BinaryPrimitives.WriteInt16BigEndian(b, v);
		s.Write(b);
	}

	private static void CString(Stream s, string text)
	{
		s.Write(Encoding.UTF8.GetBytes(text));
		s.WriteByte(0);
	}

	/// <summary>O valor como o Postgres o lê em texto. Null é NULL.</summary>
	private static string ToText(object v) => v switch
	{
		null => null,
		string s => s,
		bool b => b ? "true" : "false",
		DateTime d => d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture),
		DateTimeOffset d => d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture),
		IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
		_ => v.ToString()
	};

	/// <summary>A troca de mensagens do SCRAM-SHA-256 (RFC 5802), do lado
	/// do cliente: prova que sabemos a senha sem mandá-la, e confere que
	/// o servidor também sabe.</summary>
	private sealed class Scram
	{
		private readonly string _password;
		private readonly string _nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
		private string _firstBare;
		private byte[] _serverSignature;

		public Scram(string password) => _password = password.Normalize(NormalizationForm.FormKC);

		public string ClientFirst()
		{
			_firstBare = "n=,r=" + _nonce;
			return "n,," + _firstBare;
		}

		public string ClientFinal(string serverFirst)
		{
			string nonce = null, salt = null;
			int iterations = 0;
			foreach (string part in serverFirst.Split(','))
			{
				if (part.StartsWith("r=")) nonce = part[2..];
				else if (part.StartsWith("s=")) salt = part[2..];
				else if (part.StartsWith("i=")) iterations = int.Parse(part[2..], CultureInfo.InvariantCulture);
			}

			if (nonce == null || salt == null || iterations <= 0 || !nonce.StartsWith(_nonce))
				throw new PgException("28000", "resposta de autenticação inválida do banco");

			byte[] salted = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(_password), Convert.FromBase64String(salt),
				iterations, HashAlgorithmName.SHA256, 32);
			byte[] clientKey = HMACSHA256.HashData(salted, Encoding.ASCII.GetBytes("Client Key"));
			byte[] storedKey = SHA256.HashData(clientKey);

			string withoutProof = "c=biws,r=" + nonce;
			byte[] auth = Encoding.UTF8.GetBytes(_firstBare + "," + serverFirst + "," + withoutProof);
			byte[] signature = HMACSHA256.HashData(storedKey, auth);
			byte[] proof = new byte[clientKey.Length];
			for (int i = 0; i < proof.Length; i++) proof[i] = (byte)(clientKey[i] ^ signature[i]);

			_serverSignature = HMACSHA256.HashData(HMACSHA256.HashData(salted, Encoding.ASCII.GetBytes("Server Key")), auth);
			return withoutProof + ",p=" + Convert.ToBase64String(proof);
		}

		public void Verify(string serverFinal)
		{
			if (!serverFinal.StartsWith("v=") ||
				!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(serverFinal[2..]), _serverSignature))
				throw new PgException("28000", "o banco não provou que conhece a senha");
		}
	}
}

/// <summary>Onde está o banco e como entrar.</summary>
public sealed class PgOptions
{
	public string Host = "127.0.0.1";
	public int Port = 5432;
	public string User = "alcateia";
	public string Password = "";
	public string Database = "alcateia";
	public int MaxConnections = 16;
}

/// <summary>
/// As conexões com o banco, repartidas: quem precisa pega uma, usa e
/// devolve. Abre sob demanda até o teto; conexão que quebrou é jogada
/// fora e outra é aberta no lugar.
/// </summary>
public sealed class PgPool : IAsyncDisposable
{
	private readonly PgOptions _options;
	private readonly SemaphoreSlim _slots;
	private readonly Stack<PgConnection> _idle = new();
	private bool _closed;

	public PgPool(PgOptions options)
	{
		_options = options;
		_slots = new SemaphoreSlim(options.MaxConnections, options.MaxConnections);
	}

	public Task<PgResult> Query(string sql, params object[] args) => Use(c => c.Query(sql, args));

	public async Task<int> Exec(string sql, params object[] args) => (await Query(sql, args)).Affected;

	/// <summary>Empresta uma conexão para um trabalho inteiro.</summary>
	public async Task<T> Use<T>(Func<PgConnection, Task<T>> work)
	{
		await _slots.WaitAsync();
		PgConnection c = null;
		try
		{
			c = await Take();
			return await work(c);
		}
		finally
		{
			if (c != null) await Return(c);
			_slots.Release();
		}
	}

	/// <summary>Tudo ou nada: se <paramref name="work"/> lançar, o banco
	/// volta ao que era.</summary>
	public Task<T> Transaction<T>(Func<PgConnection, Task<T>> work) => Use(async c =>
	{
		await c.Script("BEGIN");
		try
		{
			T result = await work(c);
			await c.Script("COMMIT");
			return result;
		}
		catch
		{
			if (!c.Broken) await c.Script("ROLLBACK");
			throw;
		}
	});

	private async Task<PgConnection> Take()
	{
		lock (_idle)
		{
			while (_idle.Count > 0)
			{
				PgConnection c = _idle.Pop();
				if (!c.Broken) return c;
			}
		}

		return await PgConnection.Open(_options);
	}

	private async Task Return(PgConnection c)
	{
		if (c.Broken || _closed)
		{
			await c.DisposeAsync();
			return;
		}

		lock (_idle) _idle.Push(c);
	}

	public async ValueTask DisposeAsync()
	{
		_closed = true;
		PgConnection[] all;
		lock (_idle)
		{
			all = _idle.ToArray();
			_idle.Clear();
		}

		foreach (PgConnection c in all) await c.DisposeAsync();
	}
}
