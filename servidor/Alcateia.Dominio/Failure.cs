using System;

namespace Alcateia.Dominio;

/// <summary>Que tipo de "não dá": quem está de fora (a API) traduz para o
/// código HTTP, o domínio não sabe o que é HTTP.</summary>
public enum FailureKind
{
	Invalid,        // 400
	Unauthorized,   // 401
	Forbidden,      // 403
	NotFound,       // 404
	Conflict,       // 409
	TooLarge,       // 413
	TooMany,        // 429
	Unavailable     // 503
}

/// <summary>Um pedido que não dá para atender: o tipo, um código estável
/// (o cliente decide pelo código) e uma frase para mostrar ao jogador.</summary>
public sealed class Failure : Exception
{
	public FailureKind Kind { get; }
	public string Code { get; }

	public Failure(FailureKind kind, string code, string message) : base(message)
	{
		Kind = kind;
		Code = code;
	}
}
