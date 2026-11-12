using Godot;

public enum Slot
{
	Primary = 1,
	Secondary = 2,
	Wolf = 3,
	Grenade = 4
}

/// <summary>
/// Sistema de slots e ataque.
///
/// Transformar não é um botão à parte: o lobo ocupa o slot 3, na mesma
/// memória muscular de trocar de arma. Apertar 1, 2 ou 4 estando lobo
/// volta para humano e seleciona aquele slot.
///
/// Fase 2: o cliente manda "ataquei, olhando para cá, no tick N" e o
/// SERVIDOR rebobina as hitboxes para o instante que o atirador viu
/// (lag compensation) e decide o acerto. Nada aqui é autoritativo.
/// </summary>
public partial class Weapon : Node3D
{
	[Signal] public delegate void DamageDealtEventHandler(float amount);
	[Signal] public delegate void FiredEventHandler();
	[Signal] public delegate void SlotChangedEventHandler(int slot);
	[Signal] public delegate void GrenadesChangedEventHandler(int left);

	// --- Arma principal: cadência alta, dano baixo, automática ---
	[Export] public float PrimaryDamage = 28f;
	[Export] public float PrimaryRange = 120f;
	[Export] public float PrimaryCooldown = 0.11f;

	// --- Arma secundária: precisa, lenta, semiautomática ---
	[Export] public float SecondaryDamage = 65f;
	[Export] public float SecondaryRange = 150f;
	[Export] public float SecondaryCooldown = 0.5f;

	// --- Granada de atordoamento: a resposta do humano contra o lobo ---
	[Export] public PackedScene GrenadeScene;
	[Export] public int MaxGrenades = 2;
	[Export] public float ThrowForce = 11f;
	[Export] public float ThrowArc = 3.5f;
	[Export] public float ThrowCooldown = 0.9f;

	/// <summary>Altura mínima de queda para contar como ataque aéreo.</summary>
	[Export] public float AirAttackMinFall = 2.5f;

	/// <summary>Diagnóstico. Desligue quando o tiro estiver confirmado.</summary>
	[Export] public bool Debug = true;

	/// <summary>Traçante do disparo. Sem nenhum retorno visual não há
	/// como saber que alguém atirou em você — o bot parecia inofensivo
	/// enquanto acertava.</summary>
	[Export] public bool ShowTracer = true;

	[Export] public Color TracerColor = new Color(1f, 0.9f, 0.5f, 0.9f);
	[Export] public float TracerLife = 0.08f;

	/// <summary>Slot de arma guardado enquanto você está de lobo, para
	/// voltar nele ao destransformar.</summary>
	public Slot HumanSlot { get; private set; } = Slot.Primary;

	public Slot Current => _form.IsWolf ? Slot.Wolf : HumanSlot;

	/// <summary>Granadas restantes. Reabastece ao renascer.</summary>
	public int Grenades { get; private set; }

	/// <summary>Só a principal dispara segurando o botão.</summary>
	public bool IsAutomatic => Current == Slot.Primary;

	private Camera3D _camera;
	private Node3D _head;
	private FormController _form;
	private CharacterBody3D _body;
	private double _cooldown;
	private float _fallStartY;
	private bool _wasOnFloor = true;

	public override void _Ready()
	{
		_body = GetParent<CharacterBody3D>();
		_form = _body.GetNode<FormController>("FormController");
		_camera = _body.GetNode<Camera3D>("Head/Camera3D");
		_head = _body.GetNode<Node3D>("Head");

		Grenades = MaxGrenades;

		_body.GetNode<Health>("Health").Changed += OnHealthChanged;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_cooldown > 0)
			_cooldown -= delta;

		bool onFloor = _body.IsOnFloor();
		if (_wasOnFloor && !onFloor)
			_fallStartY = _body.GlobalPosition.Y;
		_wasOnFloor = onFloor;
	}

	/// <summary>Chamado ao apertar 1, 2, 3 ou 4.</summary>
	public void SelectSlot(Slot slot)
	{
		if (slot == Slot.Grenade)
		{
			ThrowGrenade();
			return;
		}

		if (slot == Slot.Wolf)
		{
			// Já lobo: apertar 3 de novo não faz nada. Para voltar a
			// humano, escolha uma arma.
			if (!_form.IsWolf)
				_form.ToggleForm();

			EmitSignal(SignalName.SlotChanged, (int)Slot.Wolf);
			return;
		}

		HumanSlot = slot;

		if (_form.IsWolf)
			_form.ToggleForm();

		_cooldown = 0.2f;
		EmitSignal(SignalName.SlotChanged, (int)slot);
	}

	public void TryAttack()
	{
		if (_cooldown > 0) return;

		if (!_form.CanAttack)
		{
			if (Debug) GD.Print($"ataque bloqueado: forma atual é {_form.Current}");
			return;
		}

		if (_form.IsWolf)
			WolfAttack();
		else
			Shoot();
	}

	private void Shoot()
	{
		bool primary = HumanSlot == Slot.Primary;
		float damage = primary ? PrimaryDamage : SecondaryDamage;
		float range = primary ? PrimaryRange : SecondaryRange;

		_cooldown = primary ? PrimaryCooldown : SecondaryCooldown;
		EmitSignal(SignalName.Fired);

		Vector3 from = _camera.GlobalPosition;
		Vector3 to = from - _camera.GlobalTransform.Basis.Z * range;

		Health hit = Raycast(from, to, out Vector3 impact);
		Tracer(from, hit == null ? to : impact);

		if (hit == null) return;

		hit.TakeDamage(damage);
		EmitSignal(SignalName.DamageDealt, damage);
		_form.OnDamageDealt(damage);
	}

	private void WolfAttack()
	{
		WolfBreed breed = _form.Breed;
		_cooldown = breed.AttackCooldown;
		EmitSignal(SignalName.Fired);

		// A garrada sai do corpo, não da câmera. Em terceira pessoa a
		// câmera está metros atrás — partir dela faria o lobo acertar
		// coisas que estão atrás dele.
		Vector3 from = _head.GlobalPosition;
		Vector3 to = from - _head.GlobalTransform.Basis.Z * breed.AttackRange;

		Health hit = Raycast(from, to, out _);
		if (hit == null) return;

		float damage = breed.AttackDamage;

		// Ataque aéreo: cair em cima de alguém é a jogada que as
		// pessoas lembram. Vale bônus.
		float fallen = _fallStartY - _body.GlobalPosition.Y;
		if (!_body.IsOnFloor() && fallen >= AirAttackMinFall)
			damage *= breed.AirAttackBonus;

		hit.TakeDamage(damage);
		EmitSignal(SignalName.DamageDealt, damage);
		_form.OnDamageDealt(damage);
	}

	/// <summary>
	/// Arremesso em arco. A granada e um slot de acao, nao de arma:
	/// apertar 4 joga na hora e volta para a arma anterior, em vez de
	/// deixar voce segurando granada no meio de um tiroteio.
	/// </summary>
	private void ThrowGrenade()
	{
		if (_form.IsWolf || !_form.CanAttack)
		{
			if (Debug) GD.Print("granada: so na forma humana, fora de transicao");
			return;
		}

		if (Grenades <= 0)
		{
			if (Debug) GD.Print("granada: acabou");
			EmitSignal(SignalName.GrenadesChanged, Grenades);
			return;
		}

		if (GrenadeScene == null)
		{
			GD.PrintErr("Weapon: GrenadeScene nao atribuida.");
			return;
		}

		if (_cooldown > 0) return;

		Grenades--;
		_cooldown = ThrowCooldown;

		var grenade = GrenadeScene.Instantiate<RigidBody3D>();
		_body.GetTree().CurrentScene.AddChild(grenade);

		// Nasce a frente da camera para nao colidir com o proprio corpo.
		Vector3 forward = -_camera.GlobalTransform.Basis.Z;
		grenade.GlobalPosition = _camera.GlobalPosition + forward * 0.8f;
		grenade.AddCollisionExceptionWith(_body);

		// Soma a velocidade do jogador: correr e arremessar joga mais longe.
		Vector3 impulse = forward * ThrowForce + Vector3.Up * ThrowArc;
		grenade.LinearVelocity = _body.Velocity + impulse;
		grenade.AngularVelocity = new Vector3(4f, 2f, 1f);

		EmitSignal(SignalName.GrenadesChanged, Grenades);
	}

	/// <summary>Renascer devolve o estoque de granadas.</summary>
	private void OnHealthChanged(float hp, float maxHp, float shield)
	{
		var health = _body.GetNode<Health>("Health");

		if (!health.IsDead && Grenades < MaxGrenades && Mathf.IsEqualApprox(hp, maxHp))
		{
			Grenades = MaxGrenades;
			EmitSignal(SignalName.GrenadesChanged, Grenades);
		}
	}

	/// <summary>Linha fina que aparece e some. Peça descartável — vira
	/// efeito de verdade na fase de polimento.</summary>
	private void Tracer(Vector3 from, Vector3 to)
	{
		if (!ShowTracer) return;

		var mesh = new ImmediateMesh();
		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = TracerColor,
			VertexColorUseAsAlbedo = true
		};

		mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
		mesh.SurfaceAddVertex(Vector3.Zero);
		mesh.SurfaceAddVertex(to - from);
		mesh.SurfaceEnd();

		var node = new MeshInstance3D { Mesh = mesh };
		_body.GetTree().CurrentScene.AddChild(node);
		node.GlobalPosition = from;

		Tween tween = node.CreateTween();
		tween.TweenProperty(material, "albedo_color:a", 0.0f, TracerLife);
		tween.TweenCallback(Callable.From(node.QueueFree));
	}

	private Health Raycast(Vector3 from, Vector3 to, out Vector3 impact)
	{
		impact = to;

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
		query.CollideWithAreas = false;

		var result = _body.GetWorld3D().DirectSpaceState.IntersectRay(query);

		if (result.Count == 0)
		{
			if (Debug) GD.Print("ataque: não acertou nada");
			return null;
		}

		impact = (Vector3)result["position"];

		if (result["collider"].Obj is not Node collider)
			return null;

		Health health = collider.GetNodeOrNull<Health>("Health");

		if (Debug)
		{
			GD.Print(health == null
				? $"ataque: acertou '{collider.Name}', que não tem um filho chamado Health"
				: $"ataque: acertou '{collider.Name}'");
		}

		return health;
	}
}
