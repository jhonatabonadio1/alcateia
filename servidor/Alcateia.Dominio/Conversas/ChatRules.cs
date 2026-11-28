using System;
using System.Text;

namespace Alcateia.Dominio.Conversas;

/// <summary>Uma mensagem direta entre dois amigos, como fica guardada:
/// quem mandou, para quem, o texto, quando e se quem recebeu já leu.</summary>
public sealed record DirectMessage(long Id, long From, long To, string Text, DateTime At, bool Read);

/// <summary>
/// As regras das conversas (Jhon, 27/11: "coloque chat também no [...]
/// launcher e no game, chat em partida, chat na sala e entre amigos"): o
/// chat da sala (que na partida também fala só com o time) e as mensagens
/// diretas entre amigos, guardadas para quem abre a conversa depois.
///
/// O texto sai sempre numa linha: quebra de linha e caractere de controle
/// viram espaço — a mensagem cabe na linha do HUD e ninguém desenha a tela
/// dos outros com eles.
/// </summary>
public static class ChatRules
{
	/// <summary>O chat da sala (e da partida), por mensagem.</summary>
	public const int RoomMax = 200;

	/// <summary>A mensagem direta, por mensagem.</summary>
	public const int DirectMax = 500;

	/// <summary>Mensagens diretas por ligação: <see cref="DirectBurst"/> a cada
	/// <see cref="DirectBurstWindow"/> (conversar) e <see cref="DirectPerMinute"/>
	/// por minuto (encher a caixa de alguém não compensa).</summary>
	public const int DirectBurst = 5;
	public static readonly TimeSpan DirectBurstWindow = TimeSpan.FromSeconds(5);
	public const int DirectPerMinute = 30;

	/// <summary>Quantas mensagens um pedido de histórico traz de uma vez.</summary>
	public const int HistoryPage = 50;

	/// <summary>Quantas mensagens cada conversa guarda (as mais novas).</summary>
	public const int Kept = 200;

	/// <summary>O tamanho do <c>ref</c> que o cliente pode mandar para casar o eco.</summary>
	public const int RefMax = 40;

	/// <summary>O texto como vai para os outros: numa linha, aparado e cortado em <paramref name="max"/>.</summary>
	public static string Clean(string text, int max)
	{
		if (string.IsNullOrEmpty(text)) return "";
		var line = new StringBuilder(Math.Min(text.Length, max + 8));
		foreach (char ch in text)
			line.Append(char.IsControl(ch) || ch == (char)0x2028 || ch == (char)0x2029 ? ' ' : ch);
		string clean = line.ToString().Trim();
		if (clean.Length <= max) return clean;
		// Não corta um caractere de dois pedaços (emoji) pela metade.
		int cut = max;
		if (char.IsHighSurrogate(clean[cut - 1])) cut--;
		return clean[..cut].TrimEnd();
	}

	public static Failure Empty() =>
		new(FailureKind.Invalid, "mensagem_vazia", "A mensagem está vazia.");

	public static Failure Unavailable() =>
		new(FailureKind.Unavailable, "conversa_indisponivel", "As conversas estão fora do ar. Tente de novo em instantes.");
}
