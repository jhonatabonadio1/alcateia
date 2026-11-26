using Godot;
using System.Collections.Generic;

/// <summary>
/// O salto carregado do lobo (botão direito no chão): segurar carrega,
/// soltar salta. A distância é só a da barra, em linha reta na direção da
/// mira (decisão do Jhon); só o mapa encurta.
/// </summary>
public partial class Weapon
{
	/// <summary>Salto no chão: dano alto, o lobo preso no lugar até a
	/// animação acabar.</summary>
	[ExportGroup("Lobo: salto carregado")]
	/// <summary>Multiplicador do dano do especial no alvo principal (sobre a
	/// garra). Era 2,6 (o AJUSTES dizia 2,1); 3,7 desde 26/11 (Jhon: "mais
	/// dano ao alvo principal"): com a garra de 65 do lobo do meio, 385 num
	/// humano (mata de vida cheia) e 240 num lobo (80% dos 300 de vida e
	/// escudo do canhão de vidro).</summary>
	[Export] public float HeavyDamageBonus = 3.7f;
	[Export] public float HeavyCooldown = 1f;

	/// <summary>O alcance do salto no chão, em metros: o mais longe que
	/// o ponto mirado pode estar. Mirando além disso, o lobo salta até
	/// aqui naquela direção.</summary>
	[Export] public float PounceMaxDistance = 12f;

	/// <summary>Segundos segurando o botão direito no chão até o salto
	/// carregar inteiro (decisão do Jhon): segurar mais bate mais forte
	/// (e, sem ponto mirado, vai mais longe). Solto de leve, bate fraco.</summary>
	[Export] public float PounceChargeTime = 1.0f;

	/// <summary>Até onde o salto vai sem carga nenhuma quando a mira não
	/// toca nada (céu) ou para o bot, em metros: aí a carga decide a
	/// distância, entre isto e <see cref="PounceMaxDistance"/>. Com ponto
	/// mirado, quem decide é ele (<see cref="PounceChargeLimitsReach"/>).</summary>
	[Export] public float PounceMinDistance = 1.5f;

	/// <summary>Fração do dano do salto sem carga. Carregado inteiro, o
	/// dano é o cheio (<see cref="HeavyDamageBonus"/>).</summary>
	[Export] public float PounceMinPower = 0.4f;

	/// <summary>Quanto tempo o lobo segura a carga cheia antes de saltar
	/// sozinho.</summary>
	[Export] public float PounceChargeHold = -1f;

	/// <summary>Quem decide até onde o salto vai. Falso (decisão do Jhon,
	/// 17/11): a mira — o lobo cai onde o centro da tela aponta, até
	/// <see cref="PounceMaxDistance"/>, e a carga só dá o dano. Verdadeiro:
	/// a regra de antes, a barra é o limite e a mira escolhe o ponto dentro
	/// dele.</summary>
	[Export] public bool PounceChargeLimitsReach = false;

	/// <summary>O tempo de voo com a carga cheia, em fração do tempo do
	/// clipe (0,8 s). Pedido do Jhon (17/11): carregado inteiro, o lobo
	/// chega bem mais rápido ao alvo, para não morrer no ar. Sem carga, o
	/// voo dura o clipe; cheio, 0,45 dele (0,36 s); no meio, no meio. A
	/// animação do voo acelera junto, e o dano sai quando os pés tocam o
	/// chão.</summary>
	[Export] public float PounceFullFlight = 0.45f;

	/// <summary>O mais alto que o salto sobe quando a mira está em cima de
	/// algo, em metros acima dos pés: dois contêineres empilhados (5,8 m).
	/// Mirando em cima, o lobo vai (pedido do Jhon, 17/11). Sem mirar em
	/// cima, vale <see cref="PounceMaxClear"/>.</summary>
	[Export] public float PounceMaxClimb = 6.2f;

	/// <summary>Quanto o salto pode descer, em metros abaixo dos pés: de
	/// cima de dois contêineres até o chão. Mais fundo que isso, o voo
	/// acaba no ar e o lobo cai o resto.</summary>
	[Export] public float PounceMaxDrop = 6.5f;

	/// <summary>O que o salto passa por cima, em metros acima dos pés: um
	/// contêiner (2,2 m) passa; um bloco (5 m) ou o muro, não — o salto
	/// para antes dele.</summary>
	[Export] public float PounceMaxClear = 3.2f;

	/// <summary>Altura mínima do arco do salto, em metros. Cresce com a
	/// distância e com o que houver no caminho.</summary>
	[Export] public float PounceArc = 0.6f;

	/// <summary>O raio de quem o salto pega ao cair, em metros, em volta do
	/// lobo (não só à frente, como a garra). Pedido do Jhon (17/11): mirava
	/// em alguém e caía sem dar dano — o lobo pousa em cima ou do lado do
	/// alvo, e a garra só pegava de 0,35 a 2,65 m à frente. O anel da mira
	/// tem este tamanho.</summary>
	[Export] public float PounceHitRadius = 1.8f;

	/// <summary>Quanto o centro da área do salto fica à frente do lobo.</summary>
	[Export] public float PounceHitReach = 0.4f;

	/// <summary>O principal (o travado no modo alvo, senão o inimigo mais
	/// perto de onde caiu, dentro de <see cref="PounceHitRadius"/>) leva o dano
	/// cheio. Quem estiver em volta, até <see cref="PounceAreaRadius"/>, leva
	/// uma fração que cai com a distância (Jhon, 26/11: "mais poder de dano e
	/// em área, afetando mais pessoas ao redor"; era 30% e só a 1,8 m):
	/// <see cref="PounceSplash"/> até <see cref="PounceSplashInner"/> m, caindo
	/// até <see cref="PounceSplashFar"/> na borda. Parede no meio protege.</summary>
	[Export] public float PounceSplash = 0.6f;
	[Export] public float PounceSplashFar = 0.3f;
	[Export] public float PounceSplashInner = 1.5f;
	[Export] public float PounceAreaRadius = 3.5f;

	/// <summary>
	/// Botão direito, no ar: o bote. Prende o lobo na trajetória até o
	/// fim, e o dano sai quando ele de fato cai. No chão o botão direito
	/// não faz mais nada: o salto é o especial, no Q
	/// (<see cref="TryPounce"/>).
	/// </summary>
	public void TrySpecial()
	{
		if (_charging) return;
		if (!_form.IsWolf || !_form.CanAttack) return;
		// No ar, o bote corta a garrada em curso na hora (Jhon, 23/11: "se eu estiver no ar
		// apertando o ataque primário e apertar o secundário, ele deve interromper o primário e
		// acionar o secundário imediatamente"). O bote troca o golpe: o dano da garra que ainda
		// não saiu não sai mais, e os cliques guardados são esquecidos.
		bool clawingInAir = _strikeLeft > 0 && !_diving && !_flying && !_body.IsOnFloor();
		if (_cooldown > 0 && !clawingInAir) return;
		// O teto do clique em rajada: o bote não sai antes de StrikeSpacing
		// depois do último golpe que bateu, nem antes de LeapCooldown depois do
		// último bote. Apertado antes, fica esperando no ar (Player.Actions) e
		// sai na hora em que pode.
		if (_sinceStrike < StrikeSpacing || _sinceLeap < LeapCooldown) return;
		if (!_body.IsOnFloor() && HeightAboveGround() >= LeapMinHeight)
		{
			_attackQueue = 0;
			if (clawingInAir)
			{
				// a garra some da tela enquanto o bote entra
				EmitSignal(SignalName.WolfMoveCancelled, (int)WolfMove.Claw);
				EmitSignal(SignalName.WolfMoveCancelled, (int)WolfMove.Punch);
			}
			Leap();
		}
	}

	/// <summary>Altura mínima dos pés ao chão para o bote sair (Jhon, 21/11:
	/// "preciso atingir uma altura mínima antes, não muito alto, mas o
	/// suficiente para não ser praticamente instantâneo"). Com 1,1 m, num pulo
	/// do chão plano, o bote sai uns 0,25 s depois do pulo no Bruto (que sobe
	/// até 1,6 m) e 0,18 s no Ligeiro (2,5 m). Apertado antes, fica guardado e
	/// sai quando chegar lá (Player.Actions).</summary>
	[Export] public float LeapMinHeight = 1.1f;

	/// <summary>Dos pés ao cenário logo abaixo, em metros; sem chão até 40 m,
	/// muito. Corpos (inimigo, aliado, bot) não contam: online o jogo vê os
	/// outros ~100 ms no passado e o servidor de partida no presente, e a
	/// altura sairia diferente nos dois — o bote em passos diferentes, e o
	/// servidor puxando o lobo de volta.</summary>
	public float HeightAboveGround()
	{
		Vector3 feet = _body.GlobalPosition + Vector3.Down * 0.85f;   // a cápsula tem 1,75 m, centrada
		var exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
		PhysicsDirectSpaceState3D space = _body.GetWorld3D().DirectSpaceState;
		for (int i = 0; i < 6; i++)
		{
			var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(feet, feet + Vector3.Down * 40f, 1, exclude));
			if (hit.Count == 0) return float.MaxValue;
			if (hit["collider"].Obj is CharacterBody3D) { exclude.Add((Rid)hit["rid"]); continue; }
			return feet.DistanceTo((Vector3)hit["position"]);
		}
		return float.MaxValue;
	}

	/// <summary>Fração do dano que o lobo leva com o especial ativo
	/// (armado e no salto): o "escudo extra" (pedido do Jhon, 18/11; de
	/// 0,5 para 0,4 no mesmo dia: "um pouco mais resistente").</summary>
	[Export] public float SpecialGuard = 0.4f;

	/// <summary>Até quantos metros do ponto de partida o salto persegue o
	/// alvo que se mexeu no voo.</summary>
	[Export] public float PounceChaseRange = 18f;

	/// <summary>Onde o lobo cai em relação ao alvo travado: a esta distância
	/// do centro dele, de frente para ele (as duas cápsulas têm 0,3 m de
	/// raio). O ponto de queda era o centro do alvo: o lobo chegava em cima
	/// do corpo dele, a física o barrava e o empurrava de volta no ar
	/// (Jhon, 25/11: "vai e volta no ar"). A garra do salto pega em volta
	/// (<see cref="PounceHitRadius"/>), então cair colado na frente acerta.</summary>
	[Export] public float PounceStandOff = 0.7f;

	/// <summary>O mais rápido que o ponto de queda anda atrás do alvo que se
	/// mexe, em m/s. O voo refazia o caminho inteiro a cada quadro para onde
	/// o alvo estava: o zigue-zague de quem esquiva (e o tremor do boneco da
	/// rede, que anda aos pacotes) passava inteiro para o lobo no fim do voo,
	/// que balançava de um lado para o outro. Com o teto, o lobo vem liso e
	/// ainda alcança quem corre (o mais rápido, o lobo de quatro, anda 7,7).</summary>
	[Export] public float PounceSteerSpeed = 9f;

	/// <summary>Quanto do movimento do alvo o salto antecipa: mira onde ele
	/// vai estar quando o lobo cair (a velocidade dele, alisada em
	/// <see cref="PounceLeadSmooth"/> segundos), até
	/// <see cref="PounceMaxLead"/> metros. Sem antecipar, o lobo saía para
	/// onde o alvo estava e corrigia tudo no fim, quando já devia estar
	/// caindo.</summary>
	[Export] public float PounceLeadSmooth = 0.25f;
	[Export] public float PounceMaxLead = 2.5f;

	/// <summary>Segundos para usar o especial depois de armar (Q). Passou,
	/// perde a carga (pedido do Jhon, 18/11: "depois que eu apertar Q,
	/// tenho 5 s para usar o especial, se não eu perco"). Zero ou menos:
	/// sem prazo.</summary>
	[Export] public float SpecialArmTime = 5f;

	/// <summary>
	/// Especial armado (Q; pedido do Jhon, 18/11): o lobo fica pronto para
	/// saltar, **andando livre pelo mapa**, com o modo alvo aberto
	/// (<see cref="Player.TargetMode"/>). O clique esquerdo salta
	/// (<see cref="TryPounce"/>). **Armou, não desarma** ("apertei Q uma
	/// vez, ele usou, não posso parar"): ou salta dentro de
	/// <see cref="SpecialArmTime"/>, ou perde a carga. Armado, o lobo leva
	/// menos dano (<see cref="SpecialGuard"/>).
	/// </summary>
	public bool SpecialArmed { get; private set; }

	/// <summary>Segundos que restam para saltar, armado.</summary>
	public float SpecialArmLeft { get; private set; }

	/// <summary>O prazo estourou e a carga foi embora (HUD, som).</summary>
	[Signal] public delegate void SpecialLostEventHandler();

	/// <summary>Q: arma o especial (carga cheia, lobo, sem golpe pesado em
	/// andamento). Já armado, não faz nada. Verdadeiro se está armado.</summary>
	public bool ToggleSpecial()
	{
		if (SpecialArmed) return true;

		if (_charging || _diving || _flying) return false;
		if (!_form.IsWolf || !_form.CanAttack || !PounceReady) return false;
		SpecialArmed = true;
		SpecialArmLeft = SpecialArmTime;
		return true;
	}

	/// <summary>Armado e o lobo deixou de poder saltar (morreu, virou
	/// humano, perdeu a carga): desarma. O prazo de usar. E a guarda do
	/// especial.</summary>
	private void UpdateArmed(double delta)
	{
		if (SpecialArmed && SpecialArmTime > 0f && !SpecialFree)
		{
			SpecialArmLeft -= (float)delta;
			if (SpecialArmLeft <= 0f)
			{
				SpecialArmed = false;
				SpendSpecial();
				EmitSignal(SignalName.SpecialLost);
			}
		}

		// Morreu ou virou humano armado: a carga também vai (armou, usou).
		if (SpecialArmed && (!_form.IsWolf || _health.IsDead))
		{
			SpecialArmed = false;
			SpendSpecial();
		}

		if (SpecialArmed && !PounceReady) SpecialArmed = false;
		if (_pounceGuard && (!_form.IsWolf || (!_flying && _rootLeft <= 0))) _pounceGuard = false;
		_health.GuardMultiplier = (GuardOverride ?? (SpecialArmed || _pounceGuard)) ? Mathf.Clamp(SpecialGuard, 0f, 1f) : 1f;
	}

	/// <summary>No corpo do servidor de partida (decisão 0016): a guarda que
	/// o jogo contou, no lugar da daqui — a arma do lobo só roda no jogo.
	/// Nulo em todo outro corpo.</summary>
	public bool? GuardOverride { get; set; }

	/// <summary>Armado ou no salto do especial: a guarda está ligada.</summary>
	public bool SpecialGuarded => SpecialArmed || _pounceGuard;

	private bool _pounceGuard;

	/// <summary>
	/// O salto do especial, com a força cheia, para onde a mira aponta — ou
	/// em cima do alvo travado (<see cref="Player.LockTarget"/>), e aí o voo
	/// o persegue até cair onde ele estiver (<see cref="ChaseFocus"/>). Com a
	/// carga do especial cheia, no chão ou no ar. O jogador chega aqui pelo
	/// clique com o especial armado; testes e o bot, direto.
	/// </summary>
	public bool TryPounce()
	{
		if (_cooldown > 0 || _charging || _diving) return false;
		if (!_form.IsWolf || !_form.CanAttack || !PounceReady) return false;

		SpecialArmed = false;
		// Sai na hora: o clipe começa já no impulso, sem o agachar.
		PounceSkip = WolfMoves.StepOf(WolfMove.Pounce).Delay;
		BeginCharge();
		_chargeTime = PounceChargeTime;
		ReleaseCharge();
		return true;
	}

	/// <summary>Quantos segundos do começo do clipe do salto este salto
	/// pula (o agachar): o corpo já sai voando, então a animação começa no
	/// impulso. Sem isto (até 18/11) o clipe começava agachando com o lobo
	/// já no ar, e a pancada no chão aparecia meio segundo depois de ele
	/// cair e de o dano sair ("a animação não coincide com a velocidade",
	/// Jhon). Zero na carga segurada.</summary>
	public float PounceSkip { get; private set; }

	/// <summary>Quantas vezes mais rápido que o clipe a parte de depois
	/// do voo toca (a pancada e o levantar): 0,93 s viram 0,55 s. O golpe
	/// e o fim do golpe da arma contam com isso.</summary>
	[Export] public float PounceRecoverRate = 1.7f;

	private bool _pounceLanded;

	/// <summary>O ritmo do clipe do salto agora: o do voo, o da queda, ou 1.</summary>
	public float PounceClipRate =>
		_flying ? PounceFlightRate : _pounceLanded && _rootLeft > 0 ? Mathf.Max(PounceRecoverRate, 0.1f) : 1f;

	/// <summary>
	/// O voo persegue o alvo (pedido do Jhon, 18/11): o lobo cai colado na
	/// frente dele (<see cref="PounceStandOff"/>), onde ele vai estar quando
	/// o lobo chegar — andou para a frente, para o lado ou para trás. Até
	/// <see cref="PounceChaseRange"/> metros de onde saltou; o alvo morto,
	/// vale onde ele estava.
	///
	/// O ponto de queda anda atrás do alvo no máximo a
	/// <see cref="PounceSteerSpeed"/>, e não salta para onde ele está a cada
	/// quadro (25/11): o voo fica liso com quem esquiva e com o boneco da
	/// rede. <paramref name="snap"/>: no impulso, o ponto vai direto.
	/// </summary>
	private void ChaseFocus(double delta, bool snap = false)
	{
		if (FocusPoint(delta, snap) is not Vector3 target) return;

		var space = _body.GetWorld3D().DirectSpaceState;
		float halfHeight = -((_body as Player)?.FeetOffset ?? -0.875f);

		// O lado do alvo escolhido no impulso (LandAround), que acompanha o
		// alvo; sem ele, colado na frente dele, do lado de onde o lobo vem:
		// o caminho nunca termina dentro do corpo do alvo.
		Vector3 end;
		if (_landOffset is Vector3 offset)
		{
			Vector3 spot = target + offset;
			float feet = target.Y - halfHeight;
			float floor = FloorBelow(space, new Vector3(spot.X, feet + 1.2f, spot.Z), 2.4f) is Vector3 f && Mathf.Abs(f.Y - feet) <= 0.6f ? f.Y : feet;
			end = new Vector3(spot.X, floor + halfHeight, spot.Z);
		}
		else
		{
			Vector3 from = (target - _body.GlobalPosition) with { Y = 0 };
			if (from.LengthSquared() < 0.0001f) from = _flyDir;
			end = target - from.Normalized() * Mathf.Min(PounceStandOff, from.Length());
		}

		Vector3 flat = (end - _flyStart) with { Y = 0 };
		if (flat.Length() > PounceChaseRange) flat = flat.Normalized() * PounceChaseRange;
		Vector3 wanted = new Vector3(_flyStart.X + flat.X, end.Y, _flyStart.Z + flat.Z);

		_flyEnd = snap ? wanted : _flyEnd.MoveToward(wanted, PounceSteerSpeed * (float)delta);
		Vector3 path = (_flyEnd - _flyStart) with { Y = 0 };
		if (path.LengthSquared() > 0.0001f) _flyDir = path.Normalized();
		_flyDistance = path.Length();
		_flyLand = Mathf.Clamp(_flyEnd.Y - _flyStart.Y, -PounceMaxDrop, PounceMaxClimb);
	}

	/// <summary>O centro do corpo do alvo travado quando o lobo chegar: onde
	/// ele está, mais a velocidade dele (alisada) vezes o voo que falta, no
	/// chão. Nulo sem alvo (ou morto).</summary>
	private Vector3? FocusPoint(double delta, bool snap)
	{
		(Vector3 At, Vector3 Velocity) view;
		if (_pounceFocus is Node3D focus && IsInstanceValid(focus) && focus.IsInsideTree())
		{
			if (focus.GetNodeOrNull<Health>("Health") is { IsDead: true }) return null;
			view = FocusView(focus);
		}
		else if (_chaseSeenOnly && RemoteFocusView is var (remoteAt, remoteVelocity))
			view = (remoteAt, remoteVelocity);   // no servidor: um alvo que só o jogo tem (um bot)
		else return null;

		var space = _body.GetWorld3D().DirectSpaceState;
		float halfHeight = -((_body as Player)?.FeetOffset ?? -0.875f);

		// Para onde o alvo vai: a velocidade dele, alisada (o boneco da rede
		// a tem pelo pacote; o bot e o jogador, pela física).
		var (seen, seenVelocity) = view;
		Vector3 velocity = seenVelocity with { Y = 0 };
		float follow = snap ? 1f : 1f - Mathf.Exp(-(float)delta / Mathf.Max(PounceLeadSmooth, 0.01f));
		_focusVelocity = _focusVelocity.Lerp(velocity, follow);
		float left = Mathf.Max((float)(_flyTotal - _flyTime), 0f);
		Vector3 ahead = (_focusVelocity * left).LimitLength(PounceMaxLead);

		Vector3 at = seen + ahead;
		Vector3 floor = FloorBelow(space, at, 4f) ?? FloorBelow(space, seen, 4f) ?? seen + Vector3.Down * 0.9f;
		return new Vector3(at.X, floor.Y + halfHeight, at.Z);
	}

	// ------------------------------------------------------------ partida autoritativa

	/// <summary>
	/// Onde este corpo vê o alvo travado e para onde ele vai. No jogo, o
	/// alvo como está na tela (online, arredondado como vai no comando, para
	/// o servidor fazer a mesma conta); no corpo do servidor, o que o jogo
	/// disse que via (<see cref="RemoteFocusView"/>) — o servidor vê o alvo
	/// no presente, e o jogo uns 100 ms atrás; seguindo pontos diferentes, o
	/// voo dos dois saía diferente e a correção puxava o lobo do jogador.
	/// </summary>
	private (Vector3 At, Vector3 Velocity) FocusView(Node3D focus)
	{
		if (RemoteFocusView is var (remoteAt, remoteVelocity) && focus == RemoteFocus)
			return (remoteAt, remoteVelocity);

		Vector3 at = focus.GlobalPosition;
		Vector3 velocity = focus is CharacterBody3D moving ? moving.Velocity : Vector3.Zero;
		if (Player.NetQuantize)
		{
			at = new Vector3(Quant.Position(at.X), Quant.Position(at.Y), Quant.Position(at.Z));
			velocity = new Vector3(Quant.Velocity(velocity.X), Quant.Velocity(velocity.Y), Quant.Velocity(velocity.Z));
		}
		NetFocusSeen = (at, velocity);
		_netFocusSeenFor = focus;
		_netFocusSeenFrame = Engine.GetPhysicsFrames();
		return (at, velocity);
	}

	/// <summary>No servidor de partida: o alvo travado que o jogo mandou no
	/// comando deste passo (já conferido por <c>ServerBody</c>), e onde o jogo
	/// o via. Nulo no jogo.</summary>
	public Node3D RemoteFocus { get; set; }

	/// <summary>No servidor: o voo persegue um alvo que só o jogo tem (um
	/// bot), pelo ponto que o jogo manda a cada passo.</summary>
	private bool _chaseSeenOnly;
	public (Vector3 At, Vector3 Velocity)? RemoteFocusView { get; set; }

	/// <summary>No servidor de partida: o ponto que o jogo mirou no salto que
	/// saiu neste passo (<see cref="RemoteAimNone"/>: não mirou nada).</summary>
	public Vector3? RemoteAim { get; set; }
	public bool RemoteAimNone { get; set; }

	/// <summary>Para o comando do jogo: o ponto mirado do salto que saiu
	/// neste passo de física, se saiu.</summary>
	public bool NetAimThisStep => _netAimFrame == Engine.GetPhysicsFrames() && _netAimFrame != 0;
	public Vector3? NetAim { get; private set; }
	private ulong _netAimFrame;

	/// <summary>Para o comando do jogo: o alvo do especial neste passo — o do
	/// voo que persegue, ou o travado com o especial armado — e onde o jogo o
	/// via (o mesmo número que o voo usou, se usou).</summary>
	public Node3D NetFocus => (_flying || (!_pounceLanded && _rootLeft > 0)) && _pounceFocus is Node3D chasing && IsInstanceValid(chasing)
		? chasing
		: SpecialArmed && (_body as Player)?.LockTarget is Node3D locked && IsInstanceValid(locked) ? locked : null;

	public (Vector3 At, Vector3 Velocity) NetFocusSeen { get; private set; }
	private Node3D _netFocusSeenFor;
	private ulong _netFocusSeenFrame;

	/// <summary>Onde o jogo vê <paramref name="focus"/> agora, como vai no
	/// comando: o que o voo usou neste passo, ou o de agora.</summary>
	public (Vector3 At, Vector3 Velocity) NetFocusView(Node3D focus) =>
		_netFocusSeenFor == focus && _netFocusSeenFrame == Engine.GetPhysicsFrames() ? NetFocusSeen : FocusView(focus);

	/// <summary>Onde cair em relação ao alvo travado (no chão), escolhido no
	/// impulso: o voo segue o alvo nesse lado dele.</summary>
	private Vector3? _landOffset;

	/// <summary>A velocidade do alvo travado, alisada, para antecipar.</summary>
	private Vector3 _focusVelocity;

	private bool _charging;
	private double _chargeTime;
	private float _pounceRange;

	/// <summary>Agachado carregando o salto (botão direito seguro no chão).</summary>
	public bool IsChargingPounce => _charging && _form != null && _form.IsWolf;

	/// <summary>Começou a carregar segurando o botão (sem pular o agachar).</summary>
	private void HoldCharge()
	{
		PounceSkip = 0f;
		BeginCharge();
	}

	/// <summary>Carga do salto, de 0 a 1. É o que a barra de força mostra.</summary>
	public float PounceCharge => _charging ? CombatRules.ChargePower(_chargeTime, PounceChargeTime) : 0f;

	/// <summary>Onde os pés do lobo pousam se soltar agora: o mesmo plano
	/// do salto de verdade (<see cref="PlanPounce"/>) — a mira dá o
	/// ponto (<see cref="PounceTarget"/>) e o mapa o resto: em cima do
	/// caixote ou do contêiner que o arco alcança, ou antes do muro que
	/// ele não passa.
	/// Null fora da carga. É o que a mira no chão mostra
	/// (<see cref="PounceAim"/>).</summary>
	public Vector3? PounceLanding
	{
		get
		{
			if (!IsChargingPounce) return null;

			var (direction, distance, aimY) = PounceTarget(PounceCharge, PounceAimPoint);
			PlanPounce(ref distance, direction, out float land, out _, aimY);

			float halfHeight = -((_body as Player)?.FeetOffset ?? -0.875f);
			return _body.GlobalPosition + Vector3.Down * halfHeight + direction * distance + Vector3.Up * land;
		}
	}

	/// <summary>Começa a carregar: o lobo agacha (o começo do clipe do
	/// salto) e fica preso até soltar.</summary>
	private void BeginCharge()
	{
		_pounceLanded = false;
		_charging = true;
		_chargeTime = 0;
		_comboIndex = 0;
		_attackQueued = false;
		_rootLeft = 99;
		_strikeLeft = 99;
		_cooldown = 99;
		_strikeId++;
		EmitSignal(SignalName.WolfMoveStarted, (int)WolfMove.Pounce);
	}

	/// <summary>
	/// O salto do bot: sem botão para segurar, ele já sabe a carga — a
	/// que leva até o alvo, com o erro da dificuldade — e mira no alvo como
	/// o jogador mira com a câmera. Mesmo golpe do jogador, do agachar à
	/// queda, e a mesma regra: a carga decide a distância, nada segue o
	/// alvo no voo.
	/// </summary>
	public bool BotPounce(Node3D target, float power)
	{
		if (_cooldown > 0 || _charging || !_form.IsWolf || !_form.CanAttack || !_body.IsOnFloor()) return false;

		_botAim = null;
		if (target != null)
		{
			Vector3 to = target.GlobalPosition - _body.GlobalPosition;
			to.Y = 0;
			if (to.LengthSquared() > 0.0001f) _botAim = to.Normalized();
		}
		
		if (!PounceReady) return false;

		PounceSkip = Mathf.Min(Mathf.Clamp(power, 0f, 1f) * PounceChargeTime, WolfMoves.StepOf(WolfMove.Pounce).Delay);
		BeginCharge();
		_chargeTime = Mathf.Clamp(power, 0f, 1f) * PounceChargeTime;
		ReleaseCharge();
		_botAim = null;
		return true;
	}

	/// <summary>Direção do salto do bot, só durante <see cref="BotPounce"/>.</summary>
	private Vector3? _botAim;

	/// <summary>Carga para o salto chegar a <paramref name="distance"/>
	/// metros (o ponto de queda fica a uma garra do alvo).</summary>
	public float PounceChargeFor(float distance) =>
		CombatRules.PounceChargeFor(distance, ClawReach, PounceMinDistance, PounceMaxDistance);

	/// <summary>Soltou o botão: salta com a carga que tiver.</summary>
	public void ReleaseCharge()
	{
		if (!_charging) return;

		float power = PounceCharge;
		float crouch = WolfMoves.StepOf(WolfMove.Pounce).Delay;
		float elapsed = Mathf.Min((float)_chargeTime, crouch);
		_charging = false;
		Pounce(power, elapsed);
	}

	/// <summary>Larga a carga do salto sem saltar (morreu, deixou de ser
	/// lobo, abriu o menu).</summary>
	public void CancelCharge()
	{
		if (!_charging) return;
		_charging = false;
		_rootLeft = 0;
		_strikeLeft = 0;
		_cooldown = 0;
		_strikeId++;
		EmitSignal(SignalName.WolfMoveCancelled, (int)WolfMove.Pounce);
	}

	/// <summary>
	/// Salto no chão: o "jump attack" inteiro — agacha, salta, cai — com
	/// o lobo preso no lugar. O dano sai no quadro em que os pés tocam o
	/// chão na animação, não no clique. É o golpe de quem deixou o lobo
	/// chegar parado: forte, mas lento e fácil de ver vindo.
	/// </summary>
	/// <param name="power">A carga, de 0 a 1: alcance e dano.</param>
	/// <param name="elapsed">Quanto do clipe já tocou agachando (o começo
	/// dele corre durante a carga): o resto do golpe conta daqui.</param>
	private void Pounce(float power, float elapsed)
	{
		SpendSpecial();
		_pounceGuard = true;
		_comboIndex = 0;
		_attackQueued = false;
		_pounceLanded = false;

		// O relógio do salto, em tempo de jogo: o resto do agachar, o voo
		// (mais curto com a carga) e o que vem depois dele (a pancada e o
		// levantar), acelerado. O dano sai quando as mãos batem no chão.
		var (crouch, airborne, _) = WolfMoves.StepOf(WolfMove.Pounce);
		float wait = Mathf.Max(crouch - elapsed, 0f);
		float air = airborne * Mathf.Lerp(1f, Mathf.Clamp(PounceFullFlight, 0.2f, 1f), Mathf.Clamp(power, 0f, 1f));
		float recover = Mathf.Max(PounceRecoverRate, 0.1f);
		float landed = crouch + airborne;
		float impactAt = wait + air + Mathf.Max(WolfMoves.ImpactDelay(WolfMove.Pounce) - landed, 0f) / recover;
		float total = wait + air + Mathf.Max(WolfMoves.Duration(WolfMove.Pounce) - landed, 0f) / recover;

		_rootLeft = total;
		_pounceFlew = false;
		float damage = CombatRules.PounceMultiplier(HeavyDamageBonus, PounceMinPower, power);
		ulong id = StartMove(WolfMove.Pounce, CooldownHeavy, HeavyCooldown,
			() =>
			{
				// Bateu: dali em diante o lobo só se levanta, e o pulo ou a
				// corrida cortam (Jhon, 20/11).
				MarkRecovery(_rootLeft);
				_wolfHit = Kill.WolfPounceId;
				Claw(damage, PounceHitReach, PounceHitRadius, false, _pounceFlew ? _flyDir : null,
					focus: _pounceFocus, splash: PounceSplash, area: PounceAreaRadius);
			}, Mathf.Max(elapsed, 0.001f), total, impactAt);

		// O salto é um pulo de verdade: do impulso até os pés tocarem o
		// chão, o corpo voa num arco. Para onde e até onde, diz a mira no
		// instante de soltar (decisão do Jhon, 17/11): o ponto em que o
		// centro da tela toca o mapa, na distância e na altura dele. Fica
		// fixado aqui — nada segue o alvo durante o voo.
		var (delay, duration, _) = WolfMoves.StepOf(WolfMove.Pounce);
		Vector3? aim = PounceAimPoint;
		NetAim = aim;
		_netAimFrame = Engine.GetPhysicsFrames();
		(_pounceDir, _pounceRange, _pounceAimY) = PounceTarget(power, aim);
		// O travado no modo alvo é o principal do golpe (Player.Lock); no
		// corpo do servidor, o que veio no comando.
		_pounceFocus = (_body as Player)?.LockTarget ?? RemoteFocus;
		_chaseSeenOnly = _pounceFocus == null && RemoteFocusView != null;
		float flight = duration * Mathf.Lerp(1f, Mathf.Clamp(PounceFullFlight, 0.2f, 1f), Mathf.Clamp(power, 0f, 1f));
		PounceFlightRate = duration / Mathf.Max(flight, 0.05f);
		After(Mathf.Max(delay - elapsed, 0f), id, () => BeginPounceFlight(flight));
	}

	/// <summary>Quantas vezes mais rápido que o clipe o voo deste salto
	/// anda (1 sem carga): a animação do voo toca nesse ritmo.</summary>
	public float PounceFlightRate { get; private set; } = 1f;

	private Vector3 _pounceDir = Vector3.Forward;
	private Node3D _pounceFocus;
	private float? _pounceAimY;
	private bool _pounceFlew;
	
	/// <summary>
	/// Onde o lobo quer cair: o ponto em que a mira (o centro da tela)
	/// toca o mundo.
	///
	/// - No chão, em cima de um caixote ou de um contêiner: esse ponto.
	/// - Em alguém: os pés dele. Mirar no inimigo é pedir para cair nele;
	///   antes o raio batia no corpo, que é uma "parede", e a mira era
	///   ignorada — o salto ia o que a barra mandava, passando do alvo ou
	///   ficando no caminho.
	/// - Numa parede ou num teto: o chão ao pé dela, meio metro à frente.
	///
	/// Null se a mira não toca nada (céu), se é um bot, ou se o ponto fica
	/// atrás do lobo (olhando quase reto para baixo): aí valem a carga e a
	/// direção da câmera.
	/// </summary>
	public Vector3? PounceAimPoint
	{
		get
		{
			// No servidor de partida: o ponto que o jogo mirou (ele tem a câmera).
			if (RemoteAimNone) return null;
			if (RemoteAim is Vector3 remote) return remote;
			if (_botAim != null || (_body as Player)?.AimCamera is not Camera3D camera) return null;

			Vector3 forward = -camera.GlobalBasis.Z;
			Vector3 from = camera.GlobalPosition;
			var space = _body.GetWorld3D().DirectSpaceState;

			// Travado num inimigo (botão do meio): colado na frente dele, olhe
			// para onde olhar.
			if ((_body as Player)?.LockTarget is Node3D locked && IsInstanceValid(locked))
			{
				Vector3 seen = FocusView(locked).At;
				return InFrontOf(seen, FloorBelow(space, seen, 4f) ?? seen + Vector3.Down * 0.9f);
			}

			// Em terceira pessoa a câmera fica atrás do lobo: o raio começa
			// na altura dele, para o que estiver entre os dois não contar.
			from += forward * Mathf.Max((_body.GlobalPosition - from).Dot(forward), 0f);

			var query = PhysicsRayQueryParameters3D.Create(from, from + forward * (PounceMaxDistance + 10f));
			query.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
			var hit = space.IntersectRay(query);
			if (hit.Count == 0) return null;

			Vector3 point = (Vector3)hit["position"];
			Vector3 normal = (Vector3)hit["normal"];

			if (hit["collider"].Obj is CharacterBody3D someone)
			{
				point = InFrontOf(someone.GlobalPosition, FloorBelow(space, someone.GlobalPosition, 4f) ?? someone.GlobalPosition + Vector3.Down * 0.9f);
			}
			else if (normal.Y < 0.5f)
			{
				// Parede em pé (ou teto): não dá para pousar nela. O pé dela.
				Vector3 away = normal with { Y = 0 };
				if (away.LengthSquared() < 0.01f) away = -forward with { Y = 0 };
				if (away.LengthSquared() < 0.01f) return null;

				float feetY = _body.GlobalPosition.Y + ((_body as Player)?.FeetOffset ?? -0.875f);
				Vector3 probe = point + away.Normalized() * 0.5f + normal * 0.05f;
				if (FloorBelow(space, probe, Mathf.Max(probe.Y - feetY, 0f) + PounceMaxDrop) is not Vector3 foot) return null;
				point = foot;
			}

			Vector3 flat = point - _body.GlobalPosition;
			flat.Y = 0;
			Vector3 look = forward with { Y = 0 };
			if (look.LengthSquared() > 0.0001f && flat.Dot(look.Normalized()) < 0.3f) return null;
			return point;
		}
	}

	/// <summary>O chão aos pés de <paramref name="someone"/>, recuado
	/// <see cref="PounceStandOff"/> para o lado do lobo: o salto cai colado
	/// na frente dele, e não dentro do corpo dele (onde a física barrava o
	/// lobo no ar e o fazia escorregar em volta, Jhon, 25/11).</summary>
	private Vector3 InFrontOf(Vector3 someone, Vector3 feet)
	{
		Vector3 back = (_body.GlobalPosition - someone) with { Y = 0 };
		float gap = back.Length();
		return gap > 0.0001f ? feet + back / gap * Mathf.Min(PounceStandOff, gap) : feet;
	}

	/// <summary>O alvo travado deste salto, se o voo está atrás de alguém:
	/// quem mais estiver no caminho é atravessado (WolfMoveMotor).</summary>
	public Node3D PounceFocus => _flying && _pounceFocus is Node3D focus && IsInstanceValid(focus) ? focus : null;

	/// <summary>O voo bateu em alguém (WolfMoveMotor): o lobo fica ali, no
	/// chão, e não escorrega em volta dele para chegar ao ponto de queda. É
	/// a regra de sempre ("bater em alguém para nele, e o golpe sai ali"):
	/// antes ela valia pela física, que deslizava o corpo de lado na
	/// cápsula do outro, e o lobo passava por ele e caía do outro lado.</summary>
	public void PounceBlocked(Vector3 at)
	{
		if (!_flying) return;
		_flyEnd = new Vector3(at.X, _flyEnd.Y, at.Z);
	}

	/// <summary>O chão embaixo de <paramref name="from"/>, até
	/// <paramref name="depth"/> metros. Gente não conta como chão.</summary>
	private Vector3? FloorBelow(PhysicsDirectSpaceState3D space, Vector3 from, float depth)
	{
		var query = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * depth);
		var exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };

		for (int tries = 0; tries < 4; tries++)
		{
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0) return null;

			if (hit["collider"].Obj is CharacterBody3D)
			{
				exclude.Add((Rid)hit["rid"]);
				continue;
			}

			return (Vector3)hit["position"];
		}

		return null;
	}

	/// <summary>
	/// O plano do salto com esta carga: para que lado, quantos metros no
	/// chão e a altura do ponto mirado (null se não há ponto, ou se ele
	/// fica além do alcance — aí o salto vai até o limite naquela direção
	/// e cai no que houver ali).
	///
	/// Com ponto mirado, a distância é a dele: perto, salto curto; longe,
	/// salto longo, até <see cref="PounceMaxDistance"/>. A carga só entra
	/// na distância sem ponto mirado (céu, bot) ou com
	/// <see cref="PounceChargeLimitsReach"/> ligado.
	/// </summary>
	private (Vector3 Direction, float Distance, float? AimY) PounceTarget(float charge, Vector3? aim)
	{
		Vector3 direction = PounceDirection(aim);
		float reach = CombatRules.PounceRange(PounceMinDistance, PounceMaxDistance, charge);
		if (aim is not Vector3 point) return (direction, reach, null);

		Vector3 flat = point - _body.GlobalPosition;
		flat.Y = 0;
		float wanted = flat.Length();
		float limit = PounceChargeLimitsReach ? reach : PounceMaxDistance;
		return (direction, Mathf.Clamp(wanted, 0.5f, limit), wanted <= limit + 0.01f ? point.Y : null);
	}

	/// <summary>Para onde o salto vai, no chão: do lobo ao ponto mirado;
	/// sem ponto, a direção da câmera do próprio jogador
	/// (<see cref="Player.AimCamera"/>, não a da câmera que estiver na
	/// tela); o bot, para o alvo; sem nenhuma, para a frente do corpo.</summary>
	private Vector3 PounceDirection(Vector3? aim)
	{
		Vector3 direction = _botAim
			?? ((_body as Player)?.AimCamera is Camera3D camera
				? -camera.GlobalBasis.Z
				: -_body.GlobalTransform.Basis.Z);

		// Com ponto de queda mirado, o salto vai do lobo até ele, e não na
		// direção da câmera: assim cai onde a mira está, e não ao lado.
		if (aim is Vector3 point)
		{
			Vector3 flat = point - _body.GlobalPosition;
			flat.Y = 0;
			if (flat.LengthSquared() > 0.09f) direction = flat;
		}

		direction.Y = 0;
		if (direction.LengthSquared() < 0.0001f) direction = -_body.GlobalTransform.Basis.Z with { Y = 0 };
		if (direction.LengthSquared() < 0.0001f) direction = Vector3.Forward;
		return direction.Normalized();
	}

	private bool _flying;
	private double _flyTime;
	private double _flyTotal = 1;
	private Vector3 _flyStart;
	private Vector3 _flyDir;
	private float _flyDistance;
	private float _flyLand;
	private float _flyArc;
	private Vector3 _flyEnd;

	/// <summary>Onde o voo do salto vai cair agora (o centro do corpo).</summary>
	public Vector3 PounceFlightEnd => _flyEnd;

	/// <summary>No ar no salto: o corpo segue <see cref="PounceFlightPoint"/>.</summary>
	public bool IsPounceFlying => _flying && _form != null && _form.IsWolf;

	/// <summary>
	/// Onde o corpo deve estar daqui a <paramref name="ahead"/> segundos
	/// de voo. Para a frente em sino (sai devagar, voa, assenta), como o
	/// quadril do clipe; para cima em parábola no tempo, subindo rápido no
	/// impulso — é o que passa por cima de um caixote logo à frente sem
	/// precisar de um arco enorme.
	///
	/// É um arremesso de verdade com o tempo de voo fixo (o do clipe, T,
	/// para o dano sair quando os pés tocam o chão): a distância D e o
	/// desnível H do ponto de queda dão as velocidades — no chão D/T em
	/// média, para cima (H + 4·arco)/T na saída — e a "gravidade" do salto
	/// é 8·arco/T². Longe ou alto, o lobo sai mais rápido; perto, mais
	/// devagar; e chega sempre no mesmo instante.
	/// </summary>
	public Vector3 PounceFlightPoint(double ahead)
	{
		float u0 = Mathf.Clamp((float)(_flyTime / _flyTotal), 0f, 1f);
		float u = Mathf.Clamp((float)((_flyTime + ahead) / _flyTotal), 0f, 1f);
		float s0 = _flyShape.At(u0), s = _flyShape.At(u);
		float y = _flyLand * u + 4f * _flyArc * u * (1f - u);

		// No chão, de onde o corpo está até o ponto de queda, a fração do sino
		// que falta andar neste passo. Sem o alvo mudar de lugar, é o mesmo
		// arco de sempre (start + caminho × sino). Com ele mudando, o lobo
		// corrige a partir de onde está, liso — antes o arco era refeito do
		// ponto de partida, e no fim do voo o lobo ia e voltava junto com o alvo.
		Vector3 here = _body.GlobalPosition;
		Vector3 remaining = new Vector3(_flyEnd.X - here.X, 0, _flyEnd.Z - here.Z);
		float share = 1f - s0 > 1e-4f ? (s - s0) / (1f - s0) : 1f;
		Vector3 step = remaining * share;

		// E nunca para trás no ar (Jhon, 25/11): o alvo que corre para o lobo
		// encurta o salto, mas o lobo não recua para encontrá-lo.
		float back = step.Dot(_flyLaunch);
		if (back < 0f) step -= _flyLaunch * back;

		return new Vector3(here.X + step.X, _flyStart.Y + y, here.Z + step.Z);
	}

	/// <summary>O avanço no chão ao longo do voo, de 0 a 1: sai devagar,
	/// voa, assenta.</summary>
	private static float Bell(float u) => (1f - Mathf.Cos(Mathf.Pi * u)) * 0.5f;

	/// <summary>Para onde o lobo saiu, no chão: o voo não anda para trás disso.</summary>
	private Vector3 _flyLaunch = Vector3.Forward;

	/// <summary>A forma deste voo (WolfPouncePath): sobe antes de avançar, desce na vertical no fim.</summary>
	private FlightShape _flyShape;

	private void BeginPounceFlight(float duration)
	{
		// Direção e distância guardadas no soltar, medidas de onde o corpo
		// está no impulso. Só o que não dá para atravessar (bloco, muro)
		// encurta — e isso é o mapa, igual para todo mundo.
		Vector3 direction = _pounceDir;
		float distance = _pounceRange;

		float wanted = distance;
		PlanPounce(ref distance, direction, out float land, out float arc, out float arcLimit, _pounceAimY);

		if (Debug)
		{
			float time = Mathf.Max(duration, 0.05f);
			GD.Print($"leap: travels {distance:0.0} m{(distance < wanted - 0.05f ? $" (wanted {wanted:0.0}: obstacle in the way)" : "")}, " +
				$"arc {arc:0.0} m, lands {land:+0.0;-0.0} m in {time:0.00} s " +
				$"(leaves at {distance / time:0.0} m/s along the ground and {(land + 4f * arc) / time:0.0} m/s up)");
		}

		_flyStart = _body.GlobalPosition;
		_flyTotal = Mathf.Max(duration, 0.05f);
		_flyTime = 0;
		_flying = true;
		_focusVelocity = Vector3.Zero;
		_landOffset = null;
		Vector3 end = _flyStart + direction * distance + Vector3.Up * land;

		// O caminho com o corpo inteiro (WolfPouncePath): com alvo travado,
		// o lado dele em que o voo fica mais baixo, onde ele vai estar; sem
		// alvo, o ponto mirado, no arco mais baixo que passa.
		_flyShape = default;
		if (FocusPoint(0, snap: true) is Vector3 target && LandAround(_flyStart, target, arcLimit) is var (spot, swept, shape))
		{
			end = spot;
			arc = swept;
			_flyShape = shape;
			_landOffset = (spot - target) with { Y = 0 };
		}
		else if (FlightTo(_flyStart, end, Mathf.Max(arc, arcLimit)) is var (lower, form))
		{
			arc = lower;
			_flyShape = form;
		}

		_flyEnd = end;
		_flyArc = arc;
		_flyLand = Mathf.Clamp(end.Y - _flyStart.Y, -PounceMaxDrop, PounceMaxClimb);
		Vector3 launch = (_flyEnd - _flyStart) with { Y = 0 };
		_flyDir = launch.LengthSquared() > 0.0001f ? launch.Normalized() : direction;
		_flyDistance = launch.Length();
		if (Debug)
			GD.Print(_landOffset != null
				? $"leap: on the locked target, {_flyDistance:0.0} m, arc {arc:0.0} m (rise first {_flyShape.Lead:0.00}, drop last {_flyShape.Hold:0.00}), lands {_flyLand:+0.0;-0.0} m, {((Vector3)_landOffset).Length():0.0} m from it"
				: $"leap: {(_pounceFocus != null ? "no free side around the locked target; " : "")}{_flyDistance:0.0} m, arc {arc:0.0} m (rise first {_flyShape.Lead:0.00}, drop last {_flyShape.Hold:0.00})");
		_flyLaunch = launch.LengthSquared() > 0.0001f ? launch.Normalized() : direction;
		_pounceFlew = launch.Length() > 0.05f;
	}

	/// <summary>
	/// Mede o chão no caminho do salto e escolhe o arco. O que tem até
	/// <see cref="PounceMaxClear"/> acima dos pés é pulado por cima (o
	/// arco sobe o bastante para passar); o que é mais alto (bloco, muro)
	/// encurta o salto para cair antes dele. Cair em cima de um contêiner
	/// também vale: o arco termina na altura dele.
	///
	/// <paramref name="aimY"/> é a altura do ponto mirado: o salto cai
	/// nessa superfície, e não na mais alta que houver naquele lugar
	/// (mirando o chão de dentro de um contêiner, é dentro que ele cai, e
	/// não no teto). Com teto baixo sobre o lobo ou sobre o ponto mirado, o
	/// salto é rasante: o arco cabe embaixo dele.
	/// </summary>
	private void PlanPounce(ref float distance, Vector3 direction, out float land, out float arc, float? aimY = null) =>
		PlanPounce(ref distance, direction, out land, out arc, out _, aimY);

	/// <param name="arcLimit">O arco mais alto que cabe (sob o teto, até
	/// <see cref="PounceMaxClear"/>): o caminho do salto
	/// (<see cref="SweepArc"/>) não passa disso.</param>
	private void PlanPounce(ref float distance, Vector3 direction, out float land, out float arc, out float arcLimit, float? aimY = null)
	{
		var space = _body.GetWorld3D().DirectSpaceState;
		float halfHeight = -((_body as Player)?.FeetOffset ?? -0.875f);
		Vector3 feet = _body.GlobalPosition + Vector3.Down * halfHeight;
		float aimed = distance;
		float? aimRel = aimY - feet.Y;

		// Mirando em cima de algo mais alto que o normal (dois contêineres),
		// o salto sobe até lá: o limite vira a altura mirada.
		float clear = PounceMaxClear;
		if (aimRel is float wantUp && wantUp > clear && wantUp <= PounceMaxClimb) clear = wantUp + 0.2f;
		float top = clear + 0.4f;
		float arcMax = clear + 0.6f;

		// Teto: o que há sobre a cabeça aqui e sobre o ponto mirado.
		float height = halfHeight * 2f;
		float roof = Headroom(space, feet, top + height);
		if (aimRel is float up)
			roof = Mathf.Min(roof, up + Headroom(space, feet + direction * distance + Vector3.Up * up, top + height));
		if (roof < top + height)
		{
			top = Mathf.Clamp(roof - 0.05f, 0.5f, top);
			arcMax = Mathf.Clamp(roof - height - 0.1f, 0.15f, arcMax);
		}

		// Mais alto que isto, não se pula por cima nem se pousa em cima.
		float limit = Mathf.Min(clear, top - 0.4f);
		arcLimit = arcMax;

		var path = new List<(float X, float H)>();
		for (float x = 0.4f; x <= distance + 0.01f; x += 0.4f)
		{
			float h = GroundAt(space, feet + direction * x, top);
			if (h > limit)
			{
				distance = Mathf.Max(x - 0.8f, 0f);
				break;
			}
			path.Add((x, h));
		}

		land = 0f;
		arc = PounceArc;

		// Até três tentativas: o arco que passa por tudo; se algo no fim do
		// caminho não dá para passar com esse alcance (um contêiner logo
		// antes do ponto de queda), cair em cima dele; se nem isso, cair
		// antes dele.
		for (int attempt = 0; attempt < 3; attempt++)
		{
			// O ponto de queda não pode cair dentro de algo alto (a quina de
			// um pilar, a borda de um muro entre duas amostras): recua até
			// achar chão.
			land = 0f;
			while (distance > 0.05f)
			{
				// No ponto mirado, o raio desce de logo acima dele: é nessa
				// superfície que se cai. Encurtou, vale a mais alta do lugar.
				bool atAim = aimRel != null && Mathf.Abs(distance - aimed) < 0.01f;
				float from = atAim ? Mathf.Clamp(aimRel.Value + 0.4f, 0.5f - PounceMaxDrop, top) : top;
				land = GroundAt(space, feet + direction * distance, from);
				if (land <= limit) break;
				distance = Mathf.Max(distance - 0.4f, 0f);
			}
			land = distance > 0.05f ? Mathf.Clamp(land, -PounceMaxDrop, limit) : 0f;
			arc = Mathf.Min(Mathf.Clamp(PounceArc + 0.07f * distance, PounceArc, 1.6f), arcMax);

			float failX = -1f;
			float failH = 0f;

			foreach (var (x, h) in path)
			{
				if (x >= distance) break;

				// Em que instante do voo o corpo passa por x, e quanto o arco
				// precisa subir para os pés passarem 15 cm acima.
				float s = Mathf.Clamp(x / Mathf.Max(distance, 0.01f), 0.01f, 0.99f);
				float u = Mathf.Acos(1f - 2f * s) / Mathf.Pi;
				float need = (h + 0.15f - land * u) / (4f * u * (1f - u));

				if (need > arcMax)
				{
					failX = x;
					failH = h;
					break;
				}

				arc = Mathf.Max(arc, need);
			}

			if (failX < 0f) return;

			float onTop = GroundAt(space, feet + direction * (failX + 0.4f), top);
			distance = Mathf.Abs(onTop - failH) < 0.35f && failX + 0.4f <= PounceMaxDistance
				? failX + 0.4f
				: Mathf.Max(failX - 0.8f, 0f);
		}
	}

	/// <summary>Sem chão nenhum até <see cref="PounceMaxDrop"/>: o voo
	/// desce só isto e o lobo cai o resto.</summary>
	private const float NoGroundDrop = 1.5f;

	/// <summary>Quanto há de vão sobre <paramref name="at"/> (os pés), em
	/// metros, olhando até <paramref name="reach"/>: a altura do teto. Sem
	/// teto, infinito. Gente não é teto.</summary>
	private float Headroom(PhysicsDirectSpaceState3D space, Vector3 at, float reach)
	{
		var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * 0.3f, at + Vector3.Up * reach);
		var exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };

		for (int tries = 0; tries < 4; tries++)
		{
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0) return float.PositiveInfinity;

			if (hit["collider"].Obj is CharacterBody3D)
			{
				exclude.Add((Rid)hit["rid"]);
				continue;
			}

			return ((Vector3)hit["position"]).Y - at.Y;
		}

		return float.PositiveInfinity;
	}

	/// <summary>Altura do chão em <paramref name="at"/>, relativa a ele:
	/// raio de cima para baixo. Começar dentro de algo (um muro mais alto
	/// que o raio) devolve a altura do raio inteira — é obstáculo. Gente
	/// não conta: cair em cima de alguém é o objetivo, não um degrau.</summary>
	private float GroundAt(PhysicsDirectSpaceState3D space, Vector3 at, float top)
	{
		var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * top, at + Vector3.Down * PounceMaxDrop);
		query.HitFromInside = true;
		var exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };

		for (int tries = 0; tries < 4; tries++)
		{
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0) return -NoGroundDrop;

			if (hit["collider"].Obj is CharacterBody3D)
			{
				exclude.Add((Rid)hit["rid"]);
				continue;
			}

			return ((Vector3)hit["position"]).Y - at.Y;
		}

		return -NoGroundDrop;
	}
}
