using Godot;

/// <summary>
/// O que o jogador quer fazer neste quadro, lido uma vez do teclado e do
/// mouse no começo do passo de física. Daí em diante o <see cref="Player"/>
/// só olha para a intenção, nunca para o <see cref="Input"/>.
///
/// É a costura entre quem decide e quem move: hoje quem preenche é o
/// teclado; no multiplayer será o pacote do cliente (o servidor move o
/// corpo a partir da intenção, não da posição que o cliente diz ter), e
/// num replay, a gravação. O corpo não sabe a diferença.
///
/// "Apertou" e "soltou" valem só no quadro em que aconteceram;
/// "segurando" vale enquanto a tecla estiver embaixo.
/// </summary>
public readonly record struct CharacterIntent
{
	/// <summary>Direção pedida, em −1..1: X para a direita, Y para trás
	/// (a convenção do <see cref="Input.GetVector"/>).</summary>
	public Vector2 Move { get; init; }

	/// <summary>Segurando para frente. A corrida na parede só segue com
	/// isto (ou agarrada).</summary>
	public bool Forward { get; init; }

	public bool Jump { get; init; }
	public bool JumpHeld { get; init; }

	public bool CrouchHeld { get; init; }

	/// <summary>Shift: andar devagar, em silêncio (só humano).</summary>
	public bool WalkHeld { get; init; }

	public bool Attack { get; init; }
	public bool AttackHeld { get; init; }
	public bool AttackReleased { get; init; }

	/// <summary>Botão direito: salto do lobo (segurar carrega) ou bote no ar.</summary>
	public bool Special { get; init; }
	public bool SpecialReleased { get; init; }

	/// <summary>R: ruge de lobo, recarrega de humano.</summary>
	public bool Howl { get; init; }
	public bool HowlHeld { get; init; }

	/// <summary>G: soltar a arma da mão.</summary>
	public bool Drop { get; init; }

	/// <summary>Slot escolhido nas teclas 1 a 5 neste quadro, ou
	/// <c>null</c>.</summary>
	public Slot? Slot { get; init; }

	/// <summary>Ninguém pedindo nada.</summary>
	public static readonly CharacterIntent None = new();

	/// <summary>Lê o teclado e o mouse agora.</summary>
	public static CharacterIntent FromInput() => new()
	{
		Move = Input.GetVector("move_left", "move_right", "move_forward", "move_back"),
		Forward = Input.IsActionPressed("move_forward"),
		Jump = Input.IsActionJustPressed("jump"),
		JumpHeld = Input.IsActionPressed("jump"),
		CrouchHeld = Input.IsActionPressed("crouch"),
		WalkHeld = Input.IsActionPressed("walk"),
		Attack = Input.IsActionJustPressed("attack"),
		AttackHeld = Input.IsActionPressed("attack"),
		AttackReleased = Input.IsActionJustReleased("attack"),
		Special = Input.IsActionJustPressed("special"),
		SpecialReleased = Input.IsActionJustReleased("special"),
		Howl = Input.IsActionJustPressed("howl"),
		HowlHeld = Input.IsActionPressed("howl"),
		Drop = Input.IsActionJustPressed("drop"),
		Slot = SlotPressed(),
	};

	private static readonly (string Action, Slot Slot)[] SlotKeys =
	{
		("slot_1", global::Slot.Primary),
		("slot_2", global::Slot.Secondary),
		("slot_3", global::Slot.Wolf),
		("slot_4", global::Slot.Knife),
		("slot_5", global::Slot.Grenade),
	};

	/// <summary>Duas teclas no mesmo quadro: vale a de número maior, como
	/// era quando cada uma trocava a arma em sequência.</summary>
	private static Slot? SlotPressed()
	{
		Slot? chosen = null;
		foreach (var (action, slot) in SlotKeys)
			if (Input.IsActionJustPressed(action))
				chosen = slot;
		return chosen;
	}
}
