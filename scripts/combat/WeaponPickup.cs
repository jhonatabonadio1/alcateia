using Godot;
using System.Collections.Generic;

/// <summary>
/// Arma caída no chão: solta com B ou deixada por quem morreu.
///
/// Quem passa por cima com o espaço daquela arma vazio fica com ela, com
/// o pente como estava (<see cref="Weapon.TryPickUp"/>). Com o espaço
/// ocupado, nada acontece — para trocar, solte a sua primeiro. É a regra
/// do CS, e é o que impede o jogador de trocar de arma sem querer ao
/// passar por cima de um corpo.
///
/// O voo é feito à mão, com raio de um quadro para o outro, e não com
/// corpo rígido. Corpo rígido no chão vira obstáculo para todo raio do
/// jogo — o tiro, a visão do bot, a câmera do lobo — e uma arma caída
/// não pode segurar bala nem esconder ninguém. Aqui ela é só uma área:
/// os raios passam direto.
///
/// Some sozinha depois de <see cref="Lifetime"/> segundos, e o mapa
/// guarda no máximo <see cref="MaxOnMap"/>: uma partida de dez minutos
/// com dezesseis pessoas morrendo encheria o chão.
/// </summary>
public partial class WeaponPickup : Area3D
{
	public const float Lifetime = 30f;
	public const int MaxOnMap = 20;

	/// <summary>Raio em que se pega a arma.</summary>
	public const float Reach = 0.9f;

	/// <summary>Quem soltou não pega de volta logo em seguida. Sem isto,
	/// soltar andando para frente devolveria a arma na mão no quadro
	/// seguinte.</summary>
	public const float DropperDelay = 1.2f;

	private const float FallAcceleration = 14f;

	private static readonly List<WeaponPickup> Live = new();

	public WeaponDefinition Definition { get; private set; }
	public int Ammo { get; private set; }

	private Node3D _dropper;
	private Node3D _model;
	private Vector3 _velocity;
	private bool _landed;
	private double _age;
	private float _spin;

	public WeaponPickup()
	{
		CollisionLayer = 0;
		CollisionMask = 1;
		Monitorable = false;
	}

	/// <summary>Joga uma arma no mundo a partir de <paramref name="from"/>.</summary>
	public static WeaponPickup Spawn(Node3D dropper, WeaponDefinition def, int ammo, Vector3 from, Vector3 velocity)
	{
		if (def == null || dropper == null || !dropper.IsInsideTree()) return null;

		Node parent = dropper.GetTree().CurrentScene;
		if (parent == null) return null;

		var pickup = new WeaponPickup
		{
			Definition = def,
			Ammo = ammo,
			_dropper = dropper,
			_velocity = velocity,
			_spin = (float)GD.RandRange(-1.0, 1.0) >= 0 ? 1 : -1,
			Name = $"Arma_{def.Id}"
		};

		parent.AddChild(pickup);
		pickup.GlobalPosition = from;
		pickup.Rotation = new Vector3(0, dropper.GlobalRotation.Y, 0);

		Live.Add(pickup);
		while (Live.Count > MaxOnMap)
		{
			WeaponPickup oldest = Live[0];
			Live.RemoveAt(0);
			if (IsInstanceValid(oldest)) oldest.QueueFree();
		}

		return pickup;
	}

	public override void _Ready()
	{
		AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = Reach } });

		_model = WeaponModel.Build(Definition.Id);
		AddChild(_model);
	}

	public override void _ExitTree() => Live.Remove(this);

	public override void _PhysicsProcess(double delta)
	{
		_age += delta;

		if (!_landed) Fly((float)delta);

		if (_age > Lifetime)
		{
			QueueFree();
			return;
		}

		// O último segundo encolhe: sumir de uma vez parece defeito.
		double left = Lifetime - _age;
		if (left < 1.0) _model.Scale = Vector3.One * Mathf.Max(0.05f, (float)left);

		TryGive();
	}

	private void Fly(float dt)
	{
		_velocity.Y -= FallAcceleration * dt;

		Vector3 from = GlobalPosition;
		Vector3 to = from + _velocity * dt;

		var query = PhysicsRayQueryParameters3D.Create(from, to + _velocity.Normalized() * 0.04f, 1);
		if (_dropper != null && IsInstanceValid(_dropper) && _dropper is CollisionObject3D body)
			query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };

		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);

		if (hit.Count == 0)
		{
			GlobalPosition = to;
			_model.Rotation = new Vector3(_model.Rotation.X + 7f * dt * _spin, _model.Rotation.Y + 4f * dt, 0);
			return;
		}

		var point = (Vector3)hit["position"];
		var normal = (Vector3)hit["normal"];

		// Chão: deita de lado e para. Parede (ou alguém): quica fraco e
		// continua caindo.
		if (normal.Y > 0.55f)
		{
			_landed = true;
			GlobalPosition = point + normal * 0.03f;
			_model.Rotation = new Vector3(0, (float)GD.RandRange(-Mathf.Pi, Mathf.Pi), Mathf.Pi / 2f);
			return;
		}

		_velocity = _velocity.Bounce(normal) * 0.35f;
		GlobalPosition = point + normal * 0.06f;
	}

	/// <summary>Entrega a arma ao primeiro que passar e tiver espaço.</summary>
	private void TryGive()
	{
		if (_age < 0.25) return;

		foreach (Node3D body in GetOverlappingBodies())
		{
			if (body is not CharacterBody3D) continue;
			if (body == _dropper && _age < DropperDelay) continue;

			var weapon = body.GetNodeOrNull<Weapon>("Weapon");
			if (weapon == null || !weapon.TryPickUp(Definition, Ammo)) continue;

			QueueFree();
			return;
		}
	}
}
