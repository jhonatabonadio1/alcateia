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

	/// <summary>O chão para o rabo do lobo morto. O rabo é uma mola com gravidade
	/// (WolfVisual.Tail) e só bate nas pernas e no quadril: deitado, ele pendia
	/// para dentro do chão (o rabo4 a 42 cm abaixo dele), e o corpo, empurrado
	/// para tirá-lo dali, ficava 19 cm no ar. Morto, o rabo encosta neste plano.</summary>
	private SpringBoneCollisionPlane3D _tailFloor;

	private void BeginCorpse()
	{
		if (_model == null) return;
		_deadBody ??= new DeadBody(_model, _body);
		_deadBody.Hold = CorpseHold;
		_deadBody.Fade = CorpseFade;
		LayTailFloor();
		_deadBody.Begin();
	}

	private void LayTailFloor()
	{
		if (_tail == null || !GodotObject.IsInstanceValid(_tail)) return;
		Vector3 at = _body.GlobalPosition;
		if (!DeadBody.FloorUnder(this, _body, at, out float floor)) return;
		if (_tailFloor == null || !GodotObject.IsInstanceValid(_tailFloor))
		{
			_tailFloor = new SpringBoneCollisionPlane3D { Name = "ChaoDoMorto" };
			_tail.AddChild(_tailFloor);
		}
		_tailFloor.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(at.X, floor, at.Z));
	}

	private void UpdateCorpse(double delta) =>
		_deadBody?.Step(delta, _deadTimer, _lengths.TryGetValue("death", out float clip) ? clip : 1.5f);

	private void ResetCorpse()
	{
		_deadBody?.Reset();
		if (_tailFloor != null && GodotObject.IsInstanceValid(_tailFloor)) _tailFloor.QueueFree();
		_tailFloor = null;
	}

	/// <summary>O lobo tem algum material do sumiço agora.</summary>
	public bool HasFadeMaterials => _deadBody?.HasFadeMaterials ?? false;
}
