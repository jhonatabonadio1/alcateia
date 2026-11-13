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

	/// <summary>Até onde o salto no chão leva o lobo com a carga cheia, em
	/// metros. É a arma do lobo para fechar distância: apertar de longe e
	/// cair em cima do humano. Era 12; 30% a menos desde a v0.27.1
	/// (decisão do Jhon).</summary>
	[Export] public float PounceMaxDistance = 8.4f;

	/// <summary>Segundos segurando o botão direito no chão até o salto
	/// carregar inteiro (decisão do Jhon): segurar mais vai mais longe e
	/// bate mais forte. Solto de leve, ele pula perto e fraco.</summary>
	[Export] public float PounceChargeTime = 1.0f;

	/// <summary>Até onde o salto vai sem carga nenhuma, em metros. A carga
	/// leva daqui a <see cref="PounceMaxDistance"/> em linha reta, e só ela
	/// decide (decisão do Jhon): a mesma barra dá sempre o mesmo salto,
	/// com ou sem inimigo na frente, olhando para o chão ou para o céu.</summary>
	[Export] public float PounceMinDistance = 1.5f;

	/// <summary>Fração do dano do salto sem carga. Carregado inteiro, o
	/// dano é o cheio (<see cref="HeavyDamageBonus"/>).</summary>
	[Export] public float PounceMinPower = 0.4f;

	/// <summary>Quanto tempo o lobo segura a carga cheia antes de saltar
	/// sozinho.</summary>
	[Export] public float PounceChargeHold = 0.6f;

	/// <summary>O que o salto passa por cima, em metros acima dos pés: um
	/// contêiner (2,2 m) passa; um bloco (5 m) ou o muro, não — o salto
	/// para antes dele.</summary>
	[Export] public float PounceMaxClear = 2.6f;

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

		if (_body.IsOnFloor())
			BeginCharge();
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

	private void CancelCharge()
	{
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
		_comboIndex = 0;
		_attackQueued = false;
		_rootLeft = WolfMoves.Duration(WolfMove.Pounce) - elapsed;
		_pounceFlew = false;
		float damage = CombatRules.PounceMultiplier(HeavyDamageBonus, PounceMinPower, power);
		ulong id = StartMove(WolfMove.Pounce, CooldownHeavy, HeavyCooldown,
			() => Claw(damage, ClawReach, ClawRadius, false, _pounceFlew ? _flyDir : null), elapsed);

		// O salto é um pulo de verdade: do impulso até os pés tocarem o
		// chão, o corpo voa num arco. Para onde, diz a mira no instante de
		// soltar; até onde, só a carga (decisão do Jhon): a mesma barra dá
		// sempre o mesmo salto. Nada de seguir inimigo nem de parar no ponto
		// mirado — salto que muda sozinho conforme quem está na frente é
		// vantagem que o jogador não controla.
		var (delay, duration, _) = WolfMoves.StepOf(WolfMove.Pounce);
		_pounceRange = CombatRules.PounceRange(PounceMinDistance, PounceMaxDistance, power);
		_pounceDir = PounceDirection();
		After(Mathf.Max(delay - elapsed, 0f), id, () => BeginPounceFlight(duration));
	}

	private Vector3 _pounceDir = Vector3.Forward;
	private bool _pounceFlew;

	/// <summary>Para onde o salto vai, no chão: a direção da câmera do
	/// próprio jogador (<see cref="Player.AimCamera"/>), não a da câmera que
	/// estiver na tela; o bot, para o alvo; sem nenhuma, para a frente do
	/// corpo.</summary>
	private Vector3 PounceDirection()
	{
		Vector3 direction = _botAim
			?? ((_body as Player)?.AimCamera is Camera3D camera
				? -camera.GlobalBasis.Z
				: -_body.GlobalTransform.Basis.Z);

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
		PlanPounce(ref distance, direction, out float land, out float arc);

		if (Debug)
			GD.Print($"salto: anda {distance:0.0} m{(distance < wanted - 0.05f ? $" (queria {wanted:0.0}: obstáculo no caminho)" : "")}, " +
				$"arco {arc:0.0} m, cai {land:+0.0;-0.0} m");

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
	/// </summary>
	private void PlanPounce(ref float distance, Vector3 direction, out float land, out float arc)
	{
		var space = _body.GetWorld3D().DirectSpaceState;
		float halfHeight = -((_body as Player)?.FeetOffset ?? -0.875f);
		Vector3 feet = _body.GlobalPosition + Vector3.Down * halfHeight;
		float top = PounceMaxClear + 0.4f;

		var path = new List<(float X, float H)>();
		for (float x = 0.4f; x <= distance + 0.01f; x += 0.4f)
		{
			float h = GroundAt(space, feet + direction * x, top);
			if (h > PounceMaxClear)
			{
				distance = Mathf.Max(x - 0.8f, 0f);
				break;
			}
			path.Add((x, h));
		}

		float arcMax = PounceMaxClear + 0.6f;
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
				land = GroundAt(space, feet + direction * distance, top);
				if (land <= PounceMaxClear) break;
				distance = Mathf.Max(distance - 0.4f, 0f);
			}
			land = distance > 0.05f ? Mathf.Clamp(land, -1.5f, PounceMaxClear) : 0f;
			arc = Mathf.Clamp(PounceArc + 0.07f * distance, PounceArc, 1.6f);

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

	/// <summary>Altura do chão em <paramref name="at"/>, relativa a ele:
	/// raio de cima para baixo. Começar dentro de algo (um muro mais alto
	/// que o raio) devolve a altura do raio inteira — é obstáculo. Gente
	/// não conta: cair em cima de alguém é o objetivo, não um degrau.</summary>
	private float GroundAt(PhysicsDirectSpaceState3D space, Vector3 at, float top)
	{
		var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * top, at + Vector3.Down * 1.6f);
		query.HitFromInside = true;
		var exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };

		for (int tries = 0; tries < 4; tries++)
		{
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0) return -1.6f;

			if (hit["collider"].Obj is CharacterBody3D)
			{
				exclude.Add((Rid)hit["rid"]);
				continue;
			}

			return ((Vector3)hit["position"]).Y - at.Y;
		}

		return -1.6f;
	}
}
