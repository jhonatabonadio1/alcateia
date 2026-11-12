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
/// os sinais da arma disparam a garrada, o salto e o bote; o uivo (R)
/// toca o rugido da cintura para cima, por cima do andar — o lobo ruge
/// em movimento. Virar lobo não tem animação (decisão do Jhon): o lobo
/// aparece já andando.
///
/// **O pulo usa só o voo do "mutant jumping"**: o clipe agacha, salta e
/// aterrissa sozinho, e no jogo quem salta é a física, no quadro do
/// clique — tocado inteiro, o bicho pularia duas vezes. Então o agachar
/// fica de fora, e o voo não corre no relógio: anda pela velocidade
/// vertical do corpo (saindo do chão, no topo, caindo), e o quadril fica
/// na altura de pé (quem sobe é a cápsula). Chegou devagar, toca a
/// aterrissagem; chegou correndo, volta direto para a corrida — agachar
/// com o corpo a 5 m/s faria o pé patinar.
///
/// | Clipe | Arquivo do pack | Quando |
/// |---|---|---|
/// | idle | mutant breathing idle | parado |
/// | walk / run | mutant walking / run | andando, correndo |
/// | claw | mutant swiping | garrada (clique), alternando com o punch |
/// | heavy | mutant punch | a outra garrada |
/// | pounce | mutant jump attack, inteiro | salto (direito no chão) |
/// | dive | mutant jump attack, só a queda | bote (direito no ar), até encostar |
/// | dive_land | mutant jump attack, da pancada ao fim | bote, ao encostar |
/// | howl | mutant roaring | uivando (R), da cintura para cima |
/// | jump | mutant jumping, só o voo | no ar depois de um pulo (do chão ou da parede) |
/// | jump_land | mutant jumping, a aterrissagem | tocou o chão depois do pulo, devagar |
/// | wallrun | Running Crawl | correndo na parede, de barriga para ela |
/// | death | mutant dying | morreu de lobo |
///
/// Os golpes e o rugido tocam o trecho e a velocidade de
/// <see cref="WolfMoves"/>, a mesma tabela que a arma usa para o dano
/// sair no quadro do golpe. Nenhum golpe corta outro. As garradas só
/// mexem da cintura para cima — o lobo continua correndo enquanto bate
/// —; o salto e o bote, que prendem o lobo, mexem o corpo inteiro.
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
    [Export] public float ModelScale = WolfMoves.BodyScale;

    /// <summary>Mixamo olha para +Z; o corpo, para −Z.</summary>
    [Export] public float ModelYaw = 180f;

    /// <summary>Até que velocidade (m/s) o corpo mistura parado e andar,
    /// e a partir de qual é só correr. Entre as duas, andar e correr — e
    /// a faixa é curta de propósito: os dois clipes têm ciclos de tamanhos
    /// diferentes (1,43 s e 0,87 s), e misturados o pé de um puxa o do
    /// outro e patina.</summary>
    [Export] public float WalkSpeed = 1.5f;
    [Export] public float RunSpeed = 2.2f;

    /// <summary>A velocidade em que o pé de cada clipe não patina, em m/s:
    /// a passada medida no Mutant (andar 1,22 m/s, correr 2,20 m/s) vezes a
    /// escala do modelo. O clipe toca na razão entre a velocidade do corpo
    /// e esta — o Bruto a 3,6 m/s corre o clipe a 1,5×. Antes ele tocava a
    /// 1× até 7 m/s, e o corpo andava mais do dobro do que as pernas. O
    /// limite do que fica natural é a passada: correr além de ~1,6× fica
    /// corrido, e por isso o lobo anda a 3,6 m/s (decisão do Jhon).</summary>
    [Export] public float WalkClipSpeed = 1.34f;
    [Export] public float RunClipSpeed = 2.42f;

    /// <summary>Quanto da cor do time entra na pele — só quando os dois
    /// times usam o mesmo modelo. Com um modelo por time, a pele já diz
    /// o lado.</summary>
    [Export(PropertyHint.Range, "0,1")] public float TeamTint = 0.3f;

    /// <summary>Velocidade do clipe de engatinhar na parede, em m/s
    /// (o Running Crawl anda 1,40 m em 0,67 s, vezes a escala do modelo).
    /// Mais rápido que isso o clipe acelera junto, até o dobro e meio.</summary>
    [Export] public float WallClipSpeed = 2.3f;

    /// <summary>Tempo para deitar na parede e para voltar, em segundos.</summary>
    [Export] public float WallTurnTime = 0.12f;

    [Export] public bool Debug;

    /// <summary>Clipes tocados como vêm do arquivo. Os golpes e o rugido
    /// saem de <see cref="WolfMoves.Clips"/>, recortados.</summary>
    private static readonly string[] Clips =
        { "idle", "walk", "run", "wallrun" };

    /// <summary>Disparos por cima da locomoção, na ordem da pilha: o de
    /// baixo é sobreposto pelos de cima.</summary>
    private static readonly string[] Shots =
        { "claw", "heavy", "howl", "jump_land", "pounce", "dive", "dive_land" };

    /// <summary>Disparos que tomam o corpo inteiro. As garradas ficam só
    /// no tronco: o "swiping" gira o quadril 90° e terminaria o golpe
    /// com o lobo de lado, voltando num tranco.</summary>
    private static bool FullBody(string shot) => shot is "pounce" or "dive" or "dive_land" or "jump_land";

    /// <summary>Entrada e saída de cada disparo, em segundos. A garrada
    /// entra seca: a garra chega 0,14 s depois do clique, e uma entrada
    /// mais longa comeria o golpe. Sai curta, porque a próxima vem logo.
    /// A aterrissagem do bote também entra quase seca: é o impacto.</summary>
    private static (float In, float Out) Fades(string shot) => shot switch
    {
        "claw" or "heavy" => (0.07f, 0.12f),
        "howl" => (0.15f, 0.2f),
        "jump_land" => (0.05f, 0.25f),
        "pounce" => (0.12f, 0.25f),
        "dive" => (0.08f, 0.15f),
        "dive_land" => (0.05f, 0.25f),
        _ => (0.08f, 0.2f)
    };

    /// <summary>Inclinação máxima do corpo no bote, em graus.</summary>
    [Export] public float DiveLeanMax = 40f;

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
    private float _lean;
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
        _weapon.WolfDiveLanded += OnDiveLanded;
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

        // Um arquivo pode dar mais de um clipe (o "jump attack" é o salto,
        // a queda do bote e a aterrissagem): cada um é lido uma vez só.
        var files = new Dictionary<string, Animation>();
        Animation Source(string file)
        {
            if (!files.TryGetValue(file, out Animation animation))
                files[file] = animation = LoadFile(file);
            return animation;
        }

        foreach (string clip in Clips)
        {
            Animation animation = Source(clip);
            if (animation == null) continue;

            animation.LoopMode = clip is "idle" or "walk" or "run" or "wallrun"
                ? Animation.LoopModeEnum.Linear
                : Animation.LoopModeEnum.None;

            library.AddAnimation(clip, animation);
            _lengths[clip] = (float)animation.Length;
        }

        // Golpes e rugido tocam o trecho medido em WolfMoves — o mesmo que
        // a arma usa para saber quando o dano sai.
        foreach ((string clip, WolfClip window) in WolfMoves.Clips)
        {
            Animation source = Source(window.Source);
            if (source == null) continue;

            Animation animation = Trim(source, window.Start, window.End, window.FallOnly, force: true);
            animation.LoopMode = Animation.LoopModeEnum.None;

            library.AddAnimation(clip, animation);
            _lengths[clip] = (float)animation.Length;
        }

        _anims.AddAnimationLibrary("lobo", library);
    }

    private Animation LoadFile(string file)
    {
        string path = $"{ClipFolder}/{file}.fbx";
        if (!ResourceLoader.Exists(path)) return null;

        Node temp = GD.Load<PackedScene>(path)?.Instantiate();
        if (temp == null) return null;

        Animation animation = CharacterAnimator.TakeAnimation(CharacterAnimator.FindPlayer(temp), file, Debug);
        temp.Free();
        if (animation == null) return null;

        CharacterAnimator.StripRootMotion(animation);
        return animation;
    }

    private bool Has(string clip) => _lengths.ContainsKey(clip);

    /// <summary>
    /// Recorta o clipe de <paramref name="start"/> a <paramref name="end"/>
    /// (segundos do arquivo) e começa do zero. É como o bote usa só a
    /// queda do "jump attack" e a garrada pula a preparação longa.
    /// <paramref name="force"/> copia mesmo quando o trecho é o clipe
    /// inteiro: o mesmo arquivo pode dar mais de um clipe.
    ///
    /// <paramref name="fallOnly"/>: a altura do quadril fica limitada à
    /// de pé. No ar quem levanta o corpo é a física; somar a subida do
    /// clipe deixaria o lobo flutuando acima da cápsula. A agachada do
    /// impacto, abaixo disso, fica.
    /// </summary>
    private static Animation Trim(Animation source, float start, float end, bool fallOnly, bool force = false)
    {
        end = Mathf.Min(end, (float)source.Length);
        if (!force && start <= 0.001f && end >= source.Length - 0.001f && !fallOnly) return source;

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
    /// Máquina de estados para o corpo inteiro (locomoção, uivo, parede,
    /// morte) e os disparos por cima (<see cref="Shots"/>): as garradas e
    /// o rugido só da cintura para cima, o salto e o bote no corpo
    /// inteiro.
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
        if (Has("death")) machine.AddNode("Death", Clip("death"), new Vector2(600, 100));

        if (Has("jump"))
        {
            // O voo não corre sozinho: a cada quadro o ponto do clipe é
            // escolhido pela velocidade vertical (UpdateJump), e a escala
            // zero segura o clipe parado entre um quadro e outro.
            var jump = new AnimationNodeBlendTree();
            jump.AddNode("Clip", Clip("jump"), Vector2.Zero);
            jump.AddNode("Seek", new AnimationNodeTimeSeek(), new Vector2(200, 0));
            jump.AddNode("Speed", new AnimationNodeTimeScale(), new Vector2(400, 0));
            jump.ConnectNode("Seek", 0, "Clip");
            jump.ConnectNode("Speed", 0, "Seek");
            jump.ConnectNode("output", 0, "Speed");
            machine.AddNode("Jump", jump, new Vector2(400, 300));
        }

        if (Has("wallrun"))
        {
            var wall = new AnimationNodeBlendTree();
            wall.AddNode("Clip", Clip("wallrun"), Vector2.Zero);
            wall.AddNode("Speed", new AnimationNodeTimeScale(), new Vector2(200, 0));
            wall.ConnectNode("Speed", 0, "Clip");
            wall.ConnectNode("output", 0, "Speed");
            machine.AddNode("Wall", wall, new Vector2(400, 0));
        }

        Link(machine, "Locomotion", "Wall");
        Link(machine, "Wall", "Locomotion");
        Link(machine, "Locomotion", "Jump");
        Link(machine, "Jump", "Locomotion");
        Link(machine, "Jump", "Wall");
        Link(machine, "Wall", "Jump");
        foreach (string from in new[] { "Locomotion", "Wall", "Jump" })
            Link(machine, from, "Death");
        Link(machine, "Death", "Locomotion");

        var root = new AnimationNodeBlendTree();
        root.AddNode("Machine", machine, Vector2.Zero);
        string last = "Machine";

        // Cada disparo recebe o anterior como base e põe o golpe por cima.
        foreach (string shot in Shots)
        {
            if (!Has(shot)) continue;

            var (fadeIn, fadeOut) = Fades(shot);
            var oneShot = new AnimationNodeOneShot
            {
                FadeInTime = fadeIn,
                FadeOutTime = fadeOut,
                FilterEnabled = !FullBody(shot)
            };

            if (!FullBody(shot))
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

        if (Has("jump"))
            _tree.Set("parameters/Machine/Jump/Speed/scale", 0f);

        _playback = (AnimationNodeStateMachinePlayback)_tree.Get("parameters/Machine/playback");
        _playback?.Start("Locomotion");
    }

    private static AnimationNodeAnimation Clip(string clip) => new() { Animation = $"lobo/{clip}" };

    /// <summary>Nome do nó do disparo: "dive_land" vira "DiveLand".</summary>
    private static string Cap(string s)
    {
        var name = new System.Text.StringBuilder();
        foreach (string part in s.Split('_'))
            if (part.Length > 0) name.Append(char.ToUpperInvariant(part[0])).Append(part[1..]);
        return name.ToString();
    }

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

        // 0 parado, 1 andando, 2 correndo. O clipe toca na velocidade em
        // que o pé acompanha o corpo: a do corpo dividida pela passada do
        // clipe (a mistura de andar e correr, entre os dois).
        float blend = _speed <= WalkSpeed
            ? _speed / Mathf.Max(WalkSpeed, 0.01f)
            : 1f + Mathf.Clamp((_speed - WalkSpeed) / Mathf.Max(RunSpeed - WalkSpeed, 0.01f), 0f, 1f);
        float stride = Mathf.Lerp(WalkClipSpeed, RunClipSpeed, Mathf.Clamp(blend - 1f, 0f, 1f));
        float cadence = _speed > 0.3f ? Mathf.Clamp(_speed / Mathf.Max(stride, 0.01f), 0.6f, 3f) : 1f;
        _tree.Set("parameters/Machine/Locomotion/Blend/blend_position", blend);
        _tree.Set("parameters/Machine/Locomotion/Speed/scale", cadence);

        // No ar depois de um pulo, o voo do clipe de pulo; caindo de uma
        // beirada, o corpo segue na pose de antes.
        bool onWall = _player != null && _player.IsWallRunning && Has("wallrun");

        // No ar o lobo cai, não anda: depois de um pulo (do chão ou da
        // parede) e também soltando da parede ou caindo de uma beirada.
        // Um instante no ar descendo rampa ou degrau não conta.
        _airTime = _body.IsOnFloor() ? 0f : _airTime + (float)delta;
        bool jumped = _player != null && _player.IsJumpAirborne;
        bool falling = !_body.IsOnFloor() && (_airTime > 0.12f || _body.Velocity.Y < -2f);
        bool special = _weapon.IsDiving || _weapon.IsPounceFlying || _weapon.IsRooted;
        bool jumping = !onWall && !special && (jumped || falling) && Has("jump");
        if (onWall != _onWall || jumping != _jumping)
        {
            // Acabou o pulo no chão: chegou devagar, aterrissa; chegou
            // correndo, a corrida já é a aterrissagem.
            if (_jumping && !jumping && !onWall && _body.IsOnFloor() && _speed < WalkSpeed && !_weapon.IsStriking)
                Fire("jump_land", JumpLandSpeed);

            _onWall = onWall;
            _jumping = jumping;
            _playback?.Travel(onWall ? "Wall" : jumping ? "Jump" : "Locomotion");
        }

        if (_jumping) UpdateJump();

        // Aterrissando e já saiu andando: a aterrissagem sai.
        if (Has("jump_land") && _speed > WalkSpeed
            && (bool)_tree.Get("parameters/JumpLand/active"))
            _tree.Set("parameters/JumpLand/request", (int)AnimationNodeOneShot.OneShotRequest.FadeOut);

        // O rugido vai por cima do andar, uma vez, na velocidade de
        // WolfMoves: acaba junto com o escudo voltando. Soltou R antes,
        // ele sai.
        bool howling = IsHowling();
        if (howling != _howling)
        {
            _howling = howling;
            if (howling) Fire("howl", WolfMoves.Of(WolfMove.Howl).Speed);
            else if (Has("howl")) _tree.Set("parameters/Howl/request", (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
        }

        UpdateCharge(delta);
        UpdateLean((float)delta);
        UpdateWall((float)delta);
    }

    /// <summary>
    /// No bote o corpo tomba para a frente, na inclinação do mergulho: o
    /// clipe cai de pé, com os braços para cima, e sem a inclinação o
    /// lobo parecia despencar em vez de avançar no alvo. Gira em volta do
    /// quadril, não dos pés, e volta rápido ao encostar.
    /// </summary>
    private void UpdateLean(float delta)
    {
        float target = 0f;
        if (_weapon.IsDiving)
        {
            Vector3 v = _weapon.DiveVelocity;
            float descent = Mathf.RadToDeg(Mathf.Atan2(-v.Y, new Vector2(v.X, v.Z).Length()));
            target = Mathf.Clamp(descent * 0.8f, 10f, DiveLeanMax);
        }

        float rate = target > _lean ? 400f : 250f;
        _lean = Mathf.MoveToward(_lean, target, rate * delta);
    }

    private const float LeanPivot = 0.95f;

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

        if (_lean > 0.01f)
        {
            // Nariz para baixo: girar em X negativo leva o −Z (a frente do
            // corpo) para baixo. O pivô fica na altura do quadril.
            var pivot = new Vector3(0, LeanPivot, 0);
            var tilt = new Transform3D(new Basis(Vector3.Right, -Mathf.DegToRad(_lean)), Vector3.Zero);
            rest = new Transform3D(Basis.Identity, pivot) * tilt * new Transform3D(Basis.Identity, -pivot) * rest;
        }

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

    /// <summary>Velocidade da aterrissagem do pulo: o clipe inteiro de
    /// pé de novo em 0,6 s.</summary>
    private const float JumpLandSpeed = 1.6f;

    private bool _jumping;

    /// <summary>
    /// O ponto do voo pela velocidade vertical: saindo do chão a toda, o
    /// começo do clipe; parado no alto, o topo; caindo tão rápido quanto
    /// saiu, os pés esticados para o chão. Pulo mais longo que o do clipe
    /// fica nessa última pose até encostar; mais curto (caiu em cima de
    /// um caixote) sai antes. Assim o topo do clipe é o topo do pulo, seja
    /// qual for a altura dele.
    /// </summary>
    private void UpdateJump()
    {
        WolfClip clip = WolfMoves.Clips["jump"];
        float apex = clip.Impact - clip.Start;
        float fall = clip.Length - apex;
        float vy = _body.Velocity.Y;

        // Subindo de um pulo: da saída ao topo, pela velocidade vertical.
        // Descendo — de um pulo, de um salto da parede ou de uma queda —:
        // a pose do topo, encolhido e de braços para cima, até o chão
        // chegar perto; aí as pernas esticam para pousar. Pela velocidade,
        // uma queda longa (saltar da parede) ficava de pernas esticadas e
        // parado no ar o caminho todo.
        float t;
        if (vy > 0f && _player != null && _player.IsJumpAirborne)
            t = apex * (1f - Mathf.Clamp(vy / Mathf.Max(_player.JumpSpeed, 0.1f), 0f, 1f));
        else
            t = apex + fall * 0.92f * (1f - Mathf.Clamp(GroundBelow() / LegReach, 0f, 1f));

        _tree.Set("parameters/Machine/Jump/Seek/seek_request", t);
    }

    /// <summary>A que altura do chão as pernas começam a esticar para
    /// pousar, em metros dos pés.</summary>
    private const float LegReach = 1.0f;

    private float _airTime;

    /// <summary>Distância dos pés até o chão logo abaixo, em metros.
    /// Sem chão ao alcance, longe.</summary>
    private float GroundBelow()
    {
        float feet = _player != null ? _player.FeetOffset : -0.875f;
        Vector3 from = _body.GlobalPosition + Vector3.Up * feet;
        var query = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * (LegReach + 0.5f));
        query.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
        var hit = _body.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0 ? LegReach * 2f : from.Y - ((Vector3)hit["position"]).Y;
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

        // A queda do bote acompanha o mergulho: o impacto da animação cai
        // no quadro em que o lobo deve encostar.
        if ((WolfMove)move == WolfMove.Leap)
            speed = WolfMoves.DiveSpeed(_weapon.DiveEta);

        Fire(clip, speed);
        if ((WolfMove)move == WolfMove.Pounce) _pounceClock = 0;
    }

    /// <summary>Tempo desde que o salto começou a tocar.</summary>
    private double _pounceClock = 99;
    private bool _pounceHeld;

    /// <summary>
    /// Carregando o salto, o clipe para no fim do agachar — o lobo fica
    /// encolhido, pronto — e segue quando o jogador solta. A arma conta o
    /// resto do golpe a partir dali, então o salto e o dano continuam no
    /// quadro certo.
    /// </summary>
    private void UpdateCharge(double delta)
    {
        _pounceClock += delta;
        if (!Has("pounce")) return;

        bool hold = _weapon.IsChargingPounce && _pounceClock >= WolfMoves.StepOf(WolfMove.Pounce).Delay;
        if (hold == _pounceHeld) return;

        _pounceHeld = hold;
        _tree.Set("parameters/PounceSpeed/scale", hold ? 0f : WolfMoves.Of(WolfMove.Pounce).Speed);
    }

    /// <summary>O bote encostou: a aterrissagem entra por cima da queda,
    /// no mesmo quadro do dano — mesmo que tenha encostado antes do
    /// previsto (alguém no caminho). A queda não é cortada: a
    /// aterrissagem precisa dela embaixo para entrar a partir da pose do
    /// mergulho, e não da pose parada.</summary>
    private void OnDiveLanded()
    {
        if (!_form.IsWolf || _tree == null) return;

        var (clip, speed) = WolfMoves.Land;
        Fire(clip, speed);
    }

    private void OnFormChanged(int form)
    {
        Visible = _form.IsWolf;

        if ((Form)form == Form.ShiftingToWolf)
        {
            _model.Visible = true;
            _playback?.Start("Locomotion");
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
        _jumping = false;
        _wallBlend = 0;
        _playback?.Start("Locomotion");
        Visible = _form.IsWolf;
    }
}
