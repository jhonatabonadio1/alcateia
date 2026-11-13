using Godot;

/// <summary>
/// Corrida em parede do lobo: grudar no alto do pulo, correr ao longo
/// da face, contornar quinas, agarrar com o ataque e saltar da parede.
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
	/// Era 1,25: rápido demais para escolher onde descer. 1,0388 desde a
	/// v0.26.11 (decisão do Jhon): o chão subiu 3% e a parede 7%, então a
	/// parede fica 1,07 / 1,03 acima do chão — Bruto 4,05 m/s, Ligeiro 4,38.
	/// O salto da parede leva 80% disto (<see cref="WallJumpKeepSpeed"/>).</summary>
	[Export] public float WallRunSpeedBonus = 1.0388f;

	/// <summary>Aceleração ao longo da parede, em m/s². A corrida
	/// começa aos poucos em vez de sair a toda no quadro em que gruda.</summary>
	[Export] public float WallAccel = 18f;

	/// <summary>Freada ao agarrar (botão de ataque), em m/s². Mais forte
	/// que a aceleração: agarrar é escolher o ponto, e o lobo para em
	/// meio metro.</summary>
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
	/// Impede regrudar na MESMA parede sem tocar o chão, mas libera
	/// saltar para outra. Sem isso a parede vira elevador; com isso
	/// demais, o lobo não consegue encadear paredes opostas.
	///
	/// Duas normais acima desta semelhança contam como a mesma parede.
	/// </summary>
	[Export] public float SameWallThreshold = 0.85f;

	/// <summary>Tempo mínimo entre uma corrida e a seguinte.</summary>
	[Export] public float WallRunCooldown = 0.12f;

	/// <summary>Imprime por que a corrida em parede não começou. Só
	/// imprime quando o motivo muda, então não enche o Output.</summary>
	[Export] public bool WallDebug = true;

	private bool _onWall;
	private Vector3 _wallNormal;
	private Vector3 _wallAlong;
	private double _cornerGrace;
	private Vector3 _lastWall;
	private bool _clinging;
	private double _wallCooldown;
	private string _lastWallLog = "";
	private double _wallTimer;
	public bool IsWallRunning => _onWall;

	/// <summary>Normal da parede em que o lobo corre (para fora dela).
	/// O corpo do lobo deita com os pés na parede a partir daqui.</summary>
	public Vector3 WallNormal => _onWall ? _wallNormal : Vector3.Zero;

	/// <summary>Sentido da corrida na parede.</summary>
	public Vector3 WallAlong => _onWall ? _wallAlong : Vector3.Zero;

	/// <summary>Contornando uma quina: o sentido gira com a parede até a
	/// face seguinte.</summary>
	private bool _rounding;

	/// <summary>
	/// Corrida em parede. Só no lobo, só no ar, só segurando para frente.
	///
	/// Retorna true se assumiu o controle do movimento neste frame.
	/// </summary>
	/// <summary>
	/// Corrida em parede. Só no lobo, só no ar, só na descida do pulo,
	/// só segurando para frente.
	///
	/// Retorna true se assumiu o controle do movimento neste frame.
	/// </summary>
	private bool TryWallRun(double delta)
	{
		if (!_form.IsWolf || IsOnFloor() || IsStunned)
		{
			ReleaseWall();
			return false;
		}

		// A corrida é sustentada pelo W: pula-se contra a parede e
		// segura para frente. Soltar solta a parede.
		//
		// Deixar o pulo livre é o que torna o salto entre paredes uma
		// jogada: segura-se o W o tempo todo e o pulo serve só para
		// trocar de parede, sem disputar com a tecla que sustenta.
		//
		// O botão de ataque agarra sem avançar — é a pausa que permite
		// escolher a hora de saltar em vez de correr até a parede
		// acabar.
		_clinging = _intent.AttackHeld;

		if (!_intent.Forward && !_clinging)
		{
			// Este era o caminho mudo, e é o mais provável de todos:
			// chega-se à parede sem nada segurando a corrida e o
			// código voltava antes de olhar, então o log ficava calado
			// como se estivesse tudo bem.
			if (!IsOnFloor()) WallLog("segure W para correr na parede");

			ReleaseWall();
			return false;
		}

		bool found = ProbeWall(out Vector3 normal, out float wallDistance);

		if (!_onWall)
		{
			// Uma corrida por ida ao ar. Sem isto o lobo regruda na
			// mesma altura logo após o salto e a parede vira elevador.
			if (_wallCooldown > 0)
			{
				WallLog("ainda em recarga desde a última corrida");
				return false;
			}

			if (!found)
			{
				WallLog($"nenhuma a menos de {WallCheckDistance}m");
				return false;
			}

			// Gruda no alto do pulo, não na subida: grudar subindo levava o
			// impulso para cima da parede e o lobo subia em vez de ficar na
			// altura em que encostou.
			if (Velocity.Y > WallGrabMaxRise)
			{
				WallLog("subindo — gruda no alto do pulo");
				return false;
			}

			if (IsNearFloor())
			{
				WallLog("perto demais do chão");
				return false;
			}

			// Indo embora da parede não gruda: passou da ponta dela, ou
			// saltou para longe. Sem isto a ponta que o lobo acabou de
			// deixar o puxava de volta.
			if (new Vector3(Velocity.X, 0, Velocity.Z).Dot(normal) > WallLeaveSpeed)
			{
				WallLog("indo para longe da parede");
				return false;
			}

			// A mesma parede, sem ter tocado o chão, viraria elevador:
			// sobe, salta, regruda mais alto, repete. Uma parede
			// diferente é outra coisa — é a jogada de quicar entre as
			// duas paredes de um corredor, e essa vale.
			if (_lastWall != Vector3.Zero && normal.Dot(_lastWall) > SameWallThreshold)
			{
				WallLog("mesma parede — salte para outra ou toque o chão");
				return false;
			}

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
				WallLog("os dois sentidos estão bloqueados");
				_onWall = false;
				return false;
			}

			WallLog($"grudou, normal {_wallNormal}, sentido {_wallAlong}");
		}
		else if (found)
		{
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

			// Passou da ponta: é uma quina. Face seguinte larga (bloco,
			// contêiner, pilar), contorna e continua nela. Estreita (a ponta
			// de uma parede fina), segue reto: contornar ali era voltar pelo
			// outro lado.
			float side = normal.Dot(_wallAlong);
			if (side * wallDistance > WallEndMargin && !_rounding)
			{
				if (!NextFaceIsWide(normal, wallDistance))
				{
					WallLog("ponta de parede fina: segue reto");
					LeaveWallStraight();
					return false;
				}

				_rounding = true;
				WallLog("quina: contorna para a face seguinte");
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
		}
		else if (_cornerGrace <= 0)
		{
			// Perdeu a parede: segue reto um instante antes de soltar.
			_cornerGrace = CornerGrace;
			WallLog("perdeu a parede");
		}
		else
		{
			_cornerGrace -= delta;

			if (_cornerGrace <= 0)
			{
				WallLog("soltou: nada para encostar");
				ReleaseWall();
				return false;
			}
		}

		if (WallRunMaxTime > 0)
		{
			_wallTimer -= delta;

			if (_wallTimer <= 0)
			{
				ReleaseWall();
				return false;
			}
		}

		Vector3 velocity = Velocity;

		// Salto da parede: empurra para fora e para cima. É a saída que
		// transforma a corrida em jogada ofensiva.
		if (WantsJump())
		{
			// Conserva o avanço ao longo da parede e soma o empurrão
			// para fora. Sem isso o salto zera tudo que a corrida
			// acumulou, e o lobo sai de lado em vez de seguir em frente
			// — justamente o oposto da jogada de cair em cima de alguém.
			float carried = HumanSpeed * SpeedScale * WallRunSpeedBonus * WallJumpKeepSpeed;

			velocity = _wallAlong * carried + _wallNormal * WallJumpPush;

			// Sai mais alto do que entrou. É o que permite escalar um
			// corredor ricocheteando entre as duas paredes, e é essa
			// subida que dá ao lobo o ângulo que as armas defendem mal:
			// de cima e por trás.
			velocity.Y = JumpVelocity * JumpScale * WallJumpClimb;
			IsJumpAirborne = true;
			_jumpFromWall = true;
			JumpSpeed = velocity.Y;

			// O salto de parede também é um pulo. Faltava avisar, e no
			// modo lobo é justamente este o pulo que se usa o tempo
			// todo — então o corpo saía da parede sem trocar de pose.
			_animator ??= GetNodeOrNull<CharacterAnimator>("Human");
			_animator?.Jump();

			_lastWall = _wallNormal;

			ReleaseWall();
			_wallCooldown = WallRunCooldown;

			Velocity = velocity;
			MoveAndSlide();
			return true;
		}

		// Parede à frente: é um canto. Ela vira a parede da corrida e o
		// lobo segue por ela, para longe da antiga, no mesmo passo. Antes
		// ele soltava e regrudava na parede de trás, voltando.
		if (IsBlocked(_wallAlong, out Vector3 front))
		{
			Vector3 turn = _wallNormal - front * _wallNormal.Dot(front);
			turn.Y = 0;

			if (turn.LengthSquared() < 0.04f || IsBlocked(turn.Normalized(), out _))
			{
				WallLog("soltou, caminho bloqueado à frente");
				ReleaseWall();
				return false;
			}

			float pace = new Vector3(Velocity.X, 0, Velocity.Z).Dot(_wallAlong);
			_wallAlong = turn.Normalized();
			_wallNormal = front;
			velocity = _wallAlong * Mathf.Max(pace, 0f) + Vector3.Up * velocity.Y;
			WallLog("canto: segue pela parede da frente");
		}

		// A altura fica a do instante em que grudou. O pulo não é morto
		// por isso: o lobo só gruda no alto do pulo (WallGrabMaxRise), e
		// pular colado na parede sobe primeiro e gruda depois.
		velocity.Y = WallRunVerticalDrift;

		// Agarrado com o botão de ataque e sem o pulo: fica parado.
		float target = _clinging && !_intent.JumpHeld
			? 0f
			: HumanSpeed * SpeedScale * WallRunSpeedBonus;

		// O passo muda aos poucos: acelera ao grudar e freia ao agarrar,
		// em vez de saltar entre parado e correndo num quadro. É o que
		// deixa parar no ponto certo da parede. O olhar continua livre —
		// a direção é a escolhida ao grudar.
		float current = new Vector3(velocity.X, 0, velocity.Z).Dot(_wallAlong);
		float rate = target < current ? WallBrake : WallAccel;
		float speed = Mathf.MoveToward(current, target, rate * (float)delta);

		// Puxada contra a parede proporcional à folga: mantém o corpo a
		// WallHoldDistance dela. Força fixa batia na quina e empurrava o
		// lobo para dentro do canto; proporcional, ele contorna a aresta
		// na mesma distância. Sem parede (instante de tolerância), nada.
		float stick = _cornerGrace > 0
			? 0f
			: Mathf.Clamp((wallDistance - WallHoldDistance) * 12f, -2f, WallStickForce);

		velocity.X = _wallAlong.X * speed - _wallNormal.X * stick;
		velocity.Z = _wallAlong.Z * speed - _wallNormal.Z * stick;

		Velocity = velocity;
		MoveAndSlide();
		return true;
	}

	/// <summary>Imprime só quando a mensagem muda. Sem isso cada frame
	/// de queda vira uma linha e o sinal some no meio do ruído.</summary>
	private void WallLog(string message)
	{
		if (!WallDebug || message == _lastWallLog) return;

		_lastWallLog = message;
		GD.Print($"parede: {message}");
	}

	private void ReleaseWall()
	{
		_onWall = false;
		_cornerGrace = 0;
		_rounding = false;
	}

	/// <summary>
	/// A face depois da quina tem pelo menos <see cref="WallWrapMinFace"/>?
	/// Um raio paralelo à face que acabou, a essa distância da aresta para
	/// dentro da parede, procura a face seguinte. Numa parede fina ele
	/// passa por trás dela e não acha nada.
	/// </summary>
	private bool NextFaceIsWide(Vector3 normal, float distance)
	{
		Vector3 edge = GlobalPosition - normal * distance;
		Vector3 depth = -_wallNormal;
		Vector3 from = edge + _wallAlong * 0.4f + depth * WallWrapMinFace;

		var query = PhysicsRayQueryParameters3D.Create(from, from - _wallAlong * 0.7f);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0 || hit["collider"].Obj is CharacterBody3D) return false;

		return ((Vector3)hit["normal"]).Dot(_wallAlong) > 0.7f;
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
			if (hit["collider"].Obj is CharacterBody3D) exclude.Add((Rid)hit["rid"]);

		// A segunda busca, no canto: sem a parede que ficou para trás.
		if (behind && _wallBody.IsValid) exclude.Add(_wallBody);
		_wallProbe.Exclude = exclude;

		var rest = space.GetRestInfo(_wallProbe);
		if (rest.Count == 0) return false;
		if (!behind) _wallBody = (Rid)rest["rid"];

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

	/// <summary>Altura mínima dos pés ao chão para a parede pegar.
	/// Baixa de propósito: alta demais mata a jogada em vez de
	/// filtrar o esbarrão.</summary>
	[Export] public float WallMinHeight = 0.18f;

	/// <summary>Chão logo abaixo dos pés.</summary>
	private bool IsNearFloor()
	{
		Vector3 origin = GlobalPosition;

		var query = PhysicsRayQueryParameters3D.Create(
			origin, origin + Vector3.Down * (_height * 0.5f + WallMinHeight));
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
		if (hit.Count == 0) return false;

		Vector3 n = (Vector3)hit["normal"];
		if (n.Dot(along) > 0) n = -n;
		if (Mathf.Abs(n.Y) >= 0.4f) return false;

		n.Y = 0;
		normal = n.Normalized();
		return true;
	}
}
