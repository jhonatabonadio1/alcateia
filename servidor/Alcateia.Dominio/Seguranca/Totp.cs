using System;
using System.Security.Cryptography;
using System.Text;

namespace Alcateia.Dominio.Seguranca;

/// <summary>
/// Senha de uso único por tempo (TOTP, RFC 6238): a segunda etapa do
/// login do painel. O mesmo que Google Authenticator, Authy e 1Password
/// usam: HMAC-SHA1, 30 s, 6 dígitos, segredo em Base32.
/// </summary>
public static class Totp
{
	public const int Step = 30, Digits = 6;
	private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

	public static string NewSecret() => ToBase32(RandomNumberGenerator.GetBytes(20));

	/// <summary>O código de um instante.</summary>
	public static string Code(string secret, DateTime utc, int offset = 0)
	{
		long counter = new DateTimeOffset(utc).ToUnixTimeSeconds() / Step + offset;
		byte[] message = BitConverter.GetBytes(counter);
		if (BitConverter.IsLittleEndian) Array.Reverse(message);
		byte[] hash = HMACSHA1.HashData(FromBase32(secret), message);
		int at = hash[^1] & 0x0F;
		int value = (hash[at] & 0x7F) << 24 | hash[at + 1] << 16 | hash[at + 2] << 8 | hash[at + 3];
		return (value % 1_000_000).ToString("D6");
	}

	/// <summary>Confere o código, aceitando um passo de relógio para cada lado.</summary>
	public static bool Verify(string secret, string code, DateTime utc)
	{
		code = (code ?? "").Replace(" ", "");
		if (code.Length != Digits || string.IsNullOrEmpty(secret)) return false;
		bool ok = false;
		for (int offset = -1; offset <= 1; offset++)
			ok |= Secrets.Same(Code(secret, utc, offset), code);
		return ok;
	}

	/// <summary>O endereço que vira QR code no aplicativo autenticador.</summary>
	public static string Uri(string issuer, string account, string secret) =>
		$"otpauth://totp/{System.Uri.EscapeDataString(issuer)}:{System.Uri.EscapeDataString(account)}?secret={secret}&issuer={System.Uri.EscapeDataString(issuer)}&digits={Digits}&period={Step}";

	public static string ToBase32(byte[] data)
	{
		var text = new StringBuilder();
		int buffer = 0, bits = 0;
		foreach (byte b in data)
		{
			buffer = buffer << 8 | b;
			bits += 8;
			while (bits >= 5)
			{
				text.Append(Alphabet[buffer >> (bits - 5) & 31]);
				bits -= 5;
			}
		}
		if (bits > 0) text.Append(Alphabet[buffer << (5 - bits) & 31]);
		return text.ToString();
	}

	public static byte[] FromBase32(string text)
	{
		text = (text ?? "").TrimEnd('=').ToUpperInvariant();
		var bytes = new byte[text.Length * 5 / 8];
		int buffer = 0, bits = 0, at = 0;
		foreach (char c in text)
		{
			int value = Alphabet.IndexOf(c);
			if (value < 0) continue;
			buffer = buffer << 5 | value;
			bits += 5;
			if (bits >= 8 && at < bytes.Length)
			{
				bytes[at++] = (byte)(buffer >> (bits - 8) & 0xFF);
				bits -= 8;
			}
		}
		return bytes;
	}
}
