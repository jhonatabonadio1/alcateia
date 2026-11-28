using System;
using System.Collections.Generic;
using System.Text;

namespace Alcateia.Cliente.Core;

/// <summary>
/// Um QR code, para o jogador apontar o aplicativo autenticador ao ligar a
/// verificação em duas etapas (digitar à mão uma chave de 32 letras no
/// celular é o que faz a pessoa desistir).
///
/// É o QR code comum (ISO 18004), no que este uso precisa: texto em bytes,
/// correção de erro média (M), versões 1 a 10 — até 213 bytes, e o endereço
/// do autenticador tem uns 120. Sem pacote: são a tabela das versões, o
/// Reed-Solomon e o desenho dos módulos.
/// </summary>
public sealed class QrCode
{
	/// <summary>Módulos por lado (sem a margem).</summary>
	public int Size { get; }

	private readonly bool[,] _dark, _fixed;

	public bool this[int x, int y] => x >= 0 && y >= 0 && x < Size && y < Size && _dark[y, x];

	// Por versão (1 a 10), correção M: bytes no total, bytes de correção por bloco, blocos.
	private static readonly int[] Total = { 26, 44, 70, 100, 134, 172, 196, 242, 292, 346 };
	private static readonly int[] EcPerBlock = { 10, 16, 26, 18, 24, 16, 18, 22, 22, 26 };
	private static readonly int[] Blocks = { 1, 1, 1, 2, 2, 4, 4, 4, 5, 5 };
	private static readonly int[][] Align =
	{
		Array.Empty<int>(), new[] { 6, 18 }, new[] { 6, 22 }, new[] { 6, 26 }, new[] { 6, 30 }, new[] { 6, 34 },
		new[] { 6, 22, 38 }, new[] { 6, 24, 42 }, new[] { 6, 26, 46 }, new[] { 6, 28, 50 }
	};

	private static int DataBytes(int version) => Total[version - 1] - EcPerBlock[version - 1] * Blocks[version - 1];

	/// <summary>O QR code de um texto. Lança se ele não cabe (mais de 213 bytes).</summary>
	public static QrCode Encode(string text)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
		int version = 1;
		// O cabeçalho: 4 bits do modo e o tamanho (8 bits até a versão 9, 16 na 10).
		while (version <= 10 && bytes.Length + (version < 10 ? 2 : 3) > DataBytes(version)) version++;
		if (version > 10) throw new ArgumentException("texto longo demais para o QR code");
		return new QrCode(version, bytes);
	}

	private QrCode(int version, byte[] bytes)
	{
		Size = version * 4 + 17;
		_dark = new bool[Size, Size];
		_fixed = new bool[Size, Size];

		DrawFixed(version);
		DrawData(Interleave(version, DataCodewords(version, bytes)));

		// A máscara que deixa o desenho mais fácil de ler.
		int best = 0;
		long least = long.MaxValue;
		for (int mask = 0; mask < 8; mask++)
		{
			ApplyMask(mask);
			DrawFormat(mask);
			long penalty = Penalty();
			if (penalty < least) (best, least) = (mask, penalty);
			ApplyMask(mask);   // desfaz (a máscara é um ou-exclusivo)
		}
		ApplyMask(best);
		DrawFormat(best);
	}

	// ------------------------------------------------------------------ os dados

	private static byte[] DataCodewords(int version, byte[] bytes)
	{
		var bits = new List<bool>();
		void Add(int value, int count)
		{
			for (int i = count - 1; i >= 0; i--) bits.Add((value >> i & 1) != 0);
		}

		Add(0b0100, 4);
		Add(bytes.Length, version < 10 ? 8 : 16);
		foreach (byte b in bytes) Add(b, 8);

		int capacity = DataBytes(version) * 8;
		Add(0, Math.Min(4, capacity - bits.Count));
		Add(0, (8 - bits.Count % 8) % 8);
		for (int pad = 0xEC; bits.Count < capacity; pad ^= 0xEC ^ 0x11) Add(pad, 8);

		byte[] data = new byte[bits.Count / 8];
		for (int i = 0; i < bits.Count; i++)
			if (bits[i]) data[i >> 3] |= (byte)(0x80 >> (i & 7));
		return data;
	}

	/// <summary>Divide em blocos, junta a correção de erro de cada um e intercala.</summary>
	private static byte[] Interleave(int version, byte[] data)
	{
		int blocks = Blocks[version - 1], ecLen = EcPerBlock[version - 1];
		int shortLen = data.Length / blocks, longBlocks = data.Length % blocks;
		byte[] divisor = RsDivisor(ecLen);

		var parts = new List<(byte[] Data, byte[] Ec)>();
		for (int i = 0, at = 0; i < blocks; i++)
		{
			int len = shortLen + (i >= blocks - longBlocks ? 1 : 0);
			byte[] part = data.AsSpan(at, len).ToArray();
			at += len;
			parts.Add((part, RsRemainder(part, divisor)));
		}

		var result = new List<byte>();
		for (int i = 0; i <= shortLen; i++)
			foreach ((byte[] part, _) in parts)
				if (i < part.Length) result.Add(part[i]);
		for (int i = 0; i < ecLen; i++)
			foreach ((_, byte[] ec) in parts) result.Add(ec[i]);
		return result.ToArray();
	}

	private static byte Mul(int x, int y)
	{
		int z = 0;
		for (int i = 7; i >= 0; i--)
		{
			z = z << 1 ^ (z >> 7) * 0x11D;
			z ^= (y >> i & 1) * x;
		}
		return (byte)z;
	}

	private static byte[] RsDivisor(int degree)
	{
		byte[] result = new byte[degree];
		result[degree - 1] = 1;
		int root = 1;
		for (int i = 0; i < degree; i++)
		{
			for (int j = 0; j < degree; j++)
			{
				result[j] = Mul(result[j], root);
				if (j + 1 < degree) result[j] ^= result[j + 1];
			}
			root = Mul(root, 2);
		}
		return result;
	}

	private static byte[] RsRemainder(byte[] data, byte[] divisor)
	{
		byte[] result = new byte[divisor.Length];
		foreach (byte b in data)
		{
			int factor = b ^ result[0];
			Array.Copy(result, 1, result, 0, result.Length - 1);
			result[^1] = 0;
			for (int i = 0; i < result.Length; i++) result[i] ^= Mul(divisor[i], factor);
		}
		return result;
	}

	// ------------------------------------------------------------------ o desenho

	private void Fix(int x, int y, bool dark)
	{
		if (x < 0 || y < 0 || x >= Size || y >= Size) return;
		_dark[y, x] = dark;
		_fixed[y, x] = true;
	}

	/// <summary>O que todo QR code tem no mesmo lugar: os três cantos, as
	/// linhas pontilhadas, os alvos pequenos e o lugar do formato e da versão.</summary>
	private void DrawFixed(int version)
	{
		for (int i = 0; i < Size; i++)
		{
			Fix(6, i, i % 2 == 0);
			Fix(i, 6, i % 2 == 0);
		}

		foreach ((int cx, int cy) in new[] { (3, 3), (Size - 4, 3), (3, Size - 4) })
			for (int dy = -4; dy <= 4; dy++)
				for (int dx = -4; dx <= 4; dx++)
				{
					int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
					Fix(cx + dx, cy + dy, dist != 2 && dist != 4);
				}

		int[] align = Align[version - 1];
		for (int i = 0; i < align.Length; i++)
			for (int j = 0; j < align.Length; j++)
			{
				// Onde estão os três cantos não há alvo pequeno.
				if (i == 0 && j == 0 || i == 0 && j == align.Length - 1 || i == align.Length - 1 && j == 0) continue;
				for (int dy = -2; dy <= 2; dy++)
					for (int dx = -2; dx <= 2; dx++)
						Fix(align[i] + dx, align[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
			}

		DrawFormat(0);   // reserva o lugar; o valor certo vem depois da máscara

		if (version >= 7)
		{
			int rem = version;
			for (int i = 0; i < 12; i++) rem = rem << 1 ^ (rem >> 11) * 0x1F25;
			int bits = version << 12 | rem;
			for (int i = 0; i < 18; i++)
			{
				bool bit = (bits >> i & 1) != 0;
				int a = Size - 11 + i % 3, b = i / 3;
				Fix(a, b, bit);
				Fix(b, a, bit);
			}
		}
	}

	/// <summary>Os 15 bits que dizem a correção de erro (M) e a máscara, duas vezes.</summary>
	private void DrawFormat(int mask)
	{
		int data = 0 << 3 | mask;   // M = 00
		int rem = data;
		for (int i = 0; i < 10; i++) rem = rem << 1 ^ (rem >> 9) * 0x537;
		int bits = (data << 10 | rem) ^ 0x5412;
		bool Bit(int i) => (bits >> i & 1) != 0;

		for (int i = 0; i <= 5; i++) Fix(8, i, Bit(i));
		Fix(8, 7, Bit(6));
		Fix(8, 8, Bit(7));
		Fix(7, 8, Bit(8));
		for (int i = 9; i < 15; i++) Fix(14 - i, 8, Bit(i));

		for (int i = 0; i < 8; i++) Fix(Size - 1 - i, 8, Bit(i));
		for (int i = 8; i < 15; i++) Fix(8, Size - 15 + i, Bit(i));
		Fix(8, Size - 8, true);
	}

	/// <summary>Os bits dos dados, em ziguezague de baixo para cima, da direita para a esquerda.</summary>
	private void DrawData(byte[] data)
	{
		int i = 0;
		for (int right = Size - 1; right >= 1; right -= 2)
		{
			if (right == 6) right = 5;
			for (int vert = 0; vert < Size; vert++)
				for (int j = 0; j < 2; j++)
				{
					int x = right - j;
					bool upward = (right + 1 & 2) == 0;
					int y = upward ? Size - 1 - vert : vert;
					if (_fixed[y, x] || i >= data.Length * 8) continue;
					_dark[y, x] = (data[i >> 3] >> (7 - (i & 7)) & 1) != 0;
					i++;
				}
		}
	}

	private void ApplyMask(int mask)
	{
		for (int y = 0; y < Size; y++)
			for (int x = 0; x < Size; x++)
			{
				if (_fixed[y, x]) continue;
				bool invert = mask switch
				{
					0 => (x + y) % 2 == 0,
					1 => y % 2 == 0,
					2 => x % 3 == 0,
					3 => (x + y) % 3 == 0,
					4 => (x / 3 + y / 2) % 2 == 0,
					5 => x * y % 2 + x * y % 3 == 0,
					6 => (x * y % 2 + x * y % 3) % 2 == 0,
					_ => ((x + y) % 2 + x * y % 3) % 2 == 0
				};
				if (invert) _dark[y, x] = !_dark[y, x];
			}
	}

	/// <summary>Quanto o desenho atrapalha o leitor: fileiras longas da mesma
	/// cor, quadrados cheios e desequilíbrio entre claro e escuro.</summary>
	private long Penalty()
	{
		long penalty = 0;
		for (int a = 0; a < Size; a++)
			for (int pass = 0; pass < 2; pass++)
			{
				int run = 1;
				for (int b = 1; b < Size; b++)
				{
					bool now = pass == 0 ? _dark[a, b] : _dark[b, a], before = pass == 0 ? _dark[a, b - 1] : _dark[b - 1, a];
					if (now == before) run++;
					else run = 1;
					if (run == 5) penalty += 3;
					else if (run > 5) penalty++;
				}
			}

		int dark = 0;
		for (int y = 0; y < Size; y++)
			for (int x = 0; x < Size; x++)
			{
				if (_dark[y, x]) dark++;
				if (x + 1 < Size && y + 1 < Size && _dark[y, x] == _dark[y, x + 1] && _dark[y, x] == _dark[y + 1, x] && _dark[y, x] == _dark[y + 1, x + 1])
					penalty += 3;
			}
		int percent = dark * 100 / (Size * Size);
		penalty += Math.Abs(percent - 50) / 5 * 10;
		return penalty;
	}

	/// <summary>
	/// O desenho como caminho (o formato dos caminhos de SVG, que o Avalonia
	/// lê): um retângulo por fileira de módulos escuros, com a margem branca
	/// de <paramref name="quiet"/> módulos em volta. O lado inteiro mede
	/// <c>Size + 2 * quiet</c>.
	/// </summary>
	public string ToPath(int quiet = 4)
	{
		var path = new StringBuilder();
		for (int y = 0; y < Size; y++)
			for (int x = 0; x < Size; x++)
			{
				if (!_dark[y, x]) continue;
				int start = x;
				while (x + 1 < Size && _dark[y, x + 1]) x++;
				path.Append('M').Append(start + quiet).Append(',').Append(y + quiet)
					.Append('h').Append(x - start + 1).Append("v1h").Append(-(x - start + 1)).Append('z');
			}
		return path.ToString();
	}
}
