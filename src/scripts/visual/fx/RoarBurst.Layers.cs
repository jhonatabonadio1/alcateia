using Godot;

/// <summary>
/// As camadas de shader do rugido (<see cref="RoarBurst"/>): o que fita de luz
/// não desenha bem. Cada uma é uma malha simples com um material próprio
/// (<c>src/assets/efeitos/rugido_*.gdshader</c>, tudo por conta, sem textura):
///
/// - o **chão** (<c>rugido_chao</c>): os veios de luz, a poça e as ondas;
/// - a **onda** (<c>rugido_onda</c>): o mesmo quadrado, entortando a imagem
///   por onde o anel passa;
/// - a **casca** (<c>rugido_escudo</c>): o escudo fechando de baixo para cima;
/// - a **bolha** (<c>rugido_bolha</c>): a lente de pressão dos dois estouros;
/// - o **sopro** (<c>rugido_sopro</c>): o cone de ar da boca.
///
/// O shader é do arquivo (o Godot guarda um só); o material é de cada lobo,
/// porque os números mudam por lobo. Cada camada só é desenhada enquanto tem
/// o que mostrar. A onda e a bolha leem a tela: só da qualidade Alta para cima.
/// </summary>
public partial class RoarBurst
{
	private const string ShaderDir = "res://src/assets/efeitos/";

	/// <summary>Meio lado do quadrado do chão, em metros: as ondas vão até
	/// 3,4 m e somem antes da borda.</summary>
	[Export] public float GroundRadius = 4.0f;

	/// <summary>A casca: meia largura e meia altura, e a altura do centro
	/// acima dos pés. O topo fica a 2,20 m: abaixo de <see cref="ShaftTop"/>.</summary>
	[Export] public float ShellWidth = 0.95f;
	[Export] public float ShellHalfHeight = 1.1f;
	[Export] public float ShellCenter = 1.1f;

	/// <summary>O sopro: comprimento em metros e a meia abertura no fim.</summary>
	[Export] public float BreathLength = 1.9f;
	[Export] public float BreathSpread = 0.62f;

	private MeshInstance3D _floor, _warp, _shellMesh, _bubble, _breath;
	private ShaderMaterial _floorMat, _warpMat, _shellMat, _bubbleMat, _breathMat;
	private readonly Vector4[] _waveData = new Vector4[4];
	private float _layerSeed;

	private void BuildLayers()
	{
		var square = new PlaneMesh { Size = Vector2.One * 2f };
		// A onda desenha antes (prioridade −1) do que soma luz: senão ela
		// redesenharia o chão por cima do brilho.
		_warp = Layer("Onda", square, "rugido_onda.gdshader", -1, out _warpMat);
		_floor = Layer("Chao", square, "rugido_chao.gdshader", 0, out _floorMat);

		var ball = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 32, Rings = 16 };
		_bubble = Layer("Bolha", ball, "rugido_bolha.gdshader", -1, out _bubbleMat);
		_shellMesh = Layer("Casca", ball, "rugido_escudo.gdshader", 0, out _shellMat);

		// O cone: a ponta fina na boca (embaixo), aberto no fim (em cima).
		var cone = new CylinderMesh
		{
			BottomRadius = 0.07f, TopRadius = BreathSpread, Height = 1f,
			RadialSegments = 20, Rings = 6, CapTop = false, CapBottom = false
		};
		_breath = Layer("Sopro", cone, "rugido_sopro.gdshader", 0, out _breathMat);
	}

	private MeshInstance3D Layer(string name, Mesh mesh, string shader, int priority, out ShaderMaterial material)
	{
		material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderDir + shader), RenderPriority = priority };
		var layer = new MeshInstance3D
		{
			Name = name,
			Mesh = mesh,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			GIMode = GeometryInstance3D.GIModeEnum.Disabled
		};
		AddChild(layer);
		return layer;
	}

	/// <summary>Acerta as camadas para este quadro. Com <paramref name="warm"/>,
	/// mostra todas com tudo em zero (não se vê nada): o shader compila ao
	/// nascer, e não no primeiro rugido no meio da luta.</summary>
	private void PushLayers(bool warm)
	{
		Color tint = Tint;
		var color = new Vector3(tint.R, tint.G, tint.B);
		float blast = _blast >= 0f ? 1f - _blast / BlastLife : 0f;
		bool lens = Detail >= 2;

		// As ondas: as quatro mais novas, abrindo com freada (rápido no começo).
		float strongest = 0f;
		for (int i = 0; i < _waveData.Length; i++)
		{
			int at = _waves.Count - 1 - i;
			if (at < 0) { _waveData[i] = Vector4.Zero; continue; }
			Wave w = _waves[at];
			float t = w.Age / w.Life;
			float open = 1f - Mathf.Pow(1f - t, 3f);
			float strength = w.Strength * (1f - t) * (1f - t);
			strongest = Mathf.Max(strongest, strength);
			_waveData[i] = new Vector4(0.3f + w.Reach * open, strength, w.Width * (0.6f + 0.6f * t), 0f);
		}

		float floorY = Feet + 0.04f;
		bool floorOn = warm || (Grounded && (_ground > 0.004f || _waves.Count > 0 || _sweep >= 0f));
		_floor.Visible = floorOn;
		if (floorOn)
		{
			_floor.Position = new Vector3(0, floorY, 0);
			_floor.Scale = new Vector3(GroundRadius, 1f, GroundRadius);
			_floorMat.SetShaderParameter("cor", color);
			_floorMat.SetShaderParameter("raio", GroundRadius);
			_floorMat.SetShaderParameter("carga", warm ? 0f : _charge);
			_floorMat.SetShaderParameter("brilho", warm ? 0f : _ground);
			_floorMat.SetShaderParameter("estouro", warm ? 0f : Flash);
			_floorMat.SetShaderParameter("ondas", _waveData);
			_floorMat.SetShaderParameter("semente", _layerSeed);
		}

		bool warpOn = warm || (lens && Grounded && strongest > 0.06f);
		_warp.Visible = warpOn;
		if (warpOn)
		{
			_warp.Position = new Vector3(0, floorY + 0.02f, 0);
			_warp.Scale = new Vector3(GroundRadius, 1f, GroundRadius);
			_warpMat.SetShaderParameter("raio", GroundRadius);
			_warpMat.SetShaderParameter("ondas", _waveData);
		}

		var center = new Vector3(0, Feet + ShellCenter, 0);
		bool shellOn = warm || _shell > 0.004f || _sweep >= 0f;
		_shellMesh.Visible = shellOn;
		if (shellOn)
		{
			// No escudo cheio a casca dá um pulo para fora e volta.
			float pop = 1f + 0.07f * Flash;
			_shellMesh.Position = center;
			_shellMesh.Scale = new Vector3(ShellWidth * pop, ShellHalfHeight, ShellWidth * pop);
			_shellMat.SetShaderParameter("cor", color);
			_shellMat.SetShaderParameter("nivel", warm ? 0f : _level);
			_shellMat.SetShaderParameter("forca", warm ? 0f : _shell);
			_shellMat.SetShaderParameter("estalo", warm ? 0f : Flash * Flash);
			_shellMat.SetShaderParameter("varre", _sweep >= 0f ? Mathf.SmoothStep(0f, 1f, _sweep / (SweepLife * 0.7f)) * 1.15f : -1f);
		}

		// A bolha: pequena e fraca no estouro do começo, maior no escudo cheio.
		// Nunca passa do teto da luz.
		float seal = Flash;
		float bubble = Mathf.Max(blast * 0.65f, seal);
		bool bubbleOn = warm || (lens && bubble > 0.02f);
		_bubble.Visible = bubbleOn;
		if (bubbleOn)
		{
			float grow = seal > 0f ? Mathf.Lerp(1.22f, 0.85f, seal * seal) : Mathf.Lerp(1.1f, 0.55f, blast * blast);
			float tall = Mathf.Min(ShellHalfHeight * grow, ShaftTop - ShellCenter);
			_bubble.Position = center;
			_bubble.Scale = new Vector3(ShellWidth * grow * 1.15f, tall, ShellWidth * grow * 1.15f);
			_bubbleMat.SetShaderParameter("forca", warm ? 0f : bubble);
		}

		bool breathOn = warm || (Detail >= 1 && _amount > 0.02f);
		_breath.Visible = breathOn;
		if (breathOn)
		{
			Vector3 dir = Forward.Normalized();
			Vector3 side = dir.Cross(Vector3.Up);
			side = side.LengthSquared() > 1e-6f ? side.Normalized() : Vector3.Right;
			Vector3 lift = side.Cross(dir).Normalized();
			float length = BreathLength * (1f + 0.35f * blast);
			// O cone tem 1 de altura no eixo Y: o Y vira o sentido do rugido.
			_breath.Transform = new Transform3D(new Basis(side, dir * length, lift), Mouth + dir * (length * 0.5f));
			_breathMat.SetShaderParameter("cor", color);
			_breathMat.SetShaderParameter("forca", warm ? 0f : _amount * 0.55f);
			_breathMat.SetShaderParameter("golpe", warm ? 0f : blast);
		}
	}

	private void SleepLayers()
	{
		_floor.Visible = _warp.Visible = _shellMesh.Visible = _bubble.Visible = _breath.Visible = false;
	}
}
