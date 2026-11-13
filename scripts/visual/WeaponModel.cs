using Godot;
using Godot.Collections;

/// <summary>
/// O modelo 3D de cada arma e das mãos que a seguram.
///
/// Um só construtor para os três lugares onde a arma aparece: na sua
/// mão em primeira pessoa (<see cref="ViewModel"/>), na mão dos outros
/// (<see cref="HeldWeapon"/>) e no chão quando alguém solta
/// (<see cref="WeaponPickup"/>). Assim a AK que você vê no chão é a
/// mesma AK que o inimigo segurava.
///
/// As malhas e a ficha de cada arma (peças, materiais, pontos e onde
/// vai cada mão) saem de <c>tools/gerar_armas.py</c>, para
/// <c>assets/armas/</c>. Trocar o desenho de uma arma é mexer lá e rodar
/// o script; trocar por modelo de artista é pôr os arquivos com os
/// mesmos nomes. Quem usa só pede <c>WeaponModel.Build("ak47")</c>.
///
/// Convenção: origem no punho, cano para −Z, em metros. As peças que a
/// animação mexe ficam em nós com nome: <c>Magazine</c> (o carregador),
/// <c>Ferrolho</c>, <c>Bomba</c> (a telha da escopeta), <c>Pino</c> (da
/// granada). Os pontos são marcadores: <c>Muzzle</c> (boca do cano),
/// <c>Ejecao</c> (por onde sai a cápsula), <c>Carga</c> (por onde entra
/// o cartucho).
/// </summary>
public static class WeaponModel
{
	public const string MuzzleName = "Muzzle";
	public const string MagazineName = "Magazine";
	public const string BoltName = "Ferrolho";
	public const string PumpName = "Bomba";
	public const string PinName = "Pino";
	public const string EjectName = "Ejecao";
	public const string LoadName = "Carga";

	public const string SheetPath = "res://assets/armas/armas.json";

	private static Dictionary _sheet;
	private static readonly System.Collections.Generic.Dictionary<string, StandardMaterial3D> Materials = new();

	/// <summary>Onde a palma fica em relação ao pulso, no espaço da mão:
	/// é este ponto que encosta no punho, no guarda-mão, no carregador.</summary>
	public static readonly Vector3 Palm = new(0f, -0.022f, -0.06f);

	/// <summary>Onde vai uma mão numa arma: a pose (qual malha) e a
	/// posição e o giro do pulso, no espaço da arma.</summary>
	public readonly record struct HandPlace(string Pose, Transform3D Transform);

	/// <summary>Monta o modelo da arma. Id desconhecido vira o M16:
	/// melhor uma arma qualquer na mão do que mão vazia.</summary>
	public static Node3D Build(string id)
	{
		Dictionary weapon = Weapon(id);
		var root = new Node3D { Name = string.IsNullOrEmpty(id) ? "Arma" : id };
		if (weapon == null) return root;

		foreach (Dictionary part in weapon["partes"].AsGodotArray<Dictionary>())
		{
			Node3D parent = root;
			string node = part["no"].AsString();
			if (node != "")
			{
				parent = root.GetNodeOrNull<Node3D>(node);
				if (parent == null)
				{
					parent = new Node3D { Name = node };
					root.AddChild(parent);
				}
			}

			parent.AddChild(Piece(part));
		}

		foreach (var (name, value) in weapon["pontos"].AsGodotDictionary())
			root.AddChild(new Marker3D { Name = name.AsString(), Position = ToVector(value) });

		return root;
	}

	/// <summary>Pistola fica na mão; o resto pede as duas.</summary>
	public static bool IsPistol(string id) => id == "m9";

	/// <summary>Onde vai a mão direita ou a esquerda nesta arma.</summary>
	public static HandPlace Hand(string id, bool right)
	{
		Dictionary weapon = Weapon(id);
		Dictionary hands = weapon?["maos"].AsGodotDictionary();
		string side = right ? "direita" : "esquerda";
		if (hands == null || !hands.ContainsKey(side))
			return new HandPlace("punho", Transform3D.Identity);

		Dictionary hand = hands[side].AsGodotDictionary();
		return new HandPlace(hand["pose"].AsString(),
			HandOn(ToVector(hand["alvo"]), ToVector(hand["dedos"]), ToVector(hand["palma"])));
	}

	/// <summary>
	/// A mão com a palma em <paramref name="target"/>, os dedos esticados
	/// apontando para <paramref name="fingers"/> e a palma olhando para
	/// <paramref name="palm"/>. Na mão, os dedos são −Z e a palma é −Y.
	/// </summary>
	public static Transform3D HandOn(Vector3 target, Vector3 fingers, Vector3 palm)
	{
		Vector3 z = -fingers.Normalized();
		Vector3 y = -(palm - palm.Project(z)).Normalized();
		var basis = new Basis(y.Cross(z), y, z);
		return new Transform3D(basis, target - basis * Palm);
	}

	/// <summary>Uma mão de luva com a manga, numa pose: <c>punho</c>,
	/// <c>apoio</c>, <c>fechada</c>, <c>aberta</c> — ou <c>lobo</c>, o
	/// braço da fera. O pulso fica na origem e os dedos apontam para −Z.</summary>
	public static Node3D BuildHand(string pose, bool right)
	{
		var root = new Node3D { Name = right ? "MaoDireita" : "MaoEsquerda" };
		Dictionary poses = Sheet()?["maos"].AsGodotDictionary();
		if (poses == null || !poses.ContainsKey(pose)) return root;

		Dictionary sides = poses[pose].AsGodotDictionary();
		foreach (Dictionary part in sides[right ? "direita" : "esquerda"].AsGodotArray<Dictionary>())
			root.AddChild(Piece(part));

		return root;
	}

	/// <summary>Muda a camada de render de todo o modelo — é como o
	/// corpo do dono some da câmera dele.</summary>
	public static void SetLayers(Node node, uint layers)
	{
		if (node is VisualInstance3D visual) visual.Layers = layers;
		foreach (Node child in node.GetChildren()) SetLayers(child, layers);
	}

	/// <summary>O centro da base de um nó de peças (o fundo do
	/// carregador, por exemplo), no espaço da arma.</summary>
	public static Vector3 Bottom(Node3D node)
	{
		Aabb box = Bounds(node);
		return new Vector3(box.GetCenter().X, box.Position.Y, box.GetCenter().Z);
	}

	/// <summary>A caixa que envolve as malhas de um nó, no espaço do pai dele.</summary>
	public static Aabb Bounds(Node3D node)
	{
		Aabb? total = null;
		foreach (Node child in node.GetChildren())
		{
			if (child is not MeshInstance3D { Mesh: not null } mesh) continue;
			Aabb box = mesh.Transform * mesh.Mesh.GetAabb();
			total = total?.Merge(box) ?? box;
		}

		return node.Transform * (total ?? new Aabb());
	}

	// ------------------------------------------------------------------

	private static MeshInstance3D Piece(Dictionary part)
	{
		string material = part["material"].AsString();
		return new MeshInstance3D
		{
			Mesh = GD.Load<Mesh>(part["malha"].AsString()),
			MaterialOverride = Material(material),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
	}

	private static Dictionary Weapon(string id)
	{
		Dictionary weapons = Sheet()?["armas"].AsGodotDictionary();
		if (weapons == null) return null;
		if (!string.IsNullOrEmpty(id) && weapons.ContainsKey(id)) return weapons[id].AsGodotDictionary();
		return weapons.ContainsKey("m16a2") ? weapons["m16a2"].AsGodotDictionary() : null;
	}

	private static Dictionary Sheet()
	{
		if (_sheet != null) return _sheet;
		if (!ResourceLoader.Exists(SheetPath))
		{
			GD.PushWarning($"WeaponModel: falta {SheetPath} — rode tools/gerar_armas.py");
			return null;
		}

		_sheet = GD.Load<Json>(SheetPath).Data.AsGodotDictionary();
		return _sheet;
	}

	private static Vector3 ToVector(Variant value)
	{
		var list = value.AsGodotArray();
		return new Vector3(list[0].AsSingle(), list[1].AsSingle(), list[2].AsSingle());
	}

	/// <summary>Os materiais, por nome — o mesmo nome que
	/// tools/gerar_armas.py dá a cada peça. Um de cada, compartilhado.</summary>
	private static StandardMaterial3D Material(string name)
	{
		if (Materials.TryGetValue(name, out StandardMaterial3D made)) return made;

		StandardMaterial3D m = name switch
		{
			"metal" => Solid(0.115f, 0.12f, 0.13f, 0.42f, 0.8f),
			"metal_claro" => Solid(0.55f, 0.57f, 0.6f, 0.34f, 0.85f),
			"aco" => Solid(0.78f, 0.8f, 0.82f, 0.2f, 0.95f),
			"polimero" => Solid(0.06f, 0.062f, 0.068f, 0.72f, 0f),
			"polimero_verde" => Solid(0.13f, 0.16f, 0.125f, 0.72f, 0f),
			"borracha" => Solid(0.025f, 0.025f, 0.028f, 0.95f, 0f),
			"madeira" => Solid(0.5f, 0.27f, 0.12f, 0.5f, 0f),
			"madeira_escura" => Solid(0.3f, 0.16f, 0.075f, 0.5f, 0f),
			"oliva" => Solid(0.24f, 0.29f, 0.2f, 0.6f, 0.1f),
			"ambar" => Glass(0.85f, 0.5f, 0.15f, 0.82f),
			"luva" => Solid(0.055f, 0.055f, 0.06f, 0.86f, 0f),
			"luva_reforco" => Solid(0.16f, 0.16f, 0.17f, 0.55f, 0f),
			"manga" => Solid(0.2f, 0.235f, 0.17f, 0.95f, 0f),
			"pelo" => Solid(0.2f, 0.17f, 0.15f, 1f, 0f),
			"garra" => Solid(0.86f, 0.83f, 0.74f, 0.32f, 0f),
			_ => Solid(0.5f, 0.5f, 0.5f, 0.6f, 0f)
		};

		Materials[name] = m;
		return m;
	}

	private static StandardMaterial3D Solid(float r, float g, float b, float roughness, float metallic) => new()
	{
		AlbedoColor = new Color(r, g, b),
		Roughness = roughness,
		Metallic = metallic,
		MetallicSpecular = 0.5f
	};

	private static StandardMaterial3D Glass(float r, float g, float b, float alpha) => new()
	{
		AlbedoColor = new Color(r, g, b, alpha),
		Roughness = 0.25f,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha
	};
}
