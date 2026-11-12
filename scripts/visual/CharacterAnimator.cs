using Godot;
using System.Collections.Generic;

/// <summary>
/// Liga o corpo humano animado ao estado que o personagem já calcula.
///
/// A regra do projeto vale aqui inteira: o animador **lê** estado e
/// nunca decide. Ele não move, não atira, não escolhe forma. Se o
/// boneco está correndo na tela é porque a velocidade diz isso, não
/// porque o animador resolveu que seria bonito. É o que mantém o
/// visual trocável sem risco — e, na Fase 2, o que permite animar um
/// corpo remoto a partir de estado replicado sem mudar uma linha.
///
/// Monta a árvore de animação por CÓDIGO, não no editor. Mesma razão
/// do InputSetup, do MapLoader e do NavBaker: dezesseis clipes
/// ligados à mão num BlendSpace são dezesseis chances de errar em
/// silêncio, e nada disso fica registrado em lugar nenhum que o
/// próximo agente consiga ler.
/// </summary>
public partial class CharacterAnimator : Node3D
{
    /// <summary>Pasta dos clipes importados.</summary>
    [Export(PropertyHint.Dir)]
    public string ClipFolder = "res://assets/characters/human";

    /// <summary>Caminho do modelo instanciado, a partir deste nó.</summary>
    [Export] public NodePath ModelPath = "Model";

    /// <summary>
    /// Velocidade que corresponde ao clipe de caminhada.
    ///
    /// O espaço de mistura tem a caminhada no raio 1 e a corrida no
    /// raio 2, então este número é a metade da velocidade máxima: a
    /// pé solto o personagem cai exatamente em cima do clipe de
    /// corrida, e qualquer coisa no meio mistura os dois.
    /// </summary>
    [Export] public float WalkReference = 2.1f;

    /// <summary>
    /// Inverte o eixo lateral do espaço de mistura.
    ///
    /// Os clipes de strafe do Mixamo vieram com o nome trocado e
    /// foram renomeados pela medição do deslocamento no arquivo. O
    /// que a medição não resolve é a conversão de eixo do importador,
    /// que pode espelhar Z. Se no jogo o strafe sair para o lado
    /// errado, é só marcar isto — os pares estão certos entre si.
    /// </summary>
    [Export] public bool InvertStrafe = false;

    /// <summary>Suavização da entrada do espaço de mistura. Sem ela a
    /// troca de direção pisca entre clipes.</summary>
    [Export] public float BlendSmoothing = 9f;

    /// <summary>
    /// Giro do modelo em torno de Y, em graus.
    ///
    /// Personagem do Mixamo olha para +Z no arquivo; no Godot, a
    /// frente de um corpo é −Z. Meia volta alinha os dois. Se vier
    /// alinhado do importador, ponha zero: é um número só, e é o
    /// primeiro lugar para olhar se o boneco andar de costas.
    /// </summary>
    [Export] public float ModelYaw = 180f;

    /// <summary>
    /// Nó escondido quando os clipes carregam.
    ///
    /// Serve para a cápsula: se o modelo entrar, ela some; se algo
    /// falhar no caminho, ela fica e o bot continua visível em vez de
    /// virar um inimigo invisível que atira.
    /// </summary>
    [Export] public NodePath HideOnSuccess;

    /// <summary>
    /// Câmera que não deve desenhar este corpo.
    ///
    /// Em primeira pessoa o dono tem a cabeça dentro do próprio
    /// modelo, e o que ele veria é o interior de um crânio. A saída
    /// não é esconder o corpo — isso o apagaria também para os outros
    /// jogadores na Fase 2 — e sim tirá-lo da máscara desta câmera.
    /// O corpo continua existindo, continua animado e continua
    /// projetando sombra, que é como se vê o próprio corpo num FPS.
    ///
    /// Vazio em bot: bot não tem dono olhando de dentro.
    /// </summary>
    [Export] public NodePath OwnerCameraPath;

    /// <summary>Camada de render do corpo. 2 porque a 1 é o mundo, e
    /// misturar os dois tiraria o cenário junto.</summary>
    [Export] public int BodyLayer = 2;

    /// <summary>
    /// Quanto o corpo desce ao morrer, para encostar no chão.
    ///
    /// De pé o modelo casa com a cápsula, porque os dois têm o pé na
    /// base. Deitado não: a pose final do clipe de morte deixa o corpo
    /// um pouco acima do plano de onde ele foi animado, e o que sobra
    /// aparece como um defunto pairando. Um número resolve, e é melhor
    /// como número do que como conserto na animação — clipe de morte
    /// novo traz a própria folga.
    /// </summary>
    [Export] public float CorpseDrop = 0.12f;

    /// <summary>
    /// Tempo que o corpo fica à mostra no chão antes de sumir.
    ///
    /// Contado a partir do fim do clipe de morte, não da morte: o
    /// clipe tem três segundos e o corpo passa boa parte deles ainda
    /// caindo. Contar do começo fazia o corpo desbotar no meio da
    /// queda.
    /// </summary>
    [Export] public float CorpseHold = 1.2f;

    /// <summary>Duração do desaparecimento.</summary>
    [Export] public float CorpseFade = 1.2f;

    /// <summary>
    /// Quanto o corpo afunda enquanto some. Zero: some no lugar.
    ///
    /// Afundar esconde melhor o instante da saída, mas só funciona
    /// quando o corpo está sobre piso — em cima de uma caixa ou de um
    /// telhado ele atravessa antes de apagar, e aí o truque aparece.
    /// Fica como número para quem quiser.
    /// </summary>
    [Export] public float CorpseSink = 0f;

    /// <summary>
    /// Esconder o corpo humano quando o dono vira lobo.
    ///
    /// Desligado por enquanto: o lobo ainda não tem corpo próprio, e
    /// sem isto ele vira um par de braços flutuando. Ligar no dia em
    /// que existir um quadrúpede animado.
    /// </summary>
    [Export] public bool HideInWolfForm = false;

    /// <summary>
    /// Tempo mínimo entre dois esticões de dano.
    ///
    /// Sem isto, uma rajada produz uma reação por bala e o corpo
    /// tremeluz no lugar em vez de reagir. A reação é um tempero: ela
    /// conta que você acertou, não cada vez que acertou.
    /// </summary>
    [Export] public float HitCooldown = 1.6f;

    /// <summary>
    /// Corte do clipe de andar agachado, em segundos. Zero não corta.
    ///
    /// O Mixamo não deu um ciclo de andar agachado: deu uma transição
    /// que levanta e outra que ajoelha. A segunda anda agachada por
    /// 1,7 s antes de ajoelhar — medi o quadril no arquivo, ele
    /// oscila entre 79 e 83 cm até lá e depois despenca para 33.
    ///
    /// Cortar em 1,65 s deixa só o trecho andando, que fecha em laço
    /// porque começa e termina na mesma altura. É remendo até aparecer
    /// um "Crouched Walking" de verdade, e o remendo está aqui em vez
    /// de no arquivo para que trocar o arquivo baste.
    /// </summary>
    [Export] public float CrouchWalkTrim = 1.65f;

    /// <summary>Velocidade em que o agachado está andando a pleno.</summary>
    [Export] public float CrouchWalkSpeed = 1.9f;

    [Export] public bool Debug = true;

    /// <summary>Arquivo do clipe, e o nome com que ele entra na
    /// biblioteca. O arquivo é a única fonte de nome confiável: todos
    /// os takes dentro dos FBX do Mixamo se chamam "mixamo.com".</summary>
    private static readonly string[] Clips =
    {
        "idle_rifle", "walk_fwd", "walk_back", "run_fwd", "run_back",
        "strafe_left", "strafe_right", "jump", "fire", "reload",
        "grenade", "hit", "turn_left", "turn_right",

        // Ainda não baixados. Ficam listados de propósito: arquivo
        // que não existe é pulado sem erro, então basta soltar o .fbx
        // com este nome na pasta para ele entrar sozinho.
        "death", "crouch_idle", "fall", "knife", "equip",

        // Ainda faltam de verdade.
        "crouch_walk", "land"
    };

    private Node3D _model;
    private AnimationPlayer _player;
    private AnimationTree _tree;
    private AnimationNodeStateMachinePlayback _playback;

    private CharacterBody3D _body;
    private Player _owner;
    private Health _health;
    private FormController _form;

    private Vector2 _blend;
    private bool _dead;
    private double _corpseTimer;
    private double _jumpGrace;
    private double _deathLength;
    private float _modelY;

    /// <summary>
    /// Altura dos pés agora. Segue a cápsula quando há dono, porque
    /// ela encolhe ao agachar.
    ///
    /// Vai neste nó, não no modelo dentro dele. Este nó já carrega o
    /// deslocamento vindo da cena, e aplicar a mesma altura nos dois
    /// soma duas vezes — foi assim que o corpo afundou 1,75 m, que é
    /// o dobro certinho de onde ele devia estar.
    /// </summary>
    private float BaseY => _owner != null ? _owner.FeetOffset : _modelY;
    private readonly List<StandardMaterial3D> _fadeMaterials = new();
    private bool _hasDeath;
    private bool _airborne;
    private bool _crouched;
    private float _crouchBlend;
    private double _hitTimer;
    private string _lastState = "";
    private static bool _reported;

    public override void _Ready()
    {
        _body = GetParent<CharacterBody3D>();
        _owner = _body as Player;
        _health = _body.GetNodeOrNull<Health>("Health");
        _form = _body.GetNodeOrNull<FormController>("FormController");

        _model = GetNodeOrNull<Node3D>(ModelPath);
        if (_model == null)
        {
            GD.PrintErr($"CharacterAnimator: não achei o modelo em '{ModelPath}'.");
            return;
        }

        _player = FindPlayer(_model);
        if (_player == null)
        {
            GD.PrintErr("CharacterAnimator: o modelo não traz AnimationPlayer.");
            return;
        }

        int loaded = LoadClips();

        _hasDeath = _player.HasAnimation("alcateia/death");

        // A duração do clipe é o que diz quando o corpo chegou ao
        // chão. Ler do próprio clipe em vez de cravar um número faz
        // isto continuar certo quando a morte for trocada por outra.
        if (_hasDeath)
            _deathLength = _player.GetAnimation("alcateia/death").Length;

        BuildTree();

        _model.RotationDegrees = new Vector3(
            _model.RotationDegrees.X, ModelYaw, _model.RotationDegrees.Z);

        // A altura da cena está NESTE nó; o modelo dentro dele fica em
        // zero e só se mexe para o efeito de morte.
        _modelY = Position.Y;
        _model.Position = new Vector3(_model.Position.X, 0, _model.Position.Z);

        HideFromOwner();

        if (loaded > 0)
        {
            // Caminho vazio devolve null aqui, então não precisa de
            // teste próprio: sem alvo configurado, nada acontece.
            var hide = GetNodeOrNull<Node3D>(HideOnSuccess);
            if (hide != null) hide.Visible = false;
        }

        if (_health != null)
        {
            _health.Died += _ => OnDied();
            _health.Respawned += OnRespawned;
            _health.Damaged += (_, _) => Hit();
        }

        if (_form != null)
        {
            _form.FormChanged += _ => Refresh();
            Refresh();
        }

        if (Debug && !_reported)
        {
            _reported = true;

            var missing = new List<string>();
            foreach (string clip in Clips)
                if (!_player.HasAnimation($"alcateia/{clip}")) missing.Add(clip);

            GD.Print($"CharacterAnimator: {loaded} clipes carregados" +
                     (missing.Count > 0 ? $" — faltando: {string.Join(", ", missing)}" : ""));
        }
    }

    // ------------------------------------------------------------------
    // Carregamento

    /// <summary>
    /// Traz cada clipe do seu próprio arquivo para a biblioteca do
    /// modelo.
    ///
    /// Cada FBX do Mixamo importa como uma cena inteira, com esqueleto
    /// e tudo, carregando uma animação só. Aqui a animação é arrancada
    /// da cena e a cena descartada. As trilhas apontam para
    /// "Skeleton3D:osso", que é o mesmo caminho em todos os arquivos —
    /// foi por isso que valeu conferir que os dezessete compartilham o
    /// mesmo rig antes de escrever qualquer coisa.
    /// </summary>
    private int LoadClips()
    {
        var library = new AnimationLibrary();
        int count = 0;

        foreach (string clip in Clips)
        {
            string path = $"{ClipFolder}/{clip}.fbx";

            if (!ResourceLoader.Exists(path)) continue;

            var scene = GD.Load<PackedScene>(path);
            if (scene == null) continue;

            Node temp = scene.Instantiate();
            AnimationPlayer source = FindPlayer(temp);

            Animation animation = TakeAnimation(source, clip, Debug && !_reported);

            if (animation != null)
            {
                StripRootMotion(animation);

                // Locomoção precisa repetir; golpe e morte, não.
                if (clip == "crouch_walk" && CrouchWalkTrim > 0
                    && animation.Length > CrouchWalkTrim)
                    animation.Length = CrouchWalkTrim;

                animation.LoopMode = IsLooping(clip)
                    ? Animation.LoopModeEnum.Linear
                    : Animation.LoopModeEnum.None;

                library.AddAnimation(clip, animation);
                count++;
            }

            temp.QueueFree();
        }

        _player.AddAnimationLibrary("alcateia", library);
        return count;
    }

    private static bool IsLooping(string clip) =>
        clip.StartsWith("idle") || clip.StartsWith("walk") ||
        clip.StartsWith("run") || clip.StartsWith("strafe") ||
        clip == "crouch_idle" || clip == "crouch_walk" || clip == "fall";

    /// <summary>
    /// A animação de verdade do tocador de origem.
    ///
    /// Cada FBX importado traz duas: o clipe do Mixamo e um "Take 001"
    /// que o importador deixa junto. Eu escolhia pela **duração**, e
    /// esse foi o erro: quando o take vazio é o mais longo, carrega-se
    /// um clipe sem osso nenhum. Clipe sem trilha não dá erro, não
    /// avisa e não move nada — o esqueleto fica na pose de repouso, e
    /// a pose de repouso de um rig humanoide é o T.
    ///
    /// Escolher pelo número de trilhas também não resolve: nos
    /// arquivos da segunda leva o "Take 001" tem 53 trilhas de 3,33 s
    /// — trilhas existem, mas não animam, e todas as quatro vítimas
    /// tinham exatamente essa assinatura.
    ///
    /// O nome é o sinal confiável: o clipe do Mixamo se chama
    /// "mixamo_com" e ganha de qualquer outro. Trilhas e duração
    /// viram desempate para arquivos de outra procedência.
    /// </summary>
    private static Animation TakeAnimation(AnimationPlayer source, string clip, bool debug)
    {
        if (source == null) return null;

        Animation best = null;
        int mostTracks = 0;
        double longest = 0;
        string chosen = "";

        foreach (StringName entry in source.GetAnimationList())
        {
            Animation candidate = source.GetAnimation(entry);
            if (candidate == null) continue;

            string name = entry.ToString();
            int tracks = candidate.GetTrackCount();

            // O clipe do Mixamo ganha de qualquer outro, sempre. Entre
            // os demais, mais trilhas e depois mais longo.
            bool isMixamo = name.ToLowerInvariant().Contains("mixamo");
            bool chosenIsMixamo = chosen.ToLowerInvariant().Contains("mixamo");

            bool better = best == null
                || (isMixamo && !chosenIsMixamo)
                || (isMixamo == chosenIsMixamo
                    && (tracks > mostTracks
                        || (tracks == mostTracks && candidate.Length > longest)));

            if (!better) continue;

            mostTracks = tracks;
            longest = candidate.Length;
            best = candidate;
            chosen = name;
        }

        if (debug && mostTracks == 0)
            GD.PrintErr($"CharacterAnimator: '{clip}' não traz trilha nenhuma — vai dar pose em T.");
        else if (debug)
            GD.Print($"  {clip}: '{chosen}', {mostTracks} trilhas, {longest:0.00}s");

        return best?.Duplicate() as Animation;
    }

    /// <summary>
    /// Mata o deslocamento horizontal da raiz, preservando o vertical.
    ///
    /// Oito dos clipes andam de verdade: a caminhada viaja 1,36 m em
    /// 1,37 s, a corrida 2,21 m em 0,70 s. Como o CharacterBody3D já
    /// calcula o deslocamento, deixar isso ligado soma dois movimentos
    /// e o boneco patina.
    ///
    /// O eixo Y fica: é ele que dá o peso do passo e os 32 cm de
    /// subida do pulo. Por isso as chaves são achatadas em X e Z em
    /// vez da trilha ser removida.
    ///
    /// O osso é encontrado por busca, e não por nome fixo, porque o
    /// importador reescreve nome de osso e o caminho exato depende de
    /// uma opção de importação que ninguém vai lembrar de conferir.
    /// </summary>
    private static void StripRootMotion(Animation animation)
    {
        for (int track = 0; track < animation.GetTrackCount(); track++)
        {
            if (animation.TrackGetType(track) != Animation.TrackType.Position3D)
                continue;

            if (!animation.TrackGetPath(track).ToString().Contains("Hips"))
                continue;

            int keys = animation.TrackGetKeyCount(track);
            if (keys == 0) continue;

            var first = (Vector3)animation.TrackGetKeyValue(track, 0);

            for (int key = 0; key < keys; key++)
            {
                var value = (Vector3)animation.TrackGetKeyValue(track, key);
                animation.TrackSetKeyValue(track, key,
                    new Vector3(first.X, value.Y, first.Z));
            }
        }
    }

    /// <summary>Move o corpo para uma camada própria e tira essa
    /// camada da máscara da câmera do dono.</summary>
    private void HideFromOwner()
    {
        var camera = GetNodeOrNull<Camera3D>(OwnerCameraPath);
        if (camera == null) return;

        uint bit = 1u << (BodyLayer - 1);

        SetLayer(_model, bit);
        camera.CullMask &= ~bit;
    }

    private static VisualInstance3D FirstVisual(Node node)
    {
        if (node is VisualInstance3D visual) return visual;

        foreach (Node child in node.GetChildren())
            if (FirstVisual(child) is VisualInstance3D inner) return inner;

        return null;
    }

    private static void SetLayer(Node node, uint bit)
    {
        if (node is VisualInstance3D visual) visual.Layers = bit;

        foreach (Node child in node.GetChildren())
            SetLayer(child, bit);
    }

    private static AnimationPlayer FindPlayer(Node root)
    {
        if (root is AnimationPlayer found) return found;

        foreach (Node child in root.GetChildren())
        {
            AnimationPlayer inner = FindPlayer(child);
            if (inner != null) return inner;
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Árvore

    /// <summary>
    /// Locomoção num espaço de mistura 2D, e o resto como estados.
    ///
    /// A caminhada fica no raio 1 e a corrida no raio 2 do mesmo
    /// espaço, então acelerar mistura os dois sozinho, sem estado
    /// extra e sem limiar onde o boneco troca de perna de repente.
    /// </summary>
    private void BuildTree()
    {
        var locomotion = new AnimationNodeBlendSpace2D
        {
            MinSpace = new Vector2(-2.2f, -2.2f),
            MaxSpace = new Vector2(2.2f, 2.2f)
        };

        // Pelo nome do motor, e não pela propriedade do binding, que
        // foi renomeada e volta a quebrar na próxima renomeação.
        //
        // Sincronizar mantém caminhada e corrida em fase enquanto se
        // misturam. Sem isso os dois clipes correm cada um no seu
        // tempo e o pé do boneco pisa duas vezes no mesmo passo.
        locomotion.Set("sync", true);

        Point(locomotion, "idle_rifle", 0, 0);
        Point(locomotion, "walk_fwd", 0, 1);
        Point(locomotion, "run_fwd", 0, 2);
        Point(locomotion, "walk_back", 0, -1);
        Point(locomotion, "run_back", 0, -2);
        Point(locomotion, "strafe_right", 1, 0);
        Point(locomotion, "strafe_left", -1, 0);

        var machine = new AnimationNodeStateMachine();
        machine.AddNode("Locomotion", locomotion, new Vector2(300, 100));

        AddCrouch(machine);
        Single(machine, "Jump", "jump", new Vector2(520, -20));
        Single(machine, "Fall", "fall", new Vector2(720, 40));
        Single(machine, "Land", "land", new Vector2(720, 150));
        Single(machine, "Hit", "hit", new Vector2(520, 160));
        Single(machine, "Death", "death", new Vector2(520, 280));

        // O ar é uma sequência: salta, cai, aterrissa. O salto entrega
        // para a queda sozinho quando acaba; a queda só sai quando o
        // chão aparece, e por isso é ela que repete, não o salto.
        Link(machine, "Locomotion", "Jump");
        Link(machine, "Jump", "Fall", auto: true);
        Link(machine, "Locomotion", "Fall");
        Link(machine, "Fall", "Land");
        Link(machine, "Land", "Locomotion", auto: true);
        Link(machine, "Fall", "Locomotion");

        Link(machine, "Locomotion", "Crouch");
        Link(machine, "Crouch", "Locomotion");

        Link(machine, "Locomotion", "Hit");
        Link(machine, "Hit", "Locomotion", auto: true);

        // Morrer interrompe qualquer coisa, e é por isso que precisa
        // de uma aresta vinda de cada estado: Travel anda pelo grafo,
        // e estado sem caminho de entrada é estado inalcançável.
        foreach (string from in new[] { "Locomotion", "Crouch", "Jump", "Fall", "Land", "Hit" })
            Link(machine, from, "Death");

        _tree = new AnimationTree { TreeRoot = machine };

        // A ordem importa. O caminho do tocador é relativo à própria
        // árvore, então ele só pode ser calculado depois que ela está
        // na cena; e ativar antes de ter tocador deixa a árvore ligada
        // apontando para nada.
        _model.AddChild(_tree);

        _tree.AnimPlayer = _tree.GetPathTo(_player);
        _tree.Active = true;

        _playback = (AnimationNodeStateMachinePlayback)
            _tree.Get("parameters/playback");

        _playback?.Start("Locomotion");
    }

    private void Point(AnimationNodeBlendSpace2D space, string clip, float x, float y)
    {
        if (!_player.HasAnimation($"alcateia/{clip}")) return;

        space.AddBlendPoint(
            new AnimationNodeAnimation { Animation = $"alcateia/{clip}" },
            new Vector2(x, y));
    }

    /// <summary>
    /// Agachado: parado e andando na mesma mistura.
    ///
    /// Vira estado único com mistura interna, e não dois estados, pelo
    /// mesmo motivo da locomoção — transição entre estados tem tempo
    /// de cruzamento, e começar a andar agachado não é um evento, é um
    /// contínuo. Sem o clipe de andar, sobra o parado e o estado
    /// continua valendo.
    /// </summary>
    private void AddCrouch(AnimationNodeStateMachine machine)
    {
        bool hasIdle = _player.HasAnimation("alcateia/crouch_idle");
        bool hasWalk = _player.HasAnimation("alcateia/crouch_walk");

        if (!hasIdle && !hasWalk) return;

        if (!hasWalk)
        {
            Single(machine, "Crouch", "crouch_idle", new Vector2(300, 240));
            return;
        }

        var crouch = new AnimationNodeBlendSpace1D { MinSpace = 0f, MaxSpace = 1f };
        crouch.Set("sync", true);

        if (hasIdle)
            crouch.AddBlendPoint(
                new AnimationNodeAnimation { Animation = "alcateia/crouch_idle" }, 0f);

        crouch.AddBlendPoint(
            new AnimationNodeAnimation { Animation = "alcateia/crouch_walk" }, 1f);

        machine.AddNode("Crouch", crouch, new Vector2(300, 240));
    }

    private void Single(AnimationNodeStateMachine machine, string state,
        string clip, Vector2 position)
    {
        if (!_player.HasAnimation($"alcateia/{clip}")) return;

        machine.AddNode(state,
            new AnimationNodeAnimation { Animation = $"alcateia/{clip}" },
            position);
    }

    private static void Link(AnimationNodeStateMachine machine,
        string from, string to, bool auto = false)
    {
        if (!machine.HasNode(from) || !machine.HasNode(to)) return;

        // Volta automática espera o clipe acabar; ida é imediata,
        // porque pulo que começa meio segundo depois do salto não é
        // animação, é atraso.
        var transition = new AnimationNodeStateMachineTransition
        {
            XfadeTime = 0.15f,
            SwitchMode = auto
                ? AnimationNodeStateMachineTransition.SwitchModeEnum.AtEnd
                : AnimationNodeStateMachineTransition.SwitchModeEnum.Immediate,
            AdvanceMode = auto
                ? AnimationNodeStateMachineTransition.AdvanceModeEnum.Auto
                : AnimationNodeStateMachineTransition.AdvanceModeEnum.Enabled
        };

        machine.AddTransition(from, to, transition);
    }

    // ------------------------------------------------------------------
    // Estado por quadro

    public override void _Process(double delta)
    {
        if (_dead)
        {
            UpdateCorpse(delta);
            return;
        }

        if (_tree == null) return;

        // Velocidade no referencial do corpo: é o que o espaço de
        // mistura entende. Em referencial do mundo, virar a câmera
        // trocaria a animação sem o personagem mudar de direção.
        Vector3 local = _body.GlobalTransform.Basis.Inverse() * _body.Velocity;

        var target = new Vector2(
            local.X / Mathf.Max(WalkReference, 0.01f),
            -local.Z / Mathf.Max(WalkReference, 0.01f));

        if (InvertStrafe) target.X = -target.X;

        target = target.LimitLength(2.1f);

        _blend = _blend.Lerp(target, Mathf.Min(1f, BlendSmoothing * (float)delta));

        _tree.Set("parameters/Locomotion/blend_position", _blend);

        if (_hitTimer > 0) _hitTimer -= delta;

        UpdateAir(delta);
        UpdateStance();
        UpdateCrouchBlend(delta);

        Position = new Vector3(Position.X, BaseY, Position.Z);

        ReportState();
    }

    /// <summary>
    /// Agachado é um estado à parte, não um ponto do espaço de
    /// mistura: a pose não é a caminhada mais devagar, é outra postura.
    ///
    /// Sem o clipe de andar agachado, mover-se agachado mostra a pose
    /// parada. É feio e é honesto — melhor do que o corpo levantar
    /// sozinho porque o jogador andou.
    /// </summary>
    private void UpdateStance()
    {
        if (_airborne) return;

        bool crouching = _owner != null && _owner.IsCrouching;
        if (crouching == _crouched) return;

        _crouched = crouching;
        _playback?.Travel(crouching ? "Crouch" : "Locomotion");
    }

    /// <summary>Velocidade do agachado, de parado a andando.</summary>
    private void UpdateCrouchBlend(double delta)
    {
        if (!_crouched) return;

        Vector3 flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
        float target = Mathf.Clamp(flat.Length() / Mathf.Max(CrouchWalkSpeed, 0.01f), 0f, 1f);

        _crouchBlend = Mathf.MoveToward(_crouchBlend, target, 4f * (float)delta);
        _tree.Set("parameters/Crouch/blend_position", _crouchBlend);
    }

    /// <summary>
    /// Diz no log cada vez que o estado muda.
    ///
    /// Vale mais do que parece: a árvore erra em silêncio. Estado sem
    /// aresta de entrada, estado sem gatilho e clipe que não carregou
    /// dão todos o mesmo sintoma na tela — o boneco continua na pose
    /// anterior — e nenhum deles gera erro. Ver o nome do estado
    /// separa os três em um teste.
    /// </summary>
    private void ReportState()
    {
        if (!Debug || _playback == null) return;

        string now = _playback.GetCurrentNode().ToString();
        if (now == _lastState) return;

        _lastState = now;

        // A visibilidade e a camada entram no relatório porque o
        // estado certo com o corpo fora de vista dá o mesmo sintoma do
        // estado errado: nada muda na tela.
        uint layers = 0;
        if (FirstVisual(_model) is VisualInstance3D first) layers = first.Layers;

        GD.Print($"{_body.Name}: estado '{now}'  chão {_body.IsOnFloor()}" +
                 $"  visível {Visible && _model.Visible}  camada {layers}" +
                 $"  forma {(_form != null && _form.IsWolf ? "lobo" : "humano")}");
    }

    /// <summary>
    /// Entra e sai do ar sozinho.
    ///
    /// Isto continua sendo leitura de estado, não decisão: quem
    /// decide que o corpo está no ar é a física. O animador só
    /// percebe a borda — e precisa percebê-la, porque cair de um
    /// telhado não passa por Jump() e sem isso o boneco desceria os
    /// dezessete metros do Arrival andando.
    /// </summary>
    private void UpdateAir(double delta)
    {
        if (_jumpGrace > 0) _jumpGrace -= delta;

        bool grounded = _body.IsOnFloor();

        if (!grounded && !_airborne)
        {
            _airborne = true;

            // Só vai para a queda se não estiver saltando: o salto
            // entrega para ela sozinho quando termina.
            if (_jumpGrace <= 0 && _playback?.GetCurrentNode().ToString() != "Jump")
                _playback?.Travel("Fall");
        }
        else if (grounded && _airborne)
        {
            _airborne = false;
            _crouched = false;

            // Pousa se houver clipe de pouso; o estado devolve para a
            // locomoção sozinho quando acaba.
            _playback?.Travel(_player.HasAnimation("alcateia/land") ? "Land" : "Locomotion");
        }
    }

    /// <summary>
    /// Dispara o estado do pulo. Chamado por quem pula, e não
    /// adivinhado aqui a partir de estar no ar — sair do chão também
    /// acontece ao cair de um telhado, e as duas coisas não são a
    /// mesma animação.
    ///
    /// A carência existe porque o pulo e a saída do chão acontecem no
    /// mesmo quadro: sem ela, a detecção de ar via o corpo subindo e
    /// mandava para a queda antes de o pulo aparecer.
    /// </summary>
    public void Jump()
    {
        if (_dead) return;

        _jumpGrace = 0.35;
        _playback?.Travel("Jump");

        if (Debug && _playback == null)
            GD.Print($"{_body.Name}: Jump() chamado sem árvore montada");
    }

    /// <summary>Reação a dano. Só interrompe a locomoção, e só de vez
    /// em quando — ver <see cref="HitCooldown"/>.</summary>
    public void Hit()
    {
        if (_dead || _airborne || _hitTimer > 0) return;

        _hitTimer = HitCooldown;
        _playback?.Travel("Hit");
    }

    private void OnDied()
    {
        _dead = true;
        _corpseTimer = 0;

        if (_hasDeath) _playback?.Travel("Death");
        else if (Debug) GD.Print("CharacterAnimator: sem clipe de morte.");
    }

    /// <summary>
    /// O corpo fica um instante, depois afunda e some.
    ///
    /// A pausa antes é o que faz a morte ser lida: sumir no mesmo
    /// quadro em que cai tira do jogador a chance de ver o que
    /// aconteceu, e quem matou perde a confirmação do abate.
    ///
    /// Afundar junto com a opacidade esconde o instante da saída. Só
    /// apagar deixa o corpo piscar para fora, e isso o olho pega.
    /// </summary>
    private void UpdateCorpse(double delta)
    {
        _corpseTimer += delta;

        // Nada acontece enquanto o corpo cai. Só depois de pousado é
        // que começa a contar o tempo em que ele fica à mostra.
        double grounded = _corpseTimer - _deathLength;
        if (grounded < 0) return;

        float settle = Mathf.Clamp((float)grounded / 0.25f, 0f, 1f);

        double t = (grounded - CorpseHold) / Mathf.Max(CorpseFade, 0.01f);
        float progress = t <= 0 ? 0f : Mathf.Clamp((float)t, 0f, 1f);

        // Só o afundar da morte. A altura dos pés é do nó de cima.
        _model.Position = new Vector3(
            _model.Position.X,
            -CorpseDrop * settle - CorpseSink * progress,
            _model.Position.Z);

        if (progress <= 0) return;

        if (_fadeMaterials.Count == 0) PrepareFade();

        foreach (StandardMaterial3D material in _fadeMaterials)
        {
            Color color = material.AlbedoColor;
            color.A = 1f - progress;
            material.AlbedoColor = color;
        }

        if (progress >= 1f) _model.Visible = false;
    }

    /// <summary>
    /// Prepara os materiais para receber opacidade.
    ///
    /// Duplicar é obrigatório: o material vem da importação e é
    /// compartilhado por todos os bots da cena. Sem a cópia, matar um
    /// faz os outros desbotarem junto — e esse é o tipo de defeito que
    /// se procura no código de morte, que está certo, em vez de no
    /// material, que é onde está.
    /// </summary>
    private void PrepareFade()
    {
        Collect(_model);

        void Collect(Node node)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                {
                    if (mesh.GetActiveMaterial(surface) is not StandardMaterial3D source)
                        continue;

                    var copy = (StandardMaterial3D)source.Duplicate();
                    copy.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

                    mesh.SetSurfaceOverrideMaterial(surface, copy);
                    _fadeMaterials.Add(copy);
                }
            }

            foreach (Node child in node.GetChildren()) Collect(child);
        }
    }

    private void OnRespawned()
    {
        _dead = false;
        _corpseTimer = 0;
        _blend = Vector2.Zero;
        _crouched = false;
        _airborne = false;

        // Volta tudo: um corpo que renasce meio transparente e meio
        // enterrado é o mesmo defeito, só adiado.
        foreach (StandardMaterial3D material in _fadeMaterials)
        {
            Color color = material.AlbedoColor;
            color.A = 1f;
            material.AlbedoColor = color;
        }

        _model.Position = new Vector3(_model.Position.X, 0, _model.Position.Z);
        _model.Visible = true;

        _playback?.Start("Locomotion");
    }

    /// <summary>O corpo humano some quando o dono vira lobo.</summary>
    private void Refresh()
    {
        if (_form != null) Visible = !(HideInWolfForm && _form.IsWolf);
    }
}
