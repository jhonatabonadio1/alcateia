using Godot;
using static AnimationKit;
using System.Collections.Generic;

/// <summary>
/// A árvore de animação, montada por código uma vez no _Ready: locomoção
/// num espaço de mistura 2D, pulo, queda, agachado, reação, morte, e por
/// cima a recarga e a granada só no tronco. Montar aqui e mexer por quadro
/// nos outros arquivos é a divisão: quem quiser um estado novo vem aqui.
/// </summary>
public partial class CharacterAnimator
{
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
	[ExportGroup("Árvore")]
	[Export] public float CrouchWalkTrim = 1.65f;

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
}
