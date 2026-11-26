using Godot;

/// <summary>
/// O guia das habilidades (pedido do Jhon, 18/11: "se apertar E e segurar,
/// aparece um guia das habilidades, igual Overwatch").
///
/// Enquanto o E está apertado, um painel por cima do jogo diz o que a
/// forma atual faz: de lobo, a garrada, o bote, o uivo e o especial, e
/// como ele se move; de humano, as armas e virar lobo. Cada linha tem o
/// ícone, o nome, a tecla e uma frase. Soltou o E, some. Não pausa nada e
/// não pega o mouse: é só leitura.
///
/// Os números vêm de quem manda no jogo (a raça do lobo, a duração do
/// rugido), para o guia não mentir quando o balanceamento mudar.
/// </summary>
public partial class Hud
{
	private Control _guide;
	private bool _guideForWolf;

	/// <summary>O guia está na tela. Para os testes.</summary>
	public bool GuideOpen => _guide != null && _guide.Visible;

	private void BuildGuide()
	{
		_guide = UiKit.Fill(new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false });
		_root.AddChild(_guide);
	}

	private void UpdateGuide()
	{
		if (_guide == null) return;
		bool hold = Subject == null && Input.IsActionPressed("guide") && !UiState.AnyOpen && !_health.IsDead && !Match.IsOver;
		bool wolf = _form.IsWolf;
		if (hold && (!_guide.Visible || wolf != _guideForWolf))
		{
			FillGuide(wolf);
			if (!_guide.Visible)
			{
				_guide.Visible = true;
				Motion.FadeIn(_guide, UiTokens.Fast);
			}
		}
		else if (!hold && _guide.Visible) _guide.Visible = false;
	}

	private void FillGuide(bool wolf)
	{
		_guideForWolf = wolf;
		foreach (Node child in _guide.GetChildren()) child.QueueFree();

		_guide.AddChild(UiKit.Fill(new ColorRect { Color = new Color(0.02f, 0.03f, 0.05f, 0.62f), MouseFilter = Control.MouseFilterEnum.Ignore }));

		var center = UiKit.Fill(new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore });
		_guide.AddChild(center);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(1040, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.04f, 0.05f, 0.08f, 0.9f), UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, UiTokens.S5, UiTokens.S4));
		center.AddChild(panel);

		VBoxContainer page = UiKit.Column(UiTokens.S4);
		panel.AddChild(page);

		// O cabeçalho: quem você é agora.
		HBoxContainer head = UiKit.Row(UiTokens.S3);
		Color tint = wolf ? UiTokens.WolfOrange : UiTokens.Accent;
		head.AddChild(Icon.Make(wolf ? IconKind.Wolf : IconKind.Human, 40, tint));
		VBoxContainer names = UiKit.Column(0);
		string who = wolf ? Loc.T("LOBO") : Loc.T("HUMANO");
		names.AddChild(HudStyle.Number(UiKit.Text(who, "Title"), 30));
		names.AddChild(UiKit.Caption("Habilidades", UiTokens.TextSecondary));
		head.AddChild(names);
		head.AddChild(UiKit.Spacer());
		head.AddChild(UiKit.Caption("Solte E para fechar", UiTokens.TextSecondary));
		page.AddChild(head);

		HBoxContainer columns = UiKit.Row(UiTokens.S5);
		page.AddChild(columns);
		VBoxContainer left = GuideColumn(columns, wolf ? "Ataque" : "Armas");
		VBoxContainer right = GuideColumn(columns, wolf ? "Poder e movimento" : "Corpo");

		if (wolf)
		{
			WolfBreed breed = _form.Breed;
			GuideLine(left, IconKind.Claw, "Garrada", "Clique esquerdo",
				breed != null ? Loc.F("Um golpe por clique, uma mão de cada vez. {0:0} de dano.", breed.AttackDamage)
					: Loc.T("Um golpe por clique, uma mão de cada vez."), tint);
			GuideLine(left, IconKind.FromAbove, "Bote", "Clique direito, no ar",
				Loc.T("Pulou ou caiu de cima? Mergulha em quem está embaixo de você."), tint);
			GuideLine(left, IconKind.Pounce, "Especial", "Q, depois clique",
				Loc.T("Com a carga cheia, Q arma os raios e trava no inimigo mais perto da mira. O clique salta nele, mesmo que ele se mexa. Do Q até cair, você leva metade do dano. Enche com abates e, devagar, com o tempo."), tint);

			GuideLine(right, IconKind.Shield, "Uivo", "Segure R",
				Loc.F("Segure até o fim ({0:0.0} s) e o escudo volta inteiro. Soltar antes cancela. O rugido entrega onde você está.", WolfMoves.Duration(WolfMove.Howl)), UiTokens.Accent);
			GuideLine(right, IconKind.Wolf, "Tempo de lobo", Loc.F("{0:0} s", _form.WolfTimeLimit),
				Loc.F("Acabou o tempo, você volta a humano sozinho (a barra embaixo avisa nos últimos {0:0} s). Para virar lobo de novo, espere {1:0} s.", _form.WolfWarnAt, _form.WolfCooldown), tint);
			GuideLine(right, IconKind.Wolf, "Corrida de quatro", "W W, segurando",
				Loc.T("Dois toques no W e segure: 30% mais rápido."), tint);
			GuideLine(right, IconKind.Go, "Corrida na parede", "W na parede · Espaço",
				Loc.T("Pule numa parede e segure W para correr por ela. Espaço salta da parede."), tint);
		}
		else
		{
			GuideLine(left, IconKind.Gun, "Arma principal", "1",
				Loc.T("Clique atira, clique direito mira, R recarrega."), tint);
			GuideLine(left, IconKind.Gun, "Arma secundária", "2",
				Loc.T("A reserva para quando o pente da principal acaba."), tint);
			GuideLine(left, IconKind.Knife, "Faca", "4",
				Loc.T("De perto e em silêncio."), tint);
			GuideLine(left, IconKind.Grenade, "Granada", "5",
				Loc.T("Equipe e clique para arremessar."), tint);

			GuideLine(right, IconKind.Wolf, "Virar lobo", "3",
				Loc.F("De lobo você é mais rápido, mata em duas garradas e não atira — mas tem pouco escudo e dura {0:0} s; depois espera {1:0} s para virar de novo.", _form.WolfTimeLimit, _form.WolfCooldown), UiTokens.WolfOrange);
			GuideLine(right, IconKind.Pounce, "Especial", "Q, de lobo",
				Loc.T("A carga enche com abates também de humano. Cheia, vire lobo para usar."), UiTokens.WolfOrange);
			GuideLine(right, IconKind.Human, "Andar em silêncio", "Shift",
				Loc.T("Devagar e sem barulho de passo. Ctrl agacha."), tint);
			GuideLine(right, IconKind.Go, "Soltar a arma", "G",
				Loc.T("A arma da mão cai à frente; dá para pegar outra do chão."), tint);
		}
	}

	private static VBoxContainer GuideColumn(HBoxContainer columns, string title)
	{
		VBoxContainer column = UiKit.Column(UiTokens.S3);
		column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		column.SizeFlagsStretchRatio = 1f;
		column.AddChild(UiKit.Caption(title, UiTokens.TextSecondary));
		columns.AddChild(column);
		return column;
	}

	/// <summary>Uma habilidade: o ícone no quadrado, o nome com a tecla ao lado, e a frase.</summary>
	private static void GuideLine(VBoxContainer column, IconKind kind, string name, string key, string text, Color tint)
	{
		HBoxContainer row = UiKit.Row(UiTokens.S3);

		var box = new PanelContainer { CustomMinimumSize = new Vector2(52, 52), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
		box.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.03f, 0.04f, 0.06f, 0.9f), new Color(tint, 0.7f), 1, UiTokens.RadiusSmall, 9, 9));
		box.AddChild(Icon.Make(kind, 32, tint));
		row.AddChild(box);

		VBoxContainer words = UiKit.Column(2);
		words.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		HBoxContainer top = UiKit.Row(UiTokens.S2);
		top.AddChild(UiKit.Text(name, "Subtitle", UiTokens.TextPrimary));
		var chip = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		chip.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Surface3, UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, UiTokens.S2, 1));
		chip.AddChild(UiKit.Text(key, "Caption", UiTokens.TextPrimary));
		top.AddChild(chip);
		words.AddChild(top);
		Label body = UiKit.Paragraph(text);
		body.CustomMinimumSize = new Vector2(380, 0);
		words.AddChild(body);
		row.AddChild(words);

		column.AddChild(row);
	}
}
