using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O passo cai com o pé: o ruído do passo sai a cada passada
/// (resources/body/ruido.tres), e a passada tem de ser a do clipe que
/// está tocando — velocidade em que o pé não patina × duração do ciclo ÷
/// duas pisadas. Trocar um clipe ou a velocidade de andar sem refazer a
/// conta quebra aqui.
/// </summary>
public sealed class StepRhythmTest : RegressionTest
{
	public override string Name => "PassoNoRitmo";
	public override string Description => "o som do passo cai na cadência da animação: humano, lobo e corrida de quatro";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		Player p = b.Player;
		var profile = p.Noise ?? new BodyNoiseProfile();

		var steps = new List<double>();
		double clock = 0;
		void Ear(Noise noise)
		{
			if (noise.Source == p && noise.Kind == NoiseKind.Step) steps.Add(clock);
		}
		NoiseBus.Subscribe(Ear);

		try
		{
			// Humano: o clipe de corrida toca a 1×, então o pé cai a cada
			// meio ciclo — e o som tem de cair junto.
			float cycle = ClipLength("res://assets/characters/human/run_fwd.fbx");
			await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
			RegressionBench.Press("move_forward");
			await b.Seconds(0.8);
			float speed = RegressionBench.Flat(await b.Displacement(p, 60));
			steps.Clear();
			for (int i = 0; i < 180; i++)
			{
				await b.PhysicsFrame();
				clock += 1.0 / 60.0;
			}
			RegressionBench.ReleaseAll();

			double expected = 3.0 / (cycle / 2);
			b.Near(expected, steps.Count, 1.0, $"passos do humano em 3 s (ciclo de {RegressionBench.F(cycle)} s)");
			b.Near(speed * cycle / 2, profile.StrideHuman, 0.05, "passada do humano (m)");

			// Lobo em pé: o andar do Jhon acelera até a velocidade do corpo
			// (o clipe toca na velocidade dividida pela passada), então a
			// distância por pisada é fixa: a passada do clipe × ciclo ÷ 2. O
			// perfil guarda a de frente, para quem não tem o modelo.
			var visual = p.GetNode<WolfVisual>("WolfVisual");
			float walk = WolfOwnClips.Length("walk");
			float sprint = WolfOwnClips.Length("sprint");
			b.Near(visual.WalkClipSpeed * walk / 2, profile.StrideWolf, 0.04, "passada do lobo em pé (m)");

			// E andando de lobo, o que se ouve bate com a conta.
			await b.BecomeWolf();
			await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
			RegressionBench.Press("move_forward");
			await b.Seconds(0.8);
			double wolfSpeed = RegressionBench.Flat(await b.Displacement(p, 60));
			b.Near(visual.WalkClipSpeed * walk / 2, visual.StepStride, 0.02, "o corpo usa a passada do andar de frente (m)");
			steps.Clear();
			await b.Seconds(2.0);
			RegressionBench.ReleaseAll();
			RegressionBench.Note($"lobo em pé a {RegressionBench.F((float)wolfSpeed)} m/s: {steps.Count} passos em 2 s, passada {RegressionBench.F(visual.StepStride)} m");
			b.Near(wolfSpeed * 2.0 / visual.StepStride, steps.Count, 1.0, "passos do lobo em 2 s");

			// De quatro: o clipe acompanha o corpo (velocidade ÷ SprintClipSpeed),
			// e o som sai uma vez por ciclo, no quadro em que as patas da frente
			// e as de trás se juntam (SprintGather) — o primeiro, contado do
			// começo da corrida, que sempre começa do começo do clipe.
			float sceneScale = visual.SprintAnimScale;
			visual.SprintAnimScale = 1f;
			await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
			steps.Clear();
			await b.Tap("move_forward");
			await b.Seconds(0.08);
			double start = clock;
			RegressionBench.Press("move_forward");
			for (int i = 0; i < 150; i++)
			{
				await b.PhysicsFrame();
				clock += 1.0 / 60.0;
			}
			float gallop = RegressionBench.Flat(p.Velocity);
			RegressionBench.ReleaseAll();
			visual.SprintAnimScale = sceneScale;

			var beats = steps.Where(t => t >= start).ToList();
			float rate = gallop / visual.SprintClipSpeed;
			b.Check(beats.Count >= 3, $"de quatro: {beats.Count} passos em 2,5 s");
			if (beats.Count >= 3)
			{
				RegressionBench.Note($"de quatro a {RegressionBench.F(gallop)} m/s: {beats.Count} passos, o primeiro {RegressionBench.F((float)(beats[0] - start))} s depois do W, um a cada {RegressionBench.F((float)((beats[^1] - beats[0]) / (beats.Count - 1)))} s");
				// A corrida acelera do trote até a velocidade dela no começo: o
				// primeiro passo chega um pouco depois da conta.
				b.Near(WolfVisual.SprintGather / rate, beats[0] - start, 0.1, "o primeiro passo cai quando as patas se juntam (s; a corrida começa 2 a 3 quadros depois do W)");
				b.Near(sprint / rate, (beats[^1] - beats[0]) / (beats.Count - 1), 0.02, "um passo por ciclo do clipe (s)");
			}

			// Sem o modelo (o perfil): a mesma batida, em metros.
			b.Near(visual.SprintClipSpeed * sprint, profile.StrideWolfSprint, 0.04, "passada da corrida de quatro no perfil (m)");
		}
		finally
		{
			NoiseBus.Unsubscribe(Ear);
			RegressionBench.ReleaseAll();
		}
	}

	private static float ClipLength(string fbx)
	{
		Node scene = GD.Load<PackedScene>(fbx).Instantiate();
		var player = Nodes.FindFirst<AnimationPlayer>(scene);
		float length = (float)player.GetAnimation(player.GetAnimationList()[0]).Length;
		scene.Free();
		return length;
	}
}

/// <summary>
/// O som do lugar: cada mapa tem o fundo e a reverberação dele, o chão
/// diz do que é feito (e há som de passo para cada superfície), e cada
/// arma tem as variações de tiro — as dos outros e as suas.
/// </summary>
public sealed class MapSoundTest : RegressionTest
{
	public override string Name => "SomDoMapa";
	public override string Description => "fundo e reverberação por mapa, passo por superfície, variações de tiro por arma";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();

		foreach (MapInfo map in Maps.All)
		{
			b.Check(Sfx.Has(map.Ambience), $"{map.Name}: tem som de fundo ({map.Ambience})");
			b.Check(string.IsNullOrEmpty(map.EventSound) || Sfx.Has(map.EventSound), $"{map.Name}: o som eventual existe");
		}

		// Reverberação no canal do mundo, teto na saída.
		Sfx.SetRoom(0.7f, 0.24f);
		int world = AudioServer.GetBusIndex(Sfx.BusWorld);
		b.Check(world >= 0 && AudioServer.GetBusSend(world) == Sfx.BusSfx, "o canal Mundo existe e desemboca no SFX");
		b.Check(AudioServer.GetBusEffect(world, 0) is AudioEffectReverb { Wet: > 0.2f } && AudioServer.IsBusEffectEnabled(world, 0),
			"SetRoom liga a reverberação do mapa");
		Sfx.SetRoom(0.3f, 0f);
		b.Check(!AudioServer.IsBusEffectEnabled(world, 0), "reverberação 0 desliga o efeito");
		b.Check(AudioServer.GetBusEffectCount(0) > 0 && AudioServer.GetBusEffect(0, 0) is AudioEffectHardLimiter, "limitador na saída");

		// Um som de passo e um de pouso por superfície, humano e lobo.
		foreach (string surface in new[] { "metal", "neve", "madeira", "terra" })
			foreach (string name in new[] { "passo", "pouso", "lobo_passo", "lobo_pouso" })
				b.Check(Sfx.Has($"{name}_{surface}"), $"existe {name}_{surface}");
		b.Check(Sfx.Has("impacto") && Sfx.Has("impacto_metal") && Sfx.Has("capsula"), "bala na parede e cápsula no chão");

		// Cada arma de fogo: o tiro dos outros e o seu.
		foreach (string id in new[] { "ar15", "deagle" })
			b.Check(Sfx.Has($"tiro_{id}") && Sfx.Has($"tiro_proprio_{id}") && Sfx.Has($"tiro_distante_{id}"), $"tiros da {id}");

		// O chão diz do que é feito: na Estação Polar, neve no chão e
		// metal nas passarelas.
		Node map3d = GD.Load<PackedScene>("res://scenes/maps/polar.tscn").Instantiate();
		((Node3D)map3d).Position = new Vector3(3000, 0, 0);
		b.AddChild(map3d);
		await b.PhysicsFrame();
		await b.PhysicsFrame();

		var found = new HashSet<string>();
		foreach (Node node in map3d.FindChildren("*", nameof(StaticBody3D), true, false))
		{
			string surface = Surfaces.Of(node);
			if (surface == "" || found.Contains(surface) || node is not StaticBody3D body) continue;
			var shape = body.GetNodeOrNull<CollisionShape3D>("Colisao")?.Shape as BoxShape3D;
			if (shape == null || body.GlobalTransform.Basis.Y.Dot(Vector3.Up) < 0.99f) continue;

			Vector3 top = body.GlobalPosition + Vector3.Up * (shape.Size.Y / 2 + 0.02f);
			if (Surfaces.Under(top, b.Player) == surface) found.Add(surface);
		}
		b.Check(found.Contains("neve") && found.Contains("metal"), $"superfícies achadas pelo raio na Polar: {string.Join(", ", found.OrderBy(s => s))}");

		map3d.QueueFree();
		await b.PhysicsFrame();
	}
}
