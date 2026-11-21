using Godot;
using static AnimationKit;
using System.Collections.Generic;

/// <summary>
/// A árvore do lobo, montada uma vez: locomoção, parede, pulo e morte
/// numa máquina, e os disparos (garra, salto, bote, rugido) por cima, na
/// ordem da pilha.
/// </summary>
public partial class WolfVisual
{
	/// <summary>Disparos que tomam o corpo inteiro. As garradas ficam só
	/// no tronco: o "swiping" gira o quadril 90° e terminaria o golpe
	/// com o lobo de lado, voltando num tranco.</summary>
	private static bool FullBody(string shot) => shot is "pounce" or "dive" or "dive_land" or "jump_land" or "howl";

	/// <summary>Entrada e saída de cada disparo, em segundos. A garrada
	/// entra seca: a garra chega 0,14 s depois do clique, e uma entrada
	/// mais longa comeria o golpe. Sai curta, porque a próxima vem logo.
	/// A aterrissagem do bote também entra quase seca: é o impacto.</summary>
	/// <summary>Mistura de entrada do bote sobre o pulo: dois quadros.</summary>
	public const float DiveFadeIn = 0.03f;

	private static (float In, float Out) Fades(string shot) => shot switch
	{
		"claw" or "heavy" => (0.06f, 0.1f),
		"howl" => (0.15f, 0.2f),
		"jump_land" => (0.05f, 0.25f),
		"pounce" => (0.12f, 0.25f),
		// O bote corta o pulo quase seco: em 0,08 s de mistura, num bote
		// curto (10 passos), metade do golpe saía ainda com cara de pulo
		// (Jhon, 21/11: "menos delay e quase cancelar a animação").
		"dive" => (DiveFadeIn, 0.15f),
		"dive_land" => (0.05f, 0.25f),
		_ => (0.08f, 0.2f)
	};

	/// <summary>
	/// Máquina de estados para o corpo inteiro (locomoção, uivo, parede,
	/// morte) e os disparos por cima (<see cref="Shots"/>): as garradas e
	/// o rugido só da cintura para cima, o salto e o bote no corpo
	/// inteiro.
	/// </summary>
	private void BuildTree()
	{
		// Parado e andando em ramos separados: o parado respira no ritmo
		// dele, e o passo toca na velocidade do corpo — zero quando ele
		// para, negativa andando para trás. Na mesma escala, o parado
		// congelava junto ou o passo corria no lugar.
		//
		// Com os quatro andares do Jhon (frente, trás, esquerda, direita) o
		// passo é um espaço 2D pela direção, de frente para a mira; sem eles,
		// andar e correr do Mixamo pela velocidade, virando o corpo.
		AnimationRootNode gait;
		if (Directional)
		{
			var directions = new AnimationNodeBlendSpace2D
			{
				MinSpace = new Vector2(-1, -1),
				MaxSpace = new Vector2(1, 1),
				BlendMode = AnimationNodeBlendSpace2D.BlendModeEnum.Interpolated
			};
			// Os quatro têm o mesmo ciclo (0,67 s) mas passadas diferentes:
			// sem sync, cada um no seu ritmo, e a mistura só existe na troca
			// de direção.
			directions.Set("sync", false);
			directions.AddBlendPoint(Clip("walk"), new Vector2(0, 1), -1, "walk");
			directions.AddBlendPoint(Clip("walk_back"), new Vector2(0, -1), -1, "walk_back");
			directions.AddBlendPoint(Clip("walk_left"), new Vector2(-1, 0), -1, "walk_left");
			directions.AddBlendPoint(Clip("walk_right"), new Vector2(1, 0), -1, "walk_right");
			gait = directions;
		}
		else
		{
			var speeds = new AnimationNodeBlendSpace1D { MinSpace = 0f, MaxSpace = 1f };
			speeds.Set("sync", true);
			if (Has("walk")) speeds.AddBlendPoint(Clip("walk"), 0f, -1, "walk");
			if (Has("run")) speeds.AddBlendPoint(Clip("run"), 1f, -1, "run");
			gait = speeds;
		}

		// Agachado entra por cima dos dois ramos. Parado, a pose agachada
		// (0,98 m, a altura da cápsula). Andando, o lobo engatinha: o
		// "Running Crawl" da parede, no chão, na velocidade do corpo.
		// Testado antes: misturar a pose agachada no andar deixava o pé sem
		// apoio (patinava 0,9 a 1,4 m/s em qualquer passada), e só no
		// tronco a cabeça ficava a 1,58 m — de pé, fora da cápsula.
		var locoSpeed = new AnimationNodeBlendTree();
		if (Has("idle")) locoSpeed.AddNode("Idle", Clip("idle"), Vector2.Zero);
		locoSpeed.AddNode("Gait", gait, new Vector2(0, 200));
		locoSpeed.AddNode("Speed", new AnimationNodeTimeScale(), new Vector2(200, 200));
		locoSpeed.AddNode("IdleCrouch", new AnimationNodeBlend2(), new Vector2(200, 50));
		locoSpeed.AddNode("GaitCrouch", new AnimationNodeBlend2(), new Vector2(400, 250));
		locoSpeed.AddNode("Mix", new AnimationNodeBlend2(), new Vector2(600, 100));
		locoSpeed.ConnectNode("Speed", 0, "Gait");
		if (Has("idle")) locoSpeed.ConnectNode("IdleCrouch", 0, "Idle");
		locoSpeed.ConnectNode("GaitCrouch", 0, "Speed");
		if (Has("crouch"))
		{
			locoSpeed.AddNode("CrouchIdle", Clip("crouch"), new Vector2(0, 100));
			locoSpeed.ConnectNode("IdleCrouch", 1, "CrouchIdle");
		}
		// O engatinhar é o andar agachado do Jhon (crawl); sem ele, o
		// "Running Crawl" da parede.
		string crawlClip = Has("crawl") ? "crawl" : "wallrun";
		if (Has(crawlClip))
		{
			locoSpeed.AddNode("Crawl", Clip(crawlClip), new Vector2(0, 300));
			locoSpeed.AddNode("CrawlSpeed", new AnimationNodeTimeScale(), new Vector2(200, 300));
			locoSpeed.ConnectNode("CrawlSpeed", 0, "Crawl");
			locoSpeed.ConnectNode("GaitCrouch", 1, "CrawlSpeed");
		}
		// Corrida de quatro (W duas vezes): o clipe dela por cima de tudo,
		// na velocidade do corpo. Sem ele, o "Running Crawl" de antes.
		string moving = "GaitCrouch";
		if (Has("sprint"))
		{
			// A corrida de quatro começa sempre do começo do clipe
			// (SprintSeek): é assim que o som da passada cai no quadro em
			// que as patas se juntam (SprintBeat).
			locoSpeed.AddNode("Sprint", Clip("sprint"), new Vector2(0, 400));
			locoSpeed.AddNode("SprintSeek", new AnimationNodeTimeSeek(), new Vector2(100, 400));
			locoSpeed.AddNode("SprintSpeed", new AnimationNodeTimeScale(), new Vector2(200, 400));
			locoSpeed.AddNode("GaitSprint", new AnimationNodeBlend2(), new Vector2(500, 300));
			locoSpeed.ConnectNode("SprintSeek", 0, "Sprint");
			locoSpeed.ConnectNode("SprintSpeed", 0, "SprintSeek");
			locoSpeed.ConnectNode("GaitSprint", 0, "GaitCrouch");
			locoSpeed.ConnectNode("GaitSprint", 1, "SprintSpeed");
			moving = "GaitSprint";
		}
		locoSpeed.ConnectNode("Mix", 0, "IdleCrouch");
		locoSpeed.ConnectNode("Mix", 1, moving);
		locoSpeed.ConnectNode("output", 0, "Mix");

		var machine = new AnimationNodeStateMachine();
		machine.AddNode("Locomotion", locoSpeed, new Vector2(200, 100));
		if (Has("death")) machine.AddNode("Death", Clip("death"), new Vector2(600, 100));

		if (Has("jump_air"))
		{
			// O pulo do Jhon: o voo em laço, na velocidade dele, e o impulso
			// (Jump_Start) por cima quando o lobo sai do chão pulando — não
			// caindo de uma beirada.
			var jump = new AnimationNodeBlendTree();
			jump.AddNode("Air", Clip("jump_air"), Vector2.Zero);
			jump.AddNode("StartShot", new AnimationNodeOneShot { FadeInTime = 0.04f, FadeOutTime = 0.12f }, new Vector2(400, 0));
			if (Has("jump_start"))
			{
				jump.AddNode("Start", Clip("jump_start"), new Vector2(0, 200));
				jump.ConnectNode("StartShot", 1, "Start");
			}
			jump.ConnectNode("StartShot", 0, "Air");
			jump.ConnectNode("output", 0, "StartShot");
			machine.AddNode("Jump", jump, new Vector2(400, 300));
		}
		else if (Has("jump"))
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
				FilterEnabled = !FullBody(shot),
				// O salto e os que ficam acima dele correm mesmo cobertos pelo
				// Charge_Walk (veja ChargeWalk, abaixo).
				Sync = shot is "pounce" or "dive" or "dive_land"
			};

			if (!FullBody(shot))
			{
				Animation clip = _anims.GetAnimation($"lobo/{shot}");
				FilterUpperBody(oneShot, clip);
			}

			string name = Cap(shot);
			root.AddNode($"{name}Clip", Clip(shot), Vector2.Zero);
			root.AddNode($"{name}Speed", new AnimationNodeTimeScale(), Vector2.Zero);
			root.AddNode(name, oneShot, Vector2.Zero);
			if (shot == "pounce")
			{
				// O especial começa o clipe no impulso (WolfVisual.OnMove).
				root.AddNode($"{name}Seek", new AnimationNodeTimeSeek(), Vector2.Zero);
				root.ConnectNode($"{name}Seek", 0, $"{name}Clip");
				root.ConnectNode($"{name}Speed", 0, $"{name}Seek");
			}
			else
				root.ConnectNode($"{name}Speed", 0, $"{name}Clip");
			root.ConnectNode(name, 0, last);
			root.ConnectNode(name, 1, $"{name}Speed");
			last = name;
		}

		// Andando com o salto carregado: o Charge_Walk (agachado na pose da
		// carga, passos curtos) por cima dela, no passo do corpo. Clipe
		// próprio, fora da locomoção (que está embaixo do salto): soltou, o
		// salto segue de onde parou.
		if (Has("charge_walk"))
		{
			root.AddNode("ChargeWalkClip", Clip("charge_walk"), Vector2.Zero);
			root.AddNode("ChargeWalkSpeed", new AnimationNodeTimeScale(), Vector2.Zero);
			// Sync (aqui e nos disparos do salto para cima): com o
			// Charge_Walk cheio, o salto embaixo dele continua correndo até
			// o fim do agachar. Sem isso, quem apertava o direito e já saía
			// andando (ou andava e apertava) congelava o salto no começo do
			// agachar, e ao parar de andar o lobo ficava em pé carregando
			// (Jhon, 16/11).
			root.AddNode("ChargeWalk", new AnimationNodeBlend2 { Sync = true }, Vector2.Zero);
			root.ConnectNode("ChargeWalkSpeed", 0, "ChargeWalkClip");
			root.ConnectNode("ChargeWalk", 0, last);
			root.ConnectNode("ChargeWalk", 1, "ChargeWalkSpeed");
			last = "ChargeWalk";
		}

		// Carregando o salto, o lobo fica na pose do fim do agachar e
		// respira nela: a respiração do Idle_Pose, mais funda, somada por
		// cima (Breath_Add; só coluna, ombros, braços e cabeça).
		if (Has("breath_add"))
		{
			root.AddNode("ChargeBreathClip", Clip("breath_add"), Vector2.Zero);
			root.AddNode("ChargeBreathSpeed", new AnimationNodeTimeScale(), Vector2.Zero);
			root.AddNode("ChargeBreath", new AnimationNodeAdd2(), Vector2.Zero);
			root.ConnectNode("ChargeBreathSpeed", 0, "ChargeBreathClip");
			root.ConnectNode("ChargeBreath", 0, last);
			root.ConnectNode("ChargeBreath", 1, "ChargeBreathSpeed");
			last = "ChargeBreath";
		}

		root.ConnectNode("output", 0, last);

		_tree = new AnimationTree { TreeRoot = root };
		_model.AddChild(_tree);
		_tree.AnimPlayer = _tree.GetPathTo(_anims);
		_tree.Active = true;

		if (Has("jump") && !Has("jump_air"))
			_tree.Set("parameters/Machine/Jump/Speed/scale", 0f);

		_playback = (AnimationNodeStateMachinePlayback)_tree.Get("parameters/Machine/playback");
		_playback?.Start("Locomotion");
	}

	private static AnimationNodeAnimation Clip(string clip) => new() { Animation = $"lobo/{clip}" };

	/// <summary>Com os quatro andares do Jhon: o passo vai pela direção, de
	/// frente para a mira.</summary>
	private bool Directional => Has("walk") && Has("walk_back") && Has("walk_left") && Has("walk_right");

	/// <summary>Nome do nó do disparo: "dive_land" vira "DiveLand".</summary>
	private static string Cap(string s)
	{
		var name = new System.Text.StringBuilder();
		foreach (string part in s.Split('_'))
			if (part.Length > 0) name.Append(char.ToUpperInvariant(part[0])).Append(part[1..]);
		return name.ToString();
	}
}
