using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Fora da partida: início, sala, conjuntos e inventário.
///
/// Editar conjunto só existe aqui, nunca dentro da partida
/// (docs/CONJUNTOS.md). Na partida o jogador só escolhe e paga.
///
/// A sala segue a regra de sala de jogo multijogador: cada um aperta
/// "pronto", e só o mestre — quem criou — inicia, e só com todos
/// prontos. Hoje o único humano é você, e os bots estão sempre prontos;
/// a regra já está aqui para que a Fase 2 só precise trocar de onde vêm
/// os jogadores, não como a sala funciona.
///
/// Tudo é montado em código, como o resto da interface do projeto: o
/// que importa numa tela destas é a regra, e regra escrita em código é
/// a que o próximo agente consegue ler.
/// </summary>
public partial class MainMenu : Control
{
    public const string MatchScene = "res://scenes/main.tscn";

    private const int MaxPerTeam = 8;

    private static readonly string[] BotNames =
    {
        "Corvo", "Brasa", "Trovão", "Víbora", "Lâmina", "Sombra", "Ferrugem", "Granito",
        "Faísca", "Neblina", "Breu", "Cardo", "Estilha", "Ônix", "Ventania", "Garoa"
    };

    private static readonly int[] ScoreOptions = { 20, 30, 40, 50, 60 };
    private static readonly int[] MinuteOptions = { 5, 10, 15, 20 };

    private static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
    private static readonly Color Muted = new Color(0.7f, 0.72f, 0.76f);
    private static readonly Color Good = new Color(0.5f, 0.95f, 0.5f);
    private static readonly Color Bad = new Color(1f, 0.45f, 0.4f);

    private VBoxContainer _screen;

    // --- sala ---
    private int _playerTeam = Teams.Blue;
    private readonly List<(string Name, int Team)> _bots = new();
    private bool _ready;
    private int _score = 40;
    private int _minutes = 10;
    private BotLevel _level = BotLevel.Medium;

    // --- editor de conjuntos ---
    private int _editing = 1;
    private LoadoutSet _draft;
    private Action _back;

    public override void _Ready()
    {
        AnchorRight = 1f;
        AnchorBottom = 1f;

        // Qualquer contagem de menu herdada da partida acaba aqui, e o
        // cursor é sempre do menu.
        UiState.Reset();
        Input.MouseMode = Input.MouseModeEnum.Visible;

        AddChild(new ColorRect
        {
            Color = new Color(0.05f, 0.06f, 0.08f),
            AnchorRight = 1f,
            AnchorBottom = 1f
        });

        var center = new CenterContainer { AnchorRight = 1f, AnchorBottom = 1f };
        AddChild(center);

        _screen = new VBoxContainer { CustomMinimumSize = new Vector2(960, 0) };
        _screen.AddThemeConstantOverride("separation", 14);
        center.AddChild(_screen);

        // A sala lembra a última escalação: voltar da partida para a sala
        // e jogar de novo não pode exigir montar tudo outra vez.
        _playerTeam = RoomConfig.PlayerTeam;
        _score = RoomConfig.ScoreToWin;
        _minutes = Mathf.Max(1, Mathf.RoundToInt(RoomConfig.Duration / 60f));
        _level = Difficulty.Current;
        _bots.AddRange(RoomConfig.Active ? RoomConfig.Bots : RoomConfig.DefaultBots());

        ShowHome();
    }

    // ------------------------------------------------------------------
    // Início

    private void ShowHome()
    {
        Clear();

        var title = MakeLabel("ALCATEIA", 72, Colors.White);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _screen.AddChild(title);

        var subtitle = MakeLabel("mata-mata em equipe · humano contra lobo", 18, Muted);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _screen.AddChild(subtitle);

        _screen.AddChild(Spacer(24));

        var buttons = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        buttons.AddThemeConstantOverride("separation", 10);
        _screen.AddChild(buttons);

        buttons.AddChild(MakeButton("Criar sala", () => Rebuild(ShowRoom), 320));
        buttons.AddChild(MakeButton("Conjuntos", () => OpenEditor(ShowHome), 320));
        buttons.AddChild(MakeButton("Inventário", () => Rebuild(ShowInventory), 320));
        buttons.AddChild(MakeButton("Sair", () => GetTree().Quit(), 320));
    }

    // ------------------------------------------------------------------
    // Sala

    private void ShowRoom()
    {
        Clear();

        _screen.AddChild(MakeLabel($"SALA — mestre: {Profile.PlayerName}", 30, Colors.White));
        _screen.AddChild(MakeLabel("Mapa: Hangar   ·   Modo: mata-mata em equipe", 16, Muted));

        // Regras da partida. Só o mestre mexe nelas — hoje, você.
        var rules = new HBoxContainer();
        rules.AddThemeConstantOverride("separation", 18);
        _screen.AddChild(rules);

        rules.AddChild(MakeLabel("Pontos", 16, Muted));
        rules.AddChild(Picker(ScoreOptions.Select(v => v.ToString()).ToArray(),
            Array.IndexOf(ScoreOptions, _score), i => _score = ScoreOptions[i]));

        rules.AddChild(MakeLabel("Tempo", 16, Muted));
        rules.AddChild(Picker(MinuteOptions.Select(v => $"{v} min").ToArray(),
            Array.IndexOf(MinuteOptions, _minutes), i => _minutes = MinuteOptions[i]));

        rules.AddChild(MakeLabel("Bots", 16, Muted));
        BotLevel[] levels = { BotLevel.Easy, BotLevel.Medium, BotLevel.Hard };
        rules.AddChild(Picker(levels.Select(Difficulty.Label).ToArray(),
            Array.IndexOf(levels, _level), i => _level = levels[i]));

        var teams = new HBoxContainer();
        teams.AddThemeConstantOverride("separation", 16);
        _screen.AddChild(teams);

        teams.AddChild(TeamColumn(Teams.Blue));
        teams.AddChild(TeamColumn(Teams.Red));

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 10);
        _screen.AddChild(actions);

        actions.AddChild(MakeButton("Conjuntos", () => OpenEditor(ShowRoom), 160));

        var ready = MakeButton(_ready ? "✓ PRONTO" : "Pronto", () =>
        {
            _ready = !_ready;
            Rebuild(ShowRoom);
        }, 160);
        ready.AddThemeColorOverride("font_color", _ready ? Good : Colors.White);
        actions.AddChild(ready);

        string blocker = StartBlocker();
        var start = MakeButton("Iniciar partida", StartMatch, 200);
        start.Disabled = blocker != null;
        actions.AddChild(start);

        actions.AddChild(MakeButton("Voltar", () => Rebuild(ShowHome), 120));

        _screen.AddChild(MakeLabel(blocker ?? "Todos prontos. O mestre pode iniciar.", 15,
            blocker == null ? Good : Gold));
    }

    private Control TeamColumn(int team)
    {
        Color color = Teams.ColorOf(team);
        int count = TeamCount(team);

        var panel = MakePanel(new Color(color.R * 0.18f, color.G * 0.18f, color.B * 0.18f, 0.95f));
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 6);
        panel.AddChild(stack);

        stack.AddChild(MakeLabel($"{Teams.NameOf(team)}   {count}/{MaxPerTeam}", 22, color));

        if (_playerTeam == team)
        {
            var row = new HBoxContainer();
            row.AddChild(Grow(MakeLabel($"★ {Profile.PlayerName}  (mestre)", 18, Gold)));
            row.AddChild(MakeLabel(_ready ? "PRONTO" : "aguardando", 15, _ready ? Good : Bad));
            stack.AddChild(row);
        }

        for (int i = 0; i < _bots.Count; i++)
        {
            if (_bots[i].Team != team) continue;

            int slot = i;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(Grow(MakeLabel(_bots[i].Name, 17, Colors.White)));
            row.AddChild(MakeLabel("BOT", 12, Muted));
            row.AddChild(MakeLabel("PRONTO", 15, Good));
            row.AddChild(MakeButton("✕", () =>
            {
                _bots.RemoveAt(slot);
                Rebuild(ShowRoom);
            }, 36));
            stack.AddChild(row);
        }

        stack.AddChild(Spacer(4));

        if (_playerTeam != team && count < MaxPerTeam)
            stack.AddChild(MakeButton("Entrar neste time", () =>
            {
                _playerTeam = team;
                Rebuild(ShowRoom);
            }, 0));

        if (count < MaxPerTeam)
            stack.AddChild(MakeButton("+ Adicionar bot", () =>
            {
                _bots.Add((FreeBotName(), team));
                Rebuild(ShowRoom);
            }, 0));

        return panel;
    }

    private int TeamCount(int team) =>
        _bots.Count(b => b.Team == team) + (_playerTeam == team ? 1 : 0);

    private string FreeBotName()
    {
        foreach (string name in BotNames)
            if (!_bots.Any(b => b.Name == name)) return name;

        return $"Bot {_bots.Count + 1}";
    }

    /// <summary>O que impede de iniciar, ou nulo se nada impede.</summary>
    private string StartBlocker()
    {
        if (TeamCount(Teams.Blue) == 0 || TeamCount(Teams.Red) == 0)
            return "Cada time precisa de pelo menos um jogador.";

        if (!_ready)
            return "Aguardando: você apertar Pronto.";

        return null;
    }

    private void StartMatch()
    {
        if (StartBlocker() != null) return;

        RoomConfig.Active = true;
        RoomConfig.PlayerTeam = _playerTeam;
        RoomConfig.ScoreToWin = _score;
        RoomConfig.Duration = _minutes * 60f;
        RoomConfig.Bots.Clear();
        RoomConfig.Bots.AddRange(_bots);

        Difficulty.Set(_level);

        UiState.Reset();
        GetTree().ChangeSceneToFile(MatchScene);
    }

    // ------------------------------------------------------------------
    // Conjuntos

    private void OpenEditor(Action back)
    {
        _back = back;
        _editing = Mathf.Clamp(_editing, 0, Profile.SetCount - 1);
        _draft = Profile.Sets[_editing].Clone();
        Rebuild(ShowEditor);
    }

    private void ShowEditor()
    {
        Clear();

        _screen.AddChild(MakeLabel("CONJUNTOS", 30, Colors.White));
        _screen.AddChild(MakeLabel(
            "Monta-se aqui; na partida só se escolhe e paga. O custo não tem teto — conjunto caro é meta, não erro.",
            14, Muted));

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 16);
        _screen.AddChild(body);

        // Lista dos quatro.
        var list = new VBoxContainer { CustomMinimumSize = new Vector2(260, 0) };
        list.AddThemeConstantOverride("separation", 8);
        body.AddChild(list);

        for (int i = 0; i < Profile.SetCount; i++)
        {
            int index = i;
            LoadoutSet set = index == _editing ? _draft : Profile.Sets[index];
            string price = set.IsDefault ? "grátis" : $"{set.TotalCost()} WP";

            var button = MakeButton($"[{index + 1}] {set.Name}   {price}", () =>
            {
                _editing = index;
                _draft = Profile.Sets[index].Clone();
                Rebuild(ShowEditor);
            }, 260);

            if (index == _editing) button.AddThemeColorOverride("font_color", Gold);
            list.AddChild(button);
        }

        // Edição do selecionado.
        var panel = MakePanel(new Color(0.09f, 0.1f, 0.13f, 0.97f));
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(panel);

        var form = new VBoxContainer();
        form.AddThemeConstantOverride("separation", 10);
        panel.AddChild(form);

        bool locked = _draft.IsDefault;

        var name = new LineEdit
        {
            Text = _draft.Name,
            MaxLength = LoadoutSet.MaxNameLength,
            Editable = !locked,
            CustomMinimumSize = new Vector2(320, 0)
        };
        name.TextChanged += text => _draft.Name = text;
        form.AddChild(Field("Nome", name));

        if (locked)
            form.AddChild(MakeLabel("O padrão é fixo: só itens gratuitos, e escolhê-lo rende +50 WP.", 14, Gold));

        // Primária e secundária: só o que está no inventário — e, no
        // padrão, só o que custa zero.
        form.AddChild(Field("Primária", ItemPicker(Arsenal.Primaries, _draft.Primary, locked,
            id => _draft.Primary = id)));

        form.AddChild(Field("Secundária", ItemPicker(Arsenal.Secondaries, _draft.Secondary, locked,
            id => _draft.Secondary = id)));

        form.AddChild(Field("Utilitário 1", UtilityPicker(0, locked, false)));
        form.AddChild(Field("Utilitário 2", UtilityPicker(1, locked, true)));

        form.AddChild(MakeLabel($"Total: {_draft.TotalCost()} WP", 22, Gold));

        bool valid = _draft.Validate(out string error);
        form.AddChild(MakeLabel(valid ? "✓ válido" : error, 15, valid ? Good : Bad));

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        form.AddChild(buttons);

        var save = MakeButton("Salvar", () =>
        {
            if (!_draft.Validate(out _)) return;

            Profile.Sets[_editing] = _draft.Clone();
            Profile.Save();
            Rebuild(ShowEditor);
        }, 140);
        save.Disabled = !valid;
        buttons.AddChild(save);

        buttons.AddChild(MakeButton("Voltar", () => Rebuild(_back ?? ShowHome), 140));
    }

    private Control ItemPicker(string[] ids, string current, bool freeOnly, Action<string> set)
    {
        var allowed = ids.Where(id => Profile.Owns(id) && (!freeOnly || Arsenal.CostOf(id) == 0)).ToList();
        var labels = allowed.Select(id => $"{Arsenal.NameOf(id)}   {Arsenal.CostOf(id)} WP").ToArray();

        return Picker(labels, Math.Max(0, allowed.IndexOf(current)), i =>
        {
            set(allowed[i]);
            Rebuild(ShowEditor);
        });
    }

    /// <summary>Utilitário de um espaço. O segundo só aceita luz ou
    /// fumaça — hoje nenhuma das duas existe, então ele fica vazio.</summary>
    private Control UtilityPicker(int slot, bool locked, bool secondSlot)
    {
        var options = new List<string> { "" };
        options.AddRange(Arsenal.Utilities
            .Where(u => Profile.Owns(u.Id) && (!secondSlot || u.AllowedInSecondSlot))
            .Select(u => u.Id));

        string current = slot < _draft.Utilities.Count ? _draft.Utilities[slot] : "";
        var labels = options.Select(id => id == "" ? "— nenhum —" : $"{Arsenal.NameOf(id)}   {Arsenal.CostOf(id)} WP").ToArray();

        var picker = Picker(labels, Math.Max(0, options.IndexOf(current)), i =>
        {
            var chosen = new List<string>(_draft.Utilities);
            while (chosen.Count <= slot) chosen.Add("");
            chosen[slot] = options[i];

            _draft.Utilities = chosen.Where(id => id != "").ToList();
            Rebuild(ShowEditor);
        });

        picker.Disabled = locked || options.Count == 1;
        return picker;
    }

    // ------------------------------------------------------------------
    // Inventário

    private void ShowInventory()
    {
        Clear();

        _screen.AddChild(MakeLabel("INVENTÁRIO", 30, Colors.White));
        _screen.AddChild(MakeLabel(
            "Primeira leva do arsenal. Estrelas de 0 a 5, alto é melhor — inclusive em recuo.", 14, Muted));

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(960, 560) };
        _screen.AddChild(scroll);

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        scroll.AddChild(grid);

        foreach (string id in Arsenal.Primaries.Concat(Arsenal.Secondaries))
        {
            WeaponDefinition weapon = Arsenal.Get(id);
            if (weapon != null && Profile.Owns(id)) grid.AddChild(WeaponCard(weapon));
        }

        foreach (UtilityInfo utility in Arsenal.Utilities)
            if (Profile.Owns(utility.Id)) grid.AddChild(UtilityCard(utility));

        _screen.AddChild(MakeButton("Voltar", () => Rebuild(ShowHome), 140));
    }

    private Control WeaponCard(WeaponDefinition weapon)
    {
        var panel = MakePanel(new Color(0.09f, 0.1f, 0.13f, 0.97f));
        panel.CustomMinimumSize = new Vector2(306, 0);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 5);
        panel.AddChild(stack);

        var head = new HBoxContainer();
        head.AddChild(Grow(MakeLabel(weapon.DisplayName, 22, Colors.White)));
        head.AddChild(MakeLabel(weapon.WpCost == 0 ? "padrão" : $"{weapon.WpCost} WP", 16, Gold));
        stack.AddChild(head);

        stack.AddChild(MakeLabel(ClassName(weapon.Class), 13, Muted));
        stack.AddChild(Wrap(MakeLabel(weapon.Description, 13, new Color(0.82f, 0.84f, 0.88f)), 280));

        string damage = weapon.Pellets > 1
            ? $"{weapon.Pellets} × {weapon.Damage * Arsenal.DamageScale:0}"
            : $"{weapon.Damage * Arsenal.DamageScale:0}";

        stack.AddChild(MakeLabel(
            $"Dano {damage}  ·  cabeça ×{weapon.HeadshotMultiplier}\n" +
            $"{weapon.Rpm:0} RPM  ·  pente {weapon.Magazine}\n" +
            $"Recarga {weapon.ReloadTactical:0.00}s" + (weapon.ShellByShell ? " por cartucho" : $" / {weapon.ReloadEmpty:0.00}s vazia") + "\n" +
            $"Dano cheio até {weapon.FalloffStart:0} m  ·  velocidade ×{weapon.SpeedMultiplier:0.00}",
            13, Colors.White));

        stack.AddChild(Stars("Dano", weapon.StarDamage));
        stack.AddChild(Stars("Precisão", weapon.StarAccuracy));
        stack.AddChild(Stars("Cadência", weapon.StarRate));
        stack.AddChild(Stars("Mobilidade", weapon.StarMobility));
        stack.AddChild(Stars("Recuo", weapon.StarRecoil));
        stack.AddChild(Stars("Alcance", weapon.StarRange));

        return panel;
    }

    private Control UtilityCard(UtilityInfo utility)
    {
        var panel = MakePanel(new Color(0.09f, 0.1f, 0.13f, 0.97f));
        panel.CustomMinimumSize = new Vector2(306, 0);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 5);
        panel.AddChild(stack);

        var head = new HBoxContainer();
        head.AddChild(Grow(MakeLabel(utility.DisplayName, 20, Colors.White)));
        head.AddChild(MakeLabel($"{utility.WpCost} WP", 16, Gold));
        stack.AddChild(head);

        stack.AddChild(MakeLabel("Utilitário", 13, Muted));
        stack.AddChild(Wrap(MakeLabel(utility.Description, 13, new Color(0.82f, 0.84f, 0.88f)), 280));

        return panel;
    }

    private static string ClassName(WeaponClass kind) => kind switch
    {
        WeaponClass.Rifle => "Fuzil",
        WeaponClass.Smg => "Submetralhadora",
        WeaponClass.Shotgun => "Escopeta",
        WeaponClass.Sniper => "Sniper",
        WeaponClass.Pistol => "Pistola",
        _ => "Utilitário"
    };

    /// <summary>Barra de 0 a 5. Barra e não estrelinhas: meia estrela
    /// existe no documento (2,5) e uma barra mostra isso sem fonte
    /// especial.</summary>
    private static Control Stars(string label, float value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var name = MakeLabel(label, 12, Muted);
        name.CustomMinimumSize = new Vector2(84, 0);
        row.AddChild(name);

        const float width = 150f;
        var track = new Control { CustomMinimumSize = new Vector2(width, 8) };
        track.AddChild(new ColorRect { Color = new Color(1, 1, 1, 0.1f), Size = new Vector2(width, 8) });
        track.AddChild(new ColorRect { Color = Gold, Size = new Vector2(width * Mathf.Clamp(value / 5f, 0f, 1f), 8) });
        track.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(track);

        row.AddChild(MakeLabel($"{value:0.0}", 12, Colors.White));
        return row;
    }

    // ------------------------------------------------------------------
    // Utilidades de interface

    private void Clear()
    {
        foreach (Node child in _screen.GetChildren())
        {
            _screen.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>Remonta a tela depois do sinal atual terminar. Remontar
    /// dentro do clique apagaria o próprio botão que está emitindo.</summary>
    private void Rebuild(Action build) => Callable.From(build).CallDeferred();

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Label Wrap(Label label, float width)
    {
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(width, 0);
        return label;
    }

    private static Control Grow(Control control)
    {
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return control;
    }

    private static Control Spacer(float height) => new Control { CustomMinimumSize = new Vector2(0, height) };

    private static Button MakeButton(string text, Action action, float width)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, 40),
            FocusMode = FocusModeEnum.None
        };
        button.AddThemeFontSizeOverride("font_size", 17);
        button.Pressed += action;
        return button;
    }

    private static OptionButton Picker(string[] labels, int selected, Action<int> chosen)
    {
        var picker = new OptionButton { CustomMinimumSize = new Vector2(220, 36), FocusMode = FocusModeEnum.None };

        foreach (string label in labels)
            picker.AddItem(label);

        if (labels.Length > 0)
            picker.Select(Mathf.Clamp(selected, 0, labels.Length - 1));

        picker.ItemSelected += index => chosen((int)index);
        return picker;
    }

    private static Control Field(string label, Control input)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var name = MakeLabel(label, 16, Muted);
        name.CustomMinimumSize = new Vector2(120, 0);
        row.AddChild(name);
        row.AddChild(input);

        return row;
    }

    private static PanelContainer MakePanel(Color background)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            ContentMarginLeft = 16, ContentMarginRight = 16,
            ContentMarginTop = 12, ContentMarginBottom = 14
        };
        style.SetCornerRadiusAll(8);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }
}
