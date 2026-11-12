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
///
/// Os golpes e o rugido tocam o trecho e a velocidade de
/// <see cref="WolfMoves"/>, a mesma tabela que a arma usa para o dano
/// sair no quadro do golpe. Nenhum golpe corta outro.
/// | howl | mutant roaring | uivando (R) |
/// | wallrun | Running Crawl | correndo na parede, de barriga para ela |
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

    /// <summary>Velocidade do clipe de engatinhar na parede, em m/s
    /// (o Running Crawl anda 1,3 m em 0,67 s). Mais rápido que isso o
    /// clipe acelera junto, até o dobro e meio.</summary>
    [Export] public float WallClipSpeed = 1.95f;

    /// <summary>Tempo para deitar na parede e para voltar, em segundos.</summary>
    [Export] public float WallTurnTime = 0.12f;

    [Export] public bool Debug;

    private static readonly string[] Clips =
        { "idle", "walk", "run", "claw", "heavy", "leap", "howl", "transform", "death", "wallrun" };

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
    private bool _onWall;
    private float _wallBlend;
    private Vector3 _lastNormal;
    private Vector3 _lastAlong;
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
        _weapon.WolfMoveStarted += OnMove;
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

            // Golpes e rugido tocam o trecho medido em WolfMoves — o mesmo
            // que a arma usa para saber quando o dano sai.
            if (WolfMoves.Clips.TryGetValue(clip, out WolfClip window))
                animation = Trim(animation, window.Start, window.End, window.FallOnly);

            animation.LoopMode = clip is "idle" or "walk" or "run" or "wallrun"
                ? Animation.LoopModeEnum.Linear
                : Animation.LoopModeEnum.None;

            library.AddAnimation(clip, animation);
            _lengths[clip] = (float)animation.Length;
        }

        _anims.AddAnimationLibrary("lobo", library);
    }

    private bool Has(string clip) => _lengths.ContainsKey(clip);

    /// <summary>
    /// Recorta o clipe de <paramref name="start"/> a <paramref name="end"/>
    /// (segundos do arquivo) e começa do zero. É como o bote usa só a
    /// queda do "jump attack" e a garrada pula a preparação longa.
    ///
    /// <paramref name="fallOnly"/>: a altura do quadril fica limitada à
    /// de pé. No ar quem levanta o corpo é a física; somar a subida do
    /// clipe deixaria o lobo flutuando acima da cápsula. A agachada do
    /// impacto, abaixo disso, fica.
    /// </summary>
    private static Animation Trim(Animation source, float start, float end, bool fallOnly)
    {
        end = Mathf.Min(end, (float)source.Length);
        if (start <= 0.001f && end >= source.Length - 0.001f && !fallOnly) return source;

        int hips = -1;
        float rest = 0f;
        for (int t = 0; t < source.GetTrackCount(); t++)
        {
            if (source.TrackGetType(t) == Animation.TrackType.Position3D
                && source.TrackGetPath(t).ToString().Contains("Hips")
                && source.TrackGetKeyCount(t) > 0)
            {
                hips = t;
                rest = ((Vector3)source.TrackGetKeyValue(t, 0)).Y;
                break;
            }
        }

        var cut = new Animation { Length = end - start };
        const double step = 1.0 / 30.0;

        for (int t = 0; t < source.GetTrackCount(); t++)
        {
            Animation.TrackType type = source.TrackGetType(t);
            if (type is not (Animation.TrackType.Position3D or Animation.TrackType.Rotation3D or Animation.TrackType.Scale3D))
                continue;

            int track = cut.AddTrack(type);
            cut.TrackSetPath(track, source.TrackGetPath(t));

            for (double time = start; time <= end + 0.0001; time += step)
            {
                double at = time - start;
                switch (type)
                {
                    case Animation.TrackType.Position3D:
                        Vector3 position = source.PositionTrackInterpolate(t, time);
                        if (fallOnly && t == hips) position.Y = Mathf.Min(position.Y, rest);
                        cut.PositionTrackInsertKey(track, at, position);
                        break;
                    case Animation.TrackType.Rotation3D:
                        cut.RotationTrackInsertKey(track, at, source.RotationTrackInterpolate(t, time));
                        break;
                    default:
                        cut.ScaleTrackInsertKey(track, at, source.ScaleTrackInterpolate(t, time));
                        break;
                }
            }
        }

        return cut;
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
        if (Has("howl"))
        {
            // O rugido toca uma vez, inteiro, na velocidade de WolfMoves:
            // o escudo volta exatamente quando ele acaba.
            var howl = new AnimationNodeBlendTree();
            howl.AddNode("Clip", Clip("howl"), Vector2.Zero);
            howl.AddNode("Speed", new AnimationNodeTimeScale(), new Vector2(200, 0));
            howl.ConnectNode("Speed", 0, "Clip");
            howl.ConnectNode("output", 0, "Speed");
            machine.AddNode("Howl", howl, new Vector2(400, 200));
        }
        if (Has("death")) machine.AddNode("Death", Clip("death"), new Vector2(600, 100));

        if (Has("wallrun"))
        {
            var wall = new AnimationNodeBlendTree();
            wall.AddNode("Clip", Clip("wallrun"), Vector2.Zero);
            wall.AddNode("Speed", new AnimationNodeTimeScale(), new Vector2(200, 0));
            wall.ConnectNode("Speed", 0, "Clip");
            wall.ConnectNode("output", 0, "Speed");
            machine.AddNode("Wall", wall, new Vector2(400, 0));
        }

        Link(machine, "Locomotion", "Howl");
        Link(machine, "Howl", "Locomotion");
        Link(machine, "Locomotion", "Wall");
        Link(machine, "Wall", "Locomotion");
        foreach (string from in new[] { "Locomotion", "Howl", "Wall" })
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
        if (Has("howl"))
            _tree.Set("parameters/Machine/Howl/Speed/scale", WolfMoves.Of(WolfMove.Howl).Speed);

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
        bool onWall = _player != null && _player.IsWallRunning && Has("wallrun");

        if (howling != _howling || onWall != _onWall)
        {
            _howling = howling;
            _onWall = onWall;
            _playback?.Travel(onWall ? "Wall" : howling && Has("howl") ? "Howl" : "Locomotion");
        }

        UpdateWall((float)delta);
    }

    /// <summary>
    /// Na parede, o lobo engatinha com a barriga virada para ela: o
    /// corpo deita de lado, os pés na parede e as costas para fora, de
    /// frente para o sentido da corrida. Parede à esquerda deita para a
    /// esquerda, à direita para a direita — quem decide é a normal da
    /// parede, não o lado da câmera.
    ///
    /// Gira só o modelo, não o corpo: a cápsula, a câmera e a mira
    /// continuam de pé, como a física as deixa. A entrada e a saída são
    /// suaves (<see cref="WallTurnTime"/>), senão o bicho estala de lado.
    /// </summary>
    private void UpdateWall(float delta)
    {
        if (_player == null) return;

        float target = _onWall ? 1f : 0f;
        _wallBlend = Mathf.MoveToward(_wallBlend, target, delta / Mathf.Max(WallTurnTime, 0.01f));

        var rest = new Transform3D(
            Basis.FromEuler(new Vector3(0, Mathf.DegToRad(ModelYaw), 0)).Scaled(Vector3.One * ModelScale),
            Vector3.Zero);

        if (_wallBlend <= 0f)
        {
            _model.Transform = rest;
            return;
        }

        // Saindo da parede, a normal some antes de o corpo terminar de
        // levantar: a volta usa a última parede conhecida.
        if (_player.WallNormal != Vector3.Zero && _player.WallAlong != Vector3.Zero)
        {
            _lastNormal = _player.WallNormal;
            _lastAlong = _player.WallAlong;
        }

        Vector3 normal = _lastNormal;
        Vector3 along = _lastAlong;
        if (normal == Vector3.Zero || along == Vector3.Zero)
        {
            _model.Transform = rest;
            return;
        }

        // Clipe do Mixamo olha para +Z: −Z do modelo fica contra o
        // sentido da corrida, e o "em cima" do bicho é a normal.
        Basis basis = Basis.LookingAt(-along, normal).Scaled(Vector3.One * ModelScale);

        // Os pés tocam a parede: o plano dela fica a um raio da cápsula
        // do centro do corpo.
        Vector3 center = _body.GlobalPosition;
        var onWall = new Transform3D(basis, center - normal * WallGap);

        Transform3D local = GlobalTransform.AffineInverse() * onWall;
        _model.Transform = rest.InterpolateWith(local, Smooth(_wallBlend));

        // Engatinhar no ritmo da corrida; agarrado (parado), o clipe para.
        float speed = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z).Length();
        _tree.Set("parameters/Machine/Wall/Speed/scale", Mathf.Clamp(speed / Mathf.Max(WallClipSpeed, 0.01f), 0f, 2.5f));
    }

    /// <summary>Distância do centro do corpo à parede: o raio da cápsula.</summary>
    private const float WallGap = 0.3f;

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private bool IsHowling() =>
        _player != null ? _player.HowlProgress > 0f
        : _bot != null && _bot.Action == BotAction.Howl;

    /// <summary>Dispara um golpe por cima do corpo.</summary>
    private void Fire(string shot, float speed)
    {
        if (_tree == null || !Has(shot)) return;

        string name = Cap(shot);
        _tree.Set($"parameters/{name}Speed/scale", speed);
        _tree.Set($"parameters/{name}/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
    }

    /// <summary>
    /// Golpe do lobo, com o clipe e a velocidade de
    /// <see cref="WolfMoves"/> — os mesmos que a arma usa para o dano
    /// sair no instante em que a garra chega. Quem alterna os braços e
    /// decide quando pode bater é a arma; aqui só se toca.
    /// </summary>
    private void OnMove(int move)
    {
        if (!_form.IsWolf) return;

        var (clip, speed) = WolfMoves.Of((WolfMove)move);
        Fire(clip, speed);
    }

    private void OnFormChanged(int form)
    {
        Visible = _form.IsWolf;

        if ((Form)form == Form.ShiftingToWolf)
        {
            _model.Visible = true;
            _playback?.Start("Locomotion");
            // O "flexionar" inteiro em pouco mais de um segundo.
            if (Has("transform")) Fire("transform", _lengths["transform"] / 1.1f);
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
        _onWall = false;
        _wallBlend = 0;
        _playback?.Start("Locomotion");
        Visible = _form.IsWolf;
    }
}
