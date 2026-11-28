using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Alcateia.Pacote;

/// <summary>Um arquivo de pacote (os pedaços, um atrás do outro) e o tamanho dele.</summary>
public sealed record BlobInfo(string Name, long Size);

/// <summary>
/// Um pedaço: o SHA-256 e o tamanho dele depois de aberto, e onde está
/// guardado — em qual arquivo de pacote, a partir de onde, com quantos bytes,
/// comprimido (Brotli) ou não.
/// </summary>
public sealed record ChunkInfo(string Hash, int Size, int Blob, long Offset, int Stored, bool Compressed);

/// <summary>Um arquivo do produto: o caminho dentro da pasta instalada (com
/// "/"), o tamanho, o SHA-256 dele inteiro, se é executável (Mac e Linux) e
/// os pedaços que o formam, na ordem (índices em <see cref="Manifest.Chunks"/>).</summary>
public sealed record FileEntry(string Path, long Size, string Hash, bool Exec, int[] Chunks);

/// <summary>Um manifesto que não dá para usar: quebrado, de formato mais novo
/// que este programa, ou apontando para fora da pasta.</summary>
public sealed class ManifestException : Exception
{
	public ManifestException(string message) : base(message) { }
}

/// <summary>
/// O manifesto de uma versão: a lista de arquivos do produto, os pedaços de
/// cada um e onde baixar cada pedaço. É um JSON pequeno (centenas de KB para
/// um jogo de gigabytes) que o cliente baixa para saber se há versão nova e
/// o que falta.
///
/// O endereço dele é fixo por plataforma ("…/wolfleague-windows.manifest.json",
/// sempre o da última versão); os arquivos de pacote ficam em
/// <see cref="Base"/>, que leva a versão — quem começou a baixar uma versão
/// termina nela, mesmo que saia outra no meio.
///
/// Quem lê não confia no que leu: caminho absoluto, com "..", ou pedaço que
/// não existe é manifesto recusado (<see cref="ManifestException"/>), nunca
/// arquivo escrito fora da pasta de instalação.
/// </summary>
public sealed class Manifest
{
	public const int CurrentFormat = 1;

	public int Format = CurrentFormat;
	public string Product = "", Platform = "", Version = "";

	/// <summary>O que abrir para jogar, dentro da pasta instalada.</summary>
	public string Exe = "";

	/// <summary>De onde vêm os arquivos de pacote (endereço ou pasta), com a barra no fim.</summary>
	public string Base = "";

	/// <summary>O que mudou nesta versão, em texto (o trecho do CHANGELOG).</summary>
	public string Notes = "";

	public List<BlobInfo> Blobs = new();
	public List<ChunkInfo> Chunks = new();
	public List<FileEntry> Files = new();

	/// <summary>Quanto o produto ocupa instalado.</summary>
	public long InstallSize => Files.Sum(f => f.Size);

	/// <summary>Quanto se baixa numa instalação do zero.</summary>
	public long DownloadSize => Blobs.Sum(b => b.Size);

	public string ToJson()
	{
		var blobs = new JsonArray();
		foreach (BlobInfo b in Blobs) blobs.Add((JsonNode)new JsonObject { ["name"] = b.Name, ["size"] = b.Size });

		// Os pedaços vão como listas curtas, não objetos: são milhares.
		var chunks = new JsonArray();
		foreach (ChunkInfo c in Chunks)
			chunks.Add((JsonNode)new JsonArray(c.Hash, c.Size, c.Blob, c.Offset, c.Stored, c.Compressed ? 1 : 0));

		var files = new JsonArray();
		foreach (FileEntry f in Files)
		{
			var file = new JsonObject { ["path"] = f.Path, ["size"] = f.Size, ["hash"] = f.Hash };
			if (f.Exec) file["exec"] = true;
			file["chunks"] = new JsonArray(f.Chunks.Select(i => (JsonNode)i).ToArray());
			files.Add((JsonNode)file);
		}

		return new JsonObject
		{
			["format"] = Format, ["product"] = Product, ["platform"] = Platform, ["version"] = Version,
			["exe"] = Exe, ["base"] = Base, ["notes"] = Notes,
			["blobs"] = blobs, ["chunks"] = chunks, ["files"] = files
		}.ToJsonString();
	}

	public static Manifest Parse(string json)
	{
		try
		{
			if (JsonNode.Parse(json) is not JsonObject root) throw new ManifestException("O manifesto não é um objeto JSON.");
			var m = new Manifest
			{
				Format = root["format"]?.GetValue<int>() ?? 0,
				Product = root["product"]?.GetValue<string>() ?? "",
				Platform = root["platform"]?.GetValue<string>() ?? "",
				Version = root["version"]?.GetValue<string>() ?? "",
				Exe = root["exe"]?.GetValue<string>() ?? "",
				Base = root["base"]?.GetValue<string>() ?? "",
				Notes = root["notes"]?.GetValue<string>() ?? ""
			};
			if (m.Format is < 1 or > CurrentFormat)
				throw new ManifestException($"Este manifesto é do formato {m.Format}; este programa entende até o {CurrentFormat}. Atualize o cliente.");

			foreach (JsonNode b in root["blobs"] as JsonArray ?? new JsonArray())
				m.Blobs.Add(new BlobInfo(SafeName(b["name"]?.GetValue<string>()), b["size"].GetValue<long>()));

			foreach (JsonNode c in root["chunks"] as JsonArray ?? new JsonArray())
			{
				var chunk = new ChunkInfo(c[0].GetValue<string>(), c[1].GetValue<int>(), c[2].GetValue<int>(),
					c[3].GetValue<long>(), c[4].GetValue<int>(), c[5].GetValue<int>() != 0);
				if (!Hashes.IsHash(chunk.Hash) || chunk.Size is < 0 or > Chunker.Max || chunk.Stored < 0 || chunk.Stored > Chunker.Max * 2
					|| chunk.Blob < 0 || chunk.Blob >= m.Blobs.Count || chunk.Offset < 0 || chunk.Offset + chunk.Stored > m.Blobs[chunk.Blob].Size)
					throw new ManifestException("O manifesto tem um pedaço inválido.");
				m.Chunks.Add(chunk);
			}

			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (JsonNode f in root["files"] as JsonArray ?? new JsonArray())
			{
				string path = SafePath(f["path"]?.GetValue<string>());
				if (!seen.Add(path)) throw new ManifestException($"O manifesto repete o arquivo {path}.");
				int[] chunks = (f["chunks"] as JsonArray ?? new JsonArray()).Select(i => i.GetValue<int>()).ToArray();
				long size = f["size"].GetValue<long>();
				if (chunks.Any(i => i < 0 || i >= m.Chunks.Count) || chunks.Sum(i => (long)m.Chunks[i].Size) != size)
					throw new ManifestException($"Os pedaços de {path} não fecham com o tamanho dele.");
				string hash = f["hash"]?.GetValue<string>();
				if (!Hashes.IsHash(hash)) throw new ManifestException($"O manifesto não tem o SHA-256 de {path}.");
				m.Files.Add(new FileEntry(path, size, hash, f["exec"]?.GetValue<bool>() ?? false, chunks));
			}

			if (m.Exe != "") m.Exe = SafePath(m.Exe);
			return m;
		}
		catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or NullReferenceException
			or ArgumentException or IndexOutOfRangeException or OverflowException)
		{
			throw new ManifestException("O manifesto está quebrado: " + e.Message);
		}
	}

	/// <summary>Um caminho do manifesto que fica dentro da pasta: relativo,
	/// com "/", sem "..", sem letra de unidade.</summary>
	public static string SafePath(string path)
	{
		if (string.IsNullOrEmpty(path) || path.Length > 400) throw new ManifestException("O manifesto tem um caminho vazio ou longo demais.");
		if (path.StartsWith('/') || path.Contains('\\') || path.Contains(':') || path.Contains('\0'))
			throw new ManifestException($"O manifesto tem um caminho que não é relativo: {path}");
		foreach (string part in path.Split('/'))
			if (part.Length == 0 || part == "." || part == ".." || part.EndsWith(' ') || part.EndsWith('.') && OperatingSystem.IsWindows())
				throw new ManifestException($"O manifesto tem um caminho inválido: {path}");
		// A pasta do estado da instalação é do instalador, não do produto.
		if (path.StartsWith(Installer.StateDir + "/", StringComparison.OrdinalIgnoreCase))
			throw new ManifestException($"O manifesto não pode escrever em {Installer.StateDir}.");
		return path;
	}

	/// <summary>O nome de um arquivo de pacote: um nome só, sem pasta.</summary>
	private static string SafeName(string name)
	{
		if (string.IsNullOrEmpty(name) || name.Length > 200 || name.IndexOfAny(new[] { '/', '\\', ':', '?', '#', '\0' }) >= 0 || name is "." or "..")
			throw new ManifestException($"O manifesto tem um nome de pacote inválido: {name}");
		return name;
	}

	/// <summary>O caminho no disco de um arquivo do manifesto, conferido de
	/// novo: nunca fora de <paramref name="root"/>.</summary>
	public static string OnDisk(string root, string path)
	{
		string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, path.Replace('/', System.IO.Path.DirectorySeparatorChar)));
		string inside = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
		if (!full.StartsWith(inside, StringComparison.Ordinal)) throw new ManifestException($"O caminho {path} sai da pasta de instalação.");
		return full;
	}
}

/// <summary>Compara versões do jeito do jogo: "0.5.0-alpha.17" (os números,
/// e depois do hífen a etapa e o número dela). Sem hífen é mais nova que com.</summary>
public static class Versions
{
	public static int Compare(string a, string b)
	{
		(int[] na, string pa) = Split(a);
		(int[] nb, string pb) = Split(b);
		for (int i = 0; i < Math.Max(na.Length, nb.Length); i++)
		{
			int x = i < na.Length ? na[i] : 0, y = i < nb.Length ? nb[i] : 0;
			if (x != y) return x.CompareTo(y);
		}
		if (pa == pb) return 0;
		if (pa == "") return 1;
		if (pb == "") return -1;

		string[] ea = pa.Split('.'), eb = pb.Split('.');
		for (int i = 0; i < Math.Max(ea.Length, eb.Length); i++)
		{
			if (i >= ea.Length) return -1;
			if (i >= eb.Length) return 1;
			bool ia = int.TryParse(ea[i], out int x), ib = int.TryParse(eb[i], out int y);
			int c = ia && ib ? x.CompareTo(y) : string.CompareOrdinal(ea[i], eb[i]);
			if (c != 0) return c;
		}
		return 0;
	}

	public static bool IsNewer(string candidate, string current) => Compare(candidate, current) > 0;

	private static (int[] Numbers, string Pre) Split(string version)
	{
		version = (version ?? "").Trim().TrimStart('v');
		int dash = version.IndexOf('-');
		string core = dash < 0 ? version : version[..dash];
		int[] numbers = core.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
		return (numbers, dash < 0 ? "" : version[(dash + 1)..]);
	}
}
