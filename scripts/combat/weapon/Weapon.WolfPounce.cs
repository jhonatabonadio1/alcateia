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
	[Export] public float HeavyDamageBonus = 2.1f;
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

	/// <summary>
	/// Botão direito. No chão, o salto; no ar, o bote. Os dois prendem o
	/// lobo até o fim — o salto no lugar, o bote na trajetória — e o
	/// dano dos dois sai quando o lobo de fato cai.
	/// </summary>
	public void TrySpecial()
	{
		if (_cooldown > 0 || _charging) return;
		if (!_form.IsWolf || !_form.CanAttack) return;

		// No chão, o salto espera a recarga; no ar, o bote é outra
		// habilidade e sai sempre.
		if (_body.IsOnFloor())
		{
			if (!PounceReady) return;
			BeginCharge();
		}
		else
			Leap();
	}

	private bool _charging;
	private double _chargeTime;
	private float _pounceRange;

	/// <summary>Agachado carregando o salto (botão direito seguro no chão).</summary>
	public bool IsChargingPounce => _charging && _form != null && _form.IsWolf;

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

			var (direction, distance, aimY) = PounceTarget(PounceCharge);
			PlanPounce(ref distance, direction, out float land, out _, aimY);

			float halfHeight = -((_body as Player)?.FeetOffset ?? -0.875f);
			return _body.GlobalPosition + Vector3.Down * halfHeight + direction * distance + Vector3.Up * land;
		}
	}

	/// <summary>Começa a carregar: o lobo agacha (o começo do clipe do
	/// salto) e fica preso até soltar.</summary>
	private void BeginCharge()
	{
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
		StartPounceRecharge();
		_comboIndex = 0;
		_attackQueued = false;
		float saved = WolfMoves.StepOf(WolfMove.Pounce).Duration
			* (1f - Mathf.Lerp(1f, Mathf.Clamp(PounceFullFlight, 0.2f, 1f), Mathf.Clamp(power, 0f, 1f)));
		_rootLeft = WolfMoves.Duration(WolfMove.Pounce) - elapsed - saved;
		_pounceFlew = false;
		float damage = CombatRules.PounceMultiplier(HeavyDamageBonus, PounceMinPower, power);
		ulong id = StartMove(WolfMove.Pounce, CooldownHeavy, HeavyCooldown,
			() => Claw(damage, ClawReach, ClawRadius, false, _pounceFlew ? _flyDir : null), elapsed + saved);

		// O salto é um pulo de verdade: do impulso até os pés tocarem o
		// chão, o corpo voa num arco. Para onde e até onde, diz a mira no
		// instante de soltar (decisão do Jhon, 17/11): o ponto em que o
		// centro da tela toca o mapa, na distância e na altura dele. Fica
		// fixado aqui — nada segue o alvo durante o voo.
		var (delay, duration, _) = WolfMoves.StepOf(WolfMove.Pounce);
		(_pounceDir, _pounceRange, _pounceAimY) = PounceTarget(power);
		float flight = duration * Mathf.Lerp(1f, Mathf.Clamp(PounceFullFlight, 0.2f, 1f), Mathf.Clamp(power, 0f, 1f));
		PounceFlightRate = duration / Mathf.Max(flight, 0.05f);
		After(Mathf.Max(delay - elapsed, 0f), id, () => BeginPounceFlight(flight));
	}

	/// <summary>Quantas vezes mais rápido que o clipe o voo deste salto
	/// anda (1 sem carga): a animação do voo toca nesse ritmo.</summary>
	public float PounceFlightRate { get; private set; } = 1f;

	private Vector3 _pounceDir = Vector3.Forward;
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
			if (_botAim != null || (_body as Player)?.AimCamera is not Camera3D camera) return null;

			Vector3 forward = -camera.GlobalBasis.Z;
			Vector3 from = camera.GlobalPosition;
			var space = _body.GetWorld3D().DirectSpaceState;

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
				point = FloorBelow(space, someone.GlobalPosition, 4f) ?? someone.GlobalPosition + Vector3.Down * 0.9f;
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
	private (Vector3 Direction, float Distance, float? AimY) PounceTarget(float charge)
	{
		Vector3? aim = PounceAimPoint;
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
		float u = Mathf.Clamp((float)((_flyTime + ahead) / _flyTotal), 0f, 1f);
		float s = (1f - Mathf.Cos(Mathf.Pi * u)) * 0.5f;
		float y = _flyLand * u + 4f * _flyArc * u * (1f - u);
		return _flyStart + _flyDir * (_flyDistance * s) + Vector3.Up * y;
	}

	private void BeginPounceFlight(float duration)
	{
		// Direção e distância guardadas no soltar, medidas de onde o corpo
		// está no impulso. Só o que não dá para atravessar (bloco, muro)
		// encurta — e isso é o mapa, igual para todo mundo.
		Vector3 direction = _pounceDir;
		float distance = _pounceRange;

		float wanted = distance;
		PlanPounce(ref distance, direction, out float land, out float arc, _pounceAimY);

		if (Debug)
		{
			float time = Mathf.Max(duration, 0.05f);
			GD.Print($"salto: anda {distance:0.0} m{(distance < wanted - 0.05f ? $" (queria {wanted:0.0}: obstáculo no caminho)" : "")}, " +
				$"arco {arc:0.0} m, cai {land:+0.0;-0.0} m em {time:0.00} s " +
				$"(sai a {distance / time:0.0} m/s no chão e {(land + 4f * arc) / time:0.0} m/s para cima)");
		}

		_flyStart = _body.GlobalPosition;
		_flyDir = direction;
		_flyDistance = distance;
		_flyLand = land;
		_flyArc = arc;
		_flyTotal = Mathf.Max(duration, 0.05f);
		_flyTime = 0;
		_flying = true;
		_pounceFlew = distance > 0.05f;
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
	private void PlanPounce(ref float distance, Vector3 direction, out float land, out float arc, float? aimY = null)
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
