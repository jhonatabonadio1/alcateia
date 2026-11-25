using Godot;

/// <summary>
/// O rabo do lobo com física (pedido do Jhon, 25/11: "o rabo deve ter
/// movimentação e física durante o game, podendo mexer conforme o
/// personagem anda ou corre, respeitando leis da gravidade").
///
/// O modelo traz a cadeia <c>rabo1</c>..<c>rabo5</c>, filha do quadril
/// (veja tools/lobo/montar.py); nenhum clipe mexe nela. Quem mexe é um
/// <see cref="SpringBoneSimulator3D"/> do Godot: cada junta é uma massa
/// presa à anterior por uma mola. A gravidade puxa a ponta para baixo, a
/// mola devolve ao desenho do modelo, e cápsulas nas pernas e no quadril
/// seguram o rabo fora do corpo.
///
/// A simulação roda no referencial do corpo (o centro é o CharacterBody),
/// e não no do mundo: no do mundo o amortecimento da mola vira vento, e o
/// rabo ficava esticado para trás como bandeira só de andar a passo
/// (medido em 25/11: 45° a 1,8 m/s). No do corpo, andar em linha reta e
/// velocidade constante não empurra nada, como na física de verdade; o
/// que empurra é o que <see cref="UpdateTail"/> soma como força: a
/// inércia quando o corpo acelera, freia ou vira, e um arrasto de ar
/// pequeno, que só aparece correndo. O gingado do quadril na passada
/// entra sozinho, pela animação.
///
/// É só o que se vê: nenhuma regra lê o rabo, e ele não tem hitbox. O
/// corpo do servidor (Driven) não simula.
/// </summary>
public partial class WolfVisual
{
	/// <summary>A mola que devolve o rabo ao desenho do modelo, na raiz e
	/// na ponta: a raiz é firme (o rabo nasce do corpo), a ponta é solta.</summary>
	[ExportGroup("Rabo")]
	[Export] public float TailStiffnessRoot = 3.5f;
	[Export] public float TailStiffnessTip = 0.6f;

	/// <summary>Quanto do embalo o rabo perde por passo (0 = balança para
	/// sempre, 1 = não balança).</summary>
	[Export(PropertyHint.Range, "0,1")] public float TailDrag = 0.22f;

	/// <summary>O peso do rabo, na raiz e na ponta. Na mesma unidade da
	/// mola: com os dois iguais a junta para a meio caminho entre o
	/// desenho e o prumo.</summary>
	[Export] public float TailGravityRoot = 0.3f;
	[Export] public float TailGravityTip = 1.5f;

	/// <summary>A grossura do rabo para a colisão com o corpo, na raiz e
	/// na ponta, em metros do modelo.</summary>
	[Export] public float TailRadiusRoot = 0.07f;
	[Export] public float TailRadiusTip = 0.035f;

	/// <summary>Quanto da aceleração do corpo vira força no rabo, em pesos
	/// do rabo por g (9,8 m/s²): o lobo arranca e o rabo fica para trás,
	/// freia e ele vem para a frente.</summary>
	[Export] public float TailInertia = 0.55f;

	/// <summary>O teto dessa força, em pesos do rabo: o lobo sai do zero à
	/// corrida num décimo de segundo (4 g), e sem teto o rabo dava um nó.</summary>
	[Export] public float TailInertiaMax = 1.6f;

	/// <summary>Em quanto tempo a força da inércia acompanha a aceleração,
	/// em segundos: o tranco de um quadro só não chega inteiro ao rabo.</summary>
	[Export] public float TailInertiaSmooth = 0.09f;

	/// <summary>O arrasto do ar, em pesos do rabo por (m/s)²: a 7 m/s (de
	/// quatro) levanta o rabo uns 25°, e a passo quase nada.</summary>
	[Export] public float TailAir = 0.011f;

	/// <summary>Quanto o giro do corpo joga o rabo para fora da curva, em
	/// pesos do rabo por rad/s.</summary>
	[Export] public float TailTurn = 0.16f;

	/// <summary>Os ossos do rabo, da raiz à ponta. A última junta é só a
	/// ponta (não tem malha própria).</summary>
	public const string TailRoot = "rabo1";
	public const string TailEnd = "rabo5";

	private SpringBoneSimulator3D _tail;

	/// <summary>Quantas unidades do esqueleto cabem num metro do modelo (100
	/// no esqueleto do Mixamo, que é em centímetros).</summary>
	private float _tailUnit = 1f;

	/// <summary>O simulador do rabo, ou nulo quando o modelo não tem rabo
	/// (o lobo de um personagem sem a cadeia) ou não há o que desenhar.</summary>
	public SpringBoneSimulator3D Tail => _tail;

	private void BuildTail()
	{
		if (_body is Player { Driven: true }) return;

		Skeleton3D skeleton = AnimationKit.FindSkeleton(_model);
		if (skeleton == null || skeleton.FindBone(TailRoot) < 0 || skeleton.FindBone(TailEnd) < 0) return;

		_tail = new SpringBoneSimulator3D { Name = "Rabo" };
		skeleton.AddChild(_tail);
		_tail.SettingCount = 1;
		_tail.SetRootBoneName(0, TailRoot);
		_tail.SetEndBoneName(0, TailEnd);
		_tail.SetCenterFrom(0, SpringBoneSimulator3D.CenterFrom.Node);
		_tail.SetCenterNode(0, _tail.GetPathTo(_body));
		_tail.SetRotationAxis(0, SkeletonModifier3D.RotationAxis.All);
		_tail.SetEnableAllChildCollisions(0, true);

		// Da raiz à ponta: a raiz firme e leve, a ponta solta e pesada. O
		// valor é o maior dos dois e a curva o leva ao outro (as juntas só
		// existem depois do primeiro quadro do esqueleto: configurar uma a
		// uma aqui não pegava, e o rabo ficava sem gravidade nenhuma).
		// O simulador conta na unidade do esqueleto, e o do Mixamo é em
		// centímetros (o osso do rabo tem 17 de comprido, não 0,17): mola,
		// peso, grossura e força vão vezes essa unidade, senão o rabo fica
		// cem vezes mais duro de mexer do que os números dizem.
		_tailUnit = _model.GlobalBasis.Scale.X / Mathf.Max(skeleton.GlobalBasis.Scale.X, 1e-6f);
		Along(TailStiffnessRoot * _tailUnit, TailStiffnessTip * _tailUnit, _tail.SetStiffness, _tail.SetStiffnessDampingCurve);
		Along(TailGravityRoot * _tailUnit, TailGravityTip * _tailUnit, _tail.SetGravity, _tail.SetGravityDampingCurve);
		Along(TailRadiusRoot * _tailUnit, TailRadiusTip * _tailUnit, _tail.SetRadius, _tail.SetRadiusDampingCurve);
		_tail.SetDrag(0, TailDrag);
		_tail.SetGravityDirection(0, Vector3.Down);

		// O corpo que o rabo não atravessa: as duas coxas, as duas canelas
		// e o quadril. O osso do Mixamo aponta para a junta seguinte (+Y),
		// então a cápsula vai do osso até o filho.
		TailCollider(skeleton, "mixamorig_LeftUpLeg", "mixamorig_LeftLeg", 0.115f);
		TailCollider(skeleton, "mixamorig_RightUpLeg", "mixamorig_RightLeg", 0.115f);
		TailCollider(skeleton, "mixamorig_LeftLeg", "mixamorig_LeftFoot", 0.085f);
		TailCollider(skeleton, "mixamorig_RightLeg", "mixamorig_RightFoot", 0.085f);
		// O quadril, fino: a raiz do rabo nasce a 13 cm do osso, e uma cápsula
		// que a engolisse empurrava o rabo o tempo todo (ele ficava travado).
		TailCollider(skeleton, "mixamorig_Hips", "mixamorig_Spine", 0.09f);

		// O lobo aparece (virou lobo, renasceu) em outro lugar do mundo:
		// sem recomeçar, o rabo viria chicoteando de onde estava.
		VisibilityChanged += ResetTail;
	}

	/// <summary>Um valor que vai de <paramref name="root"/> (na raiz) a
	/// <paramref name="tip"/> (na ponta) ao longo da cadeia.</summary>
	private static void Along(float root, float tip, System.Action<int, float> value, System.Action<int, Curve> curve)
	{
		float top = Mathf.Max(Mathf.Max(root, tip), 1e-4f);
		var shape = new Curve();
		shape.AddPoint(new Vector2(0f, root / top), 0, 0, Curve.TangentMode.Linear, Curve.TangentMode.Linear);
		shape.AddPoint(new Vector2(1f, tip / top), 0, 0, Curve.TangentMode.Linear, Curve.TangentMode.Linear);
		value(0, top);
		curve(0, shape);
	}

	private void ResetTail()
	{
		if (_tail == null || !GodotObject.IsInstanceValid(_tail) || !IsVisibleInTree()) return;
		_tail.Reset();
		_tail.ExternalForce = Vector3.Zero;
		_tailKnown = false;
		_tailForce = Vector3.Zero;
	}

	private bool _tailKnown;
	private Vector3 _tailAt, _tailVelocity, _tailForce;
	private float _tailYaw;

	/// <summary>As forças que o movimento do corpo põe no rabo, a cada
	/// quadro. Lê a posição do corpo (vale para o jogador, o bot e o
	/// boneco da rede) e não decide nada.</summary>
	/// <summary>O maior passo de tempo que conta como movimento: quadro
	/// mais curto que isso (o jogo engasgou e soltou dois quadros juntos)
	/// faria a velocidade explodir; mais longo não muda nada.</summary>
	private const float TailMinDelta = 1f / 240f;

	/// <summary>A velocidade mais alta que o rabo lê, em m/s (o lobo mais
	/// rápido do jogo, no salto, passa pouco de 15). Acima disso é o boneco
	/// da rede corrigido de um salto só, não movimento.</summary>
	private const float TailMaxSpeed = 20f;

	private void UpdateTail(float delta)
	{
		if (_tail == null || !GodotObject.IsInstanceValid(_tail)) return;
		// Nada de força enorme, infinita ou NaN chegando ao simulador: ele
		// leva ao esqueleto, o esqueleto à caixa da malha, e uma caixa NaN
		// quebra o índice de luzes do renderizador (o "indexing did not
		// unpair geometries from light" do log do crash ao trocar de
		// partida, 25/11).
		if (delta < TailMinDelta) return;

		Vector3 at = _body.GlobalPosition;
		float yaw = _body.GlobalRotation.Y;
		// Renasceu ou foi teleportado: não é movimento, recomeça.
		if (!_tailKnown || at.DistanceTo(_tailAt) > 3f)
		{
			_tailKnown = true;
			_tailAt = at;
			_tailYaw = yaw;
			_tailVelocity = Vector3.Zero;
			_tailForce = Vector3.Zero;
			_tail.ExternalForce = Vector3.Zero;
			return;
		}

		Vector3 velocity = ((at - _tailAt) / delta).LimitLength(TailMaxSpeed);
		Vector3 acceleration = (velocity - _tailVelocity) / delta;
		float turn = Mathf.AngleDifference(_tailYaw, yaw) / delta;
		_tailAt = at;
		_tailVelocity = velocity;
		_tailYaw = yaw;

		// No referencial do corpo a aceleração aparece ao contrário.
		Vector3 force = (-acceleration / 9.8f * TailInertia).LimitLength(TailInertiaMax);
		// O ar empurra contra o movimento, com o quadrado da velocidade.
		force += -velocity * velocity.Length() * TailAir;
		// Virando, a ponta do rabo fica para trás da curva: para o lado
		// de onde o corpo veio.
		Vector3 right = _body.GlobalBasis.X;
		force -= right * Mathf.Clamp(turn * TailTurn, -TailInertiaMax, TailInertiaMax);

		float follow = 1f - Mathf.Exp(-delta / Mathf.Max(TailInertiaSmooth, 1e-3f));
		_tailForce = _tailForce.Lerp(force.LimitLength(TailInertiaMax * 2f), follow);
		if (!_tailForce.IsFinite() || !TailIsFinite())
		{
			// Último recurso: recomeça do desenho em vez de deixar o NaN andar.
			_tail.Reset();
			_tailKnown = false;
			_tailForce = Vector3.Zero;
		}
		_tail.ExternalForce = _tailForce * _tailUnit;
	}

	/// <summary>A ponta do rabo está num lugar de verdade (o simulador não
	/// devolveu NaN nem infinito).</summary>
	private bool TailIsFinite()
	{
		if (_tail.GetSkeleton() is not Skeleton3D skeleton) return true;
		int tip = skeleton.FindBone(TailEnd);
		return tip < 0 || skeleton.GetBoneGlobalPose(tip).Origin.IsFinite();
	}

	/// <summary>Uma cápsula presa ao osso <paramref name="bone"/>, do osso
	/// até <paramref name="child"/>.</summary>
	private void TailCollider(Skeleton3D skeleton, string bone, string child, float radius)
	{
		int from = skeleton.FindBone(bone), to = skeleton.FindBone(child);
		if (from < 0 || to < 0) return;

		// No espaço do osso (na unidade do esqueleto): o filho está em rest.Origin.
		Transform3D parent = skeleton.GetBoneGlobalRest(from);
		Vector3 tip = parent.AffineInverse() * skeleton.GetBoneGlobalRest(to).Origin;
		float length = tip.Length();
		if (length < 1e-4f) return;

		var capsule = new SpringBoneCollisionCapsule3D { Name = "Rabo_" + bone };
		_tail.AddChild(capsule);
		capsule.BoneName = bone;
		capsule.PositionOffset = tip * 0.5f;
		// A cápsula do Godot é ao longo de Y: gira o Y para a direção do filho.
		capsule.RotationOffset = new Quaternion(Vector3.Up, tip / length);
		capsule.Radius = radius * _tailUnit;
		capsule.Height = length + 2f * radius * _tailUnit;
	}
}
