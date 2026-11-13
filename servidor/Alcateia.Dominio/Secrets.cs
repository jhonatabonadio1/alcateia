using System.Security.Cryptography;
using System.Text;

namespace Alcateia.Dominio;

/// <summary>Comparação de segredos (senha de sala, chave da frota) em tempo
/// constante: quem chuta não aprende nada pelo tempo da resposta.</summary>
public static class Secrets
{
	public static bool Same(string a, string b) =>
		a != null && b != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
