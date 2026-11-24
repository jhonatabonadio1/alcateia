using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O feixe de luz que a garra deixa no golpe: a cada quadro guarda a base
/// e a ponta da garra e monta uma faixa entre as posições recentes
/// (<see cref="ImmediateMesh"/>, em coordenadas do mundo).
///
/// A garra é o osso da mão do lobo, esticado para a frente na direção do
/// antebraço (o lobo não tem osso de dedo). Quem liga e desliga é o
/// <see cref="WolfFx"/>, na janela do golpe.
///
/// A força de cada ponto acompanha a velocidade da ponta: a mão que bate
/// risca forte, a outra quase nada. Assim as duas garras podem ficar
/// ligadas no mesmo golpe e só a que golpeia aparece — a alternância
/// (garra primária e secundária) sai sozinha.
/// </summary>
public partial class ClawTrail : Node3D
{
	/// <summary>Quanto o rastro dura, em segundos.</summary>
	[Export] public float Lifetime = 0.22f;

	/// <summary>Quantos pontos guarda no máximo (a qualidade baixa usa menos).</summary>
	[Export] public int MaxPoints = 32;

	/// <summary>Comprimento da garra além do osso da mão, em metros.</summary>
	[Export] public float ClawLength = 0.32f;

	/// <summary>Velocidade da ponta (m/s) a partir da qual o rastro aparece
	/// inteiro. Abaixo disso, vai sumindo.</summary>
	[Export] public float FullSpeed = 6f;

	public bool Active { get; set; }

	/// <summary>Quantos pontos o rastro tem agora e a força do mais forte
	/// (para os testes).</summary>
	public int Points => _samples.Count;
	public float Strongest => _samples.Count == 0 ? 0f : _samples.Max(x => x.Strength);

	/// <summary>A velocidade da ponta da garra no último quadro (m/s).</summary>
	public float TipSpeed { get; private set; }

	public Color Color
	{
		set => _meshInstance?.SetInstanceShaderParameter("trail_color", value);
	}

	private Skeleton3D _skeleton;
	private int _hand = -1, _arm = -1;

	private struct Sample
	{
		public Vector3 Base;
		public Vector3 Tip;
		public float Time;
		public float Strength;
	}

	private readonly List<Sample> _samples = new();
	private ImmediateMesh _mesh;
	private MeshInstance3D _meshInstance;
	private double _clock;
	private Vector3 _lastTip;
	private bool _hasLast;
	private bool _wasActive;

	/// <summary>Monta o rastro na mão <paramref name="hand"/> do esqueleto.</summary>
	public static ClawTrail For(Skeleton3D skeleton, string handSuffix, Material material)
	{
		int hand = -1;
		for (int i = 0; i < skeleton.GetBoneCount(); i++)
			if (skeleton.GetBoneName(i).EndsWith(handSuffix)) { hand = i; break; }
		if (hand < 0) return null;

		var trail = new ClawTrail
		{
			Name = $"Rastro{handSuffix}",
			_skeleton = skeleton,
			_hand = hand,
			_arm = skeleton.GetBoneParent(hand),
		};
		trail._mesh = new ImmediateMesh();
		trail._meshInstance = new MeshInstance3D
		{
			Name = "Faixa",
			Mesh = trail._mesh,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			TopLevel = true,
			// O AABB muda a cada quadro: sem isto o rastro some quando o
			// lugar onde a malha nasceu sai da tela.
			ExtraCullMargin = 4f,
		};
		trail.AddChild(trail._meshInstance);
		return trail;
	}

	public override void _Ready()
	{
		if (_meshInstance != null) _meshInstance.GlobalTransform = Transform3D.Identity;
	}

	public override void _Process(double delta)
	{
		if (_skeleton == null || !IsInstanceValid(_skeleton)) return;
		_clock += delta;

		Transform3D handPose = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_hand);
		Vector3 hand = handPose.Origin;
		Vector3 along = _arm >= 0
			? hand - (_skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_arm)).Origin
			: handPose.Basis.Y;
		along = along.LengthSquared() > 1e-8f ? along.Normalized() : Vector3.Forward;
		// O lobo é 1,1× o tamanho do modelo: a garra também.
		Vector3 tip = hand + along * ClawLength * WolfMoves.BodyScale;

		float speed = _hasLast && delta > 0 ? (tip - _lastTip).Length() / (float)delta : 0f;
		_lastTip = tip;
		TipSpeed = speed;
		_hasLast = true;

		// Golpe novo: o rastro começa do zero. Sem isto a fita ligava o fim
		// do golpe anterior ao começo deste com um risco reto.
		if (Active && !_wasActive) _samples.Clear();
		_wasActive = Active;

		if (Active)
		{
			_samples.Add(new Sample
			{
				Base = hand + along * 0.05f,
				Tip = tip,
				Time = (float)_clock,
				Strength = Mathf.Clamp((speed - FullSpeed * 0.25f) / (FullSpeed * 0.75f), 0f, 1f)
			});
			while (_samples.Count > MaxPoints) _samples.RemoveAt(0);
		}

		while (_samples.Count > 0 && _clock - _samples[0].Time > Lifetime)
			_samples.RemoveAt(0);

		Rebuild();
	}

	private void Rebuild()
	{
		_mesh.ClearSurfaces();
		if (_samples.Count < 2)
		{
			_meshInstance.Visible = false;
			return;
		}

		_meshInstance.Visible = true;
		_mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
		for (int i = 0; i < _samples.Count; i++)
		{
			Sample s = _samples[i];
			float age = Mathf.Clamp((float)((_clock - s.Time) / Lifetime), 0f, 1f);
			var color = new Color(1f, 1f, 1f, (1f - age) * s.Strength);
			float u = (float)i / (_samples.Count - 1);

			_mesh.SurfaceSetColor(color);
			_mesh.SurfaceSetUV(new Vector2(u, 0f));
			_mesh.SurfaceAddVertex(s.Base);

			_mesh.SurfaceSetColor(color);
			_mesh.SurfaceSetUV(new Vector2(u, 1f));
			_mesh.SurfaceAddVertex(s.Tip);
		}
		_mesh.SurfaceEnd();
	}

	/// <summary>Limpa o rastro (morreu, voltou a humano).</summary>
	public void Clear()
	{
		_samples.Clear();
		Active = false;
	}
}
