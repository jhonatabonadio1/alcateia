using Godot;

/// <summary>
/// Os sons do corpo de um combatente que não passam pelo
/// <see cref="NoiseBus"/>: transformação, garra, faca, recarga, troca de
/// arma, pulo, dor, morte e o marcador de acerto.
///
/// Filho do Player e do Bot, ao lado de Health, Weapon e
/// FormController — o mesmo componente nos dois, como o resto.
///
/// **Alcance curto de propósito.** Os bots não ouvem estes sons, então
/// eles não podem servir de informação a distância: se servissem, o
/// jogador ouviria algo que o bot não ouve e deixaria de prever a
/// reação dele. Até <see cref="CosmeticRange"/> metros você já está
/// vendo quem fez o som; o áudio só confirma o que o olho viu. O que é
/// informação a distância (passo, tiro, uivo) mora no WorldAudio.
///
/// Os sons do seu próprio corpo tocam sem posição.
/// </summary>
public partial class CombatantAudio : Node
{
	[Export] public float CosmeticRange = 12f;

	/// <summary>Intervalo mínimo entre gemidos de dor. Uma rajada de
	/// nove chumbos não pode virar nove gritos.</summary>
	[Export] public float HurtInterval = 0.4f;

	private CharacterBody3D _body;
	private Health _health;
	private Weapon _weapon;
	private FormController _form;
	private bool _local;

	private bool _wasOnFloor = true;
	private bool _wasReloading;
	private int _lastMag;
	private int _lastGrenades = -1;
	private double _hurtTimer;
	private double _quiet = 0.5;

	public override void _Ready()
	{
		_body = GetParent<CharacterBody3D>();
		_health = _body.GetNode<Health>("Health");
		_weapon = _body.GetNode<Weapon>("Weapon");
		_form = _body.GetNode<FormController>("FormController");

		// O grupo "player" só entra no _Ready do pai, que roda depois
		// deste. O tipo já diz quem é você.
		_local = _body is Player;

		_weapon.Fired += OnFired;
		_weapon.HitConfirmed += OnHit;
		_weapon.AmmoChanged += OnAmmo;
		_weapon.SlotChanged += OnSlot;
		_weapon.GrenadesChanged += OnGrenades;
		_form.FormChanged += OnForm;
		_health.Damaged += OnDamaged;
		_health.Died += _ => Play(_form.IsWolf ? "lobo_morte" : "morte");
		_health.Respawned += OnRespawned;
	}

	public override void _PhysicsProcess(double delta)
	{
		_hurtTimer -= delta;
		_quiet -= delta;

		if (_health.IsDead) return;

		// Pulo: saiu do chão subindo. Cair de uma beirada não é pulo.
		bool onFloor = _body.IsOnFloor();
		if (_wasOnFloor && !onFloor && _body.Velocity.Y > 1f)
			Play(_form.IsWolf ? "lobo_pulo" : "pulo");
		_wasOnFloor = onFloor;
	}

	private void OnFired()
	{
		if (_form.IsWolf)
		{
			// No ar é o bote; no chão, garrada ou patada.
			Play(_body.IsOnFloor() ? "lobo_garra" : Sfx.First("lobo_bote", "lobo_garra"));
			return;
		}

		// O tiro toca pelo NoiseBus, no WorldAudio. A faca não faz ruído
		// para os bots, então aqui ela é só o golpe de perto.
		if (_weapon.HumanSlot == Slot.Knife)
			Play("faca");
	}

	private void OnHit(bool headshot)
	{
		if (_form.IsWolf)
			Play("lobo_garra_acerto");

		// O marcador de acerto é só seu: é retorno, não som do mundo.
		if (_local)
			Sfx.Local(headshot ? Sfx.First("acerto_cabeca", "acerto") : "acerto");
	}

	private void OnAmmo(int inMag, int magSize, bool reloading)
	{
		string id = _weapon.Held?.Id ?? "";

		if (reloading && !_wasReloading)
			Play(Sfx.First($"recarga_{id}", "recarga"));
		else if (reloading && _wasReloading && inMag > _lastMag)
			Play("cartucho");
		else if (!reloading && _wasReloading && inMag >= magSize && magSize > 0)
			Play(Sfx.First($"recarga_fim_{id}", "recarga_fim"));

		_wasReloading = reloading;
		_lastMag = inMag;
	}

	private void OnSlot(int slot)
	{
		// Morto, quem troca a arma é a tela de conjuntos, não a mão.
		if (_quiet > 0 || _health.IsDead) return;
		if (slot == (int)Slot.Wolf) return;

		Play("trocar_arma");
	}

	private void OnGrenades(int left)
	{
		// Só conta como arremesso com a granada na mão: o estoque também
		// cai quando a tela de conjuntos equipa um conjunto sem granada.
		if (_lastGrenades >= 0 && left < _lastGrenades && _weapon.HumanSlot == Slot.Grenade)
			Play("granada_lancar");
		_lastGrenades = left;
	}

	private void OnForm(int form)
	{
		switch ((Form)form)
		{
			case Form.ShiftingToWolf:
				Play("lobo_transformar");
				break;
			case Form.ShiftingToHuman:
				Play(Sfx.First("lobo_destransformar", "lobo_transformar"));
				break;
		}
	}

	private void OnDamaged(float amount, Vector3 from)
	{
		if (_hurtTimer > 0 || _health.IsDead) return;
		_hurtTimer = HurtInterval;

		Play(_form.IsWolf ? "lobo_dor" : "dor");
	}

	private void OnRespawned()
	{
		// O renascer reequipa e troca de forma por código: nada disso é
		// o jogador trocando de arma, então não faz som de troca.
		_quiet = 0.5;
		_wasReloading = false;
		_wasOnFloor = true;

		if (_local) Sfx.Local("respawn");
	}

	private void Play(string name)
	{
		if (name == null) return;

		if (_local) Sfx.Local(name);
		else Sfx.At(name, _body.GlobalPosition, CosmeticRange);
	}
}
