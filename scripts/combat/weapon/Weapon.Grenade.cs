using Godot;
using System.Collections.Generic;

/// <summary>
/// A granada (explosiva desde 19/11), a resposta do humano contra o lobo. Segurar o
/// botão arma o braço; soltar arremessa na mira desse instante (decisão
/// do Jhon). O bot arremessa direto.
///
/// Em primeira pessoa a granada **sai da mão** (19/11): ao soltar o botão
/// o braço lança, e ela nasce no instante e no lugar em que a mão a solta
/// (<see cref="ReleaseFromHand"/>, chamado por quem desenha a mão), com a
/// velocidade calculada dali até o ponto que a cruz aponta. Sem mão
/// animada (bots, bonecos de rede), nasce na hora, à frente da câmera.
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
	/// de 11. 17,5 desde 20/11 ("o lançamento da granada pode ser mais
	/// forte"): o lance sai mais rápido e mais reto, e vai a uns 24 m.</summary>
	[Export] public float ThrowForce = 17.5f;
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
	public bool IsGrenadeReady => (Puppet ? RemoteGrenadeReady : _grenadeReady) && HumanSlot == Slot.Grenade && !_form.IsWolf;

	/// <summary>No boneco de outro jogador: o dono está com a granada pronta
	/// na mão (o estado que chegou, desde 21/11). O corpo arma o braço.</summary>
	public bool RemoteGrenadeReady { get; set; }
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

	/// <summary>A força máxima do arremesso, como fração de <see cref="ThrowForce"/>.
	/// Inteira desde 19/11 (era 0,8): com menos, mirando além de uns 13 m a
	/// granada caía antes do ponto, e o pedido é que ela caia **onde se
	/// mira** até <see cref="ThrowMaxRange"/>. Perto, ela continua saindo
	/// só com a força que o ponto pede.</summary>
	[Export] public float ThrowMaxFactor = 1f;

	/// <summary>A última granada arremessada.</summary>
	public Node3D LastGrenade { get; private set; }

	/// <summary>Há uma mão animada que solta a granada (o ViewModel liga
	/// quando o conjunto da granada tem o clipe de arremesso): o arremesso
	/// espera <see cref="ReleaseFromHand"/>.</summary>
	public bool ThrowFromHand;

	/// <summary>Quanto se espera a mão soltar, no máximo (s). Passou disso,
	/// a granada sai à frente da câmera, como sem mão.</summary>
	[Export] public float ThrowHandTimeout = 0.4f;

	private float _throwPending;

	/// <summary>Arremessou e a granada ainda está na mão.</summary>
	public bool IsThrowPending => _throwPending > 0;

	/// <summary>A força do arremesso em curso, de 0 a 1 (da força máxima):
	/// perto é um lance leve, longe é o braço inteiro.</summary>
	public float ThrowPower { get; private set; }

	/// <summary>A mão soltou: a granada nasce em <paramref name="hand"/>
	/// (no mundo; nulo = à frente da câmera) e voa para onde a cruz aponta
	/// agora.</summary>
	public void ReleaseFromHand(Vector3? hand)
	{
		if (_throwPending > 0) Launch(hand);
	}

	private void TickThrow(float delta)
	{
		if (_throwPending > 0 && (_throwPending -= delta) <= 0) Launch(null);
	}

	/// <summary>Até onde a mira vale como alvo da granada (m). 16 desde
	/// 19/11 (era 20, que a força de então nem alcançava): até aí ela cai
	/// no ponto; além — ou mirando o céu — vai o mais longe que o braço dá
	/// (<see cref="ThrowFarFactor"/>). 19 m e ~18 m desde o fim de 19/11
	/// ("granada pode ir mais longe também, não tanto"; eram 16 e 15,5).</summary>
	[Export] public float ThrowMaxRange = 24f;

	/// <summary>A força, como fração de <see cref="ThrowForce"/>, quando
	/// não há ponto na mira (o céu, o horizonte): o arremesso longo de
	/// sempre ("o lance da granada está muito forte", Jhon, 18/11).</summary>
	[Export] public float ThrowFarFactor = 1f;

	/// <summary>O ângulo do arremesso acima da linha até o alvo (graus), no
	/// alcance máximo: um arco de mão, que cai em cima do ponto em vez de
	/// passar rasante.</summary>
	[Export] public float ThrowLoft = 22f;

	/// <summary>O mesmo ângulo com o alvo colado (graus). O arco cresce com
	/// a distância, deste até <see cref="ThrowLoft"/> (19/11: "a granada
	/// deve sair da mão fazendo um pêndulo baseado na força, distância e
	/// mira"; "vai na direção do crosshair"): perto é um lance quase
	/// direto no ponto, que sai perto da cruz; longe, o arco alto. Antes
	/// eram 30° sempre, e mirando perto a granada subia até o alto da tela
	/// para depois cair.</summary>
	[Export] public float ThrowLoftNear = 6f;

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
	/// Mirando o céu ou além de <see cref="ThrowMaxRange"/>: o mais longe que o braço dá.
	/// </summary>
	/// <summary>O raio da esfera de colisão da granada (scenes/grenade.tscn).</summary>
	private const float GrenadeRadius = 0.14f;

	private Vector3 ThrowVelocity(Vector3 from, Vector3 forward, float gravityScale, RigidBody3D grenade = null)
	{
		Vector3 eye = _camera.GlobalPosition;
		var query = PhysicsRayQueryParameters3D.Create(eye, eye + forward * ThrowMaxRange);
		query.Exclude = grenade != null ? new Godot.Collections.Array<Rid> { _body.GetRid(), grenade.GetRid() } : new Godot.Collections.Array<Rid> { _body.GetRid() };
		var hit = _body.GetWorld3D().DirectSpaceState.IntersectRay(query);
		// Mirando o céu ou longe demais: o arremesso mais longo que o braço dá.
		Vector3 aimed = hit.Count > 0 ? hit["position"].AsVector3() : eye + forward * ThrowMaxRange;
		// A conta leva o centro da granada; quem toca é a casca. O centro mira
		// um raio para fora da superfície e o toque cai no ponto da cruz (vindo
		// baixo, mirar o ponto fazia tocar meio metro antes; 20/11).
		if (hit.Count > 0) aimed += hit["normal"].AsVector3() * GrenadeRadius;

		Vector3 to = aimed - from;
		var flat = new Vector3(to.X, 0, to.Z);
		float x = flat.Length(), y = to.Y;
		if (x < 0.6f) return forward * 4f + Vector3.Up * 1.5f;

		float g = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8) * gravityScale;
		// O teto do arremesso: um braço, não um canhão ("o lance da granada
		// está muito forte", Jhon, 18/11).
		float max = ThrowForce * (hit.Count > 0 ? ThrowMaxFactor : ThrowFarFactor);
		// O arco cresce com a distância: perto, quase direto; longe, alto.
		float loft = Mathf.Lerp(ThrowLoftNear, ThrowLoft, Mathf.Clamp(to.Length() / ThrowMaxRange, 0f, 1f));
		float angle = Mathf.Clamp(Mathf.Atan2(y, x) + Mathf.DegToRad(loft), Mathf.DegToRad(-70f), Mathf.DegToRad(70f));
		float drop = x * Mathf.Tan(angle) - y;
		float cos = Mathf.Cos(angle);
		float speed = drop > 0.01f ? Mathf.Sqrt(g * x * x / (2f * cos * cos * drop)) : max * 2f;
		if (speed > max)
		{
			// Não chega com esse arco: sai com toda a força, no ângulo (o mais
			// baixo) que leva ao ponto com ela — o arco sobe aos poucos com a
			// distância, sem salto. Nem assim chega: o ângulo que vai mais
			// longe para esse lado (45° mais metade da subida até o ponto).
			speed = max;
			float v2 = max * max, inside = v2 * v2 - g * (g * x * x + 2f * y * v2);
			angle = inside >= 0f ? Mathf.Atan((v2 - Mathf.Sqrt(inside)) / (g * x)) : Mathf.DegToRad(45f) + Mathf.Atan2(y, x) * 0.5f;
		}

		Vector3 dir = flat / x;
		return dir * (Mathf.Cos(angle) * speed) + Vector3.Up * (Mathf.Sin(angle) * speed);
	}

	private void ThrowGrenade()
	{
		if (_form.IsWolf || !_form.CanAttack)
		{
			if (Debug) GD.Print("grenade: human form only, not while transforming");
			return;
		}

		if (Grenades <= 0)
		{
			if (Debug) GD.Print("grenade: none left");
			EmitSignal(SignalName.GrenadesChanged, Grenades);
			return;
		}

		if (GrenadeScene == null)
		{
			GD.PrintErr("Weapon: GrenadeScene not assigned.");
			return;
		}

		if (_cooldown > 0) return;

		Grenades--;
		_cooldown = ThrowCooldown;

		// A força deste arremesso (para a mão: lance leve ou braço inteiro).
		Vector3 ahead = -_camera.GlobalTransform.Basis.Z;
		ThrowPower = Mathf.Clamp(ThrowVelocity(_camera.GlobalPosition + ahead * 0.5f, ahead, _grenadeGravity).Length() / (ThrowForce * ThrowMaxFactor), 0f, 1f);

		// Com mão animada, a granada espera a mão soltar; sem, sai agora.
		if (ThrowFromHand && _body is Player) _throwPending = ThrowHandTimeout;
		else Launch(null);

		EmitSignal(SignalName.GrenadesChanged, Grenades);
		EmitSignal(SignalName.Thrown);

		// Sem granadas restantes, volta sozinho para a arma.
		HumanSlot = Usable(_slotBeforeGrenade) ? _slotBeforeGrenade : BestSlot();
		EmitSignal(SignalName.SlotChanged, (int)HumanSlot);
	}

	private float _grenadeGravity = 1f;

	/// <summary>A granada nasce e voa: de <paramref name="hand"/> (a mão,
	/// no mundo) ou, sem ela, de 0,8 m à frente da câmera, na direção em que
	/// a cruz aponta neste instante.</summary>
	private void Launch(Vector3? hand)
	{
		_throwPending = 0;
		if (_health.IsDead || GrenadeScene == null) return;

		var grenade = GrenadeScene.Instantiate<RigidBody3D>();
		if (grenade is Grenade armed)
		{
			armed.Thrower = _health;
			armed.ModelId = GrenadeModelId;
			armed.Cosmetic = Puppet;
		}
		_body.GetTree().CurrentScene.AddChild(grenade);
		_grenadeGravity = grenade.GravityScale;

		Vector3 eye = _camera.GlobalPosition;
		Vector3 forward = -_camera.GlobalTransform.Basis.Z;
		// Sem mão: à frente da câmera, para não colidir com o próprio corpo.
		Vector3 from = eye + forward * 0.8f;
		if (hand.HasValue)
		{
			from = hand.Value;
			// Parede entre o olho e a mão (encostado nela): nasce antes dela.
			var query = PhysicsRayQueryParameters3D.Create(eye, from + (from - eye).Normalized() * 0.12f);
			query.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
			var wall = _body.GetWorld3D().DirectSpaceState.IntersectRay(query);
			if (wall.Count > 0) from = eye + (wall["position"].AsVector3() - eye) * 0.6f;
		}
		grenade.GlobalPosition = from;
		grenade.AddCollisionExceptionWith(_body);

		grenade.LinearVelocity = ThrowVelocity(from, forward, grenade.GravityScale, grenade);
		grenade.AngularVelocity = new Vector3(4f, 2f, 1f);
		LastGrenade = grenade;
	}
}
