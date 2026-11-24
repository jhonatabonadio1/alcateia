using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// O barulho do corpo (resources/body/ruido.tres): um passo por passada
/// (1,19 m correndo), nada agachado nem andando devagar, baque ao pousar, rugido a
/// cada meio segundo. É o que os bots ouvem — mudar isso muda a
/// furtividade inteira.
/// </summary>
public sealed class NoiseTest : RegressionTest
{
	public override string Name => "Ruido";
	public override string Description => "um passo por passada correndo, silêncio agachado e com Shift, baque ao pousar, rugido";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		var heard = new List<Noise>();
		void Ear(Noise noise)
		{
			if (noise.Source == b.Player) heard.Add(noise);
		}
		NoiseBus.Subscribe(Ear);

		try
		{
			// Correndo 3,24 m/s por 3 s: uns 9,5 m, oito passadas de 1,19 m.
			int steps = await Walk(b, heard, "move_forward");
			b.Check(steps is >= 7 and <= 9, $"passos correndo em 3 s: {steps} (esperado 8)");
			float radius = heard.Find(n => n.Kind == NoiseKind.Step).Radius;
			b.Near(13, radius, 0.01, "raio do passo humano");

			b.Check(await Walk(b, heard, "move_forward", "crouch") == 0, "agachado não faz passo");
			b.Check(await Walk(b, heard, "move_forward", "walk") == 0, "com Shift não faz passo");

			// Pulo parado: um baque no pouso.
			await b.Place(new Vector3(-15, 1, 13), -90f);
			heard.Clear();
			await b.Tap("jump");
			await b.Seconds(1.2);
			int lands = heard.FindAll(n => n.Kind == NoiseKind.Land).Count;
			b.Check(lands == 1, $"baques no pouso do pulo: {lands}");
			b.Near(17, heard.Find(n => n.Kind == NoiseKind.Land).Radius, 0.01, "raio do baque");

			// O lobo pisa mais alto.
			await b.BecomeWolf();
			await Walk(b, heard, "move_forward");
			var wolfStep = heard.Find(n => n.Kind == NoiseKind.Step);
			b.Near(20, wolfStep.Radius, 0.01, "raio do passo do lobo");

			// Rugir: um ruído já no começo e outro a cada meio segundo.
			await b.Place(new Vector3(-15, 1, 13), -90f);
			heard.Clear();
			RegressionBench.Press("howl");
			await b.Seconds(1.1);
			RegressionBench.ReleaseAll();
			int howls = heard.FindAll(n => n.Kind == NoiseKind.Howl).Count;
			b.Check(howls == 3, $"ruídos do rugido em 1,1 s: {howls} (esperado 3: 0, 0,5 e 1,0 s)");
			b.Near(45, heard.Find(n => n.Kind == NoiseKind.Howl).Radius, 0.01, "raio do rugido");
		}
		finally
		{
			NoiseBus.Unsubscribe(Ear);
			RegressionBench.ReleaseAll();
		}
	}

	private static async Task<int> Walk(RegressionBench b, List<Noise> heard, params string[] keys)
	{
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		heard.Clear();
		RegressionBench.Press(keys);
		await b.Seconds(3.0);
		RegressionBench.ReleaseAll();
		await b.Seconds(0.3);
		return heard.FindAll(n => n.Kind == NoiseKind.Step).Count;
	}
}
