using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Alcateia.Servidor;

/// <summary>
/// Senhas e segredos. A senha nunca é guardada: guarda-se o resultado de
/// PBKDF2-SHA256 com sal próprio e muitas voltas, no formato
/// <c>pbkdf2$voltas$sal$hash</c> — dá para aumentar as voltas no futuro
/// sem invalidar quem já tem conta. O token de login também não é
/// guardado: guarda-se o SHA-256 dele.
/// </summary>
public static class Passwords
{
	public const int Iterations = 210_000;
	public const int MinLength = 8;
	public const int MaxLength = 128;

	public static string Hash(string password)
	{
		byte[] salt = RandomNumberGenerator.GetBytes(16);
		byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, 32);
		return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
	}

	public static bool Verify(string password, string stored)
	{
		string[] parts = stored.Split('$');
		if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations))
			return false;

		byte[] expected = Convert.FromBase64String(parts[3]);
		byte[] actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Convert.FromBase64String(parts[2]),
			iterations, HashAlgorithmName.SHA256, expected.Length);
		return CryptographicOperations.FixedTimeEquals(actual, expected);
	}

	/// <summary>Um token novo: 32 bytes sorteados, em texto que cabe em URL.</summary>
	public static string NewToken() =>
		Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

	public static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

	/// <summary>Assinatura de um texto com um segredo (o ingresso da partida).</summary>
	public static string Sign(string secret, string text) =>
		Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(text)));

	public static bool SameSecret(string a, string b) =>
		a != null && b != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
