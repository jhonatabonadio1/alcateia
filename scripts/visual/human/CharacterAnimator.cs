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
///
/// Dividido em montar e tocar: CharacterAnimator.Model (clipes, modelo
/// do time, arma) e CharacterAnimator.Tree (a árvore) rodam uma vez no
/// _Ready; CharacterAnimator.Locomotion, .Actions e .Death leem o estado
/// a cada quadro. As ferramentas que o lobo também usa estão no
/// <see cref="AnimationKit"/>.
/// </summary>
public partial class CharacterAnimator : Node3D
{
	/// <summary>Pasta dos clipes importados.</summary>
	[ExportGroup("Modelo")]
	[Export(PropertyHint.Dir)]
	public string ClipFolder = "res://assets/characters/human";

	/// <summary>Caminho do modelo instanciado, a partir deste nó.</summary>
	[Export] public NodePath ModelPath = "Model";

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
	/// Esconder o corpo humano quando o dono vira lobo.
	///
	/// Ligado desde que o lobo tem corpo próprio (o Mutant, em
	/// <see cref="WolfVisual"/>). Desligado, o soldado aparece por
	/// dentro do lobo — braço e arma atravessando a pele.
	/// </summary>
	[Export] public bool HideInWolfForm = true;

	/// <summary>Imprime os clipes carregados e as trocas de estado.</summary>
	[ExportGroup("Diagnóstico")]
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

	private CharacterBody3D _body;
	private Player _owner;
	private Health _health;
	private FormController _form;

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

	/// <summary>O corpo humano some quando o dono vira lobo.</summary>
	private void Refresh()
	{
		if (_form != null) Visible = !(HideInWolfForm && _form.IsWolf);
	}
}
