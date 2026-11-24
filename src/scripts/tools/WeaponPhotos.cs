using Godot;
using System.Collections.Generic;

/// <summary>
/// Tira a foto de cada arma para a interface (loja, inventário, HUD, feed
/// de abates): o mesmo modelo que o jogador segura
/// (<see cref="WeaponModel.Build"/>), de lado, com o cano para a direita,
/// um pouco girado para ter volume, em fundo transparente.
///
///     godot --path . res://src/scenes/tools/fotos_armas.tscn
///
/// Grava assets/ui/armas/&lt;id&gt;.png (768×384). Rode de novo quando uma
/// arma mudar de modelo ou entrar uma nova em <see cref="Ids"/>. Precisa
/// de vídeo (não roda com --headless): a foto sai do renderizador.
/// </summary>
public partial class WeaponPhotos : Node
{
	private const int Width = 1536;
	private const int Height = 768;

	/// <summary>As armas fotografadas: todas as de assets/armas/armas.json
	/// que aparecem na interface.</summary>
	private static readonly string[] Ids = { "ar15", "ar15_ultimate", "ar15_ouro", "ar15_diamante", "ar15_8bit", "ar15_lava", "m4a4", "ak74", "scar", "svd", "deagle", "faca", "choque",
		"m4a4_neon", "m4a4_dragao", "ak74_tigre", "ak74_imperial", "scar_artico", "scar_galaxia", "svd_carbono", "svd_tempestade", "deagle_cobra", "deagle_fenix", "faca_damasco", "faca_obsidiana",
		"choque_carbono", "choque_neon", "choque_magma", "choque_ouro" };

	/// <summary>Só estas (<c>-- --armas=ar15_ouro,ar15_8bit</c>); sem isso, todas.</summary>
	private static string[] Wanted()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--armas=")) return arg["--armas=".Length..].Split(',');
		return Ids;
	}

	public override async void _Ready()
	{
		string dir = ProjectSettings.GlobalizePath("res://src/assets/ui/armas/");
		DirAccess.MakeDirRecursiveAbsolute(dir);

		foreach (string id in Wanted())
		{
			SubViewport stage = Stage(id);
			AddChild(stage);
			for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

			Image photo = stage.GetTexture().GetImage();
			photo.Resize(Width / 2, Height / 2, Image.Interpolation.Lanczos);
			photo.SavePng(dir + id + ".png");
			GD.Print($"photo: {id}");

			RemoveChild(stage);
			stage.QueueFree();
		}

		GetTree().Quit();
	}

	private static SubViewport Stage(string id)
	{
		var stage = new SubViewport
		{
			Size = new Vector2I(Width, Height),
			TransparentBg = true,
			OwnWorld3D = true,
			Msaa3D = Viewport.Msaa.Msaa4X,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always
		};

		// Um céu claro só para o metal ter o que refletir; o fundo da foto
		// continua transparente.
		var sky = new ProceduralSkyMaterial
		{
			SkyTopColor = new Color(0.82f, 0.86f, 0.92f),
			SkyHorizonColor = new Color(0.95f, 0.95f, 0.96f),
			GroundHorizonColor = new Color(0.7f, 0.72f, 0.76f),
			GroundBottomColor = new Color(0.32f, 0.34f, 0.38f)
		};
		stage.AddChild(new WorldEnvironment
		{
			Environment = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.Sky,
				Sky = new Sky { SkyMaterial = sky },
				AmbientLightSource = Godot.Environment.AmbientSource.Sky,
				AmbientLightEnergy = 1.1f,
				ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
				TonemapMode = Godot.Environment.ToneMapper.Filmic
			}
		});

		// Luz de estúdio: a principal de cima e da frente, uma de
		// enchimento fria por baixo, e uma de recorte por trás.
		stage.AddChild(Light(new Vector3(-48, -28, 0), 2.4f, new Color(1f, 0.97f, 0.92f)));
		stage.AddChild(Light(new Vector3(20, -150, 0), 0.7f, new Color(0.8f, 0.88f, 1f)));
		stage.AddChild(Light(new Vector3(-30, 165, 0), 1.5f, Colors.White));

		Node3D model = WeaponModel.Build(id);
		var pivot = new Node3D();
		pivot.AddChild(model);
		stage.AddChild(pivot);

		Aabb box = Bounds(model, Transform3D.Identity) ?? new Aabb(Vector3.Zero, Vector3.One * 0.2f);
		model.Position = -box.GetCenter();
		pivot.Basis = Pose(id);
		if (pivot.Basis != Basis.Identity && Bounds(pivot, Transform3D.Identity) is Aabb turned)
		{
			pivot.Position = -turned.GetCenter();
			box = turned;
		}

		// A câmera olha de +X: o cano (−Z) aponta para a direita da foto.
		float length = box.Size.Z;
		float height = box.Size.Y;
		float aspect = Width / (float)Height;
		float size = Mathf.Max(height * 1.3f, length * 1.12f / aspect);
		var camera = new Camera3D
		{
			Projection = Camera3D.ProjectionType.Orthogonal,
			Size = size,
			Near = 0.05f,
			Far = 10f,
			Current = true
		};
		stage.AddChild(camera);
		camera.LookAtFromPosition(new Vector3(2.6f, 0.42f, 0.5f), Vector3.Zero, Vector3.Up);
		return stage;
	}

	/// <summary>Arma de fogo: de perfil, o cano para a direita. Faca: a face
	/// da lâmina para a câmera, a ponta subindo para a direita (de perfil
	/// ela ficava em pé, um risco no meio da foto). Granada: girada para a
	/// argola ficar na frente e a alavanca no perfil (de frente, a alavanca
	/// tapava a granada). O mesmo em tools/fotos_armas.gd.</summary>
	private static Basis Pose(string id) =>
		id.StartsWith("faca") ? new Basis(Vector3.Right, Mathf.DegToRad(-35))
		: Arsenal.IsGrenade(id) ? new Basis(Vector3.Up, Mathf.DegToRad(120))
		: Basis.Identity;

	private static DirectionalLight3D Light(Vector3 degrees, float energy, Color color) => new()
	{
		RotationDegrees = degrees,
		LightEnergy = energy,
		LightColor = color,
		ShadowEnabled = false
	};

	private static Aabb? Bounds(Node node, Transform3D parent)
	{
		Aabb? total = null;
		Transform3D here = node is Node3D n ? parent * n.Transform : parent;
		if (node is MeshInstance3D { Mesh: not null } mesh)
			total = here * mesh.Mesh.GetAabb();
		foreach (Node child in node.GetChildren())
			if (Bounds(child, here) is Aabb box)
				total = total?.Merge(box) ?? box;
		return total;
	}
}
