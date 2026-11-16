using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Todo personagem de assets/personagens (docs/PERSONAGENS.md): os
/// modelos na altura dos de sempre, com os ossos que as animações e a
/// cabeça usam, e o corpo montado com eles e com a vida do personagem.
///
/// A altura importa para o combate, não só para a vista: a cabeça que leva
/// tiro é a do modelo (HeadHitbox). Um personagem 10 cm mais baixo é um
/// personagem mais difícil de acertar na cabeça.
/// </summary>
public sealed class CharacterTest : RegressionTest
{
	public override string Name => "Personagens";
	public override string Description => "cada personagem tem humano de 1,79 m e lobo de 1,66 m, os ossos certos, e o corpo usa os modelos e a vida dele";

	/// <summary>Os de sempre, a régua dos novos.</summary>
	private const string ReferenceHuman = "res://assets/characters/human/ely.fbx";
	private const string ReferenceWolf = "res://assets/characters/wolf/mutant.fbx";

	private static readonly string[] HumanBones =
		{ "mixamorig_Hips", "mixamorig_Head", "mixamorig_HeadTop_End", "mixamorig_RightHand", "mixamorig_LeftHand", "mixamorig_LeftFoot", "mixamorig_RightFoot" };
	private static readonly string[] WolfBones =
		{ "mixamorig_Hips", "mixamorig_Head", "mixamorig_HeadTop_End", "headfront", "mixamorig_LeftHand", "mixamorig_RightFoot" };

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		var skins = CharacterSkin.All();
		b.Check(skins.Count > 0, "há pelo menos um personagem em assets/personagens");

		float humanTop = HeadTop(ReferenceHuman, out _), wolfTop = HeadTop(ReferenceWolf, out _);
		RegressionBench.Note($"régua: topo da cabeça {RegressionBench.F(humanTop)} m (Ely), {RegressionBench.F(wolfTop)} m (lobo)");

		foreach (CharacterSkin skin in skins)
		{
			string who = skin.DisplayName;
			b.Check(Catalog.Get(skin.Id)?.Kind == ItemKind.Character, $"{who}: está no catálogo como {skin.Id}");
			b.Check(skin.HpMultiplier is >= 0.5f and <= 2f, $"{who}: vida ×{RegressionBench.F(skin.HpMultiplier)} entre ×0,5 e ×2");

			// Cabeça a até 4 cm da dos de sempre: é ela que leva o tiro.
			b.Near(humanTop, HeadTop(skin.HumanModel, out Skeleton3D human, HumanBones, b, $"{who} humano"), 0.04, $"{who}: topo da cabeça do humano");
			b.Near(wolfTop, HeadTop(skin.WolfModel, out Skeleton3D wolf, WolfBones, b, $"{who} lobo"), 0.04, $"{who}: topo da cabeça do lobo");
			b.Check(wolf?.GetBoneCount() == 24, $"{who}: o lobo tem os 24 ossos do lobo do jogo ({wolf?.GetBoneCount()})");
			human?.Owner?.Free();
			wolf?.Owner?.Free();

			await Body(b, skin);
		}
	}

	/// <summary>Um corpo de jogador com o personagem, como o servidor cria
	/// (Driven: sem câmera nem teclado), no time dele.</summary>
	private static async Task Body(RegressionBench b, CharacterSkin skin)
	{
		var body = GD.Load<PackedScene>("res://scenes/player.tscn").Instantiate<Player>();
		body.Driven = true;
		body.Character = skin;
		body.GetNodeOrNull("MultiplayerSynchronizer")?.Free();
		body.RemoveFromGroup("player");
		var health = body.GetNode<Health>("Health");
		health.Team = skin.Team == 0 ? Teams.Orange : skin.Team;
		b.Scene.AddChild(body);
		body.GlobalPosition = new Vector3(0, -200, 0);
		await b.Frames(2);

		string who = skin.DisplayName;
		b.Near(FormController.DefaultHumanHp * skin.HpMultiplier, health.MaxHp, 0.01, $"{who}: vida máxima");
		b.Near(health.MaxHp, health.Hp, 0.01, $"{who}: nasce de vida cheia");

		var human = body.GetNode<Node3D>("Human");
		b.Check(human.GetChildren().Any(n => n.SceneFilePath == skin.HumanModel), $"{who}: o corpo humano é o dela ({skin.HumanModel})");
		var wolf = body.GetNode<Node3D>("WolfVisual");
		b.Check(wolf.GetChildren().Any(n => n.SceneFilePath == skin.WolfModel), $"{who}: o lobo é o dela ({skin.WolfModel})");

		body.QueueFree();
		await b.Frames(1);
	}

	private static float HeadTop(string path, out Skeleton3D skeleton) => HeadTop(path, out skeleton, null, null, path);

	/// <summary>A altura do topo da cabeça na pose de repouso, em metros,
	/// com a escala dos nós acima do esqueleto (a da importação inclusa).</summary>
	private static float HeadTop(string path, out Skeleton3D skeleton, string[] bones, RegressionBench b, string who)
	{
		skeleton = null;
		if (!ResourceLoader.Exists(path) || GD.Load<PackedScene>(path)?.Instantiate() is not Node root)
		{
			b?.Check(false, $"{who}: o modelo abre ({path})");
			return 0f;
		}

		skeleton = AnimationKit.FindSkeleton(root);
		if (skeleton == null)
		{
			b?.Check(false, $"{who}: tem esqueleto");
			root.Free();
			return 0f;
		}

		foreach (string bone in bones ?? System.Array.Empty<string>())
			b?.Check(skeleton.FindBone(bone) >= 0, $"{who}: tem o osso {bone}");

		int top = skeleton.FindBone("mixamorig_HeadTop_End");
		if (top < 0) return 0f;

		Transform3D chain = skeleton.Transform;
		for (Node n = skeleton.GetParent(); n != null && n != root.GetParent(); n = n.GetParent())
			if (n is Node3D spatial) chain = spatial.Transform * chain;

		float y = (chain * skeleton.GetBoneGlobalRest(top)).Origin.Y;
		if (bones == null) root.Free();
		return y;
	}
}
