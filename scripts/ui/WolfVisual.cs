using Godot;
using System.Collections.Generic;

/// <summary>
/// O corpo do lobo: o Mutant do Mixamo, animado com o Creature Pack.
/// Cada time tem o seu — o azul usa <c>mutant.fbx</c>, o laranja
/// <c>mutant_red.fbx</c> (decisão do Jhon).
///
/// Fica no corpo e não na câmera porque a forma lobo é em terceira
/// pessoa: o dono vê o próprio lobo, e os outros reconhecem a silhueta
/// de longe — curvado, largo, braços compridos. Numa luta rápida
/// ninguém lê textura; o contorno é o que separa lobo de humano a
/// trinta metros.
///
/// Mesma regra do <see cref="CharacterAnimator"/>: **lê** estado e
/// nunca decide. A velocidade escolhe entre parado, andando e correndo;
/// os sinais da arma disparam a garrada, a patada e o bote; o uivo (R)
/// toca o rugido; virar lobo toca o "flexionar" do bicho.
///
/// **O pulo não tem animação** — decisão do Jhon: o lobo só pula, e o
/// corpo segue na pose em que estava. O clipe de pulo do pack agacha,
/// salta e aterrissa sozinho, e no jogo quem salta é a física; os dois
/// juntos dão um bicho que pula duas vezes.
///
/// | Clipe | Arquivo do pack | Quando |
/// |---|---|---|
/// | idle | mutant breathing idle | parado |
/// | walk / run | mutant walking / run | andando, correndo |
/// | claw | mutant swiping | garrada (clique), alternando com o punch |
/// | heavy | mutant punch | garrada alternada; patada (direito no chão) |
/// | leap | mutant jump attack, **só a queda** | bote (direito no ar) |
/// | howl | mutant roaring | uivando (R) |
/// | transform | mutant flexing muscles | acabou de virar lobo |
/// | death | mutant dying | morreu de lobo |
///
/// Golpes e rugido só mexem da cintura para cima: o lobo continua
/// correndo enquanto bate.
/// </summary>
public partial class WolfVisual : Node3D
{
    [Export(PropertyHint.File, "*.fbx,*.glb,*.tscn")]
    public string BlueModel = "res://assets/characters/wolf/mutant.fbx";

    [Export(PropertyHint.File, "*.fbx,*.glb,*.tscn")]
    public string OrangeModel = "res://assets/characters/wolf/mutant_red.fbx";

    [Export(PropertyHint.Dir)]
    public string ClipFolder = "res://assets/characters/wolf";

    /// <summary>O Mutant tem 1,86 m; o lobo fica um pouco maior que
    /// um homem.</summary>
    [Export] public float ModelScale = 1.1f;

    /// <summary>Mixamo olha para +Z; o corpo, para −Z.</summary>
    [Export] public float ModelYaw = 180f;

    /// <summary>Velocidades dos clipes de andar e correr, em m/s. Acima
    /// da corrida o clipe acelera junto.</summary>
    [Export] public float WalkSpeed = 2.4f;
    [Export] public float RunSpeed = 7f;

    /// <summary>Quanto da cor do time entra na pele — só quando os dois
    /// times usam o mesmo modelo. Com um modelo por time, a pele já diz
    /// o lado.</summary>
    [Export(PropertyHint.Range, "0,1")] public float TeamTint = 0.3f;

    [Export] public bool Debug;

    private static readonly string[] Clips =
        { "idle", "walk", "run", "claw", "heavy", "leap", "howl", "transform", "death" };

    private FormController _form;
    private Weapon _weapon;
    private Health _health;
    private CharacterBody3D _body;
    private Player _player;
    private Bot _bot;

    private Node3D _model;
    private AnimationPlayer _anims;
    private AnimationTree _tree;
    private AnimationNodeStateMachinePlayback _playback;
    private readonly Dictionary<string, float> _lengths = new();

    private bool _sharedModel = true;

    /// <summary>A próxima garrada sai com o outro braço.</summary>
    private bool _otherArm;
    private float _speed;
    private bool _dead;
    private bool _howling;
    private double _deadTimer;

    public override void _Ready()
    {
        _body = GetParent<CharacterBody3D>();
        _form = _body.GetNode<FormController>("FormController");
        _weapon = _body.GetNode<Weapon>("Weapon");
        _health = _body.GetNodeOrNull<Health>("Health");
        _player = _body as Player;
        _bot = _body as Bot;

        if (!BuildModel()) return;

        _form.FormChanged += OnFormChanged;
        _weapon.AttackCooldown += OnAttack;
        if (_health != null)
        {
            _health.Died += _ => OnDied();
            _health.Respawned += OnRespawned;
        }

        Visible = _form.IsWolf;
    }

    // ================================================================== montagem

    private bool BuildModel()
    {
        bool orange = _health != null && _health.Team == Teams.Orange;
        string path = orange && ResourceLoader.Exists(OrangeModel) ? OrangeModel : BlueModel;
        _sharedModel = !ResourceLoader.Exists(OrangeModel) || OrangeModel == BlueModel;

        if (!ResourceLoader.Exists(path) || GD.Load<PackedScene>(path)?.Instantiate() is not Node3D model)
        {
            GD.PrintErr($"WolfVisual: modelo do lobo não encontrado em {path}.");
            return false;
        }

        _model = model;
        _model.Scale = Vector3.One * ModelScale;
        _model.RotationDegrees = new Vector3(0, ModelYaw, 0);
        AddChild(_model);

        _anims = CharacterAnimator.FindPlayer(_model);

        // Modelo exportado de outro programa (o Mutant vermelho veio do
        // Blender) chega sem AnimationPlayer e com o esqueleto dentro de
        // "Armature". Os clipes apontam para "Skeleton3D:osso", então o
        // tocador nasce ao lado do esqueleto, onde esse caminho existe.
        if (_anims == null)
        {
            Skeleton3D skeleton = FindSkeleton(_model);
            if (skeleton == null)
            {
                GD.PrintErr("WolfVisual: o modelo do lobo não tem esqueleto.");
                return false;
            }

            _anims = new AnimationPlayer { Name = "AnimationPlayer" };
            skeleton.GetParent().AddChild(_anims);
        }

        LoadClips();
        ApplyTeamTint();
        BuildTree();
        return true;
    }

    private void LoadClips()
    {
        var library = new AnimationLibrary();

        foreach (string clip in Clips)
        {
            string path = $"{ClipFolder}/{clip}.fbx";
            if (!ResourceLoader.Exists(path)) continue;

            Node temp = GD.Load<PackedScene>(path)?.Instantiate();
            if (temp == null) continue;

            Animation animation = CharacterAnimator.TakeAnimation(CharacterAnimator.FindPlayer(temp), clip, Debug);
            temp.Free();
            if (animation == null) continue;

            CharacterAnimator.StripRootMotion(animation);
            if (clip == "leap") animation = FallOnly(animation);
            animation.LoopMode = clip is "idle" or "walk" or "run" or "howl"
                ? Animation.LoopModeEnum.Linear
                : Animation.LoopModeEnum.None;

            library.AddAnimation(clip, animation);
            _lengths[clip] = (float)animation.Length;
        }

        _anims.AddAnimationLibrary("lobo", library);
    }

    private bool Has(string clip) => _lengths.ContainsKey(clip);

    /// <summary>
    /// Só a queda do "jump attack": do ponto mais alto até o bicho voltar
    /// a ficar de pé depois do impacto. O bote começa com o lobo já no ar
    /// — agachar e saltar de novo seria pular duas vezes.
    ///
    /// O ponto mais alto e o fim são achados na altura do quadril, não
    /// cravados em segundos: trocar o clipe continua funcionando. A
    /// altura do quadril é limitada à de pé: no ar quem levanta o corpo
    /// é a física, e somar a subida do clipe deixaria o lobo flutuando
    /// acima da cápsula. A agachada do impacto, abaixo disso, fica.
    /// </summary>
    private static Animation FallOnly(Animation source)
    {
        int hips = -1;
        for (int t = 0; t < source.GetTrackCount(); t++)
        {
            if (source.TrackGetType(t) == Animation.TrackType.Position3D
                && source.TrackGetPath(t).ToString().Contains("Hips"))
            {
                hips = t;
                break;
            }
        }

        if (hips < 0 || source.TrackGetKeyCount(hips) < 2) return source;

        float rest = ((Vector3)source.TrackGetKeyValue(hips, 0)).Y;
        double apex = 0, lowest = 0;
        float top = float.MinValue, bottom = float.MaxValue;

        for (int k = 0; k < source.TrackGetKeyCount(hips); k++)
        {
            float y = ((Vector3)source.TrackGetKeyValue(hips, k)).Y;
            if (y > top) { top = y; apex = source.TrackGetKeyTime(hips, k); }
        }

        for (int k = 0; k < source.TrackGetKeyCount(hips); k++)
        {
            double time = source.TrackGetKeyTime(hips, k);
            float y = ((Vector3)source.TrackGetKeyValue(hips, k)).Y;
            if (time > apex && y < bottom) { bottom = y; lowest = time; }
        }

        // Fim: o quadril de volta à altura de pé depois do impacto.
        double end = source.Length;
        for (int k = 0; k < source.TrackGetKeyCount(hips); k++)
        {
            double time = source.TrackGetKeyTime(hips, k);
            if (time > lowest && ((Vector3)source.TrackGetKeyValue(hips, k)).Y >= rest - 0.03f)
            {
                end = time;
                break;
            }
        }

        var fall = new Animation { Length = (float)(end - apex) };
        const double step = 1.0 / 30.0;

        for (int t = 0; t < source.GetTrackCount(); t++)
        {
            Animation.TrackType type = source.TrackGetType(t);
            if (type is not (Animation.TrackType.Position3D or Animation.TrackType.Rotation3D or Animation.TrackType.Scale3D))
                continue;

            int track = fall.AddTrack(type);
            fall.TrackSetPath(track, source.TrackGetPath(t));

            for (double time = apex; time <= end + 0.0001; time += step)
            {
                double at = time - apex;
                switch (type)
                {
                    case Animation.TrackType.Position3D:
                        Vector3 position = source.PositionTrackInterpolate(t, time);
                        if (t == hips) position.Y = Mathf.Min(position.Y, rest);
                        fall.PositionTrackInsertKey(track, at, position);
                        break;
                    case Animation.TrackType.Rotation3D:
                        fall.RotationTrackInsertKey(track, at, source.RotationTrackInterpolate(t, time));
                        break;
                    default:
                        fall.ScaleTrackInsertKey(track, at, source.ScaleTrackInterpolate(t, time));
                        break;
                }
            }
        }

        return fall;
    }

    private static Skeleton3D FindSkeleton(Node node)
    {
        if (node is Skeleton3D skeleton) return skeleton;
        foreach (Node child in node.GetChildren())
        {
            Skeleton3D found = FindSkeleton(child);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Máquina de estados para o corpo inteiro (locomoção, uivo, morte) e três disparos por cima, só da cintura para cima:
    /// golpe, transformação e bote (este no corpo inteiro).
    /// </summary>
    private void BuildTree()
    {
        var locomotion = new AnimationNodeBlendSpace1D { MinSpace = 0f, MaxSpace = 2f };
        locomotion.Set("sync", true);
        if (Has("idle")) locomotion.AddBlendPoint(Clip("idle"), 0f, -1, "idle");
        if (Has("walk")) locomotion.AddBlendPoint(Clip("walk"), 1f, -1, "walk");
        if (Has("run")) locomotion.AddBlendPoint(Clip("run"), 2f, -1, "run");

        var locoSpeed = new AnimationNodeBlendTree();
        locoSpeed.AddNode("Blend", locomotion, Vector2.Zero);
        locoSpeed.AddNode("Speed", new AnimationNodeTimeScale(), new Vector2(200, 0));
        locoSpeed.ConnectNode("Speed", 0, "Blend");
        locoSpeed.ConnectNode("output", 0, "Speed");

        var machine = new AnimationNodeStateMachine();
        machine.AddNode("Locomotion", locoSpeed, new Vector2(200, 100));
        if (Has("howl")) machine.AddNode("Howl", Clip("howl"), new Vector2(400, 200));
        if (Has("death")) machine.AddNode("Death", Clip("death"), new Vector2(600, 100));

        Link(machine, "Locomotion", "Howl");
        Link(machine, "Howl", "Locomotion");
        foreach (string from in new[] { "Locomotion", "Howl" })
            Link(machine, from, "Death");
        Link(machine, "Death", "Locomotion");

        var root = new AnimationNodeBlendTree();
        root.AddNode("Machine", machine, Vector2.Zero);
        string last = "Machine";

        // Cada disparo recebe o anterior como base e põe o golpe por cima.
        foreach (string shot in new[] { "claw", "heavy", "transform", "leap" })
        {
            if (!Has(shot)) continue;

            var oneShot = new AnimationNodeOneShot
            {
                FadeInTime = 0.08f,
                FadeOutTime = 0.2f,
                FilterEnabled = shot != "leap"
            };

            if (shot != "leap")
            {
                Animation clip = _anims.GetAnimation($"lobo/{shot}");
                for (int t = 0; t < clip.GetTrackCount(); t++)
                {
                    NodePath path = clip.TrackGetPath(t);
                    if (UpperBody(path.ToString())) oneShot.SetFilterPath(path, true);
                }
            }

            string name = Cap(shot);
            root.AddNode($"{name}Clip", Clip(shot), Vector2.Zero);
            root.AddNode($"{name}Speed", new AnimationNodeTimeScale(), Vector2.Zero);
            root.AddNode(name, oneShot, Vector2.Zero);
            root.ConnectNode($"{name}Speed", 0, $"{name}Clip");
            root.ConnectNode(name, 0, last);
            root.ConnectNode(name, 1, $"{name}Speed");
            last = name;
        }

        root.ConnectNode("output", 0, last);

        _tree = new AnimationTree { TreeRoot = root };
        _model.AddChild(_tree);
        _tree.AnimPlayer = _tree.GetPathTo(_anims);
        _tree.Active = true;

        _playback = (AnimationNodeStateMachinePlayback)_tree.Get("parameters/Machine/playback");
        _playback?.Start("Locomotion");
    }

    private static AnimationNodeAnimation Clip(string clip) => new() { Animation = $"lobo/{clip}" };

    private static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    private static bool UpperBody(string path)
    {
        int colon = path.LastIndexOf(':');
        string bone = colon >= 0 ? path[(colon + 1)..] : path;
        return bone.Contains("Spine") || bone.Contains("Neck") || bone.Contains("Head")
            || bone.Contains("Shoulder") || bone.Contains("Arm") || bone.Contains("Hand");
    }

    private static void Link(AnimationNodeStateMachine machine, string from, string to)
    {
        if (!machine.HasNode(from) || !machine.HasNode(to)) return;
        machine.AddTransition(from, to, new AnimationNodeStateMachineTransition { XfadeTime = 0.15f });
    }

    /// <summary>
    /// Um toque da cor do time na pele. O material é duplicado: vem da
    /// importação e é um só para todos os lobos, e pintar o original
    /// pintaria o lobo azul e o laranja da mesma cor.
    /// </summary>
    private void ApplyTeamTint()
    {
        if (_health == null || TeamTint <= 0 || !_sharedModel) return;

        Color team = Teams.ColorOf(_health.Team);
        Color tint = Colors.White.Lerp(team, TeamTint);

        foreach (Node node in _model.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;

            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.GetActiveMaterial(surface) is not BaseMaterial3D source) continue;

                var copy = (BaseMaterial3D)source.Duplicate();
                copy.AlbedoColor = source.AlbedoColor * tint;
                mesh.SetSurfaceOverrideMaterial(surface, copy);
            }
        }
    }

    // ================================================================== estado

    public override void _Process(double delta)
    {
        if (_tree == null || !Visible) return;

        // O lobo da cena fica no pé da cápsula, que encolhe ao agachar.
        float feet = _player != null ? _player.FeetOffset : -0.875f;
        Position = new Vector3(0, feet, 0);

        if (_dead)
        {
            // Depois de cair, o corpo some.
            _deadTimer += delta;
            float hold = (_lengths.TryGetValue("death", out float d) ? d : 1.5f) + 1.2f;
            if (_deadTimer > hold) _model.Visible = false;
            return;
        }

        var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
        _speed = Mathf.Lerp(_speed, flat.Length(), Mathf.Min(1f, 10f * (float)delta));

        // 0 parado, 1 andando, 2 correndo; acima da corrida, o clipe
        // acelera em vez de o pé patinar.
        float blend = _speed <= WalkSpeed
            ? _speed / Mathf.Max(WalkSpeed, 0.01f)
            : 1f + Mathf.Clamp((_speed - WalkSpeed) / Mathf.Max(RunSpeed - WalkSpeed, 0.01f), 0f, 1f);
        _tree.Set("parameters/Machine/Locomotion/Blend/blend_position", blend);
        _tree.Set("parameters/Machine/Locomotion/Speed/scale", _speed > RunSpeed ? Mathf.Min(_speed / RunSpeed, 1.8f) : 1f);

        // No ar não há clipe próprio: o corpo segue na pose de antes.
        bool howling = IsHowling();
        if (howling != _howling)
        {
            _howling = howling;
            _playback?.Travel(howling && Has("howl") ? "Howl" : "Locomotion");
        }
    }

    private bool IsHowling() =>
        _player != null ? _player.HowlProgress > 0f
        : _bot != null && _bot.Action == BotAction.Howl;

    private void Fire(string shot, float duration)
    {
        if (_tree == null || !Has(shot)) return;

        string name = Cap(shot);
        float scale = duration > 0.05f ? _lengths[shot] / duration : 1f;
        _tree.Set($"parameters/{name}Speed/scale", Mathf.Clamp(scale, 0.5f, 4f));
        _tree.Set($"parameters/{name}/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
    }

    /// <summary>Golpe do lobo. A duração do clipe acompanha a recarga do
    /// golpe, com um mínimo para a garrada não virar um tremor.</summary>
    private void OnAttack(int kind, float seconds)
    {
        if (!_form.IsWolf) return;

        switch (kind)
        {
            case Weapon.CooldownClaw:
                // O clique esquerdo alterna os dois golpes do pack — o
                // "swiping" e o "punch" —, um braço de cada vez. Golpe
                // repetido com a mesma mão parece um boneco travado; a
                // alternância lê como uma sequência de patadas.
                string swing = _otherArm && Has("heavy") ? "heavy" : "claw";
                string previous = swing == "claw" ? "heavy" : "claw";

                // Clique rápido: o golpe anterior sai para o novo
                // aparecer. Sem isto o de cima na árvore cobre o outro.
                if (_tree != null && Has(previous))
                    _tree.Set($"parameters/{Cap(previous)}/request", (int)AnimationNodeOneShot.OneShotRequest.FadeOut);

                Fire(swing, Mathf.Max(seconds, 0.45f));
                _otherArm = !_otherArm;
                break;
            case Weapon.CooldownHeavy: Fire("heavy", Mathf.Max(seconds * 0.8f, 0.6f)); break;
            // O bote já começa no alto: a queda toca no tempo dela, um
            // pouco mais rápida para o golpe chegar com o chão.
            case Weapon.CooldownLeap: Fire("leap", _lengths.TryGetValue("leap", out float leap) ? leap / 1.2f : 0.9f); break;
        }
    }

    private void OnFormChanged(int form)
    {
        Visible = _form.IsWolf;

        if ((Form)form == Form.ShiftingToWolf)
        {
            _model.Visible = true;
            _playback?.Start("Locomotion");
            Fire("transform", 1.1f);
        }
    }

    private void OnDied()
    {
        if (!_form.IsWolf) return;
        _dead = true;
        _deadTimer = 0;
        _playback?.Travel("Death");
    }

    private void OnRespawned()
    {
        _dead = false;
        _model.Visible = true;
        _howling = false;
        _playback?.Start("Locomotion");
        Visible = _form.IsWolf;
    }
}
