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

			// Lobo: o clipe toca na velocidade do corpo dividida pela
			// passada dele, então a distância por pisada é fixa.
			var visual = p.GetNode<WolfVisual>("WolfVisual");
			float run = ClipLength("res://assets/characters/wolf/run.fbx");
			float crawl = ClipLength("res://assets/characters/wolf/wallrun.fbx");
			b.Near(visual.RunClipSpeed * run / 2, profile.StrideWolf, 0.04, "passada do lobo (m)");
			b.Near(visual.CrawlClipSpeed * crawl / 2, profile.StrideWolfSprint, 0.04, "passada da corrida de quatro (m)");

			// E correndo de lobo, o que se ouve bate com a conta.
			await b.BecomeWolf();
			await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
			RegressionBench.Press("move_forward");
			await b.Seconds(0.8);
			double wolfSpeed = RegressionBench.Flat(await b.Displacement(p, 60));
			steps.Clear();
			await b.Seconds(2.0);
			RegressionBench.ReleaseAll();
			b.Near(wolfSpeed * 2.0 / profile.StrideWolf, steps.Count, 1.0, "passos do lobo em 2 s");
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
		foreach (string id in new[] { "m16a2", "sg552", "ak47", "ump45", "m3", "m9" })
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
