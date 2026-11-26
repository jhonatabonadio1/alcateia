using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Trocar as teclas nas Opções → Controles (Jhon, 26/11: "opção de editar
/// controles nas configurações do game"). A troca muda o <see cref="InputMap"/>
/// e o jogador obedece à tecla nova; tecla já usada por outra ação pergunta e
/// troca as duas; o Esc não se troca e cancela; "voltar ao padrão" volta; e a
/// troca sobrevive a salvar e abrir o arquivo de opções. Depois, o mesmo pela
/// tela, no menu do Esc da partida: clicar na tecla, apertar a nova, o conflito
/// e o Esc que cancela sem fechar o menu.
///
/// Com <c>--fotos=pasta</c> (no Docker, com tela), guarda a aba Controles normal,
/// esperando a tecla e com a pergunta do conflito.
/// </summary>
public sealed class KeyBindingsTest : RegressionTest
{
	public override string Name => "TrocarTeclas";
	public override string Description => "Opções → Controles: trocar a tecla muda o InputMap e o jogador obedece; conflito troca as duas; Esc não se troca e cancela; padrão volta; a troca fica salva";
	public override string Scene => RegressionBench.Match;

	private static InputEventKey Key(Godot.Key key) => new() { PhysicalKeycode = key };

	private static string Bound(string action) =>
		string.Join(",", InputMap.ActionGetEvents(action).Select(KeyBindings.Encode));

	public override async Task Run(RegressionBench b)
	{
		var saved = new Dictionary<string, string>(Settings.Values.Keys ?? new());
		float sensitivity = Settings.Values.Sensitivity;
		try
		{
			await Rules(b);
			await Screen(b);
		}
		finally
		{
			RegressionBench.ReleaseAll();
			Settings.Values.Keys = saved;
			Settings.Values.Sensitivity = sensitivity;
			KeyBindings.Apply();
			Settings.Save();
		}
	}

	/// <summary>As regras, direto no <see cref="KeyBindings"/>.</summary>
	private static async Task Rules(RegressionBench b)
	{
		KeyBindings.ResetAll();
		b.Check(Bound("jump") == KeyBindings.Encode(Key(Godot.Key.Space)) && KeyBindings.Label("jump") == "Espaço", $"de fábrica, pular é o Espaço ({Bound("jump")}, {KeyBindings.Label("jump")})");
		b.Check(KeyBindings.All.All(x => KeyBindings.Default(x.Action) != null), "toda ação trocável tem tecla de fábrica");

		// Pular no K: o InputMap muda e o corpo pula com o K, não com o Espaço.
		await b.Place(new Vector3(-15, 1, 13), 0f);
		b.Check(KeyBindings.Set("jump", Key(Godot.Key.K), swap: false), "trocou pular para o K");
		b.Check(Bound("jump") == KeyBindings.Encode(Key(Godot.Key.K)), $"o InputMap de pular tem só o K ({Bound("jump")})");

		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Godot.Key.Space, Pressed = true });
		await b.Frames(2);
		bool spaceJumps = Input.IsActionPressed("jump");
		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Godot.Key.Space, Pressed = false });
		await b.Seconds(0.4);
		float before = b.Player.GlobalPosition.Y;
		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Godot.Key.K, Pressed = true });
		bool intent = false;
		float top = before;
		for (int i = 0; i < 40; i++)
		{
			intent |= CharacterIntent.FromInput().JumpHeld;
			await b.PhysicsFrame();
			top = Mathf.Max(top, b.Player.GlobalPosition.Y);
		}
		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Godot.Key.K, Pressed = false });
		RegressionBench.Note($"pular no K: o corpo subiu {RegressionBench.F((top - before) * 100)} cm");
		b.Check(!spaceJumps, "o Espaço não pula mais");
		b.Check(intent, "a intenção do jogador lê o K como pular");
		b.AtLeast(0.5, top - before, "o corpo pulou com o K (m)");

		// Mouse: a coronhada no botão do meio.
		b.Check(KeyBindings.Set("melee", new InputEventMouseButton { ButtonIndex = MouseButton.Middle }, swap: false), "trocou a coronhada para o botão do meio");
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true });
		await b.Frames(2);
		b.Check(Input.IsActionPressed("melee") && KeyBindings.Label("melee") == "Botão do meio", $"o botão do meio é a coronhada ({KeyBindings.Label("melee")})");
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = false });

		// Conflito: agachar no K, que é de pular.
		b.Check(KeyBindings.ConflictWith("crouch", Key(Godot.Key.K)) == "jump", "agachar no K: o conflito é com pular");
		b.Check(!KeyBindings.Set("crouch", Key(Godot.Key.K), swap: false) && Bound("crouch") == KeyBindings.Encode(Key(Godot.Key.Ctrl)),
			"sem confirmar a troca, nada muda");
		b.Check(KeyBindings.Set("crouch", Key(Godot.Key.K), swap: true), "confirmando, troca");
		b.Check(Bound("crouch") == KeyBindings.Encode(Key(Godot.Key.K)) && Bound("jump") == KeyBindings.Encode(Key(Godot.Key.Ctrl)),
			$"as duas trocaram: agachar no K, pular no Ctrl ({Bound("crouch")} / {Bound("jump")})");

		// O Esc não se troca.
		b.Check(!KeyBindings.Set("drop", Key(Godot.Key.Escape), swap: true) && Bound("drop") == KeyBindings.Encode(Key(Godot.Key.G)), "o Esc não se troca");

		// Padrão: uma ação, e todas.
		KeyBindings.Reset("crouch");
		b.Check(Bound("crouch") == KeyBindings.Encode(Key(Godot.Key.Ctrl)) && Bound("jump") == KeyBindings.Encode(Key(Godot.Key.K)),
			$"agachar volta ao Ctrl; pular, que estava nele, fica com o K ({Bound("crouch")} / {Bound("jump")})");
		KeyBindings.ResetAll();
		b.Check(KeyBindings.All.All(x => KeyBindings.IsDefault(x.Action)) && Bound("jump") == KeyBindings.Encode(Key(Godot.Key.Space)) && Bound("melee") == KeyBindings.Encode(Key(Godot.Key.V)),
			"restaurar tudo volta às de fábrica");

		// Salvo: troca, grava, esquece e lê de novo o arquivo.
		KeyBindings.Set("drop", Key(Godot.Key.H), swap: false);
		Settings.Values.Keys.Clear();
		KeyBindings.Apply();
		b.Check(Bound("drop") == KeyBindings.Encode(Key(Godot.Key.G)), "esquecida na memória, soltar volta ao G");
		Settings.Load();
		InputSetup.EnsureActions();
		b.Check(Bound("drop") == KeyBindings.Encode(Key(Godot.Key.H)), $"lido o arquivo de opções, soltar está no H ({Bound("drop")})");

		// Arquivo editado à mão com duas ações na mesma tecla: a segunda volta à de fábrica.
		Settings.Values.Keys["move_back"] = KeyBindings.Encode(Key(Godot.Key.W));
		KeyBindings.Apply();
		b.Check(Bound("move_forward") == KeyBindings.Encode(Key(Godot.Key.W)) && Bound("move_back") == KeyBindings.Encode(Key(Godot.Key.S)),
			"duas ações na mesma tecla no arquivo: a de depois volta à de fábrica");
		KeyBindings.ResetAll();
	}

	/// <summary>A aba Controles no menu do Esc da partida.</summary>
	private static async Task Screen(RegressionBench b)
	{
		string photos = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--fotos="))?["--fotos=".Length..];
		async Task Photo(string name)
		{
			if (photos == null) return;
			await b.Frames(8);
			System.IO.Directory.CreateDirectory(photos);
			b.GetViewport().GetTexture().GetImage()?.SavePng(System.IO.Path.Combine(photos, name + ".png"));
		}

		var menu = b.GetTree().Root.FindChildren("*", nameof(CanvasLayer), true, false).OfType<GameMenu>().FirstOrDefault();
		b.Check(menu != null, "há o menu da partida");
		if (menu == null) return;
		foreach (var d in b.GetTree().Root.FindChildren("*", "", true, false).OfType<DifficultyMenu>()) d.QueueFree();

		Input.ParseInputEvent(new InputEventAction { Action = "menu", Pressed = true });
		await b.Frames(3);
		Find<Button>(menu, x => x.Text == "Opções" && x.IsVisibleInTree())?.EmitSignal(BaseButton.SignalName.Pressed);
		await b.Frames(3);
		Button tab = Find<Button>(menu, x => x.Text == Loc.Upper("Controles") && x.IsVisibleInTree());
		b.Check(tab != null, "as Opções da partida têm a aba Controles");
		tab?.EmitSignal(BaseButton.SignalName.Pressed);
		await b.Frames(3);

		List<KeyCapture> keys = menu.FindChildren("*", "", true, false).OfType<KeyCapture>().ToList();
		b.Check(keys.Count == KeyBindings.All.Length, $"uma tecla para cada ação trocável ({keys.Count} de {KeyBindings.All.Length})");
		KeyCapture jump = keys.FirstOrDefault(k => k.Action == "jump"), crouch = keys.FirstOrDefault(k => k.Action == "crouch"),
			drop = keys.FirstOrDefault(k => k.Action == "drop");
		if (jump == null || crouch == null || drop == null) return;
		b.Check(jump.Text == "Espaço" && crouch.Text == "Ctrl", $"a aba mostra as teclas de agora ({jump.Text}, {crouch.Text})");
		await Photo("controles_1_normal");

		// Clicar em pular e apertar o L.
		jump.EmitSignal(BaseButton.SignalName.Pressed);
		await b.Frames(2);
		b.Check(jump.IsListening && KeyCapture.Listening == jump, "clicada, a tecla espera");
		await Photo("controles_2_esperando");
		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Godot.Key.L, Keycode = Godot.Key.L, Pressed = true });
		await b.Frames(2);
		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Godot.Key.L, Keycode = Godot.Key.L, Pressed = false });
		await b.Frames(2);
		b.Check(!jump.IsListening && Bound("jump") == KeyBindings.Encode(Key(Godot.Key.L)) && jump.Text == "L", $"apertado o L, pular é o L ({jump.Text})");

		// Agachar no L: pergunta; trocar troca as duas.
		crouch.EmitSignal(BaseButton.SignalName.Pressed);
		await b.Frames(2);
		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Godot.Key.L, Keycode = Godot.Key.L, Pressed = true });
		await b.Frames(3);
		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Godot.Key.L, Keycode = Godot.Key.L, Pressed = false });
		await b.Frames(2);
		Control prompt = Find<Control>(menu, x => x.Name == "Conflito" && x.IsVisibleInTree());
		b.Check(prompt != null && Bound("crouch") == KeyBindings.Encode(Key(Godot.Key.Ctrl)), "o L já é de pular: pergunta antes de trocar");
		await Photo("controles_3_conflito");
		Button swap = prompt == null ? null : Find<Button>(prompt, x => x.Text == "Trocar");
		swap?.EmitSignal(BaseButton.SignalName.Pressed);
		await b.Frames(3);
		b.Check(Bound("crouch") == KeyBindings.Encode(Key(Godot.Key.L)) && Bound("jump") == KeyBindings.Encode(Key(Godot.Key.Ctrl))
			&& crouch.Text == "L" && jump.Text == "Ctrl", $"trocou: agachar no L, pular no Ctrl ({crouch.Text}, {jump.Text})");

		// O Esc cancela a captura e não fecha o menu.
		drop.EmitSignal(BaseButton.SignalName.Pressed);
		await b.Frames(2);
		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Godot.Key.Escape, Keycode = Godot.Key.Escape, Pressed = true });
		await b.Frames(3);
		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Godot.Key.Escape, Keycode = Godot.Key.Escape, Pressed = false });
		await b.Frames(2);
		b.Check(!drop.IsListening && Bound("drop") == KeyBindings.Encode(Key(Godot.Key.G)), "o Esc cancela a troca");
		b.Check(drop.IsVisibleInTree(), "e o menu continua aberto");

		// Restaurar padrão (rodapé): tudo de fábrica.
		Find<Button>(menu, x => x.Text == "Restaurar padrão" && x.IsVisibleInTree())?.EmitSignal(BaseButton.SignalName.Pressed);
		await b.Frames(3);
		b.Check(KeyBindings.All.All(x => KeyBindings.IsDefault(x.Action)), "Restaurar padrão volta todas as teclas");
		await Photo("controles_4_padrao");

		Input.ParseInputEvent(new InputEventAction { Action = "menu", Pressed = true });
		await b.Frames(2);
		Input.ParseInputEvent(new InputEventAction { Action = "menu", Pressed = true });
		await b.Frames(2);
	}

	private static T Find<T>(Node root, System.Func<T, bool> test) where T : Node =>
		root.FindChildren("*", "", true, false).OfType<T>().FirstOrDefault(test);
}
