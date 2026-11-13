using Godot;
using System.Collections.Generic;

/// <summary>
/// A granada de choque, a resposta do humano contra o lobo. Segurar o
/// botão arma o braço; soltar arremessa na mira desse instante (decisão
/// do Jhon). O bot arremessa direto.
/// </summary>
public partial class Weapon
{
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
		if (grenade is Grenade armed) armed.Thrower = _health;
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

		// Sem granadas restantes, volta sozinho para a arma.
		HumanSlot = Usable(_slotBeforeGrenade) ? _slotBeforeGrenade : BestSlot();
		EmitSignal(SignalName.SlotChanged, (int)HumanSlot);
	}
}
