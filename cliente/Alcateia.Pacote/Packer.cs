using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Alcateia.Pacote;

/// <summary>O que empacotar e como chamar o que sai.</summary>
public sealed class PackOptions
{
	public string Product = "wolfleague", Platform = "windows", Version = "0.0.0";

	/// <summary>O que abrir para jogar, dentro da pasta (caminho com "/").</summary>
	public string Exe = "";

	/// <summary>De onde os arquivos de pacote serão baixados, com a barra no fim.</summary>
	public string Base = "";

	public string Notes = "";

	/// <summary>Tamanho máximo de cada arquivo de pacote. O GitHub aceita até
	/// 2 GB por arquivo de Release; 1,5 GB deixa folga.</summary>
	public long BlobMax = 1536L * 1024 * 1024;

	/// <summary>Esforço do Brotli, de 0 a 11. 5 comprime quase como o 9 em um
	/// quinto do tempo; o pipeline empacota gigabytes a cada lançamento.</summary>
	public int Quality = 5;
}

/// <summary>
/// Empacota uma pasta (o jogo exportado) para o cliente baixar: corta cada
/// arquivo em pedaços (<see cref="Chunker"/>), guarda cada pedaço diferente
/// uma vez só, comprimido, em arquivos de pacote
/// (<c>produto-versão-plataforma.000.pack</c>, <c>.001</c>…), e escreve o
/// manifesto que diz que pedaços formam cada arquivo.
///
/// Roda no pipeline, depois de exportar o jogo
/// (<c>cliente/Alcateia.Pacote.Ferramenta</c>).
/// </summary>
public static class Packer
{
	public static Manifest Pack(string sourceDir, string outDir, PackOptions options, Action<string> log = null)
	{
		sourceDir = Path.GetFullPath(sourceDir);
		Directory.CreateDirectory(outDir);

		var manifest = new Manifest
		{
			Product = options.Product, Platform = options.Platform, Version = options.Version,
			Exe = options.Exe, Base = options.Base, Notes = options.Notes ?? ""
		};
		var known = new Dictionary<string, int>();   // SHA-256 do pedaço → índice
		string prefix = $"{options.Product}-{options.Version}-{options.Platform}";
		FileStream blob = null;
		string blobName = null;

		void CloseBlob()
		{
			if (blob == null) return;
			manifest.Blobs.Add(new BlobInfo(blobName, blob.Length));
			blob.Dispose();
			blob = null;
		}

		// Sempre na mesma ordem: o mesmo jogo dá o mesmo pacote.
		List<string> paths = Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)
			.Select(p => Path.GetRelativePath(sourceDir, p).Replace(Path.DirectorySeparatorChar, '/'))
			.Where(p => !p.StartsWith(Installer.StateDir + "/", StringComparison.Ordinal) && Path.GetFileName(p) != ".DS_Store")
			.OrderBy(p => p, StringComparer.Ordinal).ToList();
		if (paths.Count == 0) throw new InvalidOperationException($"{sourceDir} está vazia: o jogo foi exportado?");

		byte[] packed = new byte[BrotliEncoder.GetMaxCompressedLength(Chunker.Max)];
		try
		{
			foreach (string path in paths)
			{
				string full = Path.Combine(sourceDir, path.Replace('/', Path.DirectorySeparatorChar));
				var info = new FileInfo(full);
				if (info.LinkTarget != null) throw new InvalidOperationException($"{path} é um atalho (symlink): o pacote só leva arquivos.");
				Manifest.SafePath(path);

				var chunks = new List<int>();
				using FileStream file = File.OpenRead(full);
				(long size, string hash) = Chunker.Split(file, (piece, bytes) =>
				{
					if (!known.TryGetValue(piece.Hash, out int index))
					{
						bool compressed = BrotliEncoder.TryCompress(bytes.Span, packed, out int written, options.Quality, 22) && written < bytes.Length;
						ReadOnlySpan<byte> stored = compressed ? packed.AsSpan(0, written) : bytes.Span;

						if (blob != null && blob.Length + stored.Length > options.BlobMax) CloseBlob();
						if (blob == null)
						{
							blobName = $"{prefix}.{manifest.Blobs.Count:000}.pack";
							blob = File.Create(Path.Combine(outDir, blobName));
						}
						index = manifest.Chunks.Count;
						manifest.Chunks.Add(new ChunkInfo(piece.Hash, piece.Size, manifest.Blobs.Count, blob.Length, stored.Length, compressed));
						blob.Write(stored);
						known[piece.Hash] = index;
					}
					chunks.Add(index);
				});

				bool exec = !OperatingSystem.IsWindows() && (File.GetUnixFileMode(full) & UnixFileMode.UserExecute) != 0;
				manifest.Files.Add(new FileEntry(path, size, hash, exec, chunks.ToArray()));
				log?.Invoke($"{path}  {size:N0} bytes, {chunks.Count} pedaço(s)");
			}
		}
		finally
		{
			CloseBlob();
		}

		if (manifest.Exe != "" && manifest.Files.All(f => f.Path != manifest.Exe))
			throw new InvalidOperationException($"O executável {manifest.Exe} não está na pasta empacotada.");
		return manifest;
	}

	/// <summary>O nome fixo do manifesto de um produto numa plataforma: o
	/// cliente pergunta sempre por este, e ele é sempre o da última versão.</summary>
	public static string ManifestName(string product, string platform) => $"{product}-{platform}.manifest.json";
}
