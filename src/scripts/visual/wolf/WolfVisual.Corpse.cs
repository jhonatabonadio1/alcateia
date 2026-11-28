using Godot;

/// <summary>
/// O lobo morto: cai com o clipe de morte, fica no chão e some por igual, como
/// o humano (<see cref="DeadBody"/>; Jhon, 26/11). Até 26/11 ele sumia de uma
/// vez 1,2 s depois do clipe, sem desbotar, e o "mutant dying" do Mixamo o
/// enterrava: 28 cm dentro do chão no meio da queda, 23 cm no fim.
/// </summary>
public partial class WolfVisual
{
	/// <summary>Tempo que o lobo morto fica à mostra no chão depois do clipe
	/// de morte, e quanto leva para sumir (os mesmos do humano).</summary>
	[ExportGroup("Corpo morto")]
	[Export] public float CorpseHold = 1.2f;
	[Export] public float CorpseFade = 1.2f;

	private DeadBody _deadBody;

	/// <summary>
	/// O chão do rabo do lobo morto (<see cref="TailFloor"/>): o rabo é uma mola
	/// com gravidade (WolfVisual.Tail) e, deitado, pendia 44 cm para dentro do
	/// chão; o ajuste o dobra para ficar pousado nele.
	///
	/// **E o rabo não entra na medida que encosta o corpo no chão**
	/// (<see cref="DeadBody.LooseBones"/>; Jhon, 27/11: "o lobo, ao morrer,
	/// flutua"; "parece que o lobo encosta, porém o rabo dele o levanta"). O
	/// rabo era sempre o ponto mais baixo, o corpo subia para tirá-lo do chão,
	/// o rabo esticava e pousava de novo, e o corpo ia parar 60 cm no ar
	/// (medido: quadril a 62 cm, ponta do rabo no chão).
	/// </summary>
	private TailFloor _tailFloor;

	/// <summary>A cadeia do rabo, da raiz à ponta (rabo1..rabo5).</summary>
	private static readonly string[] TailBones = { "rabo1", "rabo2", "rabo3", "rabo4", "rabo5" };

	private void BeginCorpse()
	{
		if (_model == null) return;
		_deadBody ??= new DeadBody(_model, _body, this);
		_deadBody.Hold = CorpseHold;
		_deadBody.Fade = CorpseFade;
		// O rabo é mola: não entra na medida do chão.
		_deadBody.LooseBones = TailBones;
		LayTailFloor();
		// (o lugar do modelo é o de pé, de agora — WolfVisual.OnDied —, não o da primeira morte)
		_deadBody.Begin(_model.Transform);
	}

	/// <summary>Põe o chão do rabo na altura do chão de verdade, debaixo da
	/// raiz do rabo (o corpo deitado tem dois metros: o chão do rabo pode não
	/// ser o dos pés). A cada quadro: quem morre no ar ainda vai cair. Sem
	/// chão ao alcance — caindo de alto —, o rabo fica solto até haver.</summary>
	private void LayTailFloor()
	{
		if (_tail == null || !GodotObject.IsInstanceValid(_tail) || !_tail.IsInsideTree()) return;
		if (_tail.GetSkeleton() is not Skeleton3D skeleton) return;

		if (_tailFloor == null || !GodotObject.IsInstanceValid(_tailFloor))
		{
			// Depois da mola, na ordem dos filhos do esqueleto: ajusta o que ela deixou.
			_tailFloor = new TailFloor { Name = "ChaoDoRabo", Chain = TailBones };
			skeleton.AddChild(_tailFloor);
			skeleton.MoveChild(_tailFloor, _tail.GetIndex() + 1);
		}

		int root = skeleton.FindBone(TailRoot);
		Vector3 at = root >= 0 ? skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(root).Origin : _body.GlobalPosition;
		_tailFloor.HasFloor = DeadBody.FloorUnder(this, _body, at, out float floor);
		_tailFloor.FloorY = floor;
	}

	private void UpdateCorpse(double delta)
	{
		LayTailFloor();
		_deadBody?.Step(delta, _deadTimer, _lengths.TryGetValue("death", out float clip) ? clip : 1.5f);
		// Deitado num declive (a escada), o chão do rabo é o plano do chão do corpo.
		if (_tailFloor == null || !GodotObject.IsInstanceValid(_tailFloor)) return;
		bool sloped = _deadBody is { HasGround: true };
		_tailFloor.Normal = sloped ? _deadBody.GroundNormal : Vector3.Up;
		if (sloped) _tailFloor.Point = _deadBody.GroundPoint;
	}

	private void ResetCorpse()
	{
		_deadBody?.Reset();
		if (_tailFloor != null && GodotObject.IsInstanceValid(_tailFloor)) _tailFloor.QueueFree();
		_tailFloor = null;
	}

	/// <summary>O lobo tem algum material do sumiço agora.</summary>
	public bool HasFadeMaterials => _deadBody?.HasFadeMaterials ?? false;
}
