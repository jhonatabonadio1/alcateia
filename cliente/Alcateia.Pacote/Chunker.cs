using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Alcateia.Pacote;

/// <summary>Um pedaço de um arquivo: onde começa, quanto tem e o SHA-256 dele.</summary>
public readonly record struct Piece(long Offset, int Size, string Hash);

/// <summary>
/// Corta um arquivo em pedaços **pelo conteúdo**, não por posição.
///
/// O jogo exportado é quase todo um arquivo só (o .pck, de gigabytes).
/// Cortado em blocos de tamanho fixo, uma textura que cresce no começo
/// empurra tudo o que vem depois, nenhum bloco bate com o da versão
/// anterior e a atualização baixa o jogo inteiro. Aqui o corte cai onde os
/// bytes mandam: uma soma que rola sobre os últimos 64 bytes ("gear", a do
/// FastCDC) e corta quando os bits de cima dela zeram. O que não mudou cai
/// nos mesmos pedaços, onde quer que tenha ido parar — e a atualização
/// baixa só os pedaços novos.
///
/// **Estes números são o formato.** Mudar <see cref="Min"/>, <see cref="Mask"/>,
/// <see cref="Max"/> ou a semente da tabela faz toda instalação existente
/// cortar diferente do pacote novo, e a próxima atualização de todo mundo
/// baixa tudo. Se precisar mudar, é formato novo (<see cref="Manifest.CurrentFormat"/>).
/// </summary>
public static class Chunker
{
	/// <summary>Pedaço mínimo e máximo, em bytes. O médio fica perto de 1,25 MB
	/// (o mínimo mais 2^20, a chance de corte por byte).</summary>
	public const int Min = 256 * 1024, Max = 4 * 1024 * 1024;

	/// <summary>Os 20 bits de cima da soma: zeraram, corta.</summary>
	private const ulong Mask = 0xFFFFF00000000000UL;

	private static readonly ulong[] Gear = MakeGear();

	/// <summary>A tabela da soma: 256 números tirados de uma semente fixa
	/// (SplitMix64), iguais em toda máquina e em toda versão.</summary>
	private static ulong[] MakeGear()
	{
		var table = new ulong[256];
		ulong state = 0x414C434154454941UL;   // "ALCATEIA"
		for (int i = 0; i < table.Length; i++)
		{
			state += 0x9E3779B97F4A7C15UL;
			ulong z = state;
			z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
			z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
			table[i] = z ^ (z >> 31);
		}
		return table;
	}

	/// <summary>Onde cortar o que está em <paramref name="data"/>: o tamanho
	/// do próximo pedaço. <paramref name="end"/>: o arquivo acaba aí.</summary>
	private static int Cut(ReadOnlySpan<byte> data, bool end)
	{
		if (data.Length <= Min) return data.Length;
		int limit = Math.Min(data.Length, Max);
		ulong hash = 0;
		// A soma só enxerga os últimos 64 bytes: começa a contar um pouco antes do mínimo.
		for (int i = Min - 64; i < limit; i++)
		{
			hash = (hash << 1) + Gear[data[i]];
			if (i >= Min && (hash & Mask) == 0) return i + 1;
		}
		return end || limit == Max ? limit : data.Length;
	}

	/// <summary>
	/// Corta o que vem de <paramref name="source"/>. Cada pedaço é entregue a
	/// <paramref name="each"/> com os bytes dele (que só valem durante a
	/// chamada). Devolve o tamanho total e o SHA-256 do arquivo inteiro.
	/// </summary>
	public static (long Size, string Hash) Split(Stream source, Action<Piece, ReadOnlyMemory<byte>> each)
	{
		byte[] buffer = new byte[Max * 2];
		int have = 0;
		long offset = 0;
		bool end = false;
		using var whole = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

		while (true)
		{
			while (!end && have < Max)
			{
				int read = source.Read(buffer, have, buffer.Length - have);
				if (read == 0) end = true;
				else have += read;
			}
			if (have == 0) break;

			int size = Cut(buffer.AsSpan(0, have), end);
			var bytes = new ReadOnlyMemory<byte>(buffer, 0, size);
			whole.AppendData(bytes.Span);
			each(new Piece(offset, size, Hashes.Of(bytes.Span)), bytes);

			offset += size;
			have -= size;
			if (have > 0) Buffer.BlockCopy(buffer, size, buffer, 0, have);
		}
		return (offset, Hashes.Hex(whole.GetHashAndReset()));
	}

	/// <summary>Só os cortes, sem guardar os bytes (para achar, num arquivo
	/// que já está no disco, os pedaços que servem à versão nova).</summary>
	public static List<Piece> Split(Stream source)
	{
		var pieces = new List<Piece>();
		Split(source, (piece, _) => pieces.Add(piece));
		return pieces;
	}
}

/// <summary>SHA-256 em texto (hexadecimal minúsculo), como vai no manifesto.</summary>
public static class Hashes
{
	public static string Of(ReadOnlySpan<byte> data)
	{
		Span<byte> hash = stackalloc byte[32];
		SHA256.HashData(data, hash);
		return Hex(hash);
	}

	public static string Hex(ReadOnlySpan<byte> hash) => Convert.ToHexString(hash).ToLowerInvariant();

	public static bool IsHash(string text)
	{
		if (text == null || text.Length != 64) return false;
		foreach (char c in text)
			if (!(c is >= '0' and <= '9' or >= 'a' and <= 'f')) return false;
		return true;
	}
}
