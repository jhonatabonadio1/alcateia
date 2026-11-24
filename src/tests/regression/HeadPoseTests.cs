using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// A cabeça dos corpos fica no lugar (20/11, Jhon: "Barut ficou sem rosto",
/// "partes do corpo sumindo, muitas vezes no espectador"). A arma em
/// terceira pessoa girava a cabeça por fora da animação a cada quadro; com
/// a tela mais rápida que a física (o Mac a 120 Hz, a física a 60) a
/// animação não reescrevia a pose entre dois quadros e o giro somava até a
/// cabeça virar para dentro do corpo. Aqui a física anda na metade dos
/// quadros, como lá.
/// </summary>
public sealed class HeadPoseTest : RegressionTest
{
	public override string Name => "CabecaNoLugar";
	public override string Description => "com a tela mais rápida que a física, a cabeça dos bots armados continua olhando para a frente";

	public override async Task Run(RegressionBench b)
	{
		int before = Engine.PhysicsTicksPerSecond;
		Engine.PhysicsTicksPerSecond = 30;
		try
		{
			await b.Seconds(1);
			float worst = 0f;
			int samples = 0;
			double end = b.Time + 10;
			while (b.Time < end)
			{
				await b.Frames(1);
				foreach (var bot in b.Bots())
				{
					if (bot.GetNode<Health>("Health").IsDead || bot.GetNodeOrNull<Node3D>("Human") is not Node3D human || !human.IsVisibleInTree()) continue;
					Skeleton3D skeleton = AnimationKit.FindSkeleton(human);
					int head = skeleton?.FindBone("mixamorig_Head") ?? -1, top = skeleton?.FindBone("mixamorig_HeadTop_End") ?? -1, neck = skeleton?.FindBone("mixamorig_Neck") ?? -1;
					if (head < 0 || top < 0 || neck < 0) continue;
					Transform3D s = skeleton.GlobalTransform;
					Vector3 up = ((s * skeleton.GetBoneGlobalPose(top)).Origin - (s * skeleton.GetBoneGlobalPose(head)).Origin).Normalized();
					Vector3 spine = ((s * skeleton.GetBoneGlobalPose(neck)).Origin - (s * skeleton.GetBoneGlobalPose(skeleton.FindBone("mixamorig_Spine1"))).Origin).Normalized();
					worst = Mathf.Max(worst, Mathf.RadToDeg(up.AngleTo(spine)));
					samples++;
				}
			}
			RegressionBench.Note($"{samples} amostras; a cabeça se afastou no máximo {worst:0}° do tronco");
			b.AtLeast(100, samples, "amostras de bots armados");
			b.AtMost(75, worst, "a cabeça não vira para dentro do corpo (graus entre cabeça e tronco)");
		}
		finally { Engine.PhysicsTicksPerSecond = before; }
	}
}
