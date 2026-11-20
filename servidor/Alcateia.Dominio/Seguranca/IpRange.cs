using System;
using System.Net;
using System.Net.Sockets;

namespace Alcateia.Dominio.Seguranca;

/// <summary>
/// Um endereço ou uma faixa (CIDR): <c>203.0.113.7</c>, <c>203.0.113.0/24</c>,
/// <c>2001:db8::/32</c>. É o que a blacklist e a lista de proxies de
/// confiança guardam. IPv4 que chega embrulhado em IPv6
/// (<c>::ffff:1.2.3.4</c>) é tratado como IPv4.
/// </summary>
public sealed class IpRange
{
	private readonly byte[] _network;
	public int Prefix { get; }

	/// <summary>Como foi aceito, normalizado (<c>endereço/prefixo</c>).</summary>
	public string Text { get; }

	/// <summary>Um endereço só (e não uma faixa).</summary>
	public bool Single => Prefix == _network.Length * 8;

	private IpRange(byte[] network, int prefix)
	{
		_network = network;
		Prefix = prefix;
		Text = Single ? new IPAddress(network).ToString() : $"{new IPAddress(network)}/{prefix}";
	}

	public static IPAddress Normalize(IPAddress address) =>
		address != null && address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

	/// <summary><paramref name="wide"/>: aceita faixa larga (só para as
	/// faixas internas conhecidas; o que vem do painel não pode).</summary>
	public static bool TryParse(string text, out IpRange range, bool wide = false)
	{
		range = null;
		text = (text ?? "").Trim();
		if (text.Length == 0 || text.Length > 64) return false;

		string host = text;
		int prefix = -1;
		int slash = text.IndexOf('/');
		if (slash >= 0)
		{
			host = text[..slash];
			if (!int.TryParse(text[(slash + 1)..], out prefix)) return false;
		}

		if (!IPAddress.TryParse(host, out IPAddress address)) return false;
		address = Normalize(address);
		byte[] bytes = address.GetAddressBytes();
		int bits = bytes.Length * 8;
		if (prefix < 0) prefix = bits;
		if (prefix > bits) return false;
		// Faixa larga demais bloquearia a internet inteira por engano.
		if (!wide && prefix < (address.AddressFamily == AddressFamily.InterNetwork ? 8 : 16)) return false;

		for (int bit = prefix; bit < bits; bit++) bytes[bit / 8] &= (byte)~(0x80 >> (bit % 8));
		range = new IpRange(bytes, prefix);
		return true;
	}

	public static IpRange Parse(string text, bool wide = false) =>
		TryParse(text, out IpRange range, wide) ? range : throw new Failure(FailureKind.Invalid, "ip_invalido", "Endereço ou faixa de IP inválida.");

	public bool Contains(IPAddress address)
	{
		address = Normalize(address);
		if (address == null) return false;
		byte[] bytes = address.GetAddressBytes();
		if (bytes.Length != _network.Length) return false;
		int whole = Prefix / 8, rest = Prefix % 8;
		for (int i = 0; i < whole; i++)
			if (bytes[i] != _network[i]) return false;
		if (rest == 0) return true;
		int mask = 0xFF << (8 - rest) & 0xFF;
		return (bytes[whole] & mask) == (_network[whole] & mask);
	}
}
