using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O olho de quem joga é o olho do corpo que os outros veem (Jhon, 26/11:
/// "o humano tá agachando mais do que na animação de 3ª pessoa. O nível de
/// agachamento deve ser idêntico ao de 3ª pessoa, extremamente preciso. A
/// animação de agachar em 3ª pessoa tá muito curta, precisa agachar mais").
///
/// Mede, em centímetros acima dos pés, a câmera da 1ª pessoa e os olhos do
/// corpo visto (o meio dos dois olhos da malha da cabeça, preso ao osso
/// <c>mixamorig_Head</c>: <see cref="CharacterSkin.EyeInHead"/>), em pé e
/// agachado, parado e andando, na Eve (azul) e no Barut (laranja). Andando, a
/// média do trecho (a cabeça do clipe sobe e desce no passo; a câmera não).
/// </summary>
public abstract class CrouchEyeTest : RegressionTest
{
	protected abstract int Team { get; }
	protected abstract string Who { get; }
	public override string Scene => RegressionBench.Match;
	public override string Description => $"a câmera da 1ª pessoa fica nos olhos do corpo visto de {Who}, em pé e agachado, parado e andando (até 1 cm), e o agachado fica reto e alto, sem pele no chão";

	public override void Prepare()
	{
		RoomConfig.Settings = new RoomSettings { Map = "armazem_estendido", TeamSize = 4 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Team });
		foreach (var (name, team) in RoomConfig.DefaultBots())
			RoomConfig.Members.Add(new RoomMember { Name = name, Team = team, IsBot = true });
		RoomConfig.Active = true;
	}

	/// <summary>Câmera e olho do corpo visto, em metros acima dos pés, agora.</summary>
	private static (float Camera, float Eye) Now(Player p)
	{
		float feet = p.GlobalPosition.Y + p.FeetOffset;
		float camera = p.GetNode<Camera3D>("Head/Camera3D").GlobalPosition.Y - feet;
		Vector3 eye = p.GetNode<CharacterAnimator>("Human").EyePosition;
		return (camera, eye.Y - feet);
	}

	private static async Task<(float Camera, float Eye, float Bob)> Sample(RegressionBench b, double seconds)
	{
		var cams = new List<float>();
		var eyes = new List<float>();
		double end = b.Time + seconds;
		while (b.Time < end)
		{
			await b.Frames(1);
			var (c, e) = Now(b.Player);
			cams.Add(c);
			eyes.Add(e);
		}
		return (cams.Average(), eyes.Average(), eyes.Max() - eyes.Min());
	}

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		foreach (CharacterBody3D bot in b.Bots()) RegressionBench.Freeze(bot);
		await b.Place(new Vector3(-15, 1, 13), -90f);
		await b.Seconds(0.6);

		var rows = new List<(string What, float Camera, float Eye, float Bob)>();
		async Task Measure(string what, bool crouch, bool walk)
		{
			await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.3);
			if (crouch) RegressionBench.Press("crouch");
			await b.Seconds(0.8);
			if (walk) RegressionBench.Press("move_forward");
			await b.Seconds(walk ? 0.6 : 0.2);
			// Andando, a média de passadas inteiras: o andar agachado leva 2,73 s por
			// passada (a cabeça sobe e desce 4,4 cm), e 1,2 s pegava só um pedaço dela —
			// a média dependia de em que ponto do passo a medida começava (0,8 cm num
			// dia, 1,1 no outro). Duas passadas: a diferença de verdade é 0,2 cm.
			var (c, e, bob) = await Sample(b, walk ? 5.46 : 1.2);
			RegressionBench.ReleaseAll();
			await b.Seconds(0.5);
			rows.Add((what, c, e, bob));
			RegressionBench.Note($"{Who} {what}: câmera {RegressionBench.F(c * 100)} cm, olhos do corpo {RegressionBench.F(e * 100)} cm (sobe e desce {RegressionBench.F(bob * 100)} cm), diferença {RegressionBench.F((c - e) * 100)} cm");
		}

		await Measure("em pé parado", false, false);
		await Measure("em pé andando", false, true);
		await Measure("agachado parado", true, false);
		await Measure("agachado andando", true, true);

		foreach (var (what, c, e, _) in rows)
			b.AtMost(0.01, Mathf.Abs(c - e), $"{Who} {what}: a câmera está nos olhos do corpo visto (m de diferença)");

		float standEye = rows[0].Eye, crouchEye = rows[2].Eye;
		RegressionBench.Note($"{Who}: agachado, os olhos descem {RegressionBench.F((standEye - crouchEye) * 100)} cm");
		// O corpo visto agachava pouco: os olhos desciam ~10 cm e a câmera 87,5 (26/11).
		// Reto, e mais alto (Jhon, 26/11: "eles estão muito agachados ainda,
		// precisam ficar mais altos", +20 cm sobre o joelho no chão): os olhos
		// ~19 cm abaixo dos de pé (~140 cm).
		b.AtLeast(0.15, standEye - crouchEye, $"{Who}: agachado, os olhos do corpo visto descem (m)");
		b.AtMost(0.25, standEye - crouchEye, $"{Who}: mas não fundo demais (m)");

		// Parado agachado: nada da pele no chão, a cabeça de acerto com a cabeça
		// que se vê, e o pé parado (o clipe de andar agachado não anda no lugar).
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.3);
		float standLow = LowestSkin(b.Player);
		RegressionBench.Press("crouch");
		await b.Seconds(1.0);
		float feet = b.Player.GlobalPosition.Y + b.Player.FeetOffset;
		float low = LowestSkin(b.Player);
		bool hasHead = BodyHitbox.TryGetHead(b.Player, out Vector3 piece, out _);
		float eyeNow = b.Player.GetNode<CharacterAnimator>("Human").EyePosition.Y - feet;
		Skeleton3D sk = SkinSilhouette.VisibleSkeleton(b.Player);
		int foot = sk.FindBone("mixamorig_LeftFoot");
		Vector3 first = sk.GlobalTransform * sk.GetBoneGlobalPose(foot).Origin;
		float wander = 0f;
		for (int i = 0; i < 30; i++)
		{
			await b.Frames(2);
			wander = Mathf.Max(wander, (sk.GlobalTransform * sk.GetBoneGlobalPose(foot).Origin).DistanceTo(first));
		}
		RegressionBench.ReleaseAll();
		RegressionBench.Note($"{Who} agachado: pele mais baixa {RegressionBench.F(low * 100 - feet * 100)} cm dos pés (em pé {RegressionBench.F((standLow - feet) * 100)}), peça da cabeça {RegressionBench.F((piece.Y - feet) * 100)} cm (olhos a {RegressionBench.F(eyeNow * 100)}), o pé parado andou {RegressionBench.F(wander * 100)} cm em 1 s");
		b.AtLeast(standLow - 0.015, low, $"{Who}: agachado, nenhuma pele entra no chão mais que em pé (m)");
		b.Check(hasHead && piece.Y - feet > eyeNow - 0.08f && piece.Y - feet < eyeNow + 0.12f, $"{Who}: a peça de acerto da cabeça desce com a cabeça que se vê");
		b.AtMost(0.01, wander, $"{Who}: parado agachado, o pé fica parado (m em 1 s; era 0,31, andando no lugar)");
	}

	/// <summary>O ponto mais baixo da pele do corpo visto, no mundo.</summary>
	private static float LowestSkin(Player p)
	{
		Node3D model = SkinSilhouette.VisibleSkeleton(p).GetParent<Node3D>();
		while (model.GetParent() is Node3D up && up is not CharacterAnimator) model = up;
		return SkinPoints.Lowest(model).Low;
	}
}

public sealed class CrouchEyeEveTest : CrouchEyeTest
{
	public override string Name => "OlhoNoCorpoEve";
	protected override int Team => Teams.Blue;
	protected override string Who => "Eve";
}

public sealed class CrouchEyeBarutTest : CrouchEyeTest
{
	public override string Name => "OlhoNoCorpoBarut";
	protected override int Team => Teams.Orange;
	protected override string Who => "Barut";
}
