using Godot;
using System;

/// <summary>
/// Os calls de rádio do time (pedido do Jhon, 18/11): Z abre a lista, de 1
/// a 10 (o 0 do teclado é o 10), e a fala escolhida toca **para todo o
/// time** de quem falou — e só para ele.
///
/// A voz é a do time: **azul, feminina** (<c>radio_f_1..10</c>);
/// **vermelho, masculina** (<c>radio_m_1..10</c>). Os arquivos saem de
/// <c>tools/sons/radio.py</c>.
///
/// Rádio não vem de um lugar: toca sem posição, no mesmo volume para todos
/// do time. Online, o call viaja pela partida (<see cref="NetWire.Radio"/>)
/// e cada máquina decide se ouve, pelo time do corpo de quem falou.
/// </summary>
public static class Radio
{
	/// <summary>As falas, na ordem das teclas (1…9, 0). Em inglês, como a voz.</summary>
	public static readonly string[] Calls =
	{
		"Enemy Spotted!", "Roger That!", "Follow Me!", "Cover Me!", "Go, Go, Go!",
		"Fall Back!", "Hold Position!", "Need Backup!", "Sector Clear!", "Nice Shot!"
	};

	/// <summary>Segundos entre um call e o próximo do mesmo jogador.</summary>
	public const double Cooldown = 1.2;

	/// <summary>Volume do call (dB).</summary>
	public static float VolumeDb = 1f;

	/// <summary>A lista está aberta: os números escolhem a fala, não a arma.</summary>
	public static bool MenuOpen { get; set; }

	private static ulong _numbersBusyUntil;

	/// <summary>Os números estão com o rádio (a lista aberta, ou a tecla
	/// que acabou de escolher a fala ainda no ar): não trocam de arma.</summary>
	public static bool HoldsNumbers => MenuOpen || Time.GetTicksMsec() < _numbersBusyUntil;

	/// <summary>A tecla que escolheu a fala não vale como troca de arma.</summary>
	public static void SwallowNumber() => _numbersBusyUntil = Time.GetTicksMsec() + 250;

	/// <summary>Um call que esta máquina ouviu: quem falou, o time e qual fala.</summary>
	public static event Action<string, int, int> Heard;

	private static double _lastSent = -10;

	public static string SoundOf(int team, int call) => $"radio_{(team == Teams.Orange ? "m" : "f")}_{call + 1}";

	/// <summary>O jogador desta máquina fala: toca aqui e vai para o time.
	/// Falso se ainda está no intervalo, morto ou fora da lista.</summary>
	public static bool Send(Node3D speaker, int call)
	{
		if (call < 0 || call >= Calls.Length || speaker == null) return false;
		if (speaker.GetNodeOrNull<Health>("Health") is not Health health || health.IsDead) return false;

		double now = Time.GetTicksMsec() / 1000.0;
		if (now - _lastSent < Cooldown) return false;
		_lastSent = now;

		Play(health.PublicName, health.Team, call);
		NetMatch.Current?.SendRadio(call);
		return true;
	}

	/// <summary>Um call chegou de outro corpo (pela rede, ou de um bot):
	/// toca se quem joga aqui é do mesmo time.</summary>
	public static void Receive(Node3D speaker, int call)
	{
		if (call < 0 || call >= Calls.Length || speaker == null) return;
		if (speaker.GetNodeOrNull<Health>("Health") is not Health health) return;
		if (health.Team != LocalTeam(speaker)) return;
		Play(health.PublicName, health.Team, call);
	}

	private static int LocalTeam(Node from)
	{
		foreach (Node node in from.GetTree().GetNodesInGroup(Groups.Players))
			if (node is Player player && player.IsMultiplayerAuthority() && player.GetNodeOrNull<Health>("Health") is Health health)
				return health.Team;
		return RoomConfig.PlayerTeam;
	}

	private static void Play(string who, int team, int call)
	{
		Sfx.Ui(SoundOf(team, call), VolumeDb);
		Heard?.Invoke(who, team, call);
	}

	/// <summary>Para os testes: zera o intervalo.</summary>
	public static void Reset() => _lastSent = -10;
}
