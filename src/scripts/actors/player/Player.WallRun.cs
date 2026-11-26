using Godot;

/// <summary>
/// Corrida em parede do lobo: grudar no alto do pulo, correr ao longo
/// da face, contornar quinas e saltar da parede.
/// </summary>
public partial class Player
{
	/// <summary>Distância máxima até a parede para grudar.</summary>
	[ExportGroup("Corrida na parede")]
	[Export] public float WallCheckDistance = 0.75f;

	/// <summary>Tempo máximo preso na parede. Zero ou menos = sem
	/// limite: corre enquanto segurar W e a parede existir.</summary>
	[Export] public float WallRunMaxTime = 0f;

	/// <summary>Distância de checagem à frente. Bater em algo solta
	/// da parede.</summary>
	[Export] public float WallRunClearance = 0.7f;

	/// <summary>Deriva vertical na parede, em metros por segundo.
	/// Zero mantém a altura travada: o lobo não sobe nem desce.</summary>
	[Export] public float WallRunVerticalDrift = 0f;

	/// <summary>Velocidade ao longo da parede, sobre a velocidade da forma.
	/// Era 1,25: rápido demais para escolher onde descer. 1,0388 na
	/// v0.26.11 (o chão subiu 3% e a parede 7%: 1,07 / 1,03) e mais 10% na
	/// v0.27.1 (decisões do Jhon): Bruto 4,45 m/s, Ligeiro 4,82.
	/// O salto da parede leva 80% disto (<see cref="WallJumpKeepSpeed"/>).</summary>
	[Export] public float WallRunSpeedBonus = 1.14268f;

	/// <summary>Aceleração ao longo da parede, em m/s². A corrida
	/// começa aos poucos em vez de sair a toda no quadro em que gruda.</summary>
	[Export] public float WallAccel = 18f;

	/// <summary>Freada, em m/s², quando o lobo chega à parede mais rápido
	/// que a corrida dela. Era a do agarrar (o ataque segurado parava o
	/// lobo); o agarrar saiu em 24/11 (veja <see cref="WantsWallRun"/>).</summary>
	[Export] public float WallBrake = 32f;

	/// <summary>Velocidade máxima com que o lobo é puxado contra a parede.
	/// A puxada é proporcional à folga: mantém o corpo a
	/// <see cref="WallHoldDistance"/> da parede, sem bater nem descolar.</summary>
	[Export] public float WallStickForce = 4f;

	/// <summary>Distância do centro do corpo à parede durante a corrida:
	/// o raio da cápsula e um fio de folga.</summary>
	[Export] public float WallHoldDistance = 0.34f;

	/// <summary>Tempo que a corrida sobrevive sem encostar em nada antes
	/// de soltar. Com a sonda que acha o ponto de parede mais perto, a
	/// quina não perde a parede; isto cobre falha de geometria.</summary>
	[Export] public float CornerGrace = 0.15f;

	/// <summary>Subindo mais rápido que isto (m/s), o lobo ainda não
	/// gruda: ele gruda no alto do pulo, na altura em que chegou, e fica
	/// nela. Grudar subindo carregava o impulso para cima da parede.</summary>
	[Export] public float WallGrabMaxRise = 0.6f;

	/// <summary>Com os pés a esta altura do chão, o lobo já gruda, mesmo
	/// subindo: não precisa esperar o alto do pulo (pedido do Jhon,
	/// 13/11 — colado na parede, um pulinho basta para agarrar). A subida
	/// morre ali: ele fica na altura em que grudou.</summary>
	[Export] public float WallGrabHeight = 0.45f;

	/// <summary>Quanto o lobo passa da ponta da parede, em metros, antes
	/// de decidir a quina: contornar para a face seguinte ou, se ela é
	/// estreita, seguir reto.</summary>
	[Export] public float WallEndMargin = 0.1f;

	/// <summary>Largura mínima da face seguinte, em metros, para o lobo
	/// contornar a quina. Bloco, contêiner, pilar e coluna fina contornam
	/// (decisão do Jhon: na quina ele continua pela parede); só uma chapa
	/// mais fina que isto — onde contornar seria dar meia-volta no ar —
	/// faz o lobo seguir reto.</summary>
	[Export] public float WallWrapMinFace = 0.25f;

	/// <summary>Até que fundura, atrás da face que acabou, a parede que
	/// continua conta como a mesma corrida (m): o degrau de uma fachada, o
	/// fim de um pilar, de um batente ou de uma porta de contêiner. O lobo
	/// segue colado nela em vez de achar que a parede acabou (Jhon, 26/11:
	/// "na parede, não contorna mais — anda reto").</summary>
	[Export] public float WallStepBack = 0.6f;

	/// <summary>Quanto o lobo sobe, no máximo, para passar por cima de um
	/// obstáculo pequeno à frente na parede (caixote, ar-condicionado,
	/// peitoril, cano), em metros acima de onde corre. Mais alto que isto
	/// não é pequeno: é parede, e vale a quina. Jhon, 26/11: "passa por
	/// baixo de obstáculos pequenos em vez de subir neles".</summary>
	[Export] public float WallClimbReach = 1.8f;

	/// <summary>Velocidade com que sobe por cima do obstáculo, em m/s.</summary>
	[Export] public float WallClimbSpeed = 7f;

	/// <summary>
	/// Objeto pequeno — poste, haste de placa, coluna fina, tubo: o corte
	/// dele, na altura do corpo, cabe num quadrado deste lado (m). Nele o
	/// lobo gruda e dá a volta, como no tonel, em vez de achar a "ponta de
	/// uma parede fina" e sair reto na hora em que grudou (Jhon, 26/11: "o
	/// lobo não gruda em paredes e objetos muito curtos ou pequenos, como um
	/// poste ou placa de rua"). Uma placa ou parede curta é comprida num
	/// sentido só: não é pequena, corre-se na face dela.
	/// </summary>
	[Export] public float WallPoleMaxWidth = 0.6f;

	/// <summary>Volta mais rápida em volta de um objeto, em graus por segundo.
	/// Num poste de 12 cm a corrida inteira (4,45 m/s no Bruto, a 0,4 m do
	/// eixo) dava quase duas voltas por segundo; no tonel de 3 m (170°/s) não
	/// muda nada.</summary>
	[Export] public float WallOrbitMaxTurn = 360f;

	/// <summary>Indo para longe da parede mais rápido que isto (m/s), não
	/// gruda. É o que impede de regrudar na ponta que acabou de deixar.</summary>
	[Export] public float WallLeaveSpeed = 0.5f;

	/// <summary>
	/// Multiplicador de altura do salto da parede. Acima de 1 cada
	/// salto sai mais alto do que entrou, permitindo escalar um
	/// corredor ricocheteando entre as paredes. Era 1,35 (2,8 m de
	/// subida, alto demais); 1,02 sobe 40% menos (decisão do Jhon).
	/// </summary>
	[Export] public float WallJumpClimb = 1.02f;

	/// <summary>Força do empurrão para fora ao saltar da parede.</summary>
	[Export] public float WallJumpPush = 6.5f;

	/// <summary>Fração do avanço ao longo da parede que o salto
	/// conserva. Zero faz o lobo sair de lado e perder tudo que
	/// acumulou correndo.</summary>
	[Export] public float WallJumpKeepSpeed = 0.8f;

	/// <summary>
	/// **Só gruda numa parede diferente** da última (decisão do Jhon,
	/// 17/11): deixou uma parede — saltando ou soltando —, só volta a ela
	/// depois de tocar o chão. É o que faz escalar de parede em parede:
	/// cada salto leva a outra parede, e a mesma não vira elevador. Até
	/// 17/11 a mesma parede valia de novo abaixo da altura do salto.
	///
	/// Duas normais acima desta semelhança, e a menos de
	/// <see cref="SameWallGap"/> uma da outra, contam como a mesma parede.
	/// </summary>
	[Export] public float SameWallThreshold = 0.85f;

	/// <summary>Duas paredes viradas para o mesmo lado contam como
	/// diferentes se estão a mais que isto uma da outra, em metros: o
	/// degrau de uma fachada, a parede de um contêiner à frente de outro.</summary>
	[Export] public float SameWallGap = 0.8f;

	/// <summary>
	/// O salto da parede segue a câmera (pedido do Jhon, 17/11: mais
	/// maleável, para subir em lugar alto). Olhando para a parede, ele
	/// empurra pouco para fora e sobe mais; olhando para longe dela, sai
	/// como antes; olhando ao longo dela, vai para esse lado. Fração do
	/// empurrão para fora quando se olha reto para a parede.
	/// </summary>
	[Export] public float WallJumpPushFacing = 0.3f;

	/// <summary>Quanto mais alto o salto da parede sai olhando para cima
	/// (a 45° acima do horizonte, o máximo): 0,35 é 35% a mais.</summary>
	[Export] public float WallJumpLookLift = 0.35f;

	/// <summary>Quina de cima ao alcance: saltando da parede com o topo
	/// dela até esta altura acima dos pés, o lobo sobe no topo em vez de
	/// sair para fora — é como se chega no alto de um contêiner correndo
	/// pela lateral dele.</summary>
	[Export] public float WallMantleReach = 1.9f;

	/// <summary>Tempo mínimo entre uma corrida e a seguinte.</summary>
	[Export] public float WallRunCooldown = 0.12f;

	/// <summary>Segundos depois de sair da parede (soltou o W, a parede
	/// acabou) em que o pulo ainda vale como salto da parede. Antes era
	/// preciso apertar espaço no mesmo instante em que soltava (pedido do
	/// Jhon, 17/11).</summary>
	[Export] public float WallJumpGrace = 0.22f;

	/// <summary>Correndo na parede e batendo em algo que não se contorna
	/// (caixote, quina baixa que a sonda à frente não pega): o corpo parado
	/// por este tempo solta a parede e cai, em vez de ficar pendurado
	/// (pedido do Jhon, 17/11).</summary>
	[Export] public float WallStallTime = 0.15f;

	/// <summary>Depois de soltar por ter batido, o tempo sem grudar de
	/// novo (senão gruda no próprio obstáculo e continua parado).</summary>
	[Export] public float WallStallCooldown = 0.35f;

	/// <summary>Imprime por que a corrida em parede não começou. Só
	/// imprime quando o motivo muda, então não enche o Output.</summary>
	[Export] public bool WallDebug = false;

	private bool _onWall;
	private Vector3 _wallNormal;
	private Vector3 _wallAlong;
	private double _cornerGrace;
	private Vector3 _lastWall;
	private float _lastWallHeight;
	private double _wallCooldown;
	private string _lastWallLog = "";
	private double _wallTimer;
	private double _wallJumpGrace;
	private double _wallStall;
	public bool IsWallRunning => _onWall;

	/// <summary>Normal da parede em que o lobo corre (para fora dela).
	/// O corpo do lobo deita com os pés na parede a partir daqui.</summary>
	public Vector3 WallNormal => _onWall ? _wallNormal : Vector3.Zero;

	/// <summary>Sentido da corrida na parede.</summary>
	public Vector3 WallAlong => _onWall ? _wallAlong : Vector3.Zero;

	/// <summary>Contornando uma quina: o sentido gira com a parede até a
	/// face seguinte.</summary>
	private bool _rounding;

	/// <summary>A face em que o lobo corre de fato (normal e sentido), a
	/// do último quadro com a parede ao lado, e não na frente nem atrás.
	/// Chegando perto de um pilar ou de uma saliência, o sentido gira para
	/// contorná-la; a quina seguinte é medida pela face, não por esse
	/// sentido torto (era o que fazia a parede "acabar" no meio).</summary>
	private Vector3 _faceNormal, _faceAlong;

	/// <summary>A face foi vista de lado desde que grudou (ou virou num canto):
	/// sem isto, a normal guardada é a do grudar, que perto de uma ponta sai
	/// na diagonal.</summary>
	private bool _faceKnown;

	/// <summary>Subindo por cima de um obstáculo à frente.</summary>
	private bool _climbing;

	/// <summary>O lobo está passando por cima de algo na parede (para os testes).</summary>
	public bool IsWallClimbing => _onWall && _climbing;

	/// <summary>
	/// Corrida em parede. Só no lobo, só no ar, só na descida do pulo,
	/// só segurando para frente.
	///
	/// Retorna true se assumiu o controle do movimento neste frame. Os
	/// passos, na ordem: pode correr? → acha a parede → gruda, segue (quina
	/// e canto) ou tolera um instante sem ela → tempo acabou? → salto da
	/// parede → canto à frente → correr ao longo dela.
	/// </summary>
	private bool TryWallRun(double delta)
	{
		if (_wallJumpGrace > 0) _wallJumpGrace -= delta;

		if (!WantsWallRun())
		{
			ReleaseWall();
			return TryLateWallJump();
		}

		bool found = ProbeWall(out Vector3 normal, out float wallDistance);

		if (!_onWall)
		{
			if (!TryGrabWall(found, normal)) return TryLateWallJump();
		}
		else if (found)
		{
			if (!FollowWall(ref normal, ref wallDistance)) return TryLateWallJump();
		}
		else if (!KeepWithoutWall(delta))
		{
			return TryLateWallJump();
		}

		if (WallRunTimedOut(delta)) return TryLateWallJump();

		// Salto da parede: empurra para fora e para cima. É a saída que
		// transforma a corrida em jogada ofensiva.
		if (WantsJump())
		{
			JumpOffWall();
			return true;
		}

		Vector3 velocity = Velocity;
		if (!TurnIntoCorner(ref velocity)) return false;

		RunAlongWall(velocity, wallDistance, delta);
		return true;
	}

	/// <summary>O salto da parede logo depois de sair dela: dentro de
	/// <see cref="WallJumpGrace"/>, ainda no ar, o pulo sai como se o lobo
	/// estivesse na parede (pela última parede, para onde a câmera olha).
	/// É o que deixa soltar o W e apertar espaço um instante depois.</summary>
	private bool TryLateWallJump()
	{
		if (_wallJumpGrace <= 0 || !_form.IsWolf || IsOnFloor() || IsStunned) return false;
		if (!WantsJump()) return false;

		WallLog("wall jump right after letting go");
		JumpOffWall();
		return true;
	}

	/// <summary>Lobo, no ar, sem atordoamento, e segurando W.</summary>
	private bool WantsWallRun()
	{
		if (!_form.IsWolf || IsOnFloor() || IsStunned) return false;

		// A corrida é sustentada pelo W: pula-se contra a parede e
		// segura para frente. Soltar solta a parede.
		//
		// Deixar o pulo livre é o que torna o salto entre paredes uma
		// jogada: segura-se o W o tempo todo e o pulo serve só para
		// trocar de parede, sem disputar com a tecla que sustenta.
		//
		// O botão de ataque não mexe na corrida (Jhon, 24/11: "se eu
		// estiver na parede, apertar o botão de ataque não deve parar o
		// lobo, ele deve continuar andando"). Antes ele agarrava: o lobo
		// freava até parar e ficava, mesmo sem o W.
		if (!_intent.Forward)
		{
			// Este era o caminho mudo, e é o mais provável de todos:
			// chega-se à parede sem nada segurando a corrida e o
			// código voltava antes de olhar, então o log ficava calado
			// como se estivesse tudo bem.
			if (!IsOnFloor()) WallLog("hold W to run on the wall");

			return false;
		}

		return true;
	}

	/// <summary>Gruda numa parede achada, se as regras deixam. Falso: não
	/// grudou (e o motivo vai para o log).</summary>
	private bool TryGrabWall(bool found, Vector3 normal)
	{
		// Uma corrida por ida ao ar. Sem isto o lobo regruda na
		// mesma altura logo após o salto e a parede vira elevador.
		if (_wallCooldown > 0)
		{
			WallLog("still on cooldown since the last run");
			return false;
		}

		if (!found)
		{
			WallLog($"none within {WallCheckDistance}m");
			return false;
		}

		// Subindo, só gruda depois de tirar os pés do chão o bastante
		// (WallGrabHeight); abaixo disso espera o alto do pulo. Grudar
		// rente ao chão transformava todo esbarrão em corrida na parede.
		if (Velocity.Y > WallGrabMaxRise && IsNearFloor(WallGrabHeight))
		{
			WallLog("rising — still too low to stick");
			return false;
		}

		if (IsNearFloor())
		{
			WallLog("too close to the floor");
			return false;
		}

		// Indo embora da parede não gruda: passou da ponta dela, ou
		// saltou para longe. Sem isto a ponta que o lobo acabou de
		// deixar o puxava de volta.
		if (new Vector3(Velocity.X, 0, Velocity.Z).Dot(normal) > WallLeaveSpeed)
		{
			WallLog("moving away from the wall");
			return false;
		}

		// A mesma parede, sem ter tocado o chão, viraria elevador:
		// sobe, salta, regruda mais alto, repete. Então nela o lobo só
		// regruda descendo até a altura de onde saltou — nunca acima.
		// Uma parede diferente é outra coisa — é a jogada de quicar
		// entre as duas paredes de um corredor, e essa vale de qualquer
		// altura.
		if (IsLastWall(normal))
		{
			WallLog("same wall — jump to another or touch the floor");
			return false;
		}

		// Chegando pela ponta de uma placa, de uma parede curta ou de um bloco,
		// a sonda acha a aresta antes da face: grudar ali era grudar numa
		// normal torta e, no quadro seguinte, achar a "ponta da parede fina" e
		// sair reto — numa placa de 1 m o lobo nem chegava a correr nela. A
		// face está logo à frente: espera os poucos centímetros até ficar ao
		// lado dela e gruda reto.
		if (!_wallRound && FaceJustAhead(normal))
		{
			WallLog("at the edge: waits to be beside the face");
			return false;
		}

		return CommitGrab(normal);
	}

	/// <summary>
	/// A parede achada é uma aresta (de frente para ela não há face reta) e
	/// a face está logo à frente, no sentido em que o corpo vai, ao lado
	/// dele: até 0,8 m adiante, um raio para o lado da parede acha uma face
	/// virada para o corpo. Se já há face ao lado do corpo, não é ponta: não espera.
	/// </summary>
	private bool FaceJustAhead(Vector3 normal)
	{
		var flat = new Vector3(Velocity.X, 0, Velocity.Z);
		if (flat.Length() < 0.5f || FaceStraightAhead(normal)) return false;
		Vector3 m = flat.Normalized();
		Vector3 side = -(normal - m * normal.Dot(m));
		if (side.Length() < 0.2f) return false;    // a parede está à frente, não ao lado
		side = side.Normalized();

		var space = GetWorld3D().DirectSpaceState;
		var exclude = new Godot.Collections.Array<Rid> { GetRid() };
		foreach (float ahead in new[] { 0f, 0.25f, 0.5f, 0.8f })
		{
			Vector3 from = GlobalPosition + m * ahead;
			var query = PhysicsRayQueryParameters3D.Create(from, from + side * (WallHoldDistance + WallCheckDistance));
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0 || hit["collider"].Obj is CharacterBody3D || IsInvisibleLimit(hit["collider"].Obj)) continue;
			Vector3 n = (Vector3)hit["normal"];
			n.Y = 0;
			if (n.LengthSquared() < 1e-6f || n.Normalized().Dot(-side) <= 0.9f) continue;
			// Já ao lado da face, na altura do corpo: a aresta torta que a
			// sonda achou é um enfeite da fachada, não a ponta da peça. Esperar
			// ali fazia o lobo grudar um quadro depois numa normal mais torta e
			// soltar (Distrito Neon, x −40, z 2,5: 0,56 m na parede). Gruda já.
			return ahead > 0f;
		}
		return false;
	}

	/// <summary>Gruda de fato: daqui em diante o lobo corre nesta parede.</summary>
	private bool CommitGrab(Vector3 normal)
	{
		_onWall = true;
		_wallTimer = WallRunMaxTime;
		_wallNormal = normal;
		_cornerGrace = 0;
		_rounding = false;

		// Numa quina côncava o sentido escolhido pode apontar para
		// dentro da parede vizinha. Vira para o outro lado antes de
		// desistir, senão o lobo solta no mesmo frame em que gruda.
		if (!ResolveDirection(normal, out _wallAlong))
		{
			WallLog("both directions are blocked");
			_onWall = false;
			return false;
		}

		_faceNormal = _wallNormal;
		_faceAlong = _wallAlong;
		_faceKnown = FaceStraightAhead(normal);
		_climbing = false;
		WallLog($"stuck, normal {_wallNormal}, direction {_wallAlong}");
		// Na parede não se bate: a garrada que vinha do ar some, e o relógio dela
		// junto — ao sair da parede, o clique arranha na hora (Jhon, 24/11).
		_weapon.CutClaw();
		return true;
	}

	/// <summary>
	/// Partida autoritativa: o servidor confirmou um passo com o corpo na
	/// parede em que este jogo estava fora dela (Jhon, 21/11: online o lobo
	/// "corria sem animação, em pé, mas andando na parede"). Com a rede
	/// oscilando, o servidor às vezes dava um passo a mais ou a menos (a
	/// fila de comandos, até 22/11 — veja CommandBuffer); a
	/// correção acerta só a posição, e um puxão de centímetros bastava
	/// para este jogo soltar a parede. Solto, ele não regrudava — a regra
	/// da mesma parede e a recarga são para o jogador, não para a correção
	/// — e o servidor seguia na parede: o corpo era arrastado por ela de
	/// pé, na pose do pulo. Aqui o jogo volta para a parede, se ainda está
	/// no ar, segurando W e com ela ao alcance.
	///
	/// O contrário (o jogo na parede e o servidor não) não solta aqui: um
	/// passo de diferença no grudar viraria soltar e regrudar.
	/// </summary>
	public bool ResumeWallFromServer() => ResumeWall("the server is on the wall: the client goes back to it");

	/// <summary>
	/// O outro lado diz que o corpo está na parede e este não está: volta
	/// para ela, sem a regra da mesma parede nem a recarga (essas são para o
	/// jogador, não para acertar os dois lados), mas com as regras do corpo
	/// — no ar, lobo, segurando W, a parede ao alcance. O jogo
	/// usa quando o servidor confirma a parede; o servidor, quando o jogo
	/// conta que está nela (<see cref="InputCommand.OnWall"/>; Jhon, 22/11:
	/// um tropeço do servidor tirava o corpo dele da parede e ele não
	/// voltava, e a correção arrastava o lobo de pé pela parede).
	/// </summary>
	/// <summary>Por que o último ResumeWall não voltou (o log do servidor).</summary>
	public string WallResumeBlock { get; private set; } = "";

	public bool ResumeWall(string why)
	{
		WallResumeBlock = _onWall ? "already on it" : !_form.IsWolf ? "not a wolf" : IsOnFloor() ? "on the floor"
			: IsStunned ? "stunned" : _moves.Controls ? "in a move" : !_intent.Forward ? "not holding W"
			: "";
		if (WallResumeBlock != "") return false;
		if (!ProbeWall(out Vector3 normal, out _))
		{
			WallResumeBlock = $"no wall within {WallCheckDistance}m";
			return false;
		}

		_wallCooldown = 0;
		if (!CommitGrab(normal))
		{
			WallResumeBlock = "could not grab it";
			return false;
		}
		WallResumeBlock = "";
		WallLog(why);
		return true;
	}

	/// <summary>Já na parede e ela continua ali: trata a quina (contorna ou
	/// sai reto) e deita o sentido no plano da parede de agora. Falso: saiu
	/// reto da ponta de uma parede fina.</summary>
	private bool FollowWall(ref Vector3 normal, ref float wallDistance)
	{
		// Atravessando um vão (porta, janela, o espaço entre duas casas): segue
		// reto pela linha da parede, sem puxar para dentro dele, até a parede
		// voltar ao lado. Vem antes da parede redonda: a sonda dentro do vão
		// pega o batente ou um poste e o via como coisa redonda, e a face da
		// parede virava a diagonal dele (26/11, Neon x −32 z −10: o lobo virava
		// para dentro do vão e soltava a 5,5 m).
		if (_gapLeft > 0f)
		{
			if (normal.Dot(_faceNormal) > 0.85f && Mathf.Abs(normal.Dot(_wallAlong)) < 0.2f)
			{
				_gapLeft = 0f;
				WallLog("across the gap: back on the wall");
			}
			else
			{
				normal = _faceNormal;
				wallDistance = WallHoldDistance;
				_wallNormal = normal;
				_cornerGrace = 0;
				return true;
			}
		}

		// Parede redonda (tonel, tanque, pilar cilíndrico): não tem quina.
		// A normal gira um pouco a cada quadro e o sentido gira junto, no
		// plano dela — o lobo dá a volta inteira, sem soltar. Antes a curva
		// contava como "ponta de parede fina": ele saía reto, caía,
		// grudava de novo e repetia (Jhon, 15/11, tonel da Estação Polar).
		if (_wallRound)
		{
			Vector3 around = _wallAlong - normal * normal.Dot(_wallAlong);
			around.Y = 0;
			if (around.LengthSquared() > 0.04f) _wallAlong = around.Normalized();
			_wallNormal = normal;
			_cornerGrace = 0;
			_rounding = false;
			RememberFace(normal);
			return true;
		}

		// O ponto de parede mais perto ficou para trás: ou a parede
		// acabou (quina) ou o lobo acabou de virar num canto e a parede
		// de trás é a antiga. No canto há outra parede ao lado.
		if (normal.Dot(_wallAlong) * wallDistance > WallEndMargin
			&& ProbeWall(out Vector3 beside, out float besideDistance, behind: true)
			&& beside.Dot(_wallAlong) * besideDistance <= WallEndMargin)
		{
			normal = beside;
			wallDistance = besideDistance;
		}

		// Passou da ponta. Três casos:
		//  - a parede continua logo atrás da ponta (o degrau da fachada, o
		//    fim de um pilar, de um batente, de uma saliência): segue colado
		//    nela, no mesmo sentido. Era o caso mais comum nos mapas de
		//    verdade, e caía no terceiro: o lobo largava a parede e saía reto;
		//  - é uma quina de verdade, com a face seguinte larga (bloco,
		//    contêiner, pilar): contorna e continua nela;
		//  - é a ponta de uma parede fina: segue reto (contornar ali era
		//    voltar pelo outro lado).
		float side = normal.Dot(_wallAlong);
		if (side * wallDistance > WallEndMargin && !_rounding)
		{
			if (WallGoesOn(normal, wallDistance, out Vector3 behind, out float behindDistance, out float gap))
			{
				normal = behind;
				wallDistance = gap > 0f ? WallHoldDistance : behindDistance;
				_wallAlong = _faceAlong;
				side = 0f;
				_gapLeft = gap;
				WallLog(gap > 0f ? "a gap in the wall: keeps running across it" : "the wall goes on behind the edge");
			}
			else if (!NextFaceIsWide(normal, wallDistance, _faceKnown))
			{
				WallLog("thin wall end: keep going straight");
				LeaveWallStraight();
				return false;
			}
			else
			{
				_rounding = true;
				WallLog("corner: wraps to the next face");
			}
		}

		_wallNormal = normal;
		_cornerGrace = 0;

		// O sentido deita no plano da parede de agora: no canto
		// (côncavo) sempre, e na quina enquanto contorna — a normal gira
		// em volta da aresta e o sentido gira junto, sem tranco. Chegou
		// na face seguinte (a normal parou de girar), acabou a quina.
		// Nunca vira para trás.
		if (side < 0f || _rounding)
		{
			Vector3 along = _wallAlong - normal * side;
			along.Y = 0;
			if (along.LengthSquared() > 0.04f)
				_wallAlong = along.Normalized();
		}

		if (_rounding && Mathf.Abs(side) < 0.01f)
			_rounding = false;

		// A parede ao lado (nem à frente nem atrás): é a face da corrida.
		if (!_rounding && Mathf.Abs(side) < 0.05f)
			RememberFace(normal);

		return true;
	}

	/// <summary>Ao grudar: de frente para a parede há uma face reta, virada
	/// para o corpo (não uma ponta, onde a normal do grudar sai na diagonal)?</summary>
	private bool FaceStraightAhead(Vector3 normal)
	{
		var query = PhysicsRayQueryParameters3D.Create(GlobalPosition, GlobalPosition - normal * (WallHoldDistance + WallCheckDistance));
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0) return false;
		Vector3 n = (Vector3)hit["normal"];
		n.Y = 0;
		return n.LengthSquared() > 1e-6f && n.Normalized().Dot(normal) > 0.98f;
	}

	/// <summary>Guarda a face em que o lobo corre: a normal e o sentido ao
	/// longo dela, no plano dela.</summary>
	private void RememberFace(Vector3 normal)
	{
		Vector3 along = _wallAlong - normal * normal.Dot(_wallAlong);
		along.Y = 0;
		if (along.LengthSquared() < 0.04f) return;
		_faceNormal = normal;
		_faceAlong = along.Normalized();
		_faceKnown = true;
	}

	/// <summary>
	/// Passou da ponta da face e a parede continua: logo atrás dela, virada
	/// para o mesmo lado, até <see cref="WallStepBack"/> mais funda (o degrau
	/// de uma fachada, o fim de um pilar ou de uma saliência, a porta de um
	/// contêiner) — ou depois de um vão de até <see cref="WallGapMax"/> (porta,
	/// janela, o espaço entre duas casas). Raios de frente para a parede,
	/// passando da ponta, procuram essa continuação. Devolve a normal, a
	/// distância até ela e, se há vão, quanto falta atravessar (0 sem vão).
	/// </summary>
	private bool WallGoesOn(Vector3 normal, float distance, out Vector3 behind, out float behindDistance, out float gap)
	{
		behind = Vector3.Zero;
		behindDistance = 0f;
		gap = 0f;
		if (!_faceKnown) return false;

		Vector3 edge = GlobalPosition - normal * distance;
		var exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var space = GetWorld3D().DirectSpaceState;
		// O que vem depois da ponta, de 40 em 40 cm: a face de novo na linha da
		// antiga (um vão, ou só uma junta) ou mais funda (um degrau para trás).
		// A linha da antiga ganha: num batente de porta, entrar no recuo e sair
		// dele era um zigue-zague que acabava travado; reto pela linha, o lobo
		// passa o vão como passa a porta.
		Vector3 stepNormal = Vector3.Zero;
		float stepDistance = 0f;
		for (float ahead = 0.35f; ahead <= WallGapMax + 0.01f; ahead += 0.4f)
		{
			Vector3 from = edge + _faceAlong * ahead + _faceNormal * WallHoldDistance;
			var query = PhysicsRayQueryParameters3D.Create(from, from - _faceNormal * (WallHoldDistance + WallStepBack));
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0) continue;
			// Algo na linha do corpo, ou o limite do mapa: a parede não continua por ali.
			if (hit["collider"].Obj is CharacterBody3D || IsInvisibleLimit(hit["collider"].Obj)) break;

			Vector3 n = (Vector3)hit["normal"];
			n.Y = 0;
			Vector3 point = (Vector3)hit["position"];
			float depth = (from - point).Dot(_faceNormal) - WallHoldDistance;   // atrás da linha da face antiga
			// Na frente da face antiga: o que salta para fora é obstáculo, não continuação.
			if (depth < -0.05f) break;
			if (n.LengthSquared() < 1e-6f || n.Normalized().Dot(_faceNormal) < 0.85f) continue;
			n = n.Normalized();
			if (BackedByBarrier(point, n)) break;

			if (depth <= 0.15f)
			{
				behind = n;
				behindDistance = Mathf.Max((GlobalPosition - point).Dot(n), 0.05f);
				gap = ahead > 0.4f ? ahead + 0.3f : 0f;
				return true;
			}
			if (ahead < 0.4f)
			{
				stepNormal = n;
				stepDistance = Mathf.Max((GlobalPosition - point).Dot(n), 0.05f);
			}
		}

		if (stepNormal == Vector3.Zero) return false;
		behind = stepNormal;
		behindDistance = stepDistance;
		return true;
	}

	/// <summary>O maior vão na parede que o lobo atravessa correndo nela
	/// (porta, janela, o espaço entre duas casas), em metros.</summary>
	[Export] public float WallGapMax = 1.6f;

	/// <summary>Atravessando um vão: quanto falta, em metros.</summary>
	private float _gapLeft;

	/// <summary>Já na parede e ela sumiu: segue reto por
	/// <see cref="CornerGrace"/> antes de soltar. Falso: soltou.</summary>
	private bool KeepWithoutWall(double delta)
	{
		// No vão não há parede ao lado, e é assim mesmo.
		if (_gapLeft > 0f) return true;

		if (_cornerGrace <= 0)
		{
			// Perdeu a parede: segue reto um instante antes de soltar.
			_cornerGrace = CornerGrace;
			WallLog("lost the wall");
			return true;
		}

		_cornerGrace -= delta;

		if (_cornerGrace <= 0)
		{
			WallLog("let go: nothing to lean on");
			ReleaseWall();
			return false;
		}

		return true;
	}

	/// <summary>Corrida com tempo máximo (<see cref="WallRunMaxTime"/>; zero
	/// é sem limite). Verdadeiro: o tempo acabou e soltou.</summary>
	private bool WallRunTimedOut(double delta)
	{
		if (WallRunMaxTime <= 0) return false;

		_wallTimer -= delta;
		if (_wallTimer > 0) return false;

		ReleaseWall();
		return true;
	}

	/// <summary>O salto da parede.</summary>
	private void JumpOffWall()
	{
		// Conserva o avanço ao longo da parede e soma o empurrão
		// para fora. Sem isso o salto zera tudo que a corrida
		// acumulou, e o lobo sai de lado em vez de seguir em frente
		// — justamente o oposto da jogada de cair em cima de alguém.
		float carried = HumanSpeed * SpeedScale * WallRunSpeedBonus * WallJumpKeepSpeed;

		// Para onde a câmera olha, no chão e para cima.
		Vector3 look = AimCamera is Camera3D aim ? -aim.GlobalBasis.Z : -GlobalBasis.Z;
		float lookUp = Mathf.Clamp(look.Y / 0.7071f, 0f, 1f);
		var flat = new Vector3(look.X, 0, look.Z);
		flat = flat.LengthSquared() > 0.0001f ? flat.Normalized() : _wallAlong;
		float facing = Mathf.Clamp(-flat.Dot(_wallNormal), 0f, 1f);   // 1 = olhando para a parede

		// Ao longo da parede: o lado para onde se olha; olhando reto para
		// ela, o sentido da corrida.
		Vector3 along = flat - _wallNormal * flat.Dot(_wallNormal);
		along = along.LengthSquared() > 0.04f ? along.Normalized() : _wallAlong;
		float push = WallJumpPush * Mathf.Lerp(1f, WallJumpPushFacing, facing);
		Vector3 velocity = along * carried + _wallNormal * push;

		// Sai mais alto do que entrou. É o que permite escalar um
		// corredor ricocheteando entre as duas paredes, e é essa
		// subida que dá ao lobo o ângulo que as armas defendem mal:
		// de cima e por trás. Olhando para cima, mais.
		velocity.Y = JumpVelocity * JumpScale * WallJumpClimb * (1f + WallJumpLookLift * lookUp);

		// O topo da parede ao alcance: sobe nele.
		if (MantleTop() is float rise)
		{
			velocity = along * (carried * 0.3f) - _wallNormal * 3.2f;
			velocity.Y = Mathf.Sqrt(2f * _gravity * (rise + 0.45f));
			WallLog($"climbing over the top, {rise:0.00} m above the feet");
		}
		IsJumpAirborne = true;
		_jumpFromWall = true;
		JumpSpeed = velocity.Y;

		// O salto de parede também é um pulo. Faltava avisar, e no
		// modo lobo é justamente este o pulo que se usa o tempo
		// todo — então o corpo saía da parede sem trocar de pose.
		_animator ??= GetNodeOrNull<CharacterAnimator>("Human");
		_animator?.Jump();

		RememberWall();

		ReleaseWall();
		_wallJumpGrace = 0;          // já saltou: a folga é para quem soltou sem saltar
		_wallCooldown = WallRunCooldown;

		Velocity = velocity;
		MoveAndSlide();
	}

	/// <summary>Quanto o topo da parede está acima dos pés, se dá para
	/// subir nele saltando: chão firme logo atrás da quina, até
	/// <see cref="WallMantleReach"/> acima, com espaço para o corpo em pé.
	/// Null se a parede continua alta ou o topo não serve de chão.</summary>
	private float? MantleTop()
	{
		var space = GetWorld3D().DirectSpaceState;
		float feet = GlobalPosition.Y + FeetOffset;
		Vector3 over = GlobalPosition - _wallNormal * (WallHoldDistance + 0.55f);
		Vector3 from = new Vector3(over.X, feet + WallMantleReach + 0.3f, over.Z);

		var query = PhysicsRayQueryParameters3D.Create(from, new Vector3(from.X, feet - 0.1f, from.Z));
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		// Começando dentro de algo (muro mais alto que o alcance, o
		// contêiner de cima de uma pilha), o raio para ali, sem normal:
		// não é topo.
		query.HitFromInside = true;
		var hit = space.IntersectRay(query);
		if (hit.Count == 0 || hit["collider"].Obj is CharacterBody3D) return null;
		if (((Vector3)hit["normal"]).Y < 0.7f) return null;

		float rise = ((Vector3)hit["position"]).Y - feet;
		if (rise < 0.3f || rise > WallMantleReach) return null;

		// Espaço em pé em cima.
		Vector3 top = (Vector3)hit["position"];
		var head = PhysicsRayQueryParameters3D.Create(top + Vector3.Up * 0.05f, top + Vector3.Up * (StandHeight - 0.1f));
		head.Exclude = query.Exclude;
		if (space.IntersectRay(head).Count > 0) return null;

		return rise;
	}

	/// <summary>Falso: o caminho está fechado à frente e soltou.</summary>
	private bool TurnIntoCorner(ref Vector3 velocity)
	{
		// Parede à frente: é um canto. Ela vira a parede da corrida e o
		// lobo segue por ela, para longe da antiga, no mesmo passo. Antes
		// ele soltava e regrudava na parede de trás, voltando.
		if (!IsBlocked(_wallAlong, out Vector3 front)) return true;

		// O que fecha à frente é o limite invisível do mapa: não se vira
		// para ele (seria correr em parede que não se vê). Solta.
		if (_blockedByLimit)
		{
			WallLog("let go, invisible map boundary ahead");
			ReleaseWall();
			return false;
		}

		// Obstáculo pequeno, com o topo ao alcance: não é canto, o lobo passa
		// por cima dele (RunAlongWall sobe).
		if (ClimbNeeded(_wallAlong) is float climb && climb > 0f) return true;

		Vector3 turn = _wallNormal - front * _wallNormal.Dot(front);
		turn.Y = 0;

		if (turn.LengthSquared() < 0.04f || IsBlocked(turn.Normalized(), out _))
		{
			WallLog("let go, path blocked ahead");
			ReleaseWall();
			return false;
		}

		float pace = new Vector3(Velocity.X, 0, Velocity.Z).Dot(_wallAlong);
		_wallAlong = turn.Normalized();
		_wallNormal = front;
		_faceNormal = front;
		_faceAlong = _wallAlong;
		_faceKnown = true;
		velocity = _wallAlong * Mathf.Max(pace, 0f) + Vector3.Up * velocity.Y;
		WallLog("inner corner: continues on the wall ahead");
		return true;
	}

	/// <summary>A parede continua ao lado <paramref name="rise"/> metros acima:
	/// subir só vale ao lado dela; acima do topo, o lobo sairia da parede e
	/// cairia (aí valem a quina e o salto da parede).</summary>
	private bool WallBesideAt(float rise)
	{
		Vector3 face = _faceKnown ? _faceNormal : _wallNormal;
		Vector3 from = GlobalPosition + Vector3.Up * rise;
		var query = PhysicsRayQueryParameters3D.Create(from, from - face * (WallHoldDistance + WallCheckDistance));
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 && hit["collider"].Obj is not CharacterBody3D && !IsInvisibleLimit(hit["collider"].Obj);
	}

	/// <summary>
	/// Quanto o lobo precisa subir para passar por cima do que está à frente
	/// na parede: 0 se o caminho está livre; nulo se o que fecha não é
	/// pequeno (mais alto que <see cref="WallClimbReach"/>, ou algo em cima
	/// não deixa subir) — aí vale a quina ou a parada de sempre.
	///
	/// A cápsula do corpo inteiro varre o caminho (<see cref="WallRunClearance"/>
	/// à frente): o raio do meio do corpo não via o que bate na cabeça ou nas
	/// patas, e o corpo deslizava por baixo do obstáculo ou parava nele e
	/// caía (Jhon, 26/11: "passa por baixo de obstáculos pequenos em vez de
	/// subir neles").
	/// </summary>
	private float? ClimbNeeded(Vector3 along)
	{
		Vector3 ahead = along * WallRunClearance;
		Transform3D at = GlobalTransform;
		var hit = new KinematicCollision3D();
		if (!TestMove(at, ahead, hit)) return 0f;

		// A própria parede ao lado (raspando) não fecha o caminho; gente e o
		// limite do mapa não se escalam.
		if (hit.GetCollider() is CharacterBody3D || IsInvisibleLimit(hit.GetCollider())) return null;
		Vector3 n = hit.GetNormal();
		if (n.Y < 0.7f && new Vector3(n.X, 0, n.Z).Dot(along) > -0.3f) return 0f;

		for (float h = 0.1f; h <= WallClimbReach + 0.001f; h += 0.1f)
		{
			if (TestMove(at, Vector3.Up * h)) return null;          // algo em cima
			Transform3D up = at;
			up.Origin += Vector3.Up * h;
			if (!TestMove(up, ahead)) return WallBesideAt(h) ? h : null;
		}
		return null;
	}

	/// <summary>Corre ao longo da parede, na altura em que grudou, colado a
	/// <see cref="WallHoldDistance"/> dela.</summary>
	private void RunAlongWall(Vector3 velocity, float wallDistance, double delta)
	{
		// A altura fica a do instante em que grudou. O pulo não é morto
		// por isso: o lobo só gruda no alto do pulo (WallGrabMaxRise), e
		// pular colado na parede sobe primeiro e gruda depois.
		// Obstáculo pequeno à frente: sobe por cima dele e segue na altura
		// nova.
		float climb = ClimbNeeded(_wallAlong) ?? 0f;
		if (climb > 0f && !_climbing) WallLog($"small obstacle ahead: climbs {climb:0.0} m over it");
		_climbing = climb > 0f;
		velocity.Y = _climbing ? Mathf.Min(climb / (float)delta, WallClimbSpeed) : WallRunVerticalDrift;

		float target = HumanSpeed * SpeedScale * WallRunSpeedBonus;

		// Em volta de um poste a volta é curta: com o passo inteiro o lobo
		// rodopiava. A volta tem um teto de giro (WallOrbitMaxTurn); num
		// objeto largo, como o tonel, o teto nem chega.
		if (_wallRound && WallOrbitMaxTurn > 0f)
		{
			float radius = new Vector2(GlobalPosition.X, GlobalPosition.Z).DistanceTo(_wallCenter);
			target = Mathf.Min(target, Mathf.DegToRad(WallOrbitMaxTurn) * radius);
		}

		// O passo muda aos poucos: acelera ao grudar, em vez de saltar de
		// parado para correndo num quadro. O olhar continua livre —
		// a direção é a escolhida ao grudar.
		float current = new Vector3(velocity.X, 0, velocity.Z).Dot(_wallAlong);
		float rate = target < current ? WallBrake : WallAccel;
		float speed = Mathf.MoveToward(current, target, rate * (float)delta);

		// Puxada contra a parede proporcional à folga: mantém o corpo a
		// WallHoldDistance dela. Força fixa batia na quina e empurrava o
		// lobo para dentro do canto; proporcional, ele contorna a aresta
		// na mesma distância. Sem parede (instante de tolerância), nada.
		float stick = _cornerGrace > 0 || _gapLeft > 0f
			? 0f
			: Mathf.Clamp((wallDistance - WallHoldDistance) * 12f, -2f, WallStickForce);

		velocity.X = _wallAlong.X * speed - _wallNormal.X * stick;
		velocity.Z = _wallAlong.Z * speed - _wallNormal.Z * stick;

		Velocity = velocity;
		MoveAndSlide();

		// Bateu em algo à frente e não anda: solta e cai. Mede-se o quanto o
		// corpo de fato andou ao longo da parede contra o que devia andar
		// (no começo da corrida, acelerando, os dois andam juntos).
		Vector3 real = GetRealVelocity();
		float moved = new Vector3(real.X, 0, real.Z).Dot(_wallAlong);
		if (_gapLeft > 0f)
		{
			_gapLeft -= Mathf.Max(moved, 0f) * (float)delta;
			if (_gapLeft <= 0f)
			{
				// O vão era maior do que a parede prometia: solta, como na ponta de parede fina.
				WallLog("the gap did not close: let go");
				LeaveWallStraight();
				return;
			}
		}
		// Subindo por cima de algo, o avanço espera a subida: não é estar preso.
		_wallStall = target > 1f && moved < target * 0.3f && !_climbing ? _wallStall + delta : 0;
		if (_wallStall >= WallStallTime)
		{
			WallLog("let go, stuck on something ahead");
			ReleaseWall();
			_wallCooldown = Mathf.Max(_wallCooldown, WallStallCooldown);
		}
	}

	/// <summary>Imprime só quando a mensagem muda. Sem isso cada frame
	/// de queda vira uma linha e o sinal some no meio do ruído.</summary>
	private void WallLog(string message)
	{
		if (!WallDebug || message == _lastWallLog) return;

		_lastWallLog = message;
		GD.Print($"wall: {message}");
	}

	/// <summary>É a parede que o lobo acabou de deixar? Mesma direção e
	/// mesmo plano (o corpo estava à mesma distância dela).</summary>
	private bool IsLastWall(Vector3 normal)
	{
		if (_lastWall == Vector3.Zero) return false;
		// Poste, coluna e tonel têm todas as normais: o mesmo objeto é a mesma
		// parede, de qualquer lado. Sem isto saltar do poste e voltar a ele
		// por outro lado subia o poste como elevador.
		// O objeto é a forma, não o corpo: nos mapas um corpo só guarda centenas
		// de formas, e saltar de um poste não pode valer para todos os postes dele.
		if (_lastWallRoundBody.IsValid && _wallRound && _wallBody == _lastWallRoundBody && _wallShape == _lastWallRoundShape) return true;
		if (normal.Dot(_lastWall) <= SameWallThreshold) return false;
		return Mathf.Abs(_lastWall.Dot(GlobalPosition) - _lastWallPlane) < SameWallGap;
	}

	/// <summary>Lembra a parede deixada agora: só uma outra vale até tocar
	/// o chão.</summary>
	private void RememberWall()
	{
		_lastWall = _wallNormal;
		_lastWallHeight = GlobalPosition.Y;
		_lastWallPlane = _wallNormal.Dot(GlobalPosition);
		_lastWallRoundBody = _wallRound ? _wallBody : default;
		_lastWallRoundShape = _wallShape;
	}

	/// <summary>O poste, coluna ou tonel deixado agora (vazio: era parede).</summary>
	private Rid _lastWallRoundBody;
	private int _lastWallRoundShape = -1;

	private float _lastWallPlane;

	private void ReleaseWall()
	{
		if (_onWall && !IsOnFloor())
		{
			RememberWall();
			_wallJumpGrace = WallJumpGrace;
		}
		else if (IsOnFloor())
		{
			_wallJumpGrace = 0;
		}
		_onWall = false;
		_cornerGrace = 0;
		_rounding = false;
		_wallStall = 0;
		_climbing = false;
		_gapLeft = 0f;
	}

	/// <summary>
	/// A face depois da quina tem pelo menos <see cref="WallWrapMinFace"/>?
	/// Um raio paralelo à face que acabou, a essa distância da aresta para
	/// dentro da parede, procura a face seguinte. Numa parede fina ele
	/// passa por trás dela e não acha nada.
	/// </summary>
	private bool NextFaceIsWide(Vector3 normal, float distance, bool byFace)
	{
		// Pela face em que corria (não pelo sentido de agora, que gira ao
		// contornar uma saliência): o raio vai paralelo a ela.
		Vector3 face = byFace ? _faceNormal : _wallNormal;
		Vector3 along = byFace ? _faceAlong : _wallAlong;
		Vector3 edge = GlobalPosition - normal * distance;
		// Um pouco antes da largura mínima: a mureta de exatamente 25 cm, medida
		// pela face, deixava o raio raspar no plano de trás e passar direto (o
		// lobo saía reto na ponta dela; até 26/11 contornava).
		Vector3 from = edge + along * 0.4f - face * (WallWrapMinFace * 0.92f);

		var query = PhysicsRayQueryParameters3D.Create(from, from - along * 0.7f);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0 || hit["collider"].Obj is CharacterBody3D) return false;

		return ((Vector3)hit["normal"]).Dot(along) > 0.7f;
	}

	/// <summary>A parede acabou: solta e segue no sentido da corrida, com
	/// o mesmo passo, sem a puxada contra a parede. Uma recarga curta
	/// leva o corpo para fora do alcance da ponta antes de poder grudar
	/// de novo.</summary>
	private void LeaveWallStraight()
	{
		float pace = new Vector3(Velocity.X, 0, Velocity.Z).Dot(_wallAlong);
		Velocity = _wallAlong * Mathf.Max(pace, 0f) + Vector3.Up * Velocity.Y;
		ReleaseWall();
		_wallCooldown = WallRunCooldown;
	}

	/// <summary>
	/// A direção da corrida sai do movimento que o lobo já tem, não de
	/// para onde a câmera aponta: saltar de uma parede e encostar na
	/// seguinte mantém o sentido sem tocar no mouse.
	///
	/// O olhar só decide quando não há momento horizontal — caso de
	/// quem pula parado contra a parede.
	/// </summary>
	private Vector3 ChooseDirection(Vector3 normal, out bool byMomentum)
	{
		Vector3 along = normal.Cross(Vector3.Up).Normalized();

		Vector3 flat = new Vector3(Velocity.X, 0, Velocity.Z);
		float momentum = flat.Dot(along);

		byMomentum = Mathf.Abs(momentum) > 0.5f;
		if (byMomentum)
			return momentum > 0 ? along : -along;

		return along.Dot(-GlobalTransform.Basis.Z) < 0 ? -along : along;
	}

	/// <summary>
	/// Escolhe o sentido inicial e garante que ele não esbarra de
	/// imediato. Devolve falso quando os dois lados estão fechados —
	/// um canto apertado demais para correr.
	/// </summary>
	private bool ResolveDirection(Vector3 normal, out Vector3 along)
	{
		along = ChooseDirection(normal, out bool byMomentum);

		if (!IsBlocked(along, out _)) return true;

		// Com embalo, o sentido é o do embalo: virar ao contrário fazia o
		// lobo voltar pela parede ao chegar num canto.
		if (byMomentum) return false;

		along = -along;
		return !IsBlocked(along, out _);
	}

	private PhysicsShapeQueryParameters3D _wallProbe;

	/// <summary>
	/// O ponto de parede mais perto do corpo, num cilindro do alcance de
	/// grudar em volta dele. A normal sai do ponto para o corpo, então
	/// numa aresta ela aponta na diagonal e gira suave enquanto o lobo
	/// contorna — o leque de raios antigo pulava de uma face para a outra
	/// na quina. O cilindro fica acima dos pés e abaixo da cabeça: chão e
	/// teto não contam. Gente também não é parede.
	/// </summary>
	private Rid _wallBody;

	/// <summary>A forma do corpo em que a sonda encostou (o índice dela no corpo).</summary>
	private int _wallShape = -1;

	/// <summary>A parede em que a sonda encostou é redonda (cilindro,
	/// cápsula ou esfera de colisão) ou pequena (<see cref="_wallSmall"/>):
	/// o lobo dá a volta nela.</summary>
	private bool _wallRound;

	/// <summary>A parede em que a sonda encostou é um objeto pequeno: o corte
	/// dele na altura do corpo cabe em <see cref="WallPoleMaxWidth"/>.</summary>
	private bool _wallSmall;

	/// <summary>O eixo do objeto redondo ou pequeno (x, z no mundo): a volta é
	/// em torno dele.</summary>
	private Vector2 _wallCenter;

	/// <summary>A medida de cada forma de colisão (corpo, forma, faixa de
	/// meio metro de altura): pequena e o centro. Medir uma malha grande é
	/// caro; a resposta não muda enquanto o mapa não muda. Só números, por
	/// jogador (não é cache de recurso).</summary>
	private readonly System.Collections.Generic.Dictionary<(ulong, int, int), (bool Small, Vector2 Center)> _wallSizes = new();

	/// <summary>
	/// O corte da forma de colisão na altura do corpo (a faixa da sonda, 0,9 m)
	/// cabe num quadrado de <see cref="WallPoleMaxWidth"/>? Poste, haste de
	/// placa, coluna fina. Malha: só os triângulos que passam pela faixa
	/// contam — o braço de um poste de luz, lá em cima, não faz dele uma
	/// parede. Para de medir assim que passa da largura.
	/// </summary>
	private bool SmallAt(Rid body, int index, Rid shape, PhysicsServer3D.ShapeType type, out Vector2 center)
	{
		center = Vector2.Zero;
		if (WallPoleMaxWidth <= 0f) return false;
		var key = (body.Id, index, Mathf.FloorToInt(GlobalPosition.Y / 0.5f));
		if (_wallSizes.TryGetValue(key, out var known))
		{
			center = known.Center;
			return known.Small;
		}

		Transform3D at = (Transform3D)PhysicsServer3D.BodyGetState(body, PhysicsServer3D.BodyState.Transform)
			* PhysicsServer3D.BodyGetShapeTransform(body, index);
		float low = GlobalPosition.Y - 0.45f, high = GlobalPosition.Y + 0.45f;
		Vector2 min = new(float.MaxValue, float.MaxValue), max = -min;
		bool any = false, small = true;

		void Take(Vector3 p)
		{
			min = min.Min(new Vector2(p.X, p.Z));
			max = max.Max(new Vector2(p.X, p.Z));
			any = true;
			if (max.X - min.X > WallPoleMaxWidth || max.Y - min.Y > WallPoleMaxWidth) small = false;
		}

		Variant data = PhysicsServer3D.ShapeGetData(shape);
		switch (type)
		{
			case PhysicsServer3D.ShapeType.Box:
				Vector3 h = data.AsVector3();
				for (int i = 0; i < 8 && small; i++)
					Take(at * new Vector3((i & 1) == 0 ? -h.X : h.X, (i & 2) == 0 ? -h.Y : h.Y, (i & 4) == 0 ? -h.Z : h.Z));
				break;
			case PhysicsServer3D.ShapeType.ConvexPolygon:
				foreach (Vector3 p in data.AsVector3Array())
				{
					Take(at * p);
					if (!small) break;
				}
				break;
			case PhysicsServer3D.ShapeType.ConcavePolygon:
				Vector3[] faces = data.AsGodotDictionary()["faces"].AsVector3Array();
				for (int i = 0; i + 2 < faces.Length && small; i += 3)
				{
					Vector3 a = at * faces[i], b = at * faces[i + 1], c = at * faces[i + 2];
					if (Mathf.Max(a.Y, Mathf.Max(b.Y, c.Y)) < low || Mathf.Min(a.Y, Mathf.Min(b.Y, c.Y)) > high) continue;
					Take(a); Take(b); Take(c);
				}
				break;
			case PhysicsServer3D.ShapeType.Cylinder:
			case PhysicsServer3D.ShapeType.Capsule:
				float r = data.AsGodotDictionary()["radius"].AsSingle();
				Take(at * new Vector3(-r, 0, -r));
				Take(at * new Vector3(r, 0, r));
				break;
			case PhysicsServer3D.ShapeType.Sphere:
				float s = data.AsSingle();
				Take(at * new Vector3(-s, 0, -s));
				Take(at * new Vector3(s, 0, s));
				break;
			default:
				small = false;
				break;
		}

		small &= any;
		center = small ? (min + max) * 0.5f : Vector2.Zero;
		// Solto, de pé sozinho: o mourão de uma grade, o batente de uma porta
		// ou o cano colado na parede são pequenos, mas a volta neles bateria no
		// que está do lado. Ali vale a parede de sempre.
		if (small && !StandsAlone(body, index, center, (max - min) * 0.5f)) small = false;
		_wallSizes[key] = (small, center);
		return small;
	}

	/// <summary>Nada além do próprio objeto até <see cref="WallHoldDistance"/>
	/// em volta dele, na altura do peito (meio metro em volta do centro do
	/// corpo): dá para dar a volta. O que fica embaixo, na altura das patas
	/// (banco, base, meio-fio), não conta: a volta passa por cima dele
	/// (<see cref="ClimbNeeded"/>).</summary>
	private bool StandsAlone(Rid body, int index, Vector2 center, Vector2 half)
	{
		var query = new PhysicsShapeQueryParameters3D
		{
			Shape = new BoxShape3D { Size = new Vector3(half.X * 2f + WallHoldDistance * 2f, 0.5f, half.Y * 2f + WallHoldDistance * 2f) },
			Transform = new Transform3D(Basis.Identity, new Vector3(center.X, GlobalPosition.Y, center.Y)),
			CollideWithAreas = false,
			Exclude = new Godot.Collections.Array<Rid> { GetRid() }
		};
		foreach (var hit in GetWorld3D().DirectSpaceState.IntersectShape(query, 8))
		{
			if (hit["collider"].Obj is CharacterBody3D) continue;
			if ((Rid)hit["rid"] == body && (int)hit["shape"] == index) continue;
			return false;
		}
		return true;
	}

	private bool ProbeWall(out Vector3 normal, out float distance, bool behind = false)
	{
		normal = Vector3.Zero;
		distance = 0f;

		_wallProbe ??= new PhysicsShapeQueryParameters3D
		{
			Shape = new CylinderShape3D { Radius = WallHoldDistance + WallCheckDistance * 0.55f, Height = 0.9f },
			CollideWithAreas = false
		};

		var space = GetWorld3D().DirectSpaceState;
		var exclude = new Godot.Collections.Array<Rid> { GetRid() };
		_wallProbe.Transform = new Transform3D(Basis.Identity, GlobalPosition);
		_wallProbe.Exclude = exclude;

		foreach (var hit in space.IntersectShape(_wallProbe, 8))
			if (hit["collider"].Obj is CharacterBody3D || IsInvisibleLimit(hit["collider"].Obj)) exclude.Add((Rid)hit["rid"]);

		// A segunda busca, no canto: sem a parede que ficou para trás.
		if (behind && _wallBody.IsValid) exclude.Add(_wallBody);
		_wallProbe.Exclude = exclude;

		var rest = space.GetRestInfo(_wallProbe);
		if (rest.Count == 0) return false;
		if (!behind)
		{
			_wallBody = (Rid)rest["rid"];
			int index = (int)rest["shape"];
			_wallShape = index;
			Rid shape = PhysicsServer3D.BodyGetShape(_wallBody, index);
			var type = PhysicsServer3D.ShapeGetType(shape);
			bool round = type == PhysicsServer3D.ShapeType.Cylinder || type == PhysicsServer3D.ShapeType.Capsule
				|| type == PhysicsServer3D.ShapeType.Sphere;
			// Poste, haste, coluna fina: tão pequeno quanto um tonel é redondo —
			// o lobo dá a volta nele.
			_wallSmall = SmallAt(_wallBody, index, shape, type, out _wallCenter);
			_wallRound = round || _wallSmall;
			if (round && !_wallSmall)
			{
				Transform3D at = (Transform3D)PhysicsServer3D.BodyGetState(_wallBody, PhysicsServer3D.BodyState.Transform)
					* PhysicsServer3D.BodyGetShapeTransform(_wallBody, index);
				_wallCenter = new Vector2(at.Origin.X, at.Origin.Z);
			}
		}

		if (BackedByBarrier((Vector3)rest["point"], (Vector3)rest["normal"])) return false;

		Vector3 away = GlobalPosition - (Vector3)rest["point"];
		away.Y = 0;

		if (away.LengthSquared() < 1e-6f)
		{
			away = (Vector3)rest["normal"];
			away.Y = 0;
			if (away.LengthSquared() < 1e-6f) return false;
		}

		normal = away.Normalized();
		distance = away.Length();
		return true;
	}

	/// <summary>
	/// O muro que fecha o mapa: a parede em que a sonda encostou tem uma
	/// <c>Barreira…</c> logo atrás da face (os mapas de kit põem a caixa
	/// invisível 6 cm para dentro do muro de fora, do chão ao teto). Nele
	/// o lobo não gruda nem sobe: "o lobo não pode andar nem escalar as
	/// limitações do jogo, ele deve cair" (Jhon, 20/11). As <c>Limite…</c>
	/// não contam aqui: no Armazém elas ficam rentes à parede do galpão,
	/// que é de correr.
	/// </summary>
	private bool BackedByBarrier(Vector3 point, Vector3 normal)
	{
		normal.Y = 0;
		if (normal.LengthSquared() < 1e-6f) return false;
		normal = normal.Normalized();

		var space = GetWorld3D().DirectSpaceState;
		var exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var query = PhysicsRayQueryParameters3D.Create(point + normal * 0.2f, point - normal * BarrierBehindWall);
		// A casa recuada do alinhamento fica dentro da barreira: o raio já nasce dentro dela.
		query.HitFromInside = true;
		for (int i = 0; i < 4; i++)
		{
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0) return false;
			if (hit["collider"].Obj is StaticBody3D body && ((string)body.Name).StartsWith("Barreira")) return true;
			exclude.Add((Rid)hit["rid"]);
		}
		return false;
	}

	/// <summary>Até que fundura, atrás da face da parede, uma barreira
	/// invisível faz dela muro de limite.</summary>
	[Export] public float BarrierBehindWall = 0.3f;

	/// <summary>Altura mínima dos pés ao chão para a parede pegar.
	/// Baixa de propósito: alta demais mata a jogada em vez de
	/// filtrar o esbarrão.</summary>
	[Export] public float WallMinHeight = 0.18f;

	/// <summary>Chão logo abaixo dos pés.</summary>
	private bool IsNearFloor() => IsNearFloor(WallMinHeight);

	/// <summary>Chão a menos de <paramref name="height"/> dos pés.</summary>
	private bool IsNearFloor(float height)
	{
		Vector3 origin = GlobalPosition;

		var query = PhysicsRayQueryParameters3D.Create(
			origin, origin + Vector3.Down * (_height * 0.5f + height));
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		return GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0;
	}

	/// <summary>
	/// Há obstáculo de verdade no caminho à frente, ao longo da parede?
	///
	/// Só parede bloqueia. Chão, rampa, meio-fio e beiral de telhado
	/// não — são coisas que o lobo passa por cima, e vetar a corrida
	/// por causa deles fecha os dois sentidos num mapa real.
	///
	/// A distinção não existia antes porque não precisava: com
	/// colisão de uma face, metade da geometria do mapa era invisível
	/// para o raio e quase nada bloqueava. Consertar a colisão fez
	/// este teste passar a acertar tudo, inclusive o que não deveria
	/// contar — um conserto que cria o problema seguinte.
	/// </summary>
	private bool IsBlocked(Vector3 along, out Vector3 normal)
	{
		normal = Vector3.Zero;
		Vector3 origin = GlobalPosition;

		var query = PhysicsRayQueryParameters3D.Create(
			origin, origin + along * WallRunClearance);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		_blockedByLimit = false;
		if (hit.Count == 0) return false;

		Vector3 n = (Vector3)hit["normal"];
		if (n.Dot(along) > 0) n = -n;
		if (Mathf.Abs(n.Y) >= 0.4f) return false;

		n.Y = 0;
		normal = n.Normalized();
		_blockedByLimit = IsInvisibleLimit(hit["collider"].Obj);
		return true;
	}

	/// <summary>O último <see cref="IsBlocked"/> bateu num limite invisível.</summary>
	private bool _blockedByLimit;

	/// <summary>
	/// Parede invisível do limite do mapa: as <c>Limite…</c> de volta do
	/// mapa (<see cref="MapBounds"/>) e as <c>Barreira…</c> por cima do
	/// muro de fora dos mapas de kit. O lobo não gruda nelas — "lobo não
	/// pode correr em parede invisível" (Jhon, 20/11). Elas continuam
	/// segurando o corpo: só a corrida em parede as ignora.
	/// </summary>
	public static bool IsInvisibleLimit(object body)
	{
		if (body is not StaticBody3D node) return false;
		string name = node.Name;
		return name.StartsWith("Limite") || name.StartsWith("Barreira");
	}
}
