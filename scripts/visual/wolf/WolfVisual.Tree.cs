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
		var gait = new AnimationNodeBlendSpace1D { MinSpace = 0f, MaxSpace = 1f };
		gait.Set("sync", true);
		if (Has("walk")) gait.AddBlendPoint(Clip("walk"), 0f, -1, "walk");
		if (Has("run")) gait.AddBlendPoint(Clip("run"), 1f, -1, "run");

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
		if (Has("wallrun"))
		{
			locoSpeed.AddNode("Crawl", Clip("wallrun"), new Vector2(0, 300));
			locoSpeed.AddNode("CrawlSpeed", new AnimationNodeTimeScale(), new Vector2(200, 300));
			locoSpeed.ConnectNode("CrawlSpeed", 0, "Crawl");
			locoSpeed.ConnectNode("GaitCrouch", 1, "CrawlSpeed");
		}
		locoSpeed.ConnectNode("Mix", 0, "IdleCrouch");
		locoSpeed.ConnectNode("Mix", 1, "GaitCrouch");
		locoSpeed.ConnectNode("output", 0, "Mix");

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
				FilterUpperBody(oneShot, clip);
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
}
