using Godot;
using System;

/// <summary>A tabela de controles do menu da partida (Esc → Controles). A tecla
/// de cada linha é a que vale agora (<see cref="KeyBindings"/>: a trocada nas
/// Opções → Controles, ou a de fábrica do <see cref="InputSetup"/>). O que cada
/// uma faz segue o docs/DESIGN.md ("Controles").</summary>
public static class ControlsTable
{
	private static string K(string action) => KeyBindings.Label(action);

	private static readonly (Func<string> Key, string Action)[] Rows =
	{
		(() => $"{K("move_forward")} {K("move_left")} {K("move_back")} {K("move_right")}", "Andar"),
		(() => K("jump"), "Pular · saltar da parede"),
		(() => K("sprint"), "Correr, de arma abaixada (humano)"),
		(() => K("crouch"), "Agachar"),
		(() => K("melee"), "Coronhada (com a faca, golpeia; com a granada, lança)"),
		(() => K("radio"), "Rádio do time (1 a 0 escolhe a fala)"),
		(() => K("attack"), "Atacar"),
		(() => K("pounce"), "Lobo: especial — salta na mira (também no ar). Enche com abates e, devagar, com o tempo"),
		(() => K("special"), "Mirar (humano) · bote no ar (lobo)"),
		(() => K("slot_1"), "Arma principal"),
		(() => K("slot_2"), "Arma secundária"),
		(() => K("slot_3"), "Virar lobo"),
		(() => K("slot_4"), "Faca"),
		(() => K("slot_5"), "Equipar granada"),
		(() => K("howl"), "Recarregar (humano) · uivar (lobo, segure até o fim)"),
		(() => K("drop"), "Soltar a arma da mão (cai à frente)"),
		(() => Loc.F("{0} {0} (segurando)", K("move_forward")), "Lobo: corrida de quatro, 30% mais rápida"),
		(() => Loc.F("{0} segurado", K("move_forward")), "Sustenta a corrida em parede"),
		(() => K("scoreboard"), "Placar (segurar)"),
		(() => "Esc", "Menu da partida — não pausa"),
	};

	/// <summary>As teclas de agora e o que fazem, na ordem da tabela.</summary>
	public static (string Key, string Action)[] Entries => Array.ConvertAll(Rows, r => (r.Key(), r.Action));

	public static Control Build()
	{
		var grid = new GridContainer { Columns = 2 };
		grid.AddThemeConstantOverride("h_separation", UiTokens.S4);
		grid.AddThemeConstantOverride("v_separation", UiTokens.S1 + 2);

		foreach (var (key, action) in Entries)
		{
			var keyBox = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
			keyBox.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Surface3, UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, UiTokens.S2, 2));
			Label keyLabel = UiKit.Text(key, "Caption", UiTokens.TextPrimary);
			keyBox.AddChild(keyLabel);
			keyBox.CustomMinimumSize = new Vector2(150, 0);
			grid.AddChild(keyBox);
			grid.AddChild(UiKit.Text(action, "Muted"));
		}

		return grid;
	}
}
