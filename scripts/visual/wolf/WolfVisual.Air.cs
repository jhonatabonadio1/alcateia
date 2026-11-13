using Godot;
using static AnimationKit;
using System.Collections.Generic;

/// <summary>
/// Fora do chão: a corrida na parede (o corpo deitado na face), o voo do
/// pulo pela velocidade vertical e a inclinação do bote.
/// </summary>
public partial class WolfVisual
{
	/// <summary>Velocidade do clipe de engatinhar na parede, em m/s
	/// (o Running Crawl anda 1,40 m em 0,67 s, vezes a escala do modelo).
	/// Mais rápido que isso o clipe acelera junto, até o dobro e meio.</summary>
	[ExportGroup("Parede, pulo e bote")]
	[Export] public float WallClipSpeed = 2.3f;

	/// <summary>Tempo para deitar na parede e para voltar, em segundos.</summary>
	[Export] public float WallTurnTime = 0.12f;

	/// <summary>Inclinação máxima do corpo no bote, em graus.</summary>
	[Export] public float DiveLeanMax = 40f;

	private bool _onWall;
	private float _wallBlend;
	private Vector3 _lastNormal;
	private Vector3 _lastAlong;
	private float _lean;

	/// <summary>
	/// No bote o corpo tomba para a frente, na inclinação do mergulho: o
	/// clipe cai de pé, com os braços para cima, e sem a inclinação o
	/// lobo parecia despencar em vez de avançar no alvo. Gira em volta do
	/// quadril, não dos pés, e volta rápido ao encostar.
	/// </summary>
	private void UpdateLean(float delta)
	{
		float target = 0f;
		if (_weapon.IsDiving)
		{
			Vector3 v = _weapon.DiveVelocity;
			float descent = Mathf.RadToDeg(Mathf.Atan2(-v.Y, new Vector2(v.X, v.Z).Length()));
			target = Mathf.Clamp(descent * 0.8f, 10f, DiveLeanMax);
		}

		float rate = target > _lean ? 400f : 250f;
		_lean = Mathf.MoveToward(_lean, target, rate * delta);
	}

	private const float LeanPivot = 0.95f;

	/// <summary>
	/// Na parede, o lobo engatinha com a barriga virada para ela: o
	/// corpo deita de lado, os pés na parede e as costas para fora, de
	/// frente para o sentido da corrida. Parede à esquerda deita para a
	/// esquerda, à direita para a direita — quem decide é a normal da
	/// parede, não o lado da câmera.
	///
	/// Gira só o modelo, não o corpo: a cápsula, a câmera e a mira
	/// continuam de pé, como a física as deixa. A entrada e a saída são
	/// suaves (<see cref="WallTurnTime"/>), senão o bicho estala de lado.
	/// </summary>
	private void UpdateWall(float delta)
	{
		if (_player == null) return;

		float target = _onWall ? 1f : 0f;
		_wallBlend = Mathf.MoveToward(_wallBlend, target, delta / Mathf.Max(WallTurnTime, 0.01f));

		var rest = new Transform3D(
			Basis.FromEuler(new Vector3(0, Mathf.DegToRad(ModelYaw) + _yawOffset, 0)).Scaled(Vector3.One * ModelScale),
			Vector3.Zero);

		if (_lean > 0.01f)
		{
			// Nariz para baixo: girar em X negativo leva o −Z (a frente do
			// corpo) para baixo. O pivô fica na altura do quadril.
			var pivot = new Vector3(0, LeanPivot, 0);
			var tilt = new Transform3D(new Basis(Vector3.Right, -Mathf.DegToRad(_lean)), Vector3.Zero);
			rest = new Transform3D(Basis.Identity, pivot) * tilt * new Transform3D(Basis.Identity, -pivot) * rest;
		}

		if (_wallBlend <= 0f)
		{
			_model.Transform = rest;
			return;
		}

		// Saindo da parede, a normal some antes de o corpo terminar de
		// levantar: a volta usa a última parede conhecida.
		if (_player.WallNormal != Vector3.Zero && _player.WallAlong != Vector3.Zero)
		{
			_lastNormal = _player.WallNormal;
			_lastAlong = _player.WallAlong;
		}

		Vector3 normal = _lastNormal;
		Vector3 along = _lastAlong;
		if (normal == Vector3.Zero || along == Vector3.Zero)
		{
			_model.Transform = rest;
			return;
		}

		// Clipe do Mixamo olha para +Z: −Z do modelo fica contra o
		// sentido da corrida, e o "em cima" do bicho é a normal.
		Basis basis = Basis.LookingAt(-along, normal).Scaled(Vector3.One * ModelScale);

		// Os pés tocam a parede: o plano dela fica a um raio da cápsula
		// do centro do corpo.
		Vector3 center = _body.GlobalPosition;
		var onWall = new Transform3D(basis, center - normal * WallGap);

		Transform3D local = GlobalTransform.AffineInverse() * onWall;
		_model.Transform = rest.InterpolateWith(local, Smooth(_wallBlend));

		// Engatinhar no ritmo da corrida; agarrado (parado), o clipe para.
		float speed = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z).Length();
		_tree.Set("parameters/Machine/Wall/Speed/scale", Mathf.Clamp(speed / Mathf.Max(WallClipSpeed, 0.01f), 0f, 2.5f));
	}

	/// <summary>Velocidade da aterrissagem do pulo: o clipe inteiro de
	/// pé de novo em 0,6 s.</summary>
	private const float JumpLandSpeed = 1.6f;
	private bool _jumping;

	/// <summary>
	/// O ponto do voo pela velocidade vertical: saindo do chão a toda, o
	/// começo do clipe; parado no alto, o topo; caindo tão rápido quanto
	/// saiu, os pés esticados para o chão. Pulo mais longo que o do clipe
	/// fica nessa última pose até encostar; mais curto (caiu em cima de
	/// um caixote) sai antes. Assim o topo do clipe é o topo do pulo, seja
	/// qual for a altura dele.
	/// </summary>
	private void UpdateJump()
	{
		WolfClip clip = WolfMoves.Clips["jump"];
		float apex = clip.Impact - clip.Start;
		float fall = clip.Length - apex;
		float vy = _body.Velocity.Y;

		// Subindo de um pulo: da saída ao topo, pela velocidade vertical.
		// Descendo — de um pulo, de um salto da parede ou de uma queda —:
		// a pose do topo, encolhido e de braços para cima, até o chão
		// chegar perto; aí as pernas esticam para pousar. Pela velocidade,
		// uma queda longa (saltar da parede) ficava de pernas esticadas e
		// parado no ar o caminho todo.
		float t;
		if (vy > 0f && _player != null && _player.IsJumpAirborne)
			t = apex * (1f - Mathf.Clamp(vy / Mathf.Max(_player.JumpSpeed, 0.1f), 0f, 1f));
		else
			t = apex + fall * 0.92f * (1f - Mathf.Clamp(GroundBelow() / LegReach, 0f, 1f));

		_tree.Set("parameters/Machine/Jump/Seek/seek_request", t);
	}

	/// <summary>A que altura do chão as pernas começam a esticar para
	/// pousar, em metros dos pés.</summary>
	private const float LegReach = 1.0f;
	private float _airTime;

	/// <summary>Distância dos pés até o chão logo abaixo, em metros.
	/// Sem chão ao alcance, longe.</summary>
	private float GroundBelow()
	{
		float feet = _player != null ? _player.FeetOffset : -0.875f;
		Vector3 from = _body.GlobalPosition + Vector3.Up * feet;
		var query = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * (LegReach + 0.5f));
		query.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
		var hit = _body.GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count == 0 ? LegReach * 2f : from.Y - ((Vector3)hit["position"]).Y;
	}

	/// <summary>Distância do centro do corpo à parede: o raio da cápsula.</summary>
	private const float WallGap = 0.3f;
	private static float Smooth(float t) => t * t * (3f - 2f * t);
}
