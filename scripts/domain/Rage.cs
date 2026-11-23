/// <summary>
/// O rage, o "em chamas" do Overwatch (Jhon, 21/11: "se um jogador está
/// jogando muito bem, muitas kills, ele fica em rage e ganha +XP no final
/// da partida; o rage some se ficar sem matar ou sem dar assistência por um
/// tempo").
///
/// Um medidor de calor por jogador: abate soma 100, assistência 50, até
/// 300. Passou de 200, pega fogo (dois abates seguidos bastam). Depois de
/// 10 s sem abate nem assistência, o calor cai 20 por segundo; abaixo de
/// 100 o fogo apaga — de 300, uns 20 s parado. Morrer não apaga (só o
/// tempo apaga), como no Overwatch.
///
/// Cada 6 s em chamas valem 1 de XP no fim (<see cref="Rewards"/>), até
/// <see cref="Rewards.MaxFireXp"/>. Regra pura: roda no jogo (HUD, placar)
/// e no servidor de partida (<c>Referee</c>), que é quem manda o
/// tempo em chamas no resultado.
/// </summary>
public sealed class Rage
{
	public const float KillHeat = 100f, AssistHeat = 50f, MaxHeat = 400f;
	public const float Ignite = 300f, Extinguish = 150f;
	public const float Grace = 6f, CoolPerSecond = 40f;

	public float Heat { get; private set; }
	public bool OnFire { get; private set; }

	/// <summary>Segundos em chamas na partida.</summary>
	public float FireSeconds { get; private set; }

	/// <summary>Quanto do fogo ainda resta, de 0 a 1 (para o HUD mostrar a chama diminuindo).</summary>
	public float Level => Heat / MaxHeat;

	private float _quiet = float.MaxValue;

	/// <summary>O máximo de segundos em chamas que tantos abates e
	/// assistências permitem: cada um estende o fogo, no máximo, pela folga
	/// mais o tempo de queimar o calor que deu (15 s o abate, 12,5 s a
	/// assistência), e sem 200 de calor ninguém acende. É a conta do
	/// anti-cheat: o servidor central não paga nem aceita calado um tempo em
	/// chamas maior que este (<c>Rewards</c>, <c>Plausibility</c>).</summary>
	public static int MaxFireSeconds(int kills, int assists)
	{
		kills = System.Math.Clamp(kills, 0, 1000);
		assists = System.Math.Clamp(assists, 0, 1000);
		if (kills * KillHeat + assists * AssistHeat < Ignite) return 0;
		return (int)System.Math.Ceiling(kills * (Grace + KillHeat / CoolPerSecond) + assists * (Grace + AssistHeat / CoolPerSecond));
	}

	/// <summary>Online: o estado como o servidor de partida tem. Ele é quem
	/// conta de verdade; o jogo só adivinha entre um aviso e outro.</summary>
	public void Sync(float heat, bool onFire, float fireSeconds)
	{
		if (heat < Heat - 0.5f) _quiet = float.MaxValue;   // lá já está esfriando
		Heat = System.Math.Clamp(heat, 0f, MaxHeat);
		OnFire = onFire;
		FireSeconds = System.Math.Max(0f, fireSeconds);
	}

	public void Kill() => Add(KillHeat);
	public void Assist() => Add(AssistHeat);

	private void Add(float heat)
	{
		Heat = System.Math.Min(MaxHeat, Heat + heat);
		_quiet = 0f;
		if (Heat >= Ignite) OnFire = true;
	}

	/// <summary>Passa o tempo. Devolve true se acendeu ou apagou agora.</summary>
	public bool Tick(float delta)
	{
		bool was = OnFire;
		if (OnFire) FireSeconds += delta;
		_quiet += delta;
		if (_quiet > Grace) Heat = System.Math.Max(0f, Heat - CoolPerSecond * delta);
		if (OnFire && Heat < Extinguish) OnFire = false;
		return was != OnFire;
	}
}
