using System;
using System.Security.Cryptography;
using System.Text;
using Alcateia.Aplicacao.Portas;

namespace Alcateia.Infra.Seguranca;

/// <summary>
/// Cifra com AES-256-GCM o que não pode ficar legível no banco (o segredo
/// do TOTP dos administradores). A chave vem de
/// <c>ALCATEIA_MASTER_KEY</c> e nunca vai para o banco: quem levar só o
/// banco não leva os segredos. O texto cifrado é
/// <c>v1.nonce.cifra.etiqueta</c>, em Base64 — a etiqueta acusa qualquer
/// adulteração.
/// </summary>
public sealed class AesCipher : ICipher
{
	private readonly byte[] _key;

	public AesCipher(string masterKey)
	{
		// Qualquer texto vira uma chave de 32 bytes; o prefixo separa este
		// uso de outros que a mesma chave mestra venha a ter.
		_key = SHA256.HashData(Encoding.UTF8.GetBytes("alcateia/cifra/v1|" + masterKey));
	}

	public string Encrypt(string plain)
	{
		byte[] nonce = RandomNumberGenerator.GetBytes(12);
		byte[] data = Encoding.UTF8.GetBytes(plain ?? "");
		byte[] cipher = new byte[data.Length], tag = new byte[16];
		using var aes = new AesGcm(_key, 16);
		aes.Encrypt(nonce, data, cipher, tag);
		return $"v1.{Convert.ToBase64String(nonce)}.{Convert.ToBase64String(cipher)}.{Convert.ToBase64String(tag)}";
	}

	public string Decrypt(string cipher)
	{
		string[] parts = (cipher ?? "").Split('.');
		if (parts.Length != 4 || parts[0] != "v1") throw new CryptographicException("formato de cifra desconhecido");
		byte[] nonce = Convert.FromBase64String(parts[1]), data = Convert.FromBase64String(parts[2]), tag = Convert.FromBase64String(parts[3]);
		byte[] plain = new byte[data.Length];
		using var aes = new AesGcm(_key, 16);
		aes.Decrypt(nonce, data, tag, plain);
		return Encoding.UTF8.GetString(plain);
	}
}
