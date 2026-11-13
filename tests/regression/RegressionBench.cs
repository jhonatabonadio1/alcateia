using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// A bancada dos testes de regressão: carrega a cena do teste, tira os
/// menus do caminho e dá ao teste o que ele precisa — o jogador, esperar,
/// apertar teclas, virar lobo — e as verificações.
///
///   godot --headless --fixed-fps 60 --path . res://tests/regression/bench.tscn -- --teste=Nome
///   ... -- --lista        nomes de todos os testes
///
/// Sai com 0 quando passa, 1 quando falha e 2 quando o teste não existe.
/// O tools/testar.sh roda um processo por teste (nada de estado estático
/// vazando de um para o outro) e também reprova erro no Output.
/// </summary>
public partial class RegressionBench : Node
{
	public const string Training = "res://scenes/treino.tscn";
	public const string Match = "res://scenes/main.tscn";

	private readonly List<string> _failures = new();
	private int _checks;

	/// <summary>A cena do teste, carregada como filha da bancada.</summary>
	public Node Scene { get; private set; }

	public Player Player { get; private set; }
	public FormController Form => Player.GetNode<FormController>("FormController");
	public Weapon Weapon => Player.GetNode<Weapon>("Weapon");
	public Health Health => Player.GetNode<Health>("Health");

	/// <summary>A semente dos testes de regressão.</summary>
	public const ulong TestSeed = 20261112;

	/// <summary>Segundos desde o início do teste.</summary>
	public double Time { get; private set; }

	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		ProcessPhysicsPriority = -1000;

		string[] args = OS.GetCmdlineUserArgs();
		var tests = AllTests();

		// Teste sorteia sempre igual: a mesma semente a cada rodada, a não
		// ser que se peça outra (-- --seed=N).
		if (!args.Any(a => a.StartsWith("--seed=")))
			MatchRandom.Begin(TestSeed);

		if (args.Contains("--lista"))
		{
			foreach (RegressionTest t in tests) GD.Print($"{t.Name}\t{t.Description}");
			GetTree().Quit(0);
			return;
		}

		string name = args.FirstOrDefault(a => a.StartsWith("--teste="))?["--teste=".Length..];
		RegressionTest test = tests.FirstOrDefault(t => t.Name == name);
		if (test == null)
		{
			GD.PrintErr($"Teste '{name}' não existe. Use --lista.");
			GetTree().Quit(2);
			return;
		}

		try
		{
			test.Prepare();
			await Load(test.Scene);
			await test.Run(this);
		}
		catch (Exception error)
		{
			_failures.Add($"exceção: {error}");
		}

		if (_failures.Count == 0)
			GD.Print($"TESTE ok      {test.Name} ({_checks} verificações): {test.Description}");
		else
		{
			GD.Print($"TESTE FALHOU  {test.Name}: {test.Description}");
			foreach (string failure in _failures) GD.Print($"              {failure}");
		}

		GetTree().Quit(_failures.Count == 0 ? 0 : 1);
	}

	public override void _PhysicsProcess(double delta)
	{
		Time += delta;

		// Menus pausam a árvore e prendem o mouse; o teste não tem quem os
		// feche.
		UiState.Reset();
		GetTree().Paused = false;
	}

	private static List<RegressionTest> AllTests() =>
		Assembly.GetExecutingAssembly().GetTypes()
			.Where(t => t.IsSubclassOf(typeof(RegressionTest)) && !t.IsAbstract)
			.Select(t => (RegressionTest)Activator.CreateInstance(t))
			.OrderBy(t => t.Name)
			.ToList();

	private async Task Load(string path)
	{
		Scene = GD.Load<PackedScene>(path).Instantiate();
		AddChild(Scene);
		Player = Scene.FindChild("Player", true, false) as Player
			?? GetTree().GetNodesInGroup("player").OfType<Player>().FirstOrDefault();

		await Seconds(1.0);
		CloseLoadout();
		await Seconds(0.3);
	}

	/// <summary>Fecha a escolha de conjunto, que abre sozinha no começo e
	/// ao morrer, escolhendo o padrão.</summary>
	public void CloseLoadout()
	{
		Node menu = Scene.FindChild("LoadoutMenu", true, false);
		menu?.GetType().GetMethod("Choose", BindingFlags.NonPublic | BindingFlags.Instance)
			?.Invoke(menu, new object[] { 0, false });
	}

	// ------------------------------------------------------------- tempo

	public async Task Seconds(double seconds) =>
		await ToSignal(GetTree().CreateTimer(seconds, true, true), SceneTreeTimer.SignalName.Timeout);

	public async Task PhysicsFrame() =>
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

	/// <summary>Espera exatamente <paramref name="count"/> passos de física
	/// (60 por segundo). Para medir velocidade é isto, não segundos: o
	/// cronômetro pode chegar um quadro depois e somar 3% numa janela de
	/// meio segundo.</summary>
	public async Task Frames(int count)
	{
		for (int i = 0; i < count; i++) await PhysicsFrame();
	}

	/// <summary>Quanto o corpo anda em <paramref name="frames"/> passos de
	/// física. A primeira espera só alinha: vindo de um cronômetro, o
	/// sinal do passo chega antes de o corpo andar, e a janela perderia um
	/// passo (1,7% numa janela de um segundo).</summary>
	public async Task<Vector3> Displacement(Node3D body, int frames)
	{
		await PhysicsFrame();
		Vector3 from = body.GlobalPosition;
		await Frames(frames);
		return body.GlobalPosition - from;
	}

	/// <summary>Espera a condição, quadro a quadro, até o limite. Devolve
	/// se ela chegou a valer.</summary>
	public async Task<bool> Until(Func<bool> condition, double timeout)
	{
		double end = Time + timeout;
		while (Time < end)
		{
			if (condition()) return true;
			await PhysicsFrame();
		}
		return condition();
	}

	// ------------------------------------------------------------- jogador

	/// <summary>Vira lobo (com WP de sobra) e espera a transformação.</summary>
	public async Task BecomeWolf()
	{
		Form.AddWp(5000);
		Form.ToggleForm();
		await Until(() => Form.Current == global::Form.Wolf, 3.0);
		Check(Form.Current == global::Form.Wolf, "virou lobo");
	}

	/// <summary>Põe o jogador num lugar, parado, virado para <paramref name="yawDegrees"/>
	/// (0 olha para −Z, −90 para +X), e espera assentar.</summary>
	public async Task Place(Vector3 position, float yawDegrees, float pitchDegrees = 0f, double settle = 0.8)
	{
		ReleaseAll();
		Player.GlobalPosition = position;
		Player.Velocity = Vector3.Zero;
		Player.Rotation = new Vector3(0, Mathf.DegToRad(yawDegrees), 0);
		Player.GetNode<Node3D>("Head").Rotation = new Vector3(Mathf.DegToRad(pitchDegrees), 0, 0);
		await Seconds(settle);
	}

	/// <summary>Tira os bots da cena (ou deixa um, parado, como alvo).</summary>
	public CharacterBody3D[] Bots() =>
		GetTree().GetNodesInGroup("bot").OfType<CharacterBody3D>().ToArray();

	public void RemoveBots(int keep = 0)
	{
		CharacterBody3D[] bots = Bots();
		for (int i = keep; i < bots.Length; i++) bots[i].QueueFree();
	}

	/// <summary>Congela um bot: não pensa nem anda, mas o corpo continua
	/// no mundo, sólido e atingível.</summary>
	public static void Freeze(CharacterBody3D bot)
	{
		bot.DisableMode = CollisionObject3D.DisableModeEnum.KeepActive;
		bot.ProcessMode = ProcessModeEnum.Disabled;

		// A vida segue contando (a proteção de quem nasceu acaba).
		if (bot.GetNodeOrNull<Node>("Health") is Node health)
			health.ProcessMode = ProcessModeEnum.Always;
	}

	// ------------------------------------------------------------- teclas

	private static readonly string[] Actions =
		{ "move_forward", "move_back", "move_left", "move_right", "walk", "crouch", "jump", "attack", "special" };

	public static void Press(params string[] actions)
	{
		foreach (string a in actions) Input.ActionPress(a);
	}

	public static void Release(params string[] actions)
	{
		foreach (string a in actions) Input.ActionRelease(a);
	}

	public static void ReleaseAll() => Release(Actions);

	/// <summary>Aperta e solta no quadro seguinte (um toque).</summary>
	public async Task Tap(string action)
	{
		Input.ActionPress(action);
		await PhysicsFrame();
		await PhysicsFrame();
		Input.ActionRelease(action);
	}

	// ------------------------------------------------------------- verificações

	public void Check(bool condition, string what)
	{
		_checks++;
		if (!condition) _failures.Add(what);
	}

	public void Near(double expected, double actual, double tolerance, string what)
	{
		_checks++;
		if (double.IsNaN(actual) || Math.Abs(expected - actual) > tolerance)
			_failures.Add($"{what}: esperado {F(expected)} ± {F(tolerance)}, veio {F(actual)}");
	}

	public void AtMost(double limit, double actual, string what)
	{
		_checks++;
		if (double.IsNaN(actual) || actual > limit)
			_failures.Add($"{what}: no máximo {F(limit)}, veio {F(actual)}");
	}

	public void AtLeast(double limit, double actual, string what)
	{
		_checks++;
		if (double.IsNaN(actual) || actual < limit)
			_failures.Add($"{what}: no mínimo {F(limit)}, veio {F(actual)}");
	}

	/// <summary>Anotação no Output, para quem lê o relatório.</summary>
	public static void Note(string text) => GD.Print($"  · {text}");

	public static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

	public static float Flat(Vector3 v) => new Vector2(v.X, v.Z).Length();
}
