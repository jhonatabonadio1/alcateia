using Godot;
using System;
using System.Text.Json.Nodes;

/// <summary>
/// O jogo rápido no jogo (Jhon, 27/11: "jogo rápido que escolhe os players
/// baseado nas suas habilidades, níveis, experiências [...] extremamente bem
/// equilibrado. modo aleatório, mapa aleatório").
///
/// Quem decide é o servidor (<c>LobbyService.Queue.cs</c>): o jogo só pede
/// para entrar na fila (<c>queue.join</c>) ou sair (<c>queue.leave</c>) e
/// mostra o que vem (<c>queue</c>). O servidor junta gente de nota parecida,
/// monta dois times da mesma força, sorteia o modo e o mapa e manda a sala e
/// o ingresso: daí em diante é a partida de sempre.
///
/// Enquanto procura, uma faixa fica pendurada embaixo do header — o
/// carregador girando, "PROCURANDO PARTIDA", quantos estão na fila, o tempo e
/// o X de cancelar — e o jogador segue livre para mexer na loja ou no
/// inventário (a referência é a busca do Overwatch). Achou: a faixa acende,
/// "PARTIDA ENCONTRADA", com o formato, o mapa e o modo, e a partida carrega.
///
/// Num grupo, quem procura é o líder; a faixa aparece para todos do grupo e
/// qualquer um pode cancelar (a busca para para todos).
/// </summary>
public partial class MatchSearch : Control
{
	public bool Searching { get; private set; }

	private double _elapsed;
	private bool _found, _asked, _acked, _sent;
	private PanelContainer _plate;
	private Label _text, _info, _clock;
	private Loader _loader;

	private static LobbyLink Lobby => LobbyLink.Instance;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		Visible = false;

		_plate = new PanelContainer();
		_plate.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
		_plate.GrowHorizontal = GrowDirection.Both;
		_plate.OffsetTop = UiTokens.HeaderHeight + UiTokens.S2 + 4;
		Style(found: false);
		AddChild(_plate);

		HBoxContainer row = UiKit.Row(UiTokens.S3);
		_plate.AddChild(row);
		_loader = Loader.Make(26);
		row.AddChild(_loader);
		VBoxContainer texts = UiKit.Column(0);
		_text = UiKit.Text("", "Subtitle");
		_text.AddThemeFontOverride("font", UiFonts.Display);
		_text.AddThemeFontSizeOverride("font_size", 18);
		texts.AddChild(_text);
		_info = UiKit.Text("", "Caption", UiTokens.TextSecondary);
		_info.AddThemeFontSizeOverride("font_size", 12);
		texts.AddChild(_info);
		row.AddChild(texts);
		_clock = UiKit.Text("0:00", "Numeric", UiTokens.TextSecondary);
		_clock.CustomMinimumSize = new Vector2(40, 0);
		row.AddChild(_clock);
		Button cancel = UiKit.IconButton(IconKind.Close, "Cancelar a busca", Cancel, 16);
		cancel.CustomMinimumSize = new Vector2(30, 30);
		row.AddChild(cancel);

		Lobby.QueueChanged += OnQueue;
		Lobby.Failed += OnFailed;
		// Voltou ao lobby com a busca em curso (o líder do grupo procurando): a faixa reaparece.
		if (Lobby.Queue?["state"]?.GetValue<string>() == "searching") Callable.From(() => OnQueue(Lobby.Queue)).CallDeferred();
	}

	public override void _ExitTree()
	{
		if (LobbyLink.Instance is LobbyLink link && IsInstanceValid(link))
		{
			link.QueueChanged -= OnQueue;
			link.Failed -= OnFailed;
		}
	}

	private void Style(bool found)
	{
		var box = new StyleBoxFlat
		{
			BgColor = found ? new Color(UiTokens.Accent, 0.94f) : new Color(0.03f, 0.05f, 0.075f, 0.94f),
			BorderColor = UiTokens.Accent
		};
		box.BorderWidthBottom = 2;
		box.ContentMarginLeft = UiTokens.S3;
		box.ContentMarginRight = UiTokens.S2;
		box.ContentMarginTop = box.ContentMarginBottom = UiTokens.S2;
		box.ShadowColor = new Color(0, 0, 0, 0.4f);
		box.ShadowSize = 14;
		_plate.AddThemeStyleboxOverride("panel", box);
	}

	// ------------------------------------------------------------------ começar e parar

	/// <summary>O botão do jogo rápido: entra na fila.</summary>
	public void Begin()
	{
		if (Searching) return;
		if (LobbyLink.InRoom)
		{
			LobbyShell.Instance?.GoOnlineRoom();
			return;
		}
		// Num grupo, quem procura é o líder: o grupo vai junto.
		if (Social.InGroup && !Social.LeadsGroup)
		{
			LobbyShell.Instance?.Toast("Você está num grupo: quem procura partida é o líder.", UiTokens.Danger);
			return;
		}

		Show(asked: true);
		Sfx.Ui("ui_jogar");
		_ = LobbyShell.Instance?.EnsureLink();
	}

	/// <summary>O X: sai da fila (num grupo, para a busca de todos).</summary>
	public void Cancel()
	{
		if (!Searching) return;
		if (_sent || _acked) Lobby.LeaveQueue();
		Stop();
		Sfx.Ui("ui_fechar");
	}

	private void Show(bool asked)
	{
		Searching = true;
		_found = false;
		_asked = asked;
		_acked = !asked;
		_sent = false;
		_elapsed = 0;
		Style(found: false);
		_text.Text = Loc.Upper("Procurando partida");
		_text.AddThemeColorOverride("font_color", UiTokens.TextPrimary);
		_info.Text = Loc.T("Times equilibrados por habilidade · modo e mapa sorteados");
		_info.AddThemeColorOverride("font_color", UiTokens.TextSecondary);
		_loader.Tint = UiTokens.Accent;
		_clock.Visible = true;
		Visible = true;
		_plate.PivotOffset = _plate.Size / 2f;
		Motion.FadeIn(this, UiTokens.Normal);
	}

	private void Stop()
	{
		Searching = false;
		_asked = _acked = _sent = false;
		Hide(0f);
	}

	private void Hide(float after)
	{
		Tween tween = CreateTween();
		tween.TweenInterval(after);
		tween.TweenProperty(this, "modulate:a", 0f, UiTokens.Normal);
		tween.TweenCallback(Callable.From(() => Visible = false));
	}

	// ------------------------------------------------------------------ o que vem do servidor

	public override void _Process(double delta)
	{
		if (!Searching || _found) return;

		_elapsed += delta;
		_clock.Text = $"{(int)_elapsed / 60}:{(int)_elapsed % 60:00}";

		if (!Backend.LoggedIn)
		{
			Stop();
			return;
		}
		// O pedido sai assim que a ligação com o saguão estiver de pé.
		if (_asked && !_sent && Lobby.Connected)
		{
			_sent = true;
			Lobby.JoinQueue();
		}
	}

	private void OnQueue(JsonObject q)
	{
		if (!IsInsideTree() || q == null) return;
		switch (q["state"]?.GetValue<string>())
		{
			case "searching":
				// O líder do grupo começou a procurar: a faixa aparece aqui também.
				if (!Searching) Show(asked: false);
				_acked = true;
				_elapsed = Math.Max(_elapsed, q["waited"]?.GetValue<int>() ?? 0);
				int players = q["players"]?.GetValue<int>() ?? 0, teamMin = Math.Max(q["teamMin"]?.GetValue<int>() ?? 4, 1);
				_info.Text = q["note"]?.GetValue<string>() ?? Loc.F("{0} na fila · aceitando a partir de {1} contra {1}", players, teamMin);
				break;

			case "idle":
				bool was = Searching;
				if (was) Stop();
				if (was && q["note"]?.GetValue<string>() is string note) LobbyShell.Instance?.Toast(note);
				break;

			case "found":
				Found(q);
				break;
		}
	}

	/// <summary>O servidor recusou o pedido (num grupo sem ser o líder, numa
	/// sala, rápido demais) ou a ligação caiu: o lobby mostra o recado; a faixa sai.</summary>
	private void OnFailed(string code, string message)
	{
		if (!Searching) return;
		if (code == "desconectado" || (!_acked && code is "so_o_lider" or "em_sala" or "devagar")) Stop();
	}

	private void Found(JsonObject q)
	{
		_found = true;
		Searching = false;
		_asked = _acked = _sent = false;
		Visible = true;
		Modulate = Colors.White;
		Style(found: true);
		_text.Text = Loc.Upper("Partida encontrada");
		_text.AddThemeColorOverride("font_color", UiTokens.AccentText);
		int size = q["teamSize"]?.GetValue<int>() ?? 0, play = q["play"]?.GetValue<int>() ?? 0;
		string map = Maps.Get(q["map"]?.GetValue<string>() ?? "").Name;
		string mode = Enum.IsDefined(typeof(PlayMode), play) ? PlayModes.Of((PlayMode)play).Name : "";
		_info.Text = Loc.F("{0} contra {0} · {1} · {2}", size, map, mode);
		_info.AddThemeColorOverride("font_color", UiTokens.AccentText);
		_loader.Tint = UiTokens.AccentText;
		_clock.Visible = false;
		Sfx.Ui(Sfx.First("ui_confirmar", "ui_recompensa_1"));
		_plate.PivotOffset = _plate.Size / 2f;
		Motion.Pop(_plate, 1.12f, 0.3f);
		Hide(2.2f);
	}
}
