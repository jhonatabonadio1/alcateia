using Godot;
using System.Collections.Generic;

/// <summary>
/// Vida, escudo, morte e respawn.
///
/// O escudo é separado do HP de propósito: o documento aponta isso como
/// um dos detalhes que vendem a lembrança. Dano come escudo primeiro.
///
/// Fase 1 roda local. Na Fase 2 o TakeDamage passa a ser chamado
/// SOMENTE no servidor — o cliente nunca decide dano.
/// </summary>
public partial class Health : Node
{
	[Signal] public delegate void ChangedEventHandler(float hp, float maxHp, float shield);
	[Signal] public delegate void DiedEventHandler(int killerId);
	[Signal] public delegate void RespawnedEventHandler();

	/// <summary>Levou dano, e de onde veio. A posição permite ao bot
	/// reagir a tiro pelas costas e ao HUD apontar a direção.</summary>
	[Signal] public delegate void DamagedEventHandler(float amount, Vector3 from);

	/// <summary>Time do dono. Define fogo amigo e a cor no feed.</summary>
	[Export] public int Team = Teams.Blue;

	/// <summary>Nome exibido no feed de abates.</summary>
	[Export] public string DisplayName = "";

	/// <summary>
	/// Nome para mostrar, nunca vazio.
	///
	/// Bots não tinham <see cref="DisplayName"/>, e quem reportava o
	/// abate usava <c>DisplayName ?? nome do nó</c> — que não cai no
	/// nó, porque string vazia não é nula. O feed mostrava abates sem
	/// autor, e um placar por jogador juntaria todos os bots numa linha
	/// só, a do nome "".
	/// </summary>
	public string PublicName => string.IsNullOrEmpty(DisplayName)
		? GetParent()?.Name.ToString() ?? Name.ToString()
		: DisplayName;

	[Export] public float MaxHp = 200f;
	[Export] public float RespawnDelay = 3f;

	/// <summary>Segundos de imortalidade ao renascer. Sem isso, quem
	/// morre perto de um inimigo volta direto para a mesma morte.</summary>
	[Export] public float SpawnProtection = 3f;

	public bool IsProtected => _protection > 0;

	/// <summary>Renascer sozinho ao fim da contagem. O jogador desliga
	/// isto: quem escolhe lote decide quando volta.</summary>
	[Export] public bool AutoRespawn = true;

	/// <summary>Carência antes do escudo voltar a escorrer depois de
	/// ser recarregado. Sem ela, o rugido termina e o escudo já começa
	/// a cair no mesmo instante — o jogador não vê o que ganhou.</summary>
	[Export] public float ShieldDecayGrace = 1f;

	/// <summary>Escudo perdido por segundo. Zero para quem não tem
	/// escudo que vaza, como o humano.</summary>
	public float ShieldDecay { get; set; }

	/// <summary>Segundos restantes até reviver. O HUD mostra isto —
	/// morrer sem aviso na tela é indistinguível de travar.</summary>
	public float RespawnIn => (float)Mathf.Max(_respawnTimer, 0);

	public float Hp { get; private set; }
	public float MaxShield { get; private set; }
	public float Shield { get; private set; }
	public bool IsDead { get; private set; }

	/// <summary>Multiplicador aplicado ao dano recebido. A transformação
	/// sobe isso — transformar tem que ter risco, senão a economia de WP
	/// não significa nada.</summary>
	public float DamageMultiplier { get; set; } = 1f;

	/// <summary>Multiplicador do dano recebido no corpo. Headshot não
	/// passa por aqui: é a saída que o atirador tem contra um corpo
	/// resistente.</summary>
	public float BodyResistance { get; set; } = 1f;

	private double _respawnTimer;
	private double _protection;
	private double _decayHold;

	public override void _Ready()
	{
		if (string.IsNullOrEmpty(DisplayName))
			DisplayName = GetParent().Name;

		Hp = MaxHp;
		EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
	}

	public override void _Process(double delta)
	{
		if (_decayHold > 0)
			_decayHold -= delta;

		if (!IsDead && Shield > 0 && ShieldDecay > 0 && _decayHold <= 0)
		{
			Shield = Mathf.Max(Shield - ShieldDecay * (float)delta, 0);
			EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
		}

		if (_protection > 0)
			_protection -= delta;

		if (!IsDead) return;

		_respawnTimer -= delta;

		if (_respawnTimer <= 0 && AutoRespawn)
			Respawn();
	}

	/// <summary>Troca o pool de vida ao mudar de forma. O lobo não
	/// "cura" ao transformar: ele passa a usar outro corpo.</summary>
	public void SetPool(float maxHp, float maxShield, bool refill, bool keepRatio = false)
	{
		float oldMaxHp = MaxHp;
		float oldHp = Hp;

		MaxHp = maxHp;
		MaxShield = maxShield;

		if (refill)
		{
			Hp = maxHp;
			Shield = maxShield;
		}
		else if (keepRatio && oldMaxHp > 0)
		{
			// Corpo novo, mesmo estado: meio de vida como humano vira
			// meio de vida como lobo. Sem isto, transformar cura.
			Hp = maxHp * Mathf.Clamp(oldHp / oldMaxHp, 0f, 1f);
			Shield = maxShield;
		}
		else
		{
			Hp = Mathf.Min(Hp, maxHp);
			Shield = Mathf.Min(Shield, maxShield);
		}

		EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
	}

	/// <summary>Define a vida diretamente, sem contar como dano. Usado
	/// ao restaurar o estado guardado de uma forma.</summary>
	public void SetHp(float value)
	{
		Hp = Mathf.Clamp(value, 1f, MaxHp);
		EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
	}

	/// <summary>
	/// Online: este é o corpo de outro jogador, e a vida dele mora na
	/// máquina dele. Aqui o dano não se aplica — vai pela rede
	/// (<see cref="SendHit"/>) e a vida volta espelhada
	/// (<see cref="Mirror"/>). Nulo fora de partida online.
	/// </summary>
	public System.Action<RemoteHit> Remote { get; set; }
	public bool IsRemote => Remote != null;

	/// <summary>Manda um acerto para o dono deste corpo decidir.</summary>
	public void SendHit(float amount, Vector3 from, bool headshot, KillCause cause, string weapon)
	{
		if (IsDead || amount <= 0) return;
		Remote?.Invoke(new RemoteHit(amount, from, headshot, cause, weapon ?? ""));
	}

	/// <summary>Copia a vida que o dono do corpo informou, com morte e
	/// renascimento.</summary>
	public void Mirror(float hp, float maxHp, float shield, float maxShield, bool dead)
	{
		if (dead && !IsDead) Die(0);
		else if (!dead && IsDead) Respawn();
		if (dead) return;

		MaxHp = maxHp;
		MaxShield = maxShield;
		Hp = Mathf.Clamp(hp, 0f, maxHp);
		Shield = Mathf.Clamp(shield, 0f, maxShield);
		EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
	}

	public void TakeDamage(float amount, int attackerId = 0, Vector3 from = default, bool headshot = false)
	{
		if (IsRemote) return;
		if (IsDead || amount <= 0 || IsProtected) return;

		DamageOutcome outcome = CombatRules.ApplyDamage(Hp, Shield, amount, DamageMultiplier, BodyResistance, headshot);
		EmitSignal(SignalName.Damaged, outcome.Dealt, from);

		Shield = outcome.Shield;
		Hp = outcome.Hp;
		EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);

		if (Hp <= 0)
			Die(attackerId);
	}

	/// <summary>Recarga de escudo pelo uivo. Nunca passa do teto —
	/// o Smart Wolf original provou que cura sem teto quebra o jogo.</summary>
	public void RestoreShield(float amount)
	{
		if (IsDead) return;

		Shield = Mathf.Min(Shield + amount, MaxShield);
		_decayHold = ShieldDecayGrace;

		EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
	}

	/// <summary>Quem matou este corpo da última vez — o último que o
	/// acertou, se foi há pouco. Nulo numa queda. Quem morreu usa para a
	/// câmera de morte.</summary>
	public Node Killer { get; private set; }

	private Node _lastAttacker;
	private double _lastAttackAt;

	private void Die(int killerId)
	{
		double now = Time.GetTicksMsec() / 1000.0;
		Killer = _lastAttacker != null && GodotObject.IsInstanceValid(_lastAttacker) && now - _lastAttackAt < 5.0
			? _lastAttacker : null;
		IsDead = true;
		Hp = 0;
		Shield = 0;
		_respawnTimer = RespawnDelay;

		EmitSignal(SignalName.Died, killerId);
		EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
	}

	/// <summary>Renasce agora, respeitando o tempo mínimo de espera.
	/// Usado por quem controla o próprio renascimento.</summary>
	public bool TryRespawn()
	{
		if (!IsDead || _respawnTimer > 0) return false;

		Respawn();
		return true;
	}

	/// <summary>Quem acertou este corpo, e quando. Alimenta a
	/// assistência: guarda o corpo de quem atirou, não o componente, para
	/// a vida não precisar saber nada de economia nem de forma.</summary>
	private readonly Dictionary<Node, double> _attackers = new();

	public void NoteAttacker(Node attacker)
	{
		if (attacker == null) return;
		_attackers[attacker] = Time.GetTicksMsec() / 1000.0;
		_lastAttacker = attacker;
		_lastAttackAt = _attackers[attacker];
	}

	/// <summary>Quem acertou nos últimos <paramref name="window"/>
	/// segundos, menos quem matou. Esvazia a lista: assistência se paga
	/// uma vez por morte.</summary>
	public List<Node> TakeAssists(Node killer, double window = 10.0)
	{
		double now = Time.GetTicksMsec() / 1000.0;
		var helpers = new List<Node>();

		foreach (var (node, time) in _attackers)
			if (node != killer && now - time <= window && GodotObject.IsInstanceValid(node))
				helpers.Add(node);

		_attackers.Clear();
		return helpers;
	}

	private void Respawn()
	{
		_attackers.Clear();
		_lastAttacker = null;
		IsDead = false;
		Hp = MaxHp;
		Shield = MaxShield;
		_protection = SpawnProtection;

		EmitSignal(SignalName.Respawned);
		EmitSignal(SignalName.Changed, Hp, MaxHp, Shield);
	}
}

/// <summary>Um acerto num corpo de outro jogador, a caminho da rede.</summary>
public readonly record struct RemoteHit(float Amount, Vector3 From, bool Headshot, KillCause Cause, string Weapon);
