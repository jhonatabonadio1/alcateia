using Godot;
using System.Threading.Tasks;

/// <summary>
/// Os clipes carregam de verdade: cada um com trilhas (clipe sem trilha é
/// pose em T, sem erro nenhum no Output) e, na locomoção, sem o
/// deslocamento da raiz (com ele o corpo anda duas vezes e o pé patina).
/// Guarda o <see cref="AnimationKit"/>, que o humano, o lobo e o lobby
/// dividem.
/// </summary>
public sealed class AnimationTest : RegressionTest
{
	public override string Name => "Animacoes";
	public override string Description => "os clipes do humano e do lobo carregam com trilhas e sem andar a raiz";

	private static readonly string[] Human =
	{
		"idle_rifle", "walk_fwd", "walk_back", "run_fwd", "run_back", "strafe_left", "strafe_right",
		"jump", "fire", "reload", "grenade", "hit", "death", "crouch_idle", "crouch_walk", "fall", "land"
	};

	private static readonly string[] HumanLocomotion =
		{ "walk_fwd", "walk_back", "run_fwd", "run_back", "strafe_left", "strafe_right", "crouch_walk" };

	private static readonly string[] Wolf =
		{ "idle", "walk", "run", "wallrun", "claw", "heavy", "howl", "pounce", "dive", "dive_land", "jump_land" };

	private static readonly string[] WolfLocomotion = { "walk", "run", "wallrun" };

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();

		AnimationPlayer human = AnimationKit.FindPlayer(b.Player.GetNode("Human"));
		AnimationPlayer wolf = AnimationKit.FindPlayer(b.Player.GetNode("WolfVisual"));
		b.Check(human != null && wolf != null, "os dois corpos têm tocador");
		if (human == null || wolf == null) return;

		CheckLibrary(b, human, "alcateia", Human, HumanLocomotion);
		CheckLibrary(b, wolf, "lobo", Wolf, WolfLocomotion);
	}

	private static void CheckLibrary(RegressionBench b, AnimationPlayer player, string library,
		string[] clips, string[] locomotion)
	{
		int missing = 0, empty = 0;
		foreach (string clip in clips)
		{
			string name = $"{library}/{clip}";
			if (!player.HasAnimation(name)) { missing++; RegressionBench.Note($"falta {name}"); continue; }
			if (player.GetAnimation(name).GetTrackCount() == 0) { empty++; RegressionBench.Note($"{name} sem trilha"); }
		}
		b.Check(missing == 0, $"{library}: {clips.Length - missing} de {clips.Length} clipes carregados");
		b.Check(empty == 0, $"{library}: nenhum clipe sem trilha");

		foreach (string clip in locomotion)
		{
			string name = $"{library}/{clip}";
			if (!player.HasAnimation(name)) continue;
			b.AtMost(0.001, RootTravel(player.GetAnimation(name)), $"{name}: a raiz não anda no chão");
		}
	}

	/// <summary>Quanto o quadril anda no plano do chão ao longo do clipe.</summary>
	private static float RootTravel(Animation animation)
	{
		float most = 0;
		for (int track = 0; track < animation.GetTrackCount(); track++)
		{
			if (animation.TrackGetType(track) != Animation.TrackType.Position3D) continue;
			if (!animation.TrackGetPath(track).ToString().Contains("Hips")) continue;

			int keys = animation.TrackGetKeyCount(track);
			if (keys == 0) continue;
			var first = (Vector3)animation.TrackGetKeyValue(track, 0);
			for (int key = 1; key < keys; key++)
			{
				var value = (Vector3)animation.TrackGetKeyValue(track, key);
				most = Mathf.Max(most, new Vector2(value.X - first.X, value.Z - first.Z).Length());
			}
		}
		return most;
	}
}
