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

    [Export] public bool Debug = true;

    /// <summary>Arquivo do clipe, e o nome com que ele entra na
    /// biblioteca. O arquivo é a única fonte de nome confiável: todos
    /// os takes dentro dos FBX do Mixamo se chamam "mixamo.com".</summary>
    private static readonly string[] Clips =
    {
        "idle_rifle", "walk_fwd", "walk_back", "run_fwd", "run_back",
        "strafe_left", "strafe_right", "jump", "fire", "reload",
        "grenade", "hit", "turn_left", "turn_right", "death"
    };

    private Node3D _model;
    private AnimationPlayer _player;
    private AnimationTree _tree;
    private AnimationNodeStateMachinePlayback _playback;

    private CharacterBody3D _body;
    private Health _health;
    private FormController _form;

    private Vector2 _blend;
    private bool _dead;
    private bool _hasDeath;

    public override void _Ready()
    {
        _body = GetParent<CharacterBody3D>();
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
        BuildTree();

        _model.RotationDegrees = new Vector3(
            _model.RotationDegrees.X, ModelYaw, _model.RotationDegrees.Z);

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
        }

        if (_form != null)
        {
            _form.FormChanged += _ => Refresh();
            Refresh();
        }

        if (Debug)
            GD.Print($"CharacterAnimator: {loaded} clipes carregados" +
                     (_hasDeath ? "" : " (sem clipe de morte)"));
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

            Animation animation = TakeAnimation(source);

            if (animation != null)
            {
                StripRootMotion(animation);

                // Locomoção precisa repetir; golpe e morte, não.
                animation.LoopMode = IsLooping(clip)
                    ? Animation.LoopModeEnum.Linear
                    : Animation.LoopModeEnum.None;

                library.AddAnimation(clip, animation);
                count++;

                if (clip == "death") _hasDeath = true;
            }

            temp.QueueFree();
        }

        _player.AddAnimationLibrary("alcateia", library);
        return count;
    }

    private static bool IsLooping(string clip) =>
        clip.StartsWith("idle") || clip.StartsWith("walk") ||
        clip.StartsWith("run") || clip.StartsWith("strafe");

    /// <summary>A primeira animação de verdade do tocador de origem.
    /// O importador deixa um "Take 001" vazio junto do clipe real.</summary>
    private static Animation TakeAnimation(AnimationPlayer source)
    {
        if (source == null) return null;

        Animation best = null;
        double longest = 0;

        foreach (StringName name in source.GetAnimationList())
        {
            Animation candidate = source.GetAnimation(name);
            if (candidate == null || candidate.Length <= longest) continue;

            longest = candidate.Length;
            best = candidate;
        }

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

        Single(machine, "Jump", "jump", new Vector2(520, 40));
        Single(machine, "Hit", "hit", new Vector2(520, 160));

        if (_hasDeath)
            Single(machine, "Death", "death", new Vector2(520, 280));

        Link(machine, "Locomotion", "Jump");
        Link(machine, "Jump", "Locomotion", auto: true);
        Link(machine, "Locomotion", "Hit");
        Link(machine, "Hit", "Locomotion", auto: true);

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
        if (_tree == null || _dead) return;

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
    }

    /// <summary>Dispara o estado do pulo. Chamado por quem pula, não
    /// adivinhado aqui a partir de estar no ar.</summary>
    public void Jump() => _playback?.Travel("Jump");

    /// <summary>Reação a dano. Só interrompe a locomoção.</summary>
    public void Hit() => _playback?.Travel("Hit");

    private void OnDied()
    {
        _dead = true;

        if (_hasDeath) _playback?.Travel("Death");
        else if (Debug) GD.Print("CharacterAnimator: sem clipe de morte.");
    }

    private void OnRespawned()
    {
        _dead = false;
        _blend = Vector2.Zero;
        _playback?.Start("Locomotion");
    }

    /// <summary>O corpo humano some quando o dono vira lobo.</summary>
    private void Refresh()
    {
        if (_form != null) Visible = !_form.IsWolf;
    }
}
