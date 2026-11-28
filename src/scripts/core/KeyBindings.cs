using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// As teclas que o jogador troca nas Opções → Controles (Jhon, 26/11: "opção de
/// editar controles nas configurações do game").
///
/// Cada ação trocável tem **uma** tecla ou botão do mouse. O padrão é o do
/// <see cref="InputSetup"/>; a troca fica em <see cref="Settings.Data.Keys"/>
/// (user://settings.json), um texto curto por ação (<c>"K:87"</c> é a tecla física
/// W, <c>"M:1"</c> o clique esquerdo), e vai para o <see cref="InputMap"/> na hora
/// (<see cref="Apply"/>). O jogo lê só as ações (<see cref="CharacterIntent"/>),
/// então a tecla nova vale em tudo sem mais nada.
///
/// Fica de fora o que não se troca: o Esc (menu, e é ele que cancela a captura) e
/// as teclas de número do rádio, que escolhem a fala com ele aberto.
/// </summary>
public static class KeyBindings
{
	/// <summary>Uma ação que se troca: o nome no <see cref="InputMap"/>, o grupo na
	/// aba e o que ela faz.</summary>
	public readonly record struct Binding(string Action, string Group, string Label, string Note);

	/// <summary>As ações trocáveis, na ordem da aba Controles.</summary>
	public static readonly Binding[] All =
	{
		new("move_forward", "Movimento", "Andar para a frente", "Duas vezes seguidas e segurando, o lobo corre de quatro. Segurado, sustenta a corrida na parede."),
		new("move_back", "Movimento", "Andar para trás", ""),
		new("move_left", "Movimento", "Andar para a esquerda", ""),
		new("move_right", "Movimento", "Andar para a direita", ""),
		new("jump", "Movimento", "Pular", "Pular, e saltar da parede com o lobo."),
		new("crouch", "Movimento", "Agachar", "Segurar para ficar agachado, nas duas formas."),
		new("sprint", "Movimento", "Correr", "O humano corre para a frente enquanto segura, de arma abaixada: correndo não atira."),

		new("attack", "Combate", "Atacar", "Atirar, golpear com a faca, lançar a granada, a garra do lobo."),
		new("special", "Combate", "Mirar · bote", "Mirar (humano) e o bote no ar (lobo)."),
		new("pounce", "Combate", "Especial do lobo", "Salta na mira, também no ar. Enche com abates e, devagar, com o tempo."),
		new("howl", "Combate", "Recarregar · uivar", "Recarregar (humano) e o uivo do lobo (segurar até o fim)."),
		new("melee", "Combate", "Coronhada", "A coronhada com a arma de fogo; com a faca, golpeia; com a granada, lança."),
		new("drop", "Combate", "Soltar a arma", "Solta a arma da mão, que cai à frente."),

		new("slot_1", "Armas", "Arma principal", ""),
		new("slot_2", "Armas", "Arma secundária", ""),
		new("slot_3", "Armas", "Virar lobo", "O lobo é um slot, como as armas: apertar outro slot volta a humano."),
		new("slot_4", "Armas", "Faca", ""),
		new("slot_5", "Armas", "Granada", ""),

		new("scoreboard", "Interface", "Placar", "Segurar para ver o placar."),
		new("radio", "Interface", "Rádio do time", "Abre a lista das falas; com ela aberta, as teclas de número escolhem (essas não se trocam)."),
		new("guide", "Interface", "Guia das habilidades", "Segurar para ver o que cada habilidade faz."),
		new("spectate_free", "Interface", "Câmera livre (morto)", "Morto, solta a câmera de espectador."),
		new("chat_all", "Interface", "Chat para todos", "Na partida online, abre o chat para a sala inteira. Enter manda, Esc fecha, Tab troca para o time."),
		new("chat_team", "Interface", "Chat do time", "Na partida online, abre o chat só para o seu time."),
	};

	/// <summary>O que não se pode usar numa troca: o Esc abre o menu e cancela a captura.</summary>
	public static bool Reserved(InputEvent input) =>
		input is InputEventKey key && Physical(key) == Key.Escape;

	public static bool Has(string action) => All.Any(b => b.Action == action);

	public static Binding Of(string action) => All.First(b => b.Action == action);

	// ------------------------------------------------------------------ texto salvo

	/// <summary>A tecla ou o botão em texto curto, para o arquivo de opções. Nulo se
	/// não é coisa que se troca (eixo, toque, tecla sem código).</summary>
	public static string Encode(InputEvent input) => input switch
	{
		InputEventKey key when Physical(key) != Key.None => $"K:{(long)Physical(key)}",
		InputEventMouseButton mouse when mouse.ButtonIndex != MouseButton.None => $"M:{(long)mouse.ButtonIndex}",
		_ => null
	};

	public static InputEvent Decode(string code)
	{
		if (string.IsNullOrEmpty(code) || code.Length < 3 || code[1] != ':') return null;
		if (!long.TryParse(code.AsSpan(2), out long value) || value <= 0) return null;
		return code[0] switch
		{
			'K' => new InputEventKey { PhysicalKeycode = (Key)value },
			'M' => new InputEventMouseButton { ButtonIndex = (MouseButton)value },
			_ => null
		};
	}

	private static Key Physical(InputEventKey key) => key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;

	// ------------------------------------------------------------------ o que vale

	/// <summary>A tecla de fábrica da ação.</summary>
	public static InputEvent Default(string action) => Decode(InputSetup.DefaultOf(action));

	/// <summary>A tecla da ação agora: a trocada, ou a de fábrica.</summary>
	public static InputEvent Current(string action)
	{
		Dictionary<string, string> keys = Settings.Values.Keys;
		if (keys != null && keys.TryGetValue(action, out string code) && Decode(code) is InputEvent chosen) return chosen;
		return Default(action);
	}

	public static bool IsDefault(string action) => Encode(Current(action)) == Encode(Default(action));

	/// <summary>A mesma tecla ou o mesmo botão.</summary>
	public static bool Same(InputEvent a, InputEvent b) => a != null && b != null && Encode(a) == Encode(b);

	/// <summary>Outra ação trocável que já usa esta tecla, ou nulo.</summary>
	public static string ConflictWith(string action, InputEvent input)
	{
		foreach (Binding b in All)
			if (b.Action != action && Same(Current(b.Action), input)) return b.Action;
		return null;
	}

	/// <summary>
	/// Troca a tecla da ação. Se outra ação usava a tecla nova: com
	/// <paramref name="swap"/>, ela fica com a tecla que esta tinha (as duas
	/// trocam); sem, nada muda e devolve falso. Salva e vale na hora.
	/// </summary>
	public static bool Set(string action, InputEvent input, bool swap)
	{
		if (!Has(action) || Encode(input) == null || Reserved(input)) return false;

		string other = ConflictWith(action, input);
		if (other != null && !swap) return false;

		InputEvent previous = Current(action);
		Store(action, input);
		if (other != null) Store(other, previous);
		Apply();
		Settings.Save();
		return true;
	}

	/// <summary>A ação volta à tecla de fábrica. Se outra ação estava com ela, essa
	/// fica com a tecla que esta tinha, para não ficarem duas na mesma.</summary>
	public static void Reset(string action)
	{
		InputEvent fabric = Default(action);
		if (fabric == null) return;
		string other = ConflictWith(action, fabric);
		InputEvent previous = Current(action);
		Settings.Values.Keys?.Remove(action);
		if (other != null) Store(other, previous);
		Apply();
		Settings.Save();
	}

	/// <summary>Todas de volta às de fábrica.</summary>
	public static void ResetAll()
	{
		Settings.Values.Keys?.Clear();
		Apply();
		Settings.Save();
	}

	private static void Store(string action, InputEvent input)
	{
		Settings.Values.Keys ??= new Dictionary<string, string>();
		string code = Encode(input);
		if (code == null || code == Encode(Default(action))) Settings.Values.Keys.Remove(action);
		else Settings.Values.Keys[action] = code;
	}

	/// <summary>
	/// Leva as teclas ao <see cref="InputMap"/>: cada ação trocável fica só com a
	/// dela (a trocada ou a de fábrica). Pode chamar quantas vezes quiser. Arquivo
	/// editado à mão com duas ações na mesma tecla, ou com o Esc: a de depois volta
	/// à de fábrica.
	/// </summary>
	public static void Apply()
	{
		var taken = new HashSet<string>();
		foreach (Binding b in All)
		{
			if (!InputMap.HasAction(b.Action)) continue;
			InputEvent input = Current(b.Action);
			if (input == null || Reserved(input) || !taken.Add(Encode(input)))
			{
				Settings.Values.Keys?.Remove(b.Action);
				input = Default(b.Action);
				if (input == null) continue;
				taken.Add(Encode(input));
			}
			InputMap.ActionEraseEvents(b.Action);
			InputMap.ActionAddEvent(b.Action, input);
		}
	}

	// ------------------------------------------------------------------ nome na tela

	/// <summary>O nome curto, em caixa alta, para as caixinhas do HUD ("CLIQUE",
	/// "DIREITO", "R", "ESPAÇO").</summary>
	public static string Short(string action) => Current(action) switch
	{
		InputEventMouseButton { ButtonIndex: MouseButton.Left } => Loc.T("CLIQUE"),
		InputEventMouseButton { ButtonIndex: MouseButton.Right } => Loc.T("DIREITO"),
		InputEventMouseButton { ButtonIndex: MouseButton.Middle } => Loc.T("MEIO"),
		InputEvent other => Name(other).ToUpperInvariant(),
		_ => "—"
	};

	/// <summary>O nome da tecla da ação para a tela ("W", "Espaço", "Clique esquerdo").</summary>
	public static string Label(string action) => Name(Current(action));

	public static string Name(InputEvent input)
	{
		switch (input)
		{
			case InputEventMouseButton mouse:
				return mouse.ButtonIndex switch
				{
					MouseButton.Left => Loc.T("Clique esquerdo"),
					MouseButton.Right => Loc.T("Clique direito"),
					MouseButton.Middle => Loc.T("Botão do meio"),
					MouseButton.WheelUp => Loc.T("Roda para cima"),
					MouseButton.WheelDown => Loc.T("Roda para baixo"),
					MouseButton.Xbutton1 => Loc.T("Botão lateral 1"),
					MouseButton.Xbutton2 => Loc.T("Botão lateral 2"),
					_ => Loc.F("Botão {0}", (long)mouse.ButtonIndex)
				};
			case InputEventKey key:
				Key physical = Physical(key);
				switch (physical)
				{
					case Key.Space: return Loc.T("Espaço");
					case Key.Ctrl: return "Ctrl";
					case Key.Shift: return "Shift";
					case Key.Alt: return "Alt";
					case Key.Tab: return "Tab";
					case Key.Enter: return "Enter";
					case Key.Backspace: return "Backspace";
					case Key.Capslock: return "Caps Lock";
				}
				// O nome no teclado de quem joga (a tecla física A é o Q no AZERTY).
				Key shown = DisplayServer.GetName() == "headless" ? Key.None : DisplayServer.KeyboardGetKeycodeFromPhysical(physical);
				string text = OS.GetKeycodeString(shown == Key.None ? physical : shown);
				return string.IsNullOrEmpty(text) ? "?" : text;
			default:
				return "—";
		}
	}
}
