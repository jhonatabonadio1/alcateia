using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>A locutora fala uma coisa por vez (pedido do Jhon, 18/11): três
/// abates juntos viram "triple kill" direto, e uma fala nova corta a que
/// está tocando.</summary>
public sealed class AnnouncerTest : RegressionTest
{
	public override string Name => "Locutora";
	public override string Description => "abates juntos viram a maior fala; abate novo corta a fala que está tocando";

	private static AudioStreamPlayer Voice(RegressionBench b) =>
		b.GetTree().Root.GetNodeOrNull<AudioStreamPlayer>("Locutora");

	public override async Task Run(RegressionBench b)
	{
		// Três abates no mesmo instante: só o triple.
		WorldAudio.Say(KillStory.StreakVoice(2));
		WorldAudio.Say(KillStory.StreakVoice(3));
		await b.Seconds(0.3);
		var voice = Voice(b);
		b.Check(voice != null && voice.Playing, "a locutora fala");
		b.Check(voice?.Stream == Sfx.Stream(KillStory.StreakVoice(3)), "abates juntos: diz direto triple kill");
		b.Check(b.GetTree().Root.GetChildren().OfType<AudioStreamPlayer>().Count(p => p.Name.ToString().StartsWith("Locutora")) == 1,
			"um tocador só para a voz");

		// Double tocando, abate novo no meio: corta e diz quadra.
		WorldAudio.Say(KillStory.StreakVoice(2));
		await b.Seconds(0.4);
		b.Check(voice.Playing && voice.Stream == Sfx.Stream(KillStory.StreakVoice(2)), "double kill tocando");
		WorldAudio.Say(KillStory.StreakVoice(4));
		await b.Seconds(0.25);
		b.Check(voice.Playing && voice.Stream == Sfx.Stream(KillStory.StreakVoice(4)), "abate no meio da fala: corta e diz a próxima");
	}
}
