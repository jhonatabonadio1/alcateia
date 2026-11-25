using Godot;
using System.Threading.Tasks;

/// <summary>
/// A granada na mão vista de fora tem o tamanho da da primeira pessoa (Jhon,
/// 24/11: "deixe a granada da terceira pessoa do mesmo tamanho da primeira";
/// e depois: "valide se o tamanho da granada em 3ª pessoa está o mesmo da 1ª").
/// As duas são a mesma peça do mesmo conjunto (assets/armas/fp): mede-se a
/// escala do osso que a leva (Grip). Na primeira pessoa o conjunto inteiro é
/// desenhado encolhido perto do olho e esticado com a abertura da tela
/// (ViewModel.RigScale e o stretch): lá se mede no espaço do conjunto.
/// </summary>
public sealed class GrenadeSizeTest : RegressionTest
{
	public override string Name => "GranadaMesmoTamanho";
	public override string Description => "a granada na mão em terceira pessoa (Eve e Barut) tem o tamanho da da primeira pessoa";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);

		// Primeira pessoa: a granada na mão do jogador.
		b.Weapon.SelectSlot(Slot.Grenade);
		await b.Seconds(0.8);
		var view = b.Player.GetNode<ViewModel>("Head/Camera3D/ViewModel");
		// No espaço do conjunto (o nó que o ViewModel encolhe e estica para a
		// tela): o tamanho que a peça tem, sem o truque da câmera.
		Node3D drawn = view.Held?.GetChildCount() > 0 ? view.Held.GetChild(0) as Node3D : null;
		Skeleton3D first = drawn == null ? null : AnimationKit.FindSkeleton(drawn);
		float firstSize = GripSize(first, drawn);
		b.Check(firstSize > 0, "a granada na primeira pessoa tem o osso da peça");

		foreach (int team in new[] { Teams.Blue, Teams.Orange })
		{
			var puppet = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Bot>();
			puppet.Team = team;
			puppet.Remote = true;
			b.GetTree().CurrentScene.AddChild(puppet);
			await b.Frames(3);
			Vector3 at = b.Player.GlobalPosition + new Vector3(team == Teams.Blue ? 2 : -2, 0, -4f);
			for (int i = 0; i < 40; i++)
			{
				puppet.RemoteUpdate(at, Vector3.Zero, 0f, 0f, true, Form.Human, Slot.Grenade, "ar15", "deagle", 200f, 200f, 0f, 0f, false);
				puppet.RemoteMotion(false, false, false, false, false);
				puppet.CollisionLayer = 0;
				await b.Frames(1);
			}
			HeldRig rig = Nodes.FindFirst<HeldRig>(puppet);
			float thirdSize = GripSize(rig?.Skeleton, null);
			string who = team == Teams.Blue ? "Eve" : "Barut";
			RegressionBench.Note($"{who}: granada de fora {thirdSize * 100f:0.000}, de dentro {firstSize * 100f:0.000} (escala do osso Grip × 100)");
			b.Check(thirdSize > 0, $"{who}: a granada de fora tem o osso da peça");
			b.Near(firstSize, thirdSize, firstSize * 0.02, $"{who}: do mesmo tamanho (até 2%)");
			puppet.QueueFree();
			await b.Frames(2);
		}
	}

	/// <summary>A escala do osso Grip (a média dos três eixos): no mundo, ou no
	/// espaço de <paramref name="space"/> quando ele é dado.</summary>
	private static float GripSize(Skeleton3D skeleton, Node3D space)
	{
		if (skeleton == null) return 0f;
		int grip = skeleton.FindBone("Grip");
		if (grip < 0) return 0f;
		Transform3D t = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(grip);
		if (space != null) t = space.GlobalTransform.AffineInverse() * t;
		return (t.Basis.X.Length() + t.Basis.Y.Length() + t.Basis.Z.Length()) / 3f;
	}
}
