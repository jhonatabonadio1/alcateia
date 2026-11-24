using Godot;
using System.Threading.Tasks;

/// <summary>
/// A arma vista de fora fica na mão com o corpo andando rápido (Jhon, 24/11:
/// saindo de agachado para o pulo, a pistola da Eve descolava das mãos por um
/// quadro). A arma é uma peça solta (TopLevel) posta no mundo; o alisamento do
/// desenho (VisualSmoothing) deixava o corpo um passo atrás e a arma onde a
/// física está. Agora ela anda junto com o corpo.
/// </summary>
public sealed class HeldSmoothingTest : RegressionTest
{
	public override string Name => "ArmaAcompanhaCorpo";
	public override string Description => "o boneco subindo e andando rápido: a arma de fora fica a menos de 1 cm de onde está parado em relação à mão";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		var puppet = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Bot>();
		puppet.Name = "Boneco";
		puppet.Team = 1;
		puppet.Remote = true;
		b.GetTree().CurrentScene.AddChild(puppet);
		await b.Frames(2);

		Vector3 at = b.Player.GlobalPosition + new Vector3(0, 0, -4f);
		void Tell(Vector3 position, Vector3 velocity, bool grounded)
		{
			puppet.RemoteUpdate(position, velocity, 0f, 0f, grounded, Form.Human, Slot.Secondary, "ar15", "deagle", 200f, 200f, 0f, 0f, false);
			puppet.RemoteMotion(false, false, false, false, false);
			puppet.CollisionLayer = 0;
		}

		// Mede no fim de cada quadro, depois do alisamento (sem tela não há o
		// sinal do desenho): o que se mede é o que seria desenhado.
		Vector3? last = null;
		var probe = new HeldSmoothingProbe { Look = () => last = Offset(puppet) };
		b.GetTree().CurrentScene.AddChild(probe);

		// Parado: a arma em relação à mão direita, no corpo.
		for (int i = 0; i < 90; i++)
		{
			Tell(at, Vector3.Zero, true);
			await b.Frames(1);
		}
		await b.Frames(1);
		Vector3? still = last;
		b.Check(still != null, "o boneco tem a arma do conjunto e a mão direita");
		if (still == null) { probe.QueueFree(); return; }

		// Subindo e andando 0,14 m por passo (o corpo sai do agachado para o pulo).
		float worst = 0f;
		Vector3 velocity = new Vector3(6f, 6f, 0f);
		for (int i = 0; i < 20; i++)
		{
			at += velocity / Engine.PhysicsTicksPerSecond;
			Tell(at, velocity, false);
			await b.Frames(1);
			if (i > 0 && last is Vector3 now) worst = Mathf.Max(worst, (now - still.Value).Length());
		}
		probe.QueueFree();
		b.Check(worst < 0.01f, $"andando rápido, a arma segue a mão (saiu até {worst * 100f:0.0} cm do lugar)");
	}

	/// <summary>A arma (o conjunto) menos a mão direita, no espaço do corpo.</summary>
	private static Vector3? Offset(Bot puppet)
	{
		HeldRig rig = Nodes.FindFirst<HeldRig>(puppet);
		Skeleton3D skeleton = AnimationKit.FindSkeleton(puppet.GetNode("Human"));
		if (rig == null || skeleton == null) return null;
		int hand = -1;
		for (int i = 0; i < skeleton.GetBoneCount() && hand < 0; i++)
			if (skeleton.GetBoneName(i).EndsWith("RightHand")) hand = i;
		if (hand < 0) return null;
		Vector3 palm = (skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(hand)).Origin;
		return puppet.GlobalTransform.Basis.Inverse() * (rig.GlobalPosition - palm);
	}
}

/// <summary>
/// Na troca de arma a arma nova fica na mão (Jhon, 24/11: a faca e a granada "surgem fora da mão e
/// depois se deslocam para a mão"). O corpo leva um instante para chegar à pose do saque, e a arma
/// ia direto para o fim do caminho (até 80 cm da mão). Agora ela some até a mão chegar ao bolso ou
/// à bainha e, visível, anda presa nela.
/// </summary>
public sealed class DrawInHandTest : RegressionTest
{
	public override string Name => "ArmaNovaNaMao";
	public override string Description => "o boneco troca a pistola pela faca: a faca some no começo do saque e, visível, fica a menos de 3 cm da mão";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		var puppet = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Bot>();
		puppet.Name = "Boneco";
		puppet.Team = 2;
		puppet.Remote = true;
		b.GetTree().CurrentScene.AddChild(puppet);
		await b.Frames(2);
		Vector3 at = b.Player.GlobalPosition + new Vector3(0, 0, -4f);
		void Tell(Slot slot)
		{
			puppet.RemoteUpdate(at, Vector3.Zero, 0f, 0f, true, Form.Human, slot, "ar15", "deagle", 200f, 200f, 0f, 0f, false);
			puppet.RemoteMotion(false, false, false, false, false);
			puppet.CollisionLayer = 0;
		}
		for (int i = 0; i < 90; i++) { Tell(Slot.Secondary); await b.Frames(1); }

		Transform3D? last = null;
		var probe = new HeldSmoothingProbe { Look = () => last = InHand(puppet) };
		b.GetTree().CurrentScene.AddChild(probe);
		int hidden = 0, shown = 0;
		float far = 0f;
		for (int i = 0; i < 24; i++)
		{
			Tell(Slot.Knife);
			await b.Frames(1);
			if (last is not Transform3D now) { hidden++; continue; }
			shown++;
			far = Mathf.Max(far, now.Origin.Length());
		}
		probe.QueueFree();
		b.Check(hidden > 0, $"a faca some enquanto a mão vai à bainha ({hidden} quadros)");
		b.Check(shown > 0, $"e aparece na mão ({shown} quadros)");
		// Entre dois quadros do clipe (30 por segundo) o corpo e o conjunto se separam um pouco no
		// gesto rápido do saque (até 2 cm, e 0 no quadro do clipe): o braço interpola osso por osso.
		b.Check(far < 0.03f, $"visível, a faca fica na mão (a mão do conjunto a até {far * 100f:0.0} cm da do corpo; era até 80 cm)");
	}

	/// <summary>A mão do conjunto (o que segura a arma) no espaço da mão direita do corpo, sem escala; null se a arma está escondida.</summary>
	private static Transform3D? InHand(Bot puppet)
	{
		HeldRig rig = Nodes.FindFirst<HeldRig>(puppet);
		Skeleton3D skeleton = AnimationKit.FindSkeleton(puppet.GetNode("Human"));
		if (rig == null || skeleton == null || !rig.Visible) return null;
		for (int i = 0; i < skeleton.GetBoneCount(); i++)
			if (skeleton.GetBoneName(i).EndsWith("RightHand"))
			{
				Transform3D hand = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(i);
				hand = new Transform3D(hand.Basis.Orthonormalized(), hand.Origin);
				Transform3D grip = rig.Skeleton.GlobalTransform * rig.Skeleton.GetBoneGlobalPose(rig.HandR);
				return new Transform3D(Basis.Identity, hand.AffineInverse() * grip.Origin);
			}
		return null;
	}
}

/// <summary>Chama <see cref="Look"/> no fim de cada quadro, depois de todo mundo (o
/// alisamento do corpo incluído: mesma prioridade, e entra depois na árvore).</summary>
public partial class HeldSmoothingProbe : Node
{
	public System.Action Look;
	public override void _Ready() => ProcessPriority = int.MaxValue;
	public override void _Process(double delta) => Look?.Invoke();
}
