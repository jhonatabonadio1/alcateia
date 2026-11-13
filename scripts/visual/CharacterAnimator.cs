using Godot;
using static AnimationKit;
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
	/// A passada de cada clipe, em m/s a 1× — medida no quadril do X Bot.
	/// O ponto da mistura sai da velocidade por estes números, trecho a
	/// trecho: a 1,0 m/s para a frente é a caminhada pura, a 3,0 a corrida
	/// pura, e no meio a mistura anda o que o corpo anda. O clipe nunca
	/// acelera (decisão do Jhon): quem acompanha as pernas é o corpo.
	/// Antes a caminhada ficava em 2,1 m/s e a corrida em 4,2, e o pé
	/// patinava o dobro na caminhada.
	/// </summary>
	[Export] public float WalkClip = 1.0f;
	[Export] public float RunClip = 3.02f;
	[Export] public float BackWalkClip = 1.1f;
	[Export] public float BackRunClip = 2.59f;
	[Export] public float StrafeClip = 1.03f;

	/// <summary>
	/// Inverte o eixo lateral do espaço de mistura.
	///
	/// Os clipes de strafe do Mixamo vieram com o nome trocado e
	/// foram renomeados pela medição do deslocamento no arquivo. O
	/// que a medição não resolve é a conversão de eixo do importador,
	/// que pode espelhar Z. Se no jogo o strafe sair para o lado
	/// errado, é só marcar isto — os pares estão certos entre si.
	///
	/// Marcado: medido no pé apoiado, sem isto o strafe tocava ao
	/// contrário — o pé andava junto com o corpo, 4 m/s de patinada.
	/// </summary>
	[Export] public bool InvertStrafe = true;

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
	/// Ligado desde que o lobo tem corpo próprio (o Mutant, em
	/// <see cref="WolfVisual"/>). Desligado, o soldado aparece por
	/// dentro do lobo — braço e arma atravessando a pele.
	/// </summary>
	[Export] public bool HideInWolfForm = true;

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

	/// <summary>Quanto da pose de mira em pé entra no tronco agachado: 1 é
	/// o fuzil no ombro, como de pé; 0, o fuzil baixo dos clipes
	/// agachados.</summary>
	[Export(PropertyHint.Range, "0,1")] public float CrouchAim = 1f;

	/// <summary>
	/// Pintar o corpo com a cor do time.
	///
	/// Num quatro contra quatro com o mesmo boneco para todo mundo, a
	/// primeira pergunta de cada encontro é "é dos meus?", e ela precisa
	/// de resposta antes do primeiro tiro. Cor de corpo inteiro responde
	/// a trinta metros; nome em cima da cabeça, não.
	/// </summary>
	[Export] public bool TintByTeam = true;

	/// <summary>
	/// Personagem de cada time. O time azul é o Exo Gray, o laranja é a
	/// Ely — decisão do Jhon. Os dois são do Mixamo, com o mesmo
	/// esqueleto do xbot, então todos os clipes servem sem conversão.
	///
	/// Com personagem próprio o corpo não é pintado: a roupa já diz o
	/// time, e pintar por cima de textura vira lama. Caminho vazio, ou
	/// arquivo que não existe, mantém o xbot pintado da cena.
	/// </summary>
	[Export(PropertyHint.File, "*.fbx,*.glb,*.tscn")]
	public string BlueModel = "res://assets/characters/human/exo_gray.fbx";

	[Export(PropertyHint.File, "*.fbx,*.glb,*.tscn")]
	public string OrangeModel = "res://assets/characters/human/ely.fbx";

	[Export] public Color BlueBody = new Color(0.22f, 0.45f, 0.95f);
	[Export] public Color OrangeBody = new Color(1f, 0.48f, 0.16f);

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
	private Weapon _weapon;
	private bool _teamModel;
	private bool _hasReload;
	private bool _reloadPlaying;
	private float _reloadLength;

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
	private readonly List<BaseMaterial3D> _fadeMaterials = new();
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
		_weapon = _body.GetNodeOrNull<Weapon>("Weapon");

		_model = GetNodeOrNull<Node3D>(ModelPath);
		if (_model == null)
		{
			GD.PrintErr($"CharacterAnimator: não achei o modelo em '{ModelPath}'.");
			return;
		}

		SwapTeamModel();

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
		ApplyTeamTint();
		AttachWeapon();

		if (_weapon != null)
			_weapon.AmmoChanged += OnAmmoChanged;

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

		// Os clipes são do X Bot, e o quadril deles fica na altura do X
		// Bot (1,04 m). O Exo Gray (0,96) e a Ely (0,98) têm pernas mais
		// curtas: com o quadril do X Bot os pés ficavam 6 a 8 cm acima do
		// chão. Cada clipe é ajustado para o quadril do modelo em uso.
		float modelHips = HipsRestHeight(_model);

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

				float clipHips = HipsRestHeight(temp);
				if (modelHips > 0.01f && clipHips > 0.01f)
					MatchHips(animation, modelHips / clipHips);

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

	/// <summary>
	/// Corpo na cor do time, juntas num tom escuro dela.
	///
	/// O material é duplicado antes de pintar, pelo mesmo motivo do
	/// desbotar da morte: ele vem da importação e é um só para todos os
	/// bots, e pintar o original pintaria os oito da mesma cor.
	///
	/// O desbotar da morte duplica de novo a partir daqui, porque lê o
	/// material ativo — que passa a ser esta cópia já pintada.
	/// </summary>
	private void ApplyTeamTint()
	{
		if (!TintByTeam || _health == null || _teamModel) return;

		Color body = _health.Team == Teams.Blue ? BlueBody : OrangeBody;
		Color joints = body.Darkened(0.6f);

		Paint(_model);

		void Paint(Node node)
		{
			if (node is MeshInstance3D mesh && mesh.Mesh != null)
			{
				bool isJoint = mesh.Name.ToString().ToLowerInvariant().Contains("joint");

				for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
				{
					if (mesh.GetActiveMaterial(surface) is not BaseMaterial3D source) continue;

					var copy = (BaseMaterial3D)source.Duplicate();
					copy.AlbedoColor = isJoint ? joints : body;
					mesh.SetSurfaceOverrideMaterial(surface, copy);
				}
			}

			foreach (Node child in node.GetChildren()) Paint(child);
		}
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

		_tree = new AnimationTree { TreeRoot = WithReload(machine) };

		// A ordem importa. O caminho do tocador é relativo à própria
		// árvore, então ele só pode ser calculado depois que ela está
		// na cena; e ativar antes de ter tocador deixa a árvore ligada
		// apontando para nada.
		_model.AddChild(_tree);

		_tree.AnimPlayer = _tree.GetPathTo(_player);
		_tree.Active = true;

		_playback = (AnimationNodeStateMachinePlayback)
			_tree.Get("parameters/Machine/playback");

		_playback?.Start("Locomotion");
	}

	/// <summary>
	/// Põe a recarga por cima de tudo, só da cintura para cima.
	///
	/// A recarga é um gesto dos braços: as pernas continuam no que
	/// estavam — andando, correndo, agachado. Por isso ela entra como
	/// disparo único filtrado nos ossos do tronco, braços e cabeça, e
	/// não como estado da máquina, que trocaria o corpo inteiro e
	/// pararia o boneco para recarregar.
	/// </summary>
	private AnimationRootNode WithReload(AnimationNodeStateMachine machine)
	{
		var root = new AnimationNodeBlendTree();
		root.AddNode("Machine", machine, new Vector2(0, 0));

		_hasReload = _player.HasAnimation("alcateia/reload");
		if (!_hasReload)
		{
			root.ConnectNode("output", 0, WithGrenade(root, "Machine"));
			return root;
		}

		Animation clip = _player.GetAnimation("alcateia/reload");
		_reloadLength = (float)clip.Length;

		var shot = new AnimationNodeOneShot
		{
			FadeInTime = 0.15f,
			FadeOutTime = 0.25f,
			FilterEnabled = true
		};

		FilterUpperBody(shot, clip);

		root.AddNode("ReloadClip", new AnimationNodeAnimation { Animation = "alcateia/reload" }, new Vector2(-300, 200));
		root.AddNode("ReloadSpeed", new AnimationNodeTimeScale(), new Vector2(-100, 200));
		root.AddNode("Reload", shot, new Vector2(200, 0));

		root.ConnectNode("ReloadSpeed", 0, "ReloadClip");
		root.ConnectNode("Reload", 0, "Machine");
		root.ConnectNode("Reload", 1, "ReloadSpeed");
		root.ConnectNode("output", 0, WithGrenade(root, "Reload"));
		return root;
	}

	// O clipe "grenade" (3,2 s), medido pela mão direita: puxa o braço
	// para trás até 1,60 s (a mão 0,76 m atrás do quadril), solta por cima
	// da cabeça em ~1,9 s e volta. Segurando o botão, o clipe para no
	// braço armado; soltou, o resto toca no dobro, para a mão passar na
	// frente logo depois de a granada sair (2,5×).
	private const float GrenadeFrom = 0.5f;
	private const float GrenadeCocked = 1.6f;
	private const float GrenadeThrowSpeed = 2.5f;

	private bool _hasGrenade;
	private bool _grenadeShot;
	private double _grenadeClock;

	/// <summary>A granada por cima, da cintura para cima, como a recarga:
	/// arma o braço enquanto o botão está seguro e arremessa quando solta.
	/// É o que os outros veem; o próprio jogador vê a mão na tela
	/// (<see cref="ViewModel"/>).</summary>
	private string WithGrenade(AnimationNodeBlendTree root, string below)
	{
		_hasGrenade = _player.HasAnimation("alcateia/grenade");
		if (!_hasGrenade) return below;

		Animation clip = _player.GetAnimation("alcateia/grenade");
		var shot = new AnimationNodeOneShot { FadeInTime = 0.12f, FadeOutTime = 0.25f, FilterEnabled = true };
		FilterUpperBody(shot, clip);

		root.AddNode("GrenadeClip", new AnimationNodeAnimation { Animation = "alcateia/grenade" }, new Vector2(-300, 400));
		root.AddNode("GrenadeSeek", new AnimationNodeTimeSeek(), new Vector2(-200, 400));
		root.AddNode("GrenadeSpeed", new AnimationNodeTimeScale(), new Vector2(-100, 400));
		root.AddNode("Grenade", shot, new Vector2(400, 0));
		root.ConnectNode("GrenadeSeek", 0, "GrenadeClip");
		root.ConnectNode("GrenadeSpeed", 0, "GrenadeSeek");
		root.ConnectNode("Grenade", 0, below);
		root.ConnectNode("Grenade", 1, "GrenadeSpeed");
		return "Grenade";
	}

	private void UpdateGrenade(double delta)
	{
		if (!_hasGrenade || _tree == null || _weapon == null || _dead) return;

		bool ready = _weapon.IsGrenadeReady;
		if (ready && !_grenadeShot)
		{
			_grenadeShot = true;
			_grenadeClock = GrenadeFrom;
			_tree.Set("parameters/GrenadeSeek/seek_request", GrenadeFrom);
			_tree.Set("parameters/GrenadeSpeed/scale", 1f);
			_tree.Set("parameters/Grenade/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
			return;
		}

		if (!_grenadeShot) return;

		if (ready)
		{
			_grenadeClock += delta;
			if (_grenadeClock >= GrenadeCocked) _tree.Set("parameters/GrenadeSpeed/scale", 0f);
			return;
		}

		// Soltou: o resto do arremesso toca rápido e o disparo acaba sozinho.
		_grenadeShot = false;
		_tree.Set("parameters/GrenadeSpeed/scale", GrenadeThrowSpeed);
	}

	/// <summary>
	/// A recarga começa e termina com a da arma. A velocidade do clipe
	/// é ajustada para ele acabar junto com a recarga: a vazia, mais
	/// longa, fica mais lenta na tela também. Na escopeta o clipe toca
	/// uma vez no tempo dele, como gesto de "carregando".
	/// </summary>
	private void OnAmmoChanged(int inMag, int magSize, bool reloading)
	{
		if (!_hasReload || _tree == null || _dead) return;

		if (reloading && !_reloadPlaying)
		{
			_reloadPlaying = true;
			float duration = _weapon.ReloadDuration;
			bool shells = _weapon.Held?.ShellByShell == true;
			float scale = shells || duration <= 0.05f ? 1f : _reloadLength / duration;

			_tree.Set("parameters/ReloadSpeed/scale", Mathf.Clamp(scale, 0.4f, 3f));
			_tree.Set("parameters/Reload/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
			return;
		}

		if (!reloading && _reloadPlaying)
		{
			_reloadPlaying = false;

			// Acabou cheio: deixa o clipe terminar sozinho. Interrompida
			// (trocou de arma, atirou na escopeta): corta.
			if (inMag < magSize) StopReload();
		}
	}

	private void StopReload()
	{
		_reloadPlaying = false;
		if (_hasReload && _tree != null)
			_tree.Set("parameters/Reload/request", (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
	}

	/// <summary>
	/// Troca o xbot da cena pelo personagem do time. Acontece antes de
	/// carregar os clipes, porque eles entram no tocador do modelo que
	/// fica.
	/// </summary>
	private void SwapTeamModel()
	{
		if (_health == null) return;

		string path = _health.Team == Teams.Orange ? OrangeModel : BlueModel;
		if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path)) return;

		if (GD.Load<PackedScene>(path)?.Instantiate() is not Node3D model) return;

		Node3D old = _model;
		model.Name = old.Name;
		model.Transform = old.Transform;
		RemoveChild(old);
		old.QueueFree();
		AddChild(model);

		_model = model;
		_teamModel = true;
	}

	/// <summary>A arma na mão, para os outros verem.</summary>
	private void AttachWeapon()
	{
		if (_weapon == null) return;

		var camera = GetNodeOrNull<Camera3D>(OwnerCameraPath);
		uint layers = camera != null ? 1u << (BodyLayer - 1) : 1u;

		var held = new HeldWeapon { Name = "HeldWeapon" };
		AddChild(held);
		held.Attach(_model, _body, layers);
	}

	private void Point(AnimationNodeBlendSpace2D space, string clip, float x, float y)
	{
		if (!_player.HasAnimation($"alcateia/{clip}")) return;

		space.AddBlendPoint(
			new AnimationNodeAnimation { Animation = $"alcateia/{clip}" },
			new Vector2(x, y), -1, clip);
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

		AnimationRootNode pose;
		if (!hasWalk)
			pose = new AnimationNodeAnimation { Animation = "alcateia/crouch_idle" };
		else
		{
			var space = new AnimationNodeBlendSpace1D { MinSpace = 0f, MaxSpace = 1f };
			space.Set("sync", true);

			if (hasIdle)
				space.AddBlendPoint(
					new AnimationNodeAnimation { Animation = "alcateia/crouch_idle" }, 0f, -1, "crouch_idle");

			space.AddBlendPoint(
				new AnimationNodeAnimation { Animation = "alcateia/crouch_walk" }, 1f, -1, "crouch_walk");
			pose = space;
		}

		// Agachado de arma em punho (decisão do Jhon: o humano agacha e
		// atira). Os clipes agachados seguram o fuzil baixo, na cintura —
		// o tiro saía com a arma apontada para o chão. Da cintura para
		// cima entra a pose de mira do parado em pé (idle_rifle), a mesma
		// de quem atira de pé; as pernas ficam ajoelhadas ou andando.
		var crouch = new AnimationNodeBlendTree();
		crouch.AddNode("Pose", pose, Vector2.Zero);

		if (_player.HasAnimation("alcateia/idle_rifle"))
		{
			var aim = new AnimationNodeBlend2 { FilterEnabled = true };
			Animation clip = _player.GetAnimation("alcateia/idle_rifle");
			FilterUpperBody(aim, clip);

			crouch.AddNode("Aim", new AnimationNodeAnimation { Animation = "alcateia/idle_rifle" }, new Vector2(0, 200));
			crouch.AddNode("AimBlend", aim, new Vector2(200, 100));
			crouch.ConnectNode("AimBlend", 0, "Pose");
			crouch.ConnectNode("AimBlend", 1, "Aim");
			crouch.ConnectNode("output", 0, "AimBlend");
			_crouchAim = true;
		}
		else
			crouch.ConnectNode("output", 0, "Pose");

		_crouchWalk = hasWalk;
		machine.AddNode("Crouch", crouch, new Vector2(300, 240));
	}

	private bool _crouchAim;
	private bool _crouchWalk;

	private void Single(AnimationNodeStateMachine machine, string state,
		string clip, Vector2 position)
	{
		if (!_player.HasAnimation($"alcateia/{clip}")) return;

		machine.AddNode(state,
			new AnimationNodeAnimation { Animation = $"alcateia/{clip}" },
			position);
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

		float ahead = -local.Z;
		var target = new Vector2(
			local.X / Mathf.Max(StrafeClip, 0.01f),
			ahead >= 0f ? Axis(ahead, WalkClip, RunClip) : -Axis(-ahead, BackWalkClip, BackRunClip));

		if (InvertStrafe) target.X = -target.X;

		target = target.LimitLength(2.1f);

		_blend = _blend.Lerp(target, Mathf.Min(1f, BlendSmoothing * (float)delta));

		_tree.Set("parameters/Machine/Locomotion/blend_position", _blend);

		if (_hitTimer > 0) _hitTimer -= delta;

		UpdateAir(delta);
		UpdateStance();
		UpdateCrouchBlend(delta);
		UpdateGrenade(delta);

		Position = new Vector3(Position.X, BaseY, Position.Z);

		ReportState();
	}

	/// <summary>Velocidade para o raio da mistura: 0 parado, 1 na
	/// caminhada, 2 na corrida, em linha reta entre os clipes.</summary>
	private static float Axis(float speed, float walk, float run)
	{
		if (speed <= walk) return speed / Mathf.Max(walk, 0.01f);
		return 1f + (speed - walk) / Mathf.Max(run - walk, 0.01f);
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
		if (_crouchWalk) _tree.Set("parameters/Machine/Crouch/Pose/blend_position", _crouchBlend);
		if (_crouchAim) _tree.Set("parameters/Machine/Crouch/AimBlend/blend_amount", CrouchAim);
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
		StopReload();
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

		foreach (BaseMaterial3D material in _fadeMaterials)
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
					if (mesh.GetActiveMaterial(surface) is not BaseMaterial3D source)
						continue;

					var copy = (BaseMaterial3D)source.Duplicate();
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
		foreach (BaseMaterial3D material in _fadeMaterials)
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
