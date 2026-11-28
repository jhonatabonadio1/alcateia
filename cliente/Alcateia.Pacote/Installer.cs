using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Alcateia.Pacote;

public enum InstallPhase
{
	/// <summary>Olhando o que já está no disco.</summary>
	Checking,

	/// <summary>Montando os arquivos novos (baixando o que falta).</summary>
	Downloading,

	/// <summary>Pondo os arquivos novos no lugar e tirando os que saíram.</summary>
	Finishing,

	Done
}

/// <summary>Por onde a instalação anda. <c>Done</c>/<c>Total</c>: bytes da
/// fase (conferidos, ou escritos); <c>Downloaded</c>/<c>DownloadTotal</c>:
/// bytes que vieram da rede, do que precisa vir.</summary>
public readonly record struct InstallProgress(InstallPhase Phase, long Done, long Total, long Downloaded, long DownloadTotal, string File);

/// <summary>Uma instalação que não deu, com um recado que se mostra ao jogador.</summary>
public sealed class InstallException : Exception
{
	public InstallException(string message, Exception inner = null) : base(message, inner) { }
}

/// <summary>O que uma atualização vai custar, sem mexer em nada.</summary>
public sealed record UpdateEstimate(int Files, long WriteBytes, long DownloadBytes);

/// <summary>
/// Leva uma pasta até a versão de um manifesto, baixando só o que falta.
/// Instalar, atualizar e reparar são a mesma operação: "deixe esta pasta
/// igual a este manifesto".
///
/// **Como economiza download.** Cada arquivo do manifesto é uma lista de
/// pedaços com SHA-256. Antes de baixar um pedaço, procura-se ele no que já
/// está no disco: nos arquivos da versão instalada (o manifesto dela diz
/// onde cada pedaço está) e, para arquivo que ninguém registrou — uma
/// instalação antiga feita pelo instalador, ou um arquivo estragado —,
/// cortando o arquivo do mesmo jeito que o empacotador cortou
/// (<see cref="Chunker"/>). Só o que não se acha em lugar nenhum vem da
/// rede, em trechos seguidos de um arquivo de pacote (<c>Range</c>).
///
/// **Como não quebra o que está instalado.** O arquivo novo é montado ao
/// lado (<c>.baixando</c>), com cada pedaço conferido pelo SHA-256, e só
/// troca de lugar com o antigo quando todos estão prontos. Interrompido
/// (cancelou, caiu a rede, acabou a luz), o jogo de antes continua inteiro
/// e a próxima tentativa segue de onde parou: o que já está no
/// <c>.baixando</c> é conferido e aproveitado.
///
/// **O que o desinstalar apaga.** Só o que o manifesto instalado listou —
/// nunca a pasta inteira, caso alguém instale numa pasta com outras coisas
/// (a mesma regra do instalador antigo, <c>instalador/gerar_lista.py</c>).
///
/// O estado mora em <see cref="StateDir"/>, dentro da pasta: o manifesto
/// instalado e, por arquivo, o tamanho, a data e o SHA-256 de quando foi
/// escrito (arquivo com tamanho e data iguais não é lido de novo a cada
/// conferência; o "reparar" lê tudo).
/// </summary>
public sealed class Installer
{
	public const string StateDir = ".pacote";
	private const string ManifestFile = "manifest.json", RecordsFile = "files.json";
	private const string PartialSuffix = ".baixando", AsideMark = ".antigo-";

	/// <summary>Quanto se pede de uma vez ao servidor de arquivos.</summary>
	private const long MaxRun = 32L * 1024 * 1024;

	/// <summary>Folga de disco além do que os arquivos novos ocupam.</summary>
	private const long SpaceMargin = 64L * 1024 * 1024;

	private const int Attempts = 5;

	private readonly string _root, _state;
	private readonly IBlobSource _source;

	private sealed record FileRecord(long Size, long Mtime, string Hash);

	/// <summary>Um pedaço que já está no disco: em que arquivo e a partir de onde.</summary>
	private readonly record struct LocalChunk(string Disk, long Offset, int Size);

	/// <summary><paramref name="stateDir"/>: onde guardar o estado da
	/// instalação, se não for dentro da própria pasta (<see cref="StateDir"/>).
	/// O cliente usa para se atualizar no Mac: um arquivo a mais dentro do
	/// .app quebra o lacre da assinatura dele.</summary>
	public Installer(string root, IBlobSource source, string stateDir = null)
	{
		_root = Path.GetFullPath(root);
		_state = StatePath(_root, stateDir);
		_source = source;
	}

	private static string StatePath(string root, string stateDir) =>
		stateDir == null ? Path.Combine(root, StateDir) : Path.GetFullPath(stateDir);

	// ------------------------------------------------------------------ o que está instalado

	/// <summary>O manifesto da versão instalada na pasta, ou null se não há
	/// (nunca instalou por aqui, ou o estado se perdeu).</summary>
	public static Manifest Installed(string root, string stateDir = null)
	{
		string path = Path.Combine(StatePath(root, stateDir), ManifestFile);
		try
		{
			return File.Exists(path) ? Manifest.Parse(File.ReadAllText(path)) : null;
		}
		catch (Exception e) when (e is ManifestException or IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	/// <summary>
	/// Quanto custa ir até <paramref name="target"/>, olhando só o estado
	/// guardado (não lê os arquivos): quantos arquivos mudam e quanto se
	/// baixa. Pasta sem estado conta como instalação do zero — o que o
	/// <see cref="Apply"/> aproveitar dela vem de brinde.
	/// </summary>
	public UpdateEstimate Estimate(Manifest target)
	{
		Manifest old = Installed(_root, _state);
		Dictionary<string, FileRecord> records = LoadRecords();
		var have = new HashSet<string>();
		if (old != null)
			foreach (FileEntry f in old.Files)
				if (Trusted(records, f, out _))
					foreach (int i in f.Chunks) have.Add(old.Chunks[i].Hash);

		int files = 0;
		long write = 0, download = 0;
		foreach (FileEntry f in target.Files)
		{
			if (Trusted(records, f, out _)) continue;
			files++;
			write += f.Size;
			foreach (int i in f.Chunks)
				if (have.Add(target.Chunks[i].Hash)) download += target.Chunks[i].Stored;
		}
		return new UpdateEstimate(files, write, download);
	}

	// ------------------------------------------------------------------ instalar, atualizar, reparar

	/// <summary>
	/// Deixa a pasta igual a <paramref name="target"/>.
	/// <paramref name="deep"/>: não confia no estado guardado e lê todos os
	/// arquivos (o "verificar e reparar"). Cancelar deixa tudo pronto para
	/// continuar depois.
	/// </summary>
	public async Task Apply(Manifest target, bool deep, IProgress<InstallProgress> progress, CancellationToken cancel)
	{
		try
		{
			Directory.CreateDirectory(_root);
			Directory.CreateDirectory(_state);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			throw new InstallException($"Não consegui escrever em {_root}. Escolha outra pasta de instalação.", e);
		}
		CleanAside();

		Manifest old = Installed(_root, _state);
		Dictionary<string, FileRecord> records = LoadRecords();
		var report = new Reporter(progress);

		// 1. O que já está certo e o que precisa ser montado.
		List<FileEntry> rebuild = await Task.Run(() => FindStale(target, records, deep, report, cancel), cancel);

		// 2. Os pedaços que já estão no disco, e de onde cada arquivo novo continua.
		var local = new Dictionary<string, LocalChunk>();
		var resume = new Dictionary<string, (int Index, long Offset)>();
		await Task.Run(() => IndexLocal(target, old, records, rebuild, deep, local, resume, report, cancel), cancel);

		// 3. Quanto falta escrever e baixar; cabe no disco?
		long writeTotal = 0, downloadTotal = 0;
		var coming = new HashSet<string>();
		foreach (FileEntry f in rebuild)
		{
			(int from, long offset) = resume[f.Path];
			writeTotal += f.Size - offset;
			for (int i = from; i < f.Chunks.Length; i++)
			{
				ChunkInfo c = target.Chunks[f.Chunks[i]];
				if (!local.ContainsKey(c.Hash) && coming.Add(c.Hash)) downloadTotal += c.Stored;
			}
		}
		RequireSpace(writeTotal);

		// 4. Monta cada arquivo novo ao lado do antigo.
		report.Start(InstallPhase.Downloading, writeTotal, downloadTotal);
		byte[] data = new byte[Chunker.Max], stored = new byte[Chunker.Max * 2];
		await BuildAll(target, rebuild, resume, local, data, stored, report, cancel);

		// 5. Troca os novos pelo antigos, tira o que saiu do produto e guarda o estado.
		report.Start(InstallPhase.Finishing, rebuild.Count, 0);
		foreach (FileEntry f in rebuild)
		{
			cancel.ThrowIfCancellationRequested();
			string disk = Manifest.OnDisk(_root, f.Path);
			Replace(disk + PartialSuffix, disk);
			records[f.Path] = Record(disk, f.Hash);
			SaveRecords(records);
			report.Step(1, 0, f.Path);
		}
		foreach (FileEntry f in target.Files) SetExec(f);

		if (old != null)
		{
			var kept = new HashSet<string>(target.Files.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
			foreach (FileEntry gone in old.Files.Where(f => !kept.Contains(f.Path)))
			{
				DeleteQuietly(Manifest.OnDisk(_root, gone.Path));
				records.Remove(gone.Path);
			}
			PruneEmptyDirs(old.Files.Select(f => f.Path));
		}
		foreach (string path in records.Keys.Where(p => target.Files.All(f => f.Path != p)).ToList()) records.Remove(path);
		SaveRecords(records);
		WriteAtomic(Path.Combine(_state, ManifestFile), target.ToJson());
		report.Start(InstallPhase.Done, 1, 0);
		report.Step(1, 0, "", force: true);
	}

	/// <summary>Quais arquivos do manifesto não estão no disco como deviam.</summary>
	private List<FileEntry> FindStale(Manifest target, Dictionary<string, FileRecord> records, bool deep, Reporter report, CancellationToken cancel)
	{
		var stale = new List<FileEntry>();
		report.Start(InstallPhase.Checking, deep ? target.InstallSize : target.Files.Count, 0);
		foreach (FileEntry f in target.Files)
		{
			cancel.ThrowIfCancellationRequested();
			string disk = Manifest.OnDisk(_root, f.Path);
			bool ok;
			if (!deep)
			{
				ok = Trusted(records, f, out _);
				report.Step(1, 0, f.Path);
			}
			else
			{
				bool sized = File.Exists(disk) && new FileInfo(disk).Length == f.Size;
				if (!sized) report.Step(f.Size, 0, f.Path);
				ok = sized && HashFile(disk, report, cancel) == f.Hash;
				if (ok) records[f.Path] = Record(disk, f.Hash);
				else records.Remove(f.Path);
			}
			if (!ok) stale.Add(f);
		}
		return stale;
	}

	/// <summary>
	/// Junta em <paramref name="local"/> os pedaços de que os arquivos novos
	/// precisam e que já estão no disco, e em <paramref name="resume"/> até
	/// onde o <c>.baixando</c> de cada um já chegou.
	/// </summary>
	private void IndexLocal(Manifest target, Manifest old, Dictionary<string, FileRecord> records, List<FileEntry> rebuild, bool deep,
		Dictionary<string, LocalChunk> local, Dictionary<string, (int Index, long Offset)> resume, Reporter report, CancellationToken cancel)
	{
		var needed = new HashSet<string>();
		foreach (FileEntry f in rebuild)
		{
			resume[f.Path] = (0, 0);
			foreach (int i in f.Chunks) needed.Add(target.Chunks[i].Hash);
		}
		if (needed.Count == 0) return;

		void AddLayout(Manifest of, FileEntry f)
		{
			string disk = Manifest.OnDisk(_root, f.Path);
			long offset = 0;
			foreach (int i in f.Chunks)
			{
				ChunkInfo c = of.Chunks[i];
				if (needed.Contains(c.Hash)) local.TryAdd(c.Hash, new LocalChunk(disk, offset, c.Size));
				offset += c.Size;
			}
		}

		// Arquivos em que se pode confiar: sabe-se onde está cada pedaço sem ler nada.
		var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (FileEntry f in target.Files)
			if (rebuild.All(r => r.Path != f.Path))
			{
				AddLayout(target, f);
				known.Add(f.Path);
			}
		if (old != null && !deep)
			foreach (FileEntry f in old.Files)
				if (!known.Contains(f.Path) && Trusted(records, f, out _))
				{
					AddLayout(old, f);
					known.Add(f.Path);
				}

		// Arquivos que estão no lugar mas ninguém garante (instalação antiga,
		// arquivo estragado): corta do mesmo jeito do empacotador e vê o que bate.
		List<FileEntry> unknown = rebuild.Where(f => !known.Contains(f.Path) && File.Exists(Manifest.OnDisk(_root, f.Path))).ToList();
		List<FileEntry> partial = rebuild.Where(f => File.Exists(Manifest.OnDisk(_root, f.Path) + PartialSuffix)).ToList();
		long scan = unknown.Sum(f => new FileInfo(Manifest.OnDisk(_root, f.Path)).Length)
			+ partial.Sum(f => new FileInfo(Manifest.OnDisk(_root, f.Path) + PartialSuffix).Length);
		report.Start(InstallPhase.Checking, scan, 0);

		foreach (FileEntry f in unknown)
		{
			string disk = Manifest.OnDisk(_root, f.Path);
			try
			{
				using FileStream file = Open(disk);
				Chunker.Split(file, (piece, _) =>
				{
					cancel.ThrowIfCancellationRequested();
					if (needed.Contains(piece.Hash)) local.TryAdd(piece.Hash, new LocalChunk(disk, piece.Offset, piece.Size));
					report.Step(piece.Size, 0, f.Path);
				});
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// Não deu para ler: os pedaços dele vêm da rede.
			}
		}

		// O que uma tentativa anterior já montou: confere pedaço a pedaço, do
		// começo, e continua do primeiro que faltar.
		byte[] buffer = new byte[Chunker.Max];
		foreach (FileEntry f in rebuild)
		{
			resume[f.Path] = (0, 0);
			string part = Manifest.OnDisk(_root, f.Path) + PartialSuffix;
			if (!File.Exists(part)) continue;
			try
			{
				using FileStream file = Open(part);
				int index = 0;
				long offset = 0;
				for (; index < f.Chunks.Length; index++)
				{
					cancel.ThrowIfCancellationRequested();
					ChunkInfo c = target.Chunks[f.Chunks[index]];
					if (ReadFully(file, buffer, c.Size) != c.Size || Hashes.Of(buffer.AsSpan(0, c.Size)) != c.Hash) break;
					local.TryAdd(c.Hash, new LocalChunk(part, offset, c.Size));
					offset += c.Size;
					report.Step(c.Size, 0, f.Path);
				}
				resume[f.Path] = (index, offset);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
			}
		}
	}

	/// <summary>
	/// Monta os arquivos novos, cada um no <c>.baixando</c> dele, a partir de
	/// onde a tentativa anterior parou.
	///
	/// Os pedaços que faltam e estão colados no pacote vêm num pedido só,
	/// **mesmo atravessando arquivos**: o jogo tem centenas de arquivos
	/// pequenos (as DLLs), e um pedido por arquivo custava mais na ida e volta
	/// do que no download. Uma instalação do zero são umas dezenas de pedidos.
	/// </summary>
	private async Task BuildAll(Manifest target, List<FileEntry> rebuild, Dictionary<string, (int Index, long Offset)> resume,
		Dictionary<string, LocalChunk> local, byte[] data, byte[] stored, Reporter report, CancellationToken cancel)
	{
		// Tudo o que falta escrever, na ordem: de que arquivo e que pedaço.
		var steps = new List<(FileEntry File, ChunkInfo Chunk)>();
		foreach (FileEntry f in rebuild)
		{
			for (int i = resume[f.Path].Index; i < f.Chunks.Length; i++) steps.Add((f, target.Chunks[f.Chunks[i]]));
			// Arquivo vazio, ou que a tentativa anterior já terminou: só garante o tamanho.
			if (resume[f.Path].Index >= f.Chunks.Length)
				await using (FileStream done = OpenPartial(f)) done.SetLength(f.Size);
		}

		FileEntry current = null;
		FileStream output = null;
		string part = null;
		long offset = 0;

		async Task Close()
		{
			if (output == null) return;
			await output.FlushAsync(cancel);
			await output.DisposeAsync();
			output = null;
		}

		async Task Write(FileEntry f, ChunkInfo c, int network)
		{
			if (f != current)
			{
				await Close();
				current = f;
				offset = resume[f.Path].Offset;
				output = OpenPartial(f);
				part = output.Name;
				output.SetLength(offset);
				output.Seek(offset, SeekOrigin.Begin);
			}
			await output.WriteAsync(data.AsMemory(0, c.Size), cancel);
			// O mesmo pedaço pode voltar noutro arquivo: daqui em diante ele está no disco.
			local.TryAdd(c.Hash, new LocalChunk(part, offset, c.Size));
			offset += c.Size;
			report.Step(c.Size, network, f.Path);
		}

		try
		{
			int i = 0;
			while (i < steps.Count)
			{
				cancel.ThrowIfCancellationRequested();
				(FileEntry f, ChunkInfo c) = steps[i];
				if (local.TryGetValue(c.Hash, out LocalChunk have))
				{
					if (output != null && have.Disk == part) await output.FlushAsync(cancel);
					if (ReadLocal(have, c, data))
					{
						await Write(f, c, 0);
						i++;
						continue;
					}
					local.Remove(c.Hash);   // o arquivo mudou por baixo: este pedaço vem da rede
				}

				var run = new List<ChunkInfo> { c };
				long size = c.Stored;
				int j = i + 1;
				for (; j < steps.Count; j++)
				{
					ChunkInfo next = steps[j].Chunk, last = run[^1];
					if (local.ContainsKey(next.Hash) || next.Blob != last.Blob || next.Offset != last.Offset + last.Stored || size + next.Stored > MaxRun) break;
					run.Add(next);
					size += next.Stored;
				}

				int at = i;
				await Download(target, run, data, stored, (chunk, network) => Write(steps[at++].File, chunk, network), cancel);
				i = j;
			}
		}
		finally
		{
			// Cancelado ou com erro, o que já foi escrito fica no disco para a próxima tentativa.
			if (output != null)
			{
				try { await output.FlushAsync(CancellationToken.None); } catch (IOException) { }
				await output.DisposeAsync();
			}
		}
	}

	private FileStream OpenPartial(FileEntry f)
	{
		string disk = Manifest.OnDisk(_root, f.Path);
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(disk));
			return new FileStream(disk + PartialSuffix, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, 1 << 20, FileOptions.Asynchronous);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			throw new InstallException($"Não consegui escrever {f.Path}. Feche o jogo e tente de novo.", e);
		}
	}

	/// <summary>Baixa pedaços colados num arquivo de pacote, conferindo cada um.
	/// Caiu a rede no meio, tenta de novo do pedaço em que parou.</summary>
	private async Task Download(Manifest target, List<ChunkInfo> run, byte[] data, byte[] stored, Func<ChunkInfo, int, Task> write, CancellationToken cancel)
	{
		string blob = target.Blobs[run[0].Blob].Name;
		int k = 0;
		for (int attempt = 1; ; attempt++)
		{
			try
			{
				long left = 0;
				for (int x = k; x < run.Count; x++) left += run[x].Stored;
				await using Stream stream = await _source.OpenRange(blob, run[k].Offset, left, cancel);
				for (; k < run.Count; k++)
				{
					ChunkInfo c = run[k];
					await stream.ReadExactlyAsync(stored.AsMemory(0, c.Stored), cancel);
					if (!Decode(c, stored, data)) throw new InvalidDataException($"Um pedaço de {blob} chegou diferente do que o manifesto diz.");
					await write(c, c.Stored);
				}
				return;
			}
			catch (Exception e) when (!cancel.IsCancellationRequested
				&& e is HttpRequestException or IOException or InvalidDataException or TaskCanceledException)
			{
				if (attempt >= Attempts)
					throw new InstallException(e is InvalidDataException
						? "O download chegou corrompido várias vezes. Tente de novo mais tarde."
						: "A conexão caiu durante o download. Confira a internet e continue: o que já baixou fica guardado.", e);
				await Task.Delay(TimeSpan.FromSeconds(Math.Min(15, attempt * attempt)), cancel);
			}
		}
	}

	/// <summary>Abre um pedaço como veio do pacote e confere o SHA-256.</summary>
	private static bool Decode(ChunkInfo c, byte[] stored, byte[] data)
	{
		if (c.Compressed)
		{
			if (!BrotliDecoder.TryDecompress(stored.AsSpan(0, c.Stored), data, out int written) || written != c.Size) return false;
		}
		else
		{
			if (c.Stored != c.Size) return false;
			Buffer.BlockCopy(stored, 0, data, 0, c.Size);
		}
		return Hashes.Of(data.AsSpan(0, c.Size)) == c.Hash;
	}

	/// <summary>Lê um pedaço de um arquivo do disco. Falso se ele não é mais o
	/// que se esperava (o arquivo mudou, encolheu, está em uso).</summary>
	private static bool ReadLocal(LocalChunk have, ChunkInfo c, byte[] data)
	{
		if (have.Size != c.Size) return false;
		try
		{
			using FileStream file = Open(have.Disk);
			file.Seek(have.Offset, SeekOrigin.Begin);
			return ReadFully(file, data, c.Size) == c.Size && Hashes.Of(data.AsSpan(0, c.Size)) == c.Hash;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return false;
		}
	}

	// ------------------------------------------------------------------ desinstalar

	/// <summary>
	/// Apaga o que o manifesto instalado listou, as sobras de download e o
	/// estado; pastas que ficaram vazias saem junto, e a de instalação
	/// também, se nada mais sobrou nela. Falso se a pasta não tinha uma
	/// instalação. O que o jogador salva não mora aqui (fica na pasta de
	/// dados do usuário) e não é tocado.
	/// </summary>
	public static bool Uninstall(string root, string stateDir = null)
	{
		root = Path.GetFullPath(root);
		Manifest installed = Installed(root, stateDir);
		if (installed == null) return false;

		var installer = new Installer(root, null, stateDir);
		foreach (FileEntry f in installed.Files)
		{
			string disk = Manifest.OnDisk(root, f.Path);
			DeleteQuietly(disk);
			DeleteQuietly(disk + PartialSuffix);
		}
		installer.CleanAside();
		installer.PruneEmptyDirs(installed.Files.Select(f => f.Path));
		try
		{
			Directory.Delete(installer._state, true);
			if (!Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
		}
		return true;
	}

	// ------------------------------------------------------------------ o estado

	private string RecordsPath => Path.Combine(_state, RecordsFile);

	private Dictionary<string, FileRecord> LoadRecords()
	{
		var records = new Dictionary<string, FileRecord>();
		try
		{
			if (!File.Exists(RecordsPath) || JsonNode.Parse(File.ReadAllText(RecordsPath)) is not JsonObject root) return records;
			foreach (var (path, node) in root)
				records[path] = new FileRecord(node["size"].GetValue<long>(), node["mtime"].GetValue<long>(), node["hash"].GetValue<string>());
		}
		catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or NullReferenceException)
		{
			records.Clear();   // estado ilegível: confere-se tudo de novo
		}
		return records;
	}

	private void SaveRecords(Dictionary<string, FileRecord> records)
	{
		var root = new JsonObject();
		foreach (var (path, r) in records)
			root[path] = new JsonObject { ["size"] = r.Size, ["mtime"] = r.Mtime, ["hash"] = r.Hash };
		WriteAtomic(RecordsPath, root.ToJsonString());
	}

	private static void WriteAtomic(string path, string text)
	{
		string temp = path + ".tmp";
		File.WriteAllText(temp, text);
		File.Move(temp, path, true);
	}

	private static FileRecord Record(string disk, string hash) =>
		new(new FileInfo(disk).Length, File.GetLastWriteTimeUtc(disk).Ticks, hash);

	/// <summary>O arquivo está no disco como o estado guardou (mesmo tamanho e
	/// data) e é o que o manifesto pede?</summary>
	private bool Trusted(Dictionary<string, FileRecord> records, FileEntry f, out string disk)
	{
		disk = Manifest.OnDisk(_root, f.Path);
		if (!records.TryGetValue(f.Path, out FileRecord r) || r.Hash != f.Hash || r.Size != f.Size) return false;
		var info = new FileInfo(disk);
		return info.Exists && info.Length == r.Size && info.LastWriteTimeUtc.Ticks == r.Mtime;
	}

	// ------------------------------------------------------------------ disco

	private static FileStream Open(string path) =>
		new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 20, FileOptions.SequentialScan);

	private static int ReadFully(Stream stream, byte[] buffer, int count)
	{
		int total = 0;
		while (total < count)
		{
			int read = stream.Read(buffer, total, count - total);
			if (read == 0) break;
			total += read;
		}
		return total;
	}

	private static string HashFile(string disk, Reporter report, CancellationToken cancel)
	{
		try
		{
			using FileStream file = Open(disk);
			using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
			byte[] buffer = new byte[1 << 20];
			int read;
			while ((read = file.Read(buffer, 0, buffer.Length)) > 0)
			{
				cancel.ThrowIfCancellationRequested();
				hash.AppendData(buffer, 0, read);
				report.Step(read, 0, Path.GetFileName(disk));
			}
			return Hashes.Hex(hash.GetHashAndReset());
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return "";
		}
	}

	private void RequireSpace(long bytes)
	{
		long free;
		try
		{
			free = new DriveInfo(Path.GetPathRoot(_root)).AvailableFreeSpace;
		}
		catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
		{
			return;   // não deu para medir: tenta, e a escrita diz se faltar
		}
		if (bytes + SpaceMargin > free)
			throw new InstallException($"Falta espaço em disco: são precisos {Sizes.Text(bytes + SpaceMargin)} livres e há {Sizes.Text(free)}.");
	}

	/// <summary>Põe o arquivo novo no lugar do antigo. Antigo em uso (no
	/// Windows, um programa aberto não se sobrescreve, mas troca de nome):
	/// sai da frente com outro nome e é apagado na próxima vez.</summary>
	private static void Replace(string part, string disk)
	{
		try
		{
			File.Move(part, disk, true);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			try
			{
				string aside = disk + AsideMark + Guid.NewGuid().ToString("N")[..8];
				File.Move(disk, aside);
				File.Move(part, disk);
				DeleteQuietly(aside);
			}
			catch (Exception again) when (again is IOException or UnauthorizedAccessException)
			{
				throw new InstallException($"Não consegui trocar {Path.GetFileName(disk)}. Feche o jogo e tente de novo.", again);
			}
		}
	}

	/// <summary>Apaga os arquivos que uma troca anterior tirou da frente.</summary>
	private void CleanAside()
	{
		try
		{
			if (!Directory.Exists(_root)) return;
			foreach (string path in Directory.EnumerateFiles(_root, "*" + AsideMark + "*", SearchOption.AllDirectories)) DeleteQuietly(path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
		}
	}

	private void SetExec(FileEntry f)
	{
		if (!f.Exec || OperatingSystem.IsWindows()) return;
		try
		{
			string disk = Manifest.OnDisk(_root, f.Path);
			File.SetUnixFileMode(disk, File.GetUnixFileMode(disk) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
		}
	}

	private static void DeleteQuietly(string path)
	{
		try
		{
			if (File.Exists(path)) File.Delete(path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
		}
	}

	/// <summary>Tira as pastas que ficaram vazias, das mais fundas para as de fora.</summary>
	private void PruneEmptyDirs(IEnumerable<string> paths)
	{
		var dirs = new HashSet<string>();
		foreach (string path in paths)
			for (string dir = Path.GetDirectoryName(Manifest.OnDisk(_root, path)); dir != null && dir.Length > _root.Length; dir = Path.GetDirectoryName(dir))
				dirs.Add(dir);

		foreach (string dir in dirs.OrderByDescending(d => d.Length))
		{
			try
			{
				if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any(e => Path.GetFileName(e) != ".DS_Store"))
					Directory.Delete(dir, true);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
			}
		}
	}

	// ------------------------------------------------------------------ progresso

	/// <summary>Junta o progresso e avisa no máximo umas dez vezes por segundo:
	/// milhares de pedaços não viram milhares de redesenhos.</summary>
	private sealed class Reporter
	{
		private readonly IProgress<InstallProgress> _progress;
		private readonly Stopwatch _since = Stopwatch.StartNew();
		private InstallPhase _phase;
		private long _done, _total, _downloaded, _downloadTotal;

		public Reporter(IProgress<InstallProgress> progress) => _progress = progress;

		public void Start(InstallPhase phase, long total, long downloadTotal)
		{
			_phase = phase;
			_done = 0;
			_total = total;
			_downloaded = 0;
			_downloadTotal = downloadTotal;
			Step(0, 0, "", force: true);
		}

		public void Step(long done, long downloaded, string file, bool force = false)
		{
			_done += done;
			_downloaded += downloaded;
			if (!force && _since.ElapsedMilliseconds < 100 && _done < _total) return;
			_since.Restart();
			_progress?.Report(new InstallProgress(_phase, _done, _total, _downloaded, _downloadTotal, file));
		}
	}
}

/// <summary>Tamanhos em texto: "1,4 GB", "320 MB".</summary>
public static class Sizes
{
	public static string Text(long bytes)
	{
		if (bytes >= 1L << 30) return $"{bytes / (double)(1L << 30):0.0} GB".Replace('.', ',');
		if (bytes >= 1L << 20) return $"{bytes / (double)(1L << 20):0} MB";
		if (bytes >= 1L << 10) return $"{bytes / (double)(1L << 10):0} KB";
		return $"{bytes} B";
	}
}
