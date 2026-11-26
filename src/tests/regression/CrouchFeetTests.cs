using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O pé do humano agachado no chão (Jhon, 26/11, no "Crouch Walking" do
/// Mixamo inteiro: "parece que tá na ponta do pé", "pé afundando no chão").
///
/// O clipe é de outro personagem, de pé e perna de outras proporções. Antes
/// do ajuste (AnimationKit.Legs), o pé da Eve agachada apontava 16 a 27° mais
/// para baixo que o repouso, o tornozelo ficava 4,8 cm alto e a ponta da bota
/// entrava até 5,4 cm no chão no passo (3,4 no Barut parado).
///
/// Toca os clipes do corpo visto (a biblioteca já ajustada ao personagem)
/// numa cópia do modelo, quadro a quadro: parado, os dois pés retos e no chão;
/// andando, o ciclo inteiro, a sola nunca abaixo do chão.
/// </summary>
public abstract class CrouchFeetTest : RegressionTest
{
	protected abstract int Team { get; }
	protected abstract string Who { get; }
	public override string Scene => RegressionBench.Match;
	public override string Description => $"{Who} agachado: parado, os dois pés retos no chão; andando, a sola nunca entra no chão";

	public override void Prepare()
	{
		RoomConfig.Settings = new RoomSettings { Map = "armazem_estendido", TeamSize = 4 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Team });
		RoomConfig.Active = true;
	}

	/// <summary>Põe o esqueleto na pose do clipe no instante <paramref name="time"/>.</summary>
	private static void Pose(Animation clip, Skeleton3D skeleton, double time)
	{
		for (int track = 0; track < clip.GetTrackCount(); track++)
		{
			string path = clip.TrackGetPath(track).ToString();
			int bone = skeleton.FindBone(path[(path.LastIndexOf(':') + 1)..]);
			if (bone < 0) continue;
			switch (clip.TrackGetType(track))
			{
				case Animation.TrackType.Position3D: skeleton.SetBonePosePosition(bone, clip.PositionTrackInterpolate(track, time)); break;
				case Animation.TrackType.Rotation3D: skeleton.SetBonePoseRotation(bone, clip.RotationTrackInterpolate(track, time)); break;
				case Animation.TrackType.Scale3D: skeleton.SetBonePoseScale(bone, clip.ScaleTrackInterpolate(track, time)); break;
			}
		}
	}

	/// <summary>Quanto o pé aponta para baixo além do repouso (graus; o osso vai do
	/// tornozelo à base dos dedos) e quanto o tornozelo está acima do repouso (m).</summary>
	private static (float Pitch, float Lift) Foot(Skeleton3D skeleton, string side)
	{
		int foot = skeleton.FindBone($"mixamorig_{side}Foot"), toes = skeleton.FindBone($"mixamorig_{side}ToeBase");
		Transform3D g = skeleton.GlobalTransform;
		Vector3 a = g * skeleton.GetBoneGlobalPose(foot).Origin, t = g * skeleton.GetBoneGlobalPose(toes).Origin;
		Vector3 ra = g * skeleton.GetBoneGlobalRest(foot).Origin, rt = g * skeleton.GetBoneGlobalRest(toes).Origin;
		static float Down(Vector3 v) => Mathf.RadToDeg(Mathf.Atan2(-v.Y, new Vector2(v.X, v.Z).Length()));
		return (Down(t - a) - Down(rt - ra), a.Y - ra.Y);
	}

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		Node3D shown = SkinSilhouette.VisibleSkeleton(b.Player).GetParent<Node3D>();
		while (shown.GetParent() is Node3D up && up is not CharacterAnimator) shown = up;
		AnimationPlayer clips = AnimationKit.FindPlayer(shown);
		RegressionBench.Note($"modelo: {shown.SceneFilePath}");

		var copy = GD.Load<PackedScene>(shown.SceneFilePath).Instantiate<Node3D>();
		b.Scene.AddChild(copy);
		copy.GlobalPosition = new Vector3(300, 0, 300);
		AnimationKit.FindPlayer(copy)?.Stop();
		Skeleton3D skeleton = AnimationKit.FindSkeleton(copy);
		float floor = copy.GlobalPosition.Y;
		float Sole(string side) => SkinPoints.Lowest(copy, n => n.Contains($"{side}Foot") || n.Contains($"{side}Toe")).Low - floor;
		await b.Frames(1);

		// Em pé, para comparar.
		Pose(clips.GetAnimation("alcateia/idle_rifle"), skeleton, 0);
		RegressionBench.Note($"{Who} em pé: sola {RegressionBench.F(Sole("Left") * 100)} / {RegressionBench.F(Sole("Right") * 100)} cm do chão");

		// Parado: um instante do andar, com os dois pés retos (no instante, um está saindo do chão).
		Pose(clips.GetAnimation("alcateia/crouch_idle"), skeleton, 0);
		foreach (string side in new[] { "Left", "Right" })
		{
			var (pitch, lift) = Foot(skeleton, side);
			float sole = Sole(side);
			RegressionBench.Note($"{Who} parado agachado, pé {side}: {RegressionBench.F(pitch)}° além do repouso, tornozelo {RegressionBench.F(lift * 100)} cm acima do repouso, sola {RegressionBench.F(sole * 100)} cm do chão");
			b.AtMost(3.0, Mathf.Abs(pitch), $"{Who} parado agachado: o pé {side} reto, não na ponta (graus)");
			b.AtLeast(-0.005, sole, $"{Who} parado agachado: a sola do pé {side} não entra no chão (m)");
			b.AtMost(0.01, sole, $"{Who} parado agachado: o pé {side} no chão (m acima)");
		}

		// Andando: o ciclo inteiro.
		Animation walk = clips.GetAnimation("alcateia/crouch_walk");
		float lowest = float.MaxValue;
		double at = 0;
		for (double t = 0; t <= walk.Length; t += 1.0 / 30.0)
		{
			Pose(walk, skeleton, t);
			float now = Mathf.Min(Sole("Left"), Sole("Right"));
			if (now < lowest) { lowest = now; at = t; }
		}
		RegressionBench.Note($"{Who} andando agachado: a sola mais baixa do ciclo a {RegressionBench.F(lowest * 100)} cm do chão (em {RegressionBench.F(at)} s de {RegressionBench.F(walk.Length)})");
		// Medido em 26/11: -0,1 cm nos dois (era -3,4 na Eve).
		b.AtLeast(-0.01, lowest, $"{Who} andando agachado: a sola não entra no chão (m)");
		copy.QueueFree();
	}
}

public sealed class CrouchFeetEveTest : CrouchFeetTest
{
	public override string Name => "PeNoChaoEve";
	protected override int Team => Teams.Blue;
	protected override string Who => "Eve";
}

public sealed class CrouchFeetBarutTest : CrouchFeetTest
{
	public override string Name => "PeNoChaoBarut";
	protected override int Team => Teams.Orange;
	protected override string Who => "Barut";
}
