using Godot;
using System.Collections.Generic;

/// <summary>
/// A poeira da corrida de quatro (pedido do Jhon, 18/11: "ao correr com o
/// W W com o lobo, deve sair leves pontinhos da cor do chão que ele está
/// pisando, para dar efeito de corrida").
///
/// (No mesmo dia: "devem ser redondos e menores, sem ser muito sólido,
/// geométrico" — cada grão é um ponto redondo de borda macia, de 2 a 6 cm.)
///
/// A cada passada do galope saem alguns grãos pequenos de onde as patas
/// batem, jogados para trás e para cima, que caem e somem em menos de meio
/// segundo. **A cor é a do chão embaixo do lobo**: um raio para baixo acha
/// a peça, e a cor vem dela (<c>metadata/mapa_cor</c>, a mesma do
/// minimapa; senão o material da malha; senão um cinza de concreto),
/// um pouco clareada para ler como poeira.
///
/// Leve de propósito: é tempero, não fumaça. Fica no mundo — o lobo passa
/// e a poeira fica para trás. Os grãos são simulados aqui mesmo (no máximo
/// 90) e desenhados de uma vez só (MultiMesh).
/// </summary>
public partial class SprintDust : Node3D
{
	/// <summary>Grãos por segundo, na corrida cheia.</summary>
	[Export] public float Rate = 48f;

	/// <summary>Tamanho do grão, em metros.</summary>
	[Export] public float GrainSize = 0.04f;

	[Export] public Color Fallback = new Color(0.56f, 0.55f, 0.53f);

	/// <summary>Grãos soltos até agora. Para os testes.</summary>
	public int Emitted { get; private set; }

	/// <summary>A cor do último chão lido. Para os testes.</summary>
	public Color LastColor { get; private set; }

	private const string ColorMeta = "mapa_cor";

	private struct Grain
	{
		public Vector3 At, Velocity;
		public Color Color;
		public float Age, Life, Size;
	}

	private const int Max = 90;

	private readonly Grain[] _pool = new Grain[Max];
	private int _live;
	private MultiMesh _mesh;
	private MultiMeshInstance3D _grains;
	private readonly RandomNumberGenerator _rng = new();
	private readonly Dictionary<ulong, Color> _colors = new();
	private double _debt, _sinceRay;
	private Color _floor;

	/// <summary>Grãos no ar agora. Para os testes.</summary>
	public int Live => _live;

	public override void _Ready()
	{
		_rng.Randomize();
		_floor = Fallback;

		// Simulados aqui (são poucos) e desenhados de uma vez: cada grão é
		// um quadradinho de frente para a câmera, com a própria cor.
		_mesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseColors = true,
			Mesh = new QuadMesh
			{
				Size = Vector2.One,
				Material = new StandardMaterial3D
				{
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
					AlbedoTexture = Dot(),
					VertexColorUseAsAlbedo = true,
					Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
					BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
					BillboardKeepScale = true
				}
			},
			InstanceCount = Max,
			VisibleInstanceCount = 0
		};
		_grains = new MultiMeshInstance3D
		{
			Name = "Graos",
			Multimesh = _mesh,
			TopLevel = true,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			CustomAabb = new Aabb(new Vector3(-500, -50, -500), new Vector3(1000, 200, 1000))
		};
		AddChild(_grains);
		_grains.GlobalTransform = Transform3D.Identity;
	}

	/// <summary>
	/// Um quadro da corrida. <paramref name="running"/>: correndo de quatro,
	/// no chão. <paramref name="feet"/>: o ponto do chão embaixo do corpo.
	/// </summary>
	public void Tick(CharacterBody3D body, bool running, Vector3 feet, double delta)
	{
		if (_mesh == null) return;
		Spawn(body, running, feet, delta);
		Simulate((float)delta);
	}

	private void Spawn(CharacterBody3D body, bool running, Vector3 feet, double delta)
	{
		if (!running) { _debt = 0; return; }

		Vector3 velocity = body.Velocity;
		var flat = new Vector3(velocity.X, 0, velocity.Z);
		float speed = flat.Length();
		if (speed < 3f) return;

		_sinceRay -= delta;
		if (_sinceRay <= 0)
		{
			_sinceRay = 0.12;
			ReadFloor(body, feet);
		}

		_debt += Rate * delta;
		Vector3 back = -flat / speed;
		Vector3 side = back.Cross(Vector3.Up);
		while (_debt >= 1.0)
		{
			_debt -= 1.0;
			if (_live >= Max) continue;

			// Das patas: um pouco para os lados, rente ao chão, chutado
			// para trás e para cima.
			// A cor do chão, um pouco mais clara ou mais escura a cada grão.
			float shade = _rng.RandfRange(-0.12f, 0.2f);
			Color color = shade >= 0f ? _floor.Lightened(shade) : _floor.Darkened(-shade);
			color.A = _rng.RandfRange(0.5f, 0.85f);
			_pool[_live++] = new Grain
			{
				// Das quatro patas: das mãos (quase um metro à frente do
				// quadril) aos pés, abrindo para os lados.
				At = feet + side * _rng.RandfRange(-0.42f, 0.42f) + back * _rng.RandfRange(-0.95f, 0.3f) + Vector3.Up * 0.03f,
				Velocity = back * _rng.RandfRange(0.4f, 1.8f) + side * _rng.RandfRange(-1.3f, 1.3f) + Vector3.Up * _rng.RandfRange(1.3f, 3.2f),
				Color = color,
				Life = _rng.RandfRange(0.35f, 0.6f),
				Size = GrainSize * _rng.RandfRange(0.6f, 1.5f)
			};
			Emitted++;
		}
	}

	/// <summary>Um ponto redondo e macio: o grão não é um quadrado.</summary>
	private static GradientTexture2D Dot()
	{
		var gradient = new Gradient();
		gradient.SetColor(0, Colors.White);
		gradient.SetColor(1, new Color(1, 1, 1, 0));
		gradient.SetOffset(0, 0.12f);
		gradient.SetOffset(1, 1f);
		return new GradientTexture2D
		{
			Gradient = gradient, Width = 32, Height = 32,
			Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f)
		};
	}

	private void Simulate(float delta)
	{
		for (int i = 0; i < _live; i++)
		{
			Grain g = _pool[i];
			g.Age += delta;
			if (g.Age >= g.Life)
			{
				_pool[i--] = _pool[--_live];
				continue;
			}

			g.Velocity += Vector3.Down * 7f * delta;
			g.Velocity *= 1f - Mathf.Min(2.2f * delta, 0.9f);
			g.At += g.Velocity * delta;
			_pool[i] = g;

			float t = g.Age / g.Life;
			float size = g.Size * (t < 0.2f ? 0.7f + 1.5f * t : 1f - 0.6f * (t - 0.2f));
			Color color = g.Color;
			color.A *= t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
			_mesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * size), g.At));
			_mesh.SetInstanceColor(i, color);
		}

		_mesh.VisibleInstanceCount = _live;
	}

	private void ReadFloor(CharacterBody3D body, Vector3 feet)
	{
		var query = PhysicsRayQueryParameters3D.Create(feet + Vector3.Up * 0.5f, feet + Vector3.Down * 0.6f);
		query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };
		var hit = body.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0 || hit["collider"].AsGodotObject() is not Node collider) return;

		ulong id = collider.GetInstanceId();
		if (!_colors.TryGetValue(id, out Color color))
		{
			color = ColorOf(collider);
			_colors[id] = color;
		}

		_floor = color;
		LastColor = color;
	}

	/// <summary>A cor de uma peça do mapa: a gravada nela, a do material
	/// da malha que ela carrega (ou da vizinha), ou o cinza.</summary>
	private Color ColorOf(Node collider)
	{
		if (collider.HasMeta(ColorMeta)) return collider.GetMeta(ColorMeta).AsColor();

		foreach (Node around in new[] { collider, collider.GetParent() })
		{
			if (around == null) continue;
			if (Albedo(around) is Color own) return own;
			foreach (Node child in around.GetChildren())
				if (Albedo(child) is Color found) return found;
		}

		return Fallback;
	}

	private static Color? Albedo(Node node)
	{
		if (node is not MeshInstance3D mesh) return null;
		Material material = mesh.MaterialOverride ?? (mesh.Mesh != null && mesh.Mesh.GetSurfaceCount() > 0 ? mesh.GetActiveMaterial(0) : null);
		return material is BaseMaterial3D standard ? standard.AlbedoColor : null;
	}
}
