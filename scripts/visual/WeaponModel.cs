using Godot;
using System.Collections.Generic;

/// <summary>
/// O modelo 3D de cada arma, montado com caixas e cilindros.
///
/// Um só construtor para os três lugares onde a arma aparece: na sua
/// mão em primeira pessoa (<see cref="ViewModel"/>), na mão dos outros
/// (<see cref="HeldWeapon"/>) e no chão quando alguém solta
/// (<see cref="WeaponPickup"/>). Assim a AK que você vê no chão é a
/// mesma AK que o inimigo segurava.
///
/// Geometria simples de propósito, como o resto do jogo em cinza: o que
/// importa é reconhecer a arma pela silhueta — a alça do M16, o
/// carregador curvo e a madeira da AK, o tubo da M3. Trocar por modelo
/// de verdade é trocar este arquivo; quem usa só pede
/// <c>WeaponModel.Build("ak47")</c>.
///
/// Convenção: origem no punho, cano para −Z, em metros. Cada modelo traz
/// um nó <c>Muzzle</c> na boca do cano (armas de fogo) e, quando tem
/// carregador, um nó <c>Magazine</c> que a animação de recarga tira e
/// põe.
/// </summary>
public static class WeaponModel
{
	public const string MuzzleName = "Muzzle";
	public const string MagazineName = "Magazine";

	private static readonly Color Polymer = new(0.11f, 0.115f, 0.125f);
	private static readonly Color Metal = new(0.19f, 0.2f, 0.22f);
	private static readonly Color MetalLight = new(0.6f, 0.62f, 0.66f);
	private static readonly Color Wood = new(0.46f, 0.27f, 0.13f);
	private static readonly Color WoodDark = new(0.3f, 0.17f, 0.08f);
	private static readonly Color Olive = new(0.27f, 0.31f, 0.22f);
	private static readonly Color Amber = new(0.85f, 0.5f, 0.15f, 0.82f);
	private static readonly Color Grenade = new(0.3f, 0.38f, 0.26f);

	private static readonly Dictionary<Color, StandardMaterial3D> Materials = new();

	/// <summary>Monta o modelo da arma. Id desconhecido vira um fuzil
	/// genérico: melhor uma arma qualquer na mão do que mão vazia.</summary>
	public static Node3D Build(string id)
	{
		var root = new Node3D { Name = string.IsNullOrEmpty(id) ? "Arma" : id };

		switch (id)
		{
			case "ak47": Ak47(root); break;
			case "sg552": Sg552(root); break;
			case "ump45": Ump45(root); break;
			case "m3": M3(root); break;
			case "m9": M9(root); break;
			case "faca": Knife(root); break;
			case "choque": Stun(root); break;
			default: M16(root); break;
		}

		return root;
	}

	/// <summary>Pistola fica na mão; o resto pede as duas.</summary>
	public static bool IsPistol(string id) => id == "m9";

	/// <summary>Muda a camada de render de todo o modelo — é como o
	/// corpo do dono some da câmera dele.</summary>
	public static void SetLayers(Node node, uint layers)
	{
		if (node is VisualInstance3D visual) visual.Layers = layers;
		foreach (Node child in node.GetChildren()) SetLayers(child, layers);
	}

	// ================================================================== armas

	/// <summary>M16A2: comprida, preta, com a alça de transporte em cima
	/// — a silhueta que ninguém confunde.</summary>
	private static void M16(Node3D r)
	{
		Box(r, Polymer, new(0.045f, 0.085f, 0.28f), new(0, -0.005f, 0.22f), rx: -4);
		Box(r, Polymer, new(0.05f, 0.1f, 0.02f), new(0, -0.012f, 0.365f));
		Box(r, Metal, new(0.045f, 0.06f, 0.2f), new(0, 0, -0.02f));
		Box(r, Metal, new(0.043f, 0.05f, 0.24f), new(0, 0.055f, -0.03f));
		Box(r, Metal, new(0.02f, 0.035f, 0.15f), new(0, 0.115f, -0.03f));
		Box(r, Metal, new(0.016f, 0.035f, 0.02f), new(0, 0.09f, 0.03f));
		Box(r, Metal, new(0.016f, 0.035f, 0.02f), new(0, 0.09f, -0.09f));
		Cyl(r, Polymer, 0.028f, 0.3f, new(0, 0.045f, -0.29f));
		Cyl(r, Metal, 0.009f, 0.2f, new(0, 0.045f, -0.53f));
		Box(r, Metal, new(0.012f, 0.07f, 0.02f), new(0, 0.085f, -0.45f));
		Cyl(r, Metal, 0.012f, 0.045f, new(0, 0.045f, -0.65f));
		Grip(r, Polymer, new(0, -0.065f, 0.055f));
		Box(r, Polymer, new(0.01f, 0.012f, 0.06f), new(0, -0.04f, 0.0f));
		Mag(r, Metal, new(0.03f, 0.17f, 0.07f), new(0, -0.1f, -0.07f), 6);
		Muzzle(r, new(0, 0.045f, -0.68f));
	}

	/// <summary>AK-47: madeira na coronha e no guarda-mão, carregador
	/// curvo para frente, tubo de gás por cima do cano.</summary>
	private static void Ak47(Node3D r)
	{
		Box(r, Wood, new(0.04f, 0.075f, 0.28f), new(0, -0.03f, 0.23f), rx: -7);
		Box(r, WoodDark, new(0.045f, 0.09f, 0.02f), new(0, -0.05f, 0.365f), rx: -7);
		Box(r, Metal, new(0.045f, 0.07f, 0.27f), new(0, 0.025f, -0.03f));
		Box(r, Metal, new(0.04f, 0.02f, 0.22f), new(0, 0.068f, -0.03f));
		Box(r, Wood, new(0.05f, 0.045f, 0.2f), new(0, 0.015f, -0.27f));
		Box(r, Wood, new(0.034f, 0.03f, 0.19f), new(0, 0.063f, -0.27f));
		Cyl(r, Metal, 0.009f, 0.22f, new(0, 0.03f, -0.47f));
		Box(r, Metal, new(0.02f, 0.055f, 0.025f), new(0, 0.055f, -0.55f));
		Cyl(r, Metal, 0.012f, 0.03f, new(0, 0.03f, -0.6f));
		Grip(r, WoodDark, new(0, -0.06f, 0.065f));

		// Carregador curvo: três gomos, cada um mais inclinado.
		var mag = new Node3D { Name = MagazineName };
		r.AddChild(mag);
		Box(mag, Metal, new(0.032f, 0.075f, 0.066f), new(0, -0.045f, -0.08f), rx: 6);
		Box(mag, Metal, new(0.032f, 0.07f, 0.064f), new(0, -0.11f, -0.1f), rx: 18);
		Box(mag, Metal, new(0.032f, 0.07f, 0.062f), new(0, -0.17f, -0.135f), rx: 32);

		Muzzle(r, new(0, 0.03f, -0.62f));
	}

	/// <summary>SG-552: curta, coronha vazada, mira integrada em cima e
	/// carregador translúcido.</summary>
	private static void Sg552(Node3D r)
	{
		Box(r, Polymer, new(0.02f, 0.02f, 0.22f), new(0, 0.035f, 0.19f));
		Box(r, Polymer, new(0.02f, 0.02f, 0.22f), new(0, -0.04f, 0.19f), rx: 8);
		Box(r, Polymer, new(0.035f, 0.11f, 0.025f), new(0, -0.005f, 0.31f));
		Box(r, Polymer, new(0.05f, 0.08f, 0.3f), new(0, 0.03f, -0.06f));
		Box(r, Polymer, new(0.026f, 0.035f, 0.18f), new(0, 0.088f, -0.04f));
		Box(r, Olive, new(0.055f, 0.06f, 0.14f), new(0, 0.02f, -0.27f));
		Cyl(r, Metal, 0.01f, 0.13f, new(0, 0.03f, -0.4f));
		Cyl(r, Metal, 0.013f, 0.035f, new(0, 0.03f, -0.48f));
		Grip(r, Polymer, new(0, -0.065f, 0.06f));
		Mag(r, Amber, new(0.03f, 0.16f, 0.07f), new(0, -0.1f, -0.1f), 10, transparent: true);
		Muzzle(r, new(0, 0.03f, -0.5f));
	}

	/// <summary>UMP-45: caixa grossa, cano curto, carregador reto e
	/// largo.</summary>
	private static void Ump45(Node3D r)
	{
		Box(r, Polymer, new(0.03f, 0.08f, 0.22f), new(0, 0.005f, 0.22f));
		Box(r, Polymer, new(0.06f, 0.1f, 0.38f), new(0, 0.03f, -0.08f));
		Box(r, Metal, new(0.03f, 0.018f, 0.3f), new(0, 0.089f, -0.08f));
		Cyl(r, Metal, 0.013f, 0.07f, new(0, 0.035f, -0.3f));
		Grip(r, Polymer, new(0, -0.06f, 0.05f));
		Mag(r, Polymer, new(0.036f, 0.15f, 0.06f), new(0, -0.09f, -0.13f), 0);
		Muzzle(r, new(0, 0.035f, -0.34f));
	}

	/// <summary>M3: escopeta de bomba — cano longo, tubo embaixo e a
	/// telha que corre. Sem carregador: recarrega cartucho por
	/// cartucho.</summary>
	private static void M3(Node3D r)
	{
		Box(r, Polymer, new(0.045f, 0.09f, 0.28f), new(0, -0.02f, 0.25f), rx: -5);
		Box(r, Metal, new(0.05f, 0.07f, 0.2f), new(0, 0.025f, -0.01f));
		Cyl(r, Metal, 0.013f, 0.52f, new(0, 0.048f, -0.37f));
		Cyl(r, Metal, 0.012f, 0.4f, new(0, 0.008f, -0.31f));
		var pump = Box(r, Polymer, new(0.055f, 0.05f, 0.16f), new(0, 0.008f, -0.27f));
		pump.Name = "Pump";
		Box(r, Metal, new(0.008f, 0.015f, 0.01f), new(0, 0.065f, -0.62f));
		Grip(r, Polymer, new(0, -0.06f, 0.07f));
		Muzzle(r, new(0, 0.048f, -0.64f));
	}

	/// <summary>M9: ferrolho, armação e o carregador dentro do punho.</summary>
	private static void M9(Node3D r)
	{
		Box(r, Metal, new(0.03f, 0.035f, 0.21f), new(0, 0.035f, -0.06f));
		Box(r, Polymer, new(0.028f, 0.025f, 0.15f), new(0, 0.006f, -0.035f));
		Box(r, Polymer, new(0.034f, 0.11f, 0.046f), new(0, -0.05f, 0.03f), rx: -12);
		Box(r, Polymer, new(0.008f, 0.03f, 0.05f), new(0, -0.015f, -0.04f));
		Cyl(r, Metal, 0.007f, 0.012f, new(0, 0.04f, -0.17f));
		Mag(r, Metal, new(0.026f, 0.1f, 0.036f), new(0, -0.06f, 0.032f), -12);
		Muzzle(r, new(0, 0.04f, -0.175f));
	}

	private static void Knife(Node3D r)
	{
		Box(r, Polymer, new(0.03f, 0.035f, 0.11f), new(0, 0, 0.03f));
		Box(r, Metal, new(0.05f, 0.012f, 0.014f), new(0, 0, -0.03f));
		Box(r, MetalLight, new(0.006f, 0.034f, 0.16f), new(0, 0.004f, -0.115f));
		Box(r, MetalLight, new(0.006f, 0.024f, 0.024f), new(0, 0.0f, -0.2f), rx: 45);
	}

	private static void Stun(Node3D r)
	{
		Cyl(r, Grenade, 0.032f, 0.09f, Vector3.Zero, upright: true);
		Cyl(r, Metal, 0.016f, 0.02f, new(0, 0.055f, 0), upright: true);
		Box(r, Metal, new(0.012f, 0.07f, 0.008f), new(0, 0.02f, 0.034f));
		var ring = new MeshInstance3D
		{
			Mesh = new TorusMesh { InnerRadius = 0.008f, OuterRadius = 0.014f },
			MaterialOverride = Mat(MetalLight),
			Position = new(0.02f, 0.07f, 0),
			RotationDegrees = new(0, 0, 90)
		};
		r.AddChild(ring);
	}

	// ================================================================== peças

	private static void Grip(Node3D r, Color color, Vector3 position) =>
		Box(r, color, new(0.034f, 0.1f, 0.045f), position, rx: -16);

	private static void Mag(Node3D r, Color color, Vector3 size, Vector3 position, float rx, bool transparent = false)
	{
		var mag = new Node3D { Name = MagazineName, Position = position, RotationDegrees = new(rx, 0, 0) };
		r.AddChild(mag);
		Box(mag, color, size, Vector3.Zero);
	}

	private static void Muzzle(Node3D r, Vector3 position) =>
		r.AddChild(new Marker3D { Name = MuzzleName, Position = position });

	private static MeshInstance3D Box(Node3D parent, Color color, Vector3 size, Vector3 position, float rx = 0)
	{
		var mesh = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size },
			MaterialOverride = Mat(color),
			Position = position,
			RotationDegrees = new(rx, 0, 0)
		};
		parent.AddChild(mesh);
		return mesh;
	}

	/// <summary>Cilindro deitado no eixo do cano, ou em pé
	/// (<paramref name="upright"/>) para a granada.</summary>
	private static MeshInstance3D Cyl(Node3D parent, Color color, float radius, float length, Vector3 position, bool upright = false)
	{
		var mesh = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = length, RadialSegments = 10, Rings = 1 },
			MaterialOverride = Mat(color),
			Position = position,
			RotationDegrees = upright ? Vector3.Zero : new(90, 0, 0)
		};
		parent.AddChild(mesh);
		return mesh;
	}

	/// <summary>Um material por cor, compartilhado: dezesseis armas na
	/// cena não precisam de dezesseis cópias do mesmo preto.</summary>
	private static StandardMaterial3D Mat(Color color)
	{
		if (Materials.TryGetValue(color, out StandardMaterial3D found) && GodotObject.IsInstanceValid(found))
			return found;

		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = 0.6f,
			Metallic = color.R < 0.25f ? 0.25f : 0.05f,
			Transparency = color.A < 1f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled
		};
		Materials[color] = material;
		return material;
	}
}
