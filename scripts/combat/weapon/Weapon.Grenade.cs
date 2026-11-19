using Godot;
using System.Collections.Generic;

/// <summary>
/// A granada de choque, a resposta do humano contra o lobo. Segurar o
/// botão arma o braço; soltar arremessa na mira desse instante (decisão
/// do Jhon). O bot arremessa direto.
/// </summary>
public partial class Weapon
{
	public const string DefaultGrenadeScene = "res://scenes/grenade.tscn";

	[ExportGroup("Granada")]
	[Export] public PackedScene GrenadeScene;
	[Export] public int MaxGrenades = 2;

	/// <summary>Velocidade do arremesso da granada na direção da mira, em
	/// m/s, e o empurrão para cima. Mais forte desde a v0.26.10 (decisão do
	/// Jhon; era 11 e 3,5): olhando reto, de pé, ela cai a uns 16 m em vez
	/// de 11.</summary>
	[Export] public float ThrowForce = 14.5f;
	[Export] public float ThrowArc = 4f;
	[Export] public float ThrowCooldown = 0.9f;

	/// <summary>Granadas restantes. Reabastece ao renascer.</summary>
	public int Grenades { get; private set; }

	/// <summary>Volta para a arma anterior depois de arremessar: ficar
	/// com a mão vazia no meio de um tiroteio não é escolha de
	/// ninguém.</summary>
	private Slot _slotBeforeGrenade = Slot.Primary;

	/// <summary>
	/// Arremesso em arco. A granada e um slot de acao, nao de arma:
	/// apertar 4 joga na hora e volta para a arma anterior, em vez de
	/// deixar voce segurando granada no meio de um tiroteio.
	/// </summary>
	/// <summary>Granada pronta na mão, esperando o botão soltar.</summary>
	public bool IsGrenadeReady => _grenadeReady && HumanSlot == Slot.Grenade && !_form.IsWolf;
	private bool _grenadeReady;

	/// <summary>
	/// Apertou o ataque com a granada: ela fica pronta na mão. Sai quando o
	/// botão soltar (<see cref="ReleaseGrenade"/>), na direção da mira
	/// daquele instante — dá para apontar com calma, mais alto para ir
	/// mais longe. A física do arremesso é a mesma de sempre.
	/// </summary>
	private void PrepareGrenade()
	{
		if (_form.IsWolf || !_form.CanAttack || Grenades <= 0 || GrenadeScene == null || _cooldown > 0) return;
		_grenadeReady = true;
	}

	/// <summary>Soltou o botão: arremessa a granada preparada.</summary>
	public void ReleaseGrenade()
	{
		if (!_grenadeReady) return;
		_grenadeReady = false;
		if (HumanSlot == Slot.Grenade) ThrowGrenade();
	}

	/// <summary>Até onde a mira vale como alvo da granada (m).</summary>
	[Export] public float ThrowMaxRange = 32f;

	/// <summary>O ângulo do arremesso acima da linha até o alvo (graus):
	/// um arco de mão, que cai em cima do ponto em vez de passar rasante.</summary>
	[Export] public float ThrowLoft = 24f;

	/// <summary>
	/// A velocidade que faz a granada **cair onde a mira aponta** (pedido
	/// do Jhon, 18/11: "melhore a precisão do lançamento para ficar mais
	/// fiel à distância que estou mirando"). Antes era sempre a mesma
	/// força: mirar perto ou longe mudava pouco, e correr mudava muito.
	///
	/// Um raio da mira acha o ponto; a conta da parábola dá a velocidade
	/// para chegar lá saindo <see cref="ThrowLoft"/> graus acima da linha
	/// até ele. Longe demais para a força máxima
	/// (<see cref="ThrowForce"/> × 1,35): sai a 45°, o mais longe que dá.
	/// Mirando o céu (sem ponto): o arremesso de antes.
	/// </summary>
	private Vector3 ThrowVelocity(RigidBody3D grenade, Vector3 forward)
	{
		Vector3 eye = _camera.GlobalPosition;
		var query = PhysicsRayQueryParameters3D.Create(eye, eye + forward * ThrowMaxRange);
		query.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid(), grenade.GetRid() };
		var hit = _body.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0) return forward * ThrowForce + Vector3.Up * ThrowArc;

		Vector3 to = hit["position"].AsVector3() - grenade.GlobalPosition;
		var flat = new Vector3(to.X, 0, to.Z);
		float x = flat.Length(), y = to.Y;
		if (x < 0.6f) return forward * 4f + Vector3.Up * 1.5f;

		float g = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8) * grenade.GravityScale;
		float max = ThrowForce * 1.35f;
		float angle = Mathf.Clamp(Mathf.Atan2(y, x) + Mathf.DegToRad(ThrowLoft), Mathf.DegToRad(8f), Mathf.DegToRad(70f));
		float drop = x * Mathf.Tan(angle) - y;
		float cos = Mathf.Cos(angle);
		float speed = drop > 0.01f ? Mathf.Sqrt(g * x * x / (2f * cos * cos * drop)) : max;
		if (speed > max)
		{
			speed = max;
			angle = Mathf.DegToRad(45f);
		}

		Vector3 dir = flat / x;
		return dir * (Mathf.Cos(angle) * speed) + Vector3.Up * (Mathf.Sin(angle) * speed);
	}

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
		if (grenade is Grenade armed)
		{
			armed.Thrower = _health;
			armed.Cosmetic = Puppet;
		}
		_body.GetTree().CurrentScene.AddChild(grenade);

		// Nasce a frente da camera para nao colidir com o proprio corpo.
		Vector3 forward = -_camera.GlobalTransform.Basis.Z;
		grenade.GlobalPosition = _camera.GlobalPosition + forward * 0.8f;
		grenade.AddCollisionExceptionWith(_body);

		grenade.LinearVelocity = ThrowVelocity(grenade, forward);
		grenade.AngularVelocity = new Vector3(4f, 2f, 1f);

		EmitSignal(SignalName.GrenadesChanged, Grenades);
		EmitSignal(SignalName.Thrown);

		// Sem granadas restantes, volta sozinho para a arma.
		HumanSlot = Usable(_slotBeforeGrenade) ? _slotBeforeGrenade : BestSlot();
		EmitSignal(SignalName.SlotChanged, (int)HumanSlot);
	}
}
