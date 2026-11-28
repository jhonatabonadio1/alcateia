using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Alcateia.Pacote;

/// <summary>De onde vêm os arquivos de pacote: um trecho de um deles, por nome.</summary>
public interface IBlobSource
{
	/// <summary>Abre <paramref name="length"/> bytes de <paramref name="name"/>
	/// a partir de <paramref name="offset"/>.</summary>
	Task<Stream> OpenRange(string name, long offset, long length, CancellationToken cancel);
}

/// <summary>Os pacotes num servidor de arquivos (as Releases do GitHub, um
/// balde, qualquer coisa que aceite <c>Range</c>): baixa só o trecho pedido.</summary>
public sealed class HttpBlobSource : IBlobSource
{
	private readonly HttpClient _http;
	private readonly Uri _base;

	public HttpBlobSource(HttpClient http, string baseUrl)
	{
		_http = http;
		_base = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
	}

	public async Task<Stream> OpenRange(string name, long offset, long length, CancellationToken cancel)
	{
		var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_base, Uri.EscapeDataString(name)));
		request.Headers.Range = new RangeHeaderValue(offset, offset + length - 1);
		HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
		if ((int)response.StatusCode == 206) return await response.Content.ReadAsStreamAsync(cancel);

		int status = (int)response.StatusCode;
		response.Dispose();
		// 200 seria o arquivo inteiro desde o começo: o servidor ignorou o Range, e os bytes não são os pedidos.
		throw new HttpRequestException(status == 200
			? $"O servidor de arquivos não aceita baixar em partes ({name})."
			: $"O servidor de arquivos respondeu {status} para {name}.", null, response.StatusCode);
	}
}

/// <summary>Os pacotes numa pasta do disco (testes, e instalar de um pen drive).</summary>
public sealed class LocalBlobSource : IBlobSource
{
	private readonly string _dir;

	public LocalBlobSource(string dir) => _dir = dir;

	public Task<Stream> OpenRange(string name, long offset, long length, CancellationToken cancel)
	{
		var file = new FileStream(Path.Combine(_dir, name), FileMode.Open, FileAccess.Read, FileShare.Read);
		file.Seek(offset, SeekOrigin.Begin);
		return Task.FromResult<Stream>(file);
	}
}

public static class BlobSources
{
	/// <summary>A fonte para a base de um manifesto: endereço http(s), ou pasta
	/// (caminho ou file://). Base vazia: a pasta de onde o manifesto veio.</summary>
	public static IBlobSource For(string baseUrl, HttpClient http, string manifestLocation = null)
	{
		if (string.IsNullOrEmpty(baseUrl) && manifestLocation != null)
		{
			if (manifestLocation.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || manifestLocation.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				baseUrl = manifestLocation[..(manifestLocation.LastIndexOf('/') + 1)];
			else baseUrl = Path.GetDirectoryName(Path.GetFullPath(manifestLocation));
		}
		if (baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			return new HttpBlobSource(http, baseUrl);
		if (baseUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) baseUrl = new Uri(baseUrl).LocalPath;
		return new LocalBlobSource(baseUrl);
	}
}
