using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// PERCEPÇÃO: visão e audição independentes alimentando uma crença só —
/// onde o alvo provavelmente está. Escolha de alvo, reação a levar tiro e
/// a vigília depois dele. O bot nunca age sobre a posição real do alvo.
/// </summary>
public partial class Bot
{
	[ExportGroup("Percepção")]
	[Export] public float SightRange = 40f;

	/// <summary>Intervalo entre reavaliações de alvo, em segundos.</summary>
	[Export] public float RetargetInterval = 0.5f;

	/// <summary>
	/// Vantagem, em metros, do alvo atual sobre um candidato novo.
	///
	/// Sem ela, dois inimigos a distâncias parecidas fazem o bot trocar
	/// de alvo a cada reavaliação, e ele passa a luta girando a cabeça
	/// entre os dois sem acertar nenhum.
	/// </summary>
	[Export] public float TargetStickiness = 6f;

	/// <summary>Campo de visão em graus, total.</summary>
	[Export] public float FieldOfView = 140f;

	/// <summary>Tempo que continua acreditando no alvo depois de
	/// perdê-lo de vista.</summary>
	[Export] public float Memory = 3f;

	/// <summary>
	/// Tempo que o bot segue vigiando de onde veio o tiro, depois de
	/// ter se virado.
	///
	/// A memória normal de três segundos serve para perder alguém de
	/// vista numa troca de tiros. Levar um tiro pelas costas é outra
	/// coisa: ali existe alguém, e o bot sabe disso. Esquecer em três
	/// segundos faz ele voltar a patrulhar de costas para o mesmo
	/// corredor, que é o comportamento que faz um bot parecer burro.
	/// </summary>
	[Export] public float Vigilance = 7f;

	/// <summary>Multiplicador do raio dos ruídos. Abaixo de 1 o bot é
	/// surdo para sons distantes.</summary>
	[Export] public float Hearing = 1f;

	/// <summary>Erro ao localizar um som, em metros. Ouvir não é ver:
	/// o bot vai à direção certa, não ao ponto exato.</summary>
	[Export] public float HearingError = 2.5f;

	/// <summary>
	/// Tendência a ir atrás do que ouviu, de 0 a 1.
	///
	/// Sorteada por bot no início, dentro de uma faixa. Dois bots
	/// idênticos reagindo igual ao mesmo tiro entregam que são
	/// scripts; um indo e outro ficando parece decisão. É o mesmo
	/// motivo do erro de mira ter variação: regularidade é o que
	/// denuncia a máquina.
	/// </summary>
	[Export] public float Curiosity = 0.6f;

	/// <summary>Variação sorteada em volta da curiosidade base.</summary>
	[Export] public float CuriositySpread = 0.35f;
	[Export] public float ReactionTime = 0.35f;
	[Export] public float ReactionJitter = 0.2f;
	[Export] public float AlertTurnSpeed = 4.5f;

	private Node3D _target;
	private Health _targetHealth;
	private double _retargetTimer;

	/// <summary>Onde o bot acredita que o alvo está. Toda ação sai
	/// daqui, nunca da posição real.</summary>
	private Vector3 _lastKnown;
	private bool _hasBelief;
	private double _memoryTimer;
	private double _vigilTimer;
	private double _reactionTimer;
	private bool _reacting;
	private bool _alerted;
	private Action<Noise> _ear;
	private double _threatAge = 99;
	private double _beliefAge = 99;

	/// <summary>Visão: precisa estar perto, à frente e sem parede no
	/// meio. Os três, não um dos três.</summary>
	private void UpdateSight(double delta)
	{
		_memoryTimer -= delta;
		UpdateVigilance(delta);

		if (_target == null) return;
		if (_targetHealth != null && _targetHealth.IsDead) return;

		Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
		if (toTarget.Length() > SightRange) return;

		Vector3 flat = new Vector3(toTarget.X, 0, toTarget.Z).Normalized();
		Vector3 facing = -GlobalTransform.Basis.Z;

		float angle = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(flat.Dot(facing), -1f, 1f)));
		if (angle > FieldOfView * 0.5f) return;

		if (!HasLineOfSight()) return;

		Believe(_target.GlobalPosition, Memory);
		TeamIntel.Report(_health.Team, _target, _target.GlobalPosition);
	}

	/// <summary>
	/// Audição. Som não revela o quê, só o onde — e com erro, porque
	/// ouvir não é ver. Um bot que localiza som com precisão cirúrgica
	/// parece trapacear.
	/// </summary>
	private void OnNoise(Noise noise)
	{
		if (_health.IsDead) return;
		if (noise.Source == this) return;

		// Passo de aliado não é pista. Sem isto o time inteiro passaria
		// a partida investigando os próprios passos dentro da base.
		if (noise.Source?.GetNodeOrNull<Health>("Health") is Health source
			&& source.Team == _health.Team)
			return;

		float distance = GlobalPosition.DistanceTo(noise.Position);
		if (distance > noise.Radius * Hearing) return;

		// Já vendo o alvo, som não acrescenta nada.
		if (State == BotState.Combat) return;

		// Nem todo som vira ida. Tiro e explosão chamam mais atenção
		// que um passo, e cada bot tem a própria tendência a investigar.
		float interest = noise.Kind switch
		{
			NoiseKind.Shot => 1f,
			NoiseKind.Explosion => 1f,
			NoiseKind.Howl => 0.9f,
			NoiseKind.Land => 0.6f,
			_ => 0.45f
		};

		// Som perto importa mais que som no limite da audição.
		float closeness = 1f - Mathf.Clamp(distance / (noise.Radius * Hearing), 0f, 1f);
		float chance = Curiosity * interest * (0.45f + closeness * 0.55f);

		if (!_rng.Chance(chance))
		{
			// Ouviu e decidiu ficar. Mesmo assim fica alerta: vira na
			// direção do som sem abandonar a posição.
			_alerted = true;
			_threatAge = 0;

			if (Debug) GD.Print($"{Name} heard {noise.Kind} and stayed");
			return;
		}

		var error = new Vector3(
			_rng.Range(-HearingError, HearingError),
			0,
			_rng.Range(-HearingError, HearingError));

		Believe(noise.Position + error, Memory);

		if (Debug) GD.Print($"{Name} heard {noise.Kind} at {distance:0}m and went to look");
	}

	/// <summary>Registra onde o bot acha que o alvo está.</summary>
	private void Believe(Vector3 position, double duration)
	{
		_lastKnown = position;
		_hasBelief = true;
		_memoryTimer = duration;
		_beliefAge = 0;
	}

	private bool CanSeeTarget()
	{
		if (_target == null) return false;
		if (_targetHealth != null && _targetHealth.IsDead) return false;

		Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
		if (toTarget.Length() > SightRange) return false;

		Vector3 flat = new Vector3(toTarget.X, 0, toTarget.Z).Normalized();
		float angle = Mathf.RadToDeg(
			Mathf.Acos(Mathf.Clamp(flat.Dot(-GlobalTransform.Basis.Z), -1f, 1f)));

		return angle <= FieldOfView * 0.5f && HasLineOfSight();
	}

	/// <summary>
	/// Escolhe o inimigo a perseguir.
	///
	/// Antes o alvo era sempre o jogador, pego uma vez pelo grupo
	/// "player". Com times isso deixa de fazer sentido de duas formas:
	/// o bot aliado mirava em você, e dois bots inimigos passavam um
	/// pelo outro sem se ver.
	///
	/// Agora vale qualquer um vivo do outro time. Quem está à vista
	/// ganha de quem não está, e entre iguais ganha o mais perto — com a
	/// vantagem de <see cref="TargetStickiness"/> para o alvo atual, que
	/// é o que impede a cabeça de ficar indo e voltando entre dois.
	/// </summary>
	private void ChooseTarget()
	{
		Node3D best = null;
		Health bestHealth = null;
		float bestScore = float.MaxValue;

		foreach (var (body, health) in OtherFighters())
		{
			if (health.Team == _health.Team) continue;

			float distance = GlobalPosition.DistanceTo(body.GlobalPosition);
			float score = distance;

			if (distance <= SightRange && CanSee(body)) score -= 1000f;
			if (body == _target) score -= TargetStickiness;

			if (score >= bestScore) continue;

			bestScore = score;
			best = body;
			bestHealth = health;
		}

		// A crença não é zerada na troca, de propósito. Com vários
		// inimigos ela deixa de ser "onde fulano está" e passa a ser "de
		// onde vem a ameaça" — e zerar aqui apagava justamente o tiro
		// pelas costas que a vigília existe para lembrar: o bot levava
		// tiro de um, trocava de alvo meio segundo depois e esquecia de
		// onde o tiro veio.
		// Achou alguém depois de um tempo sem ninguém: avisa no rádio
		// (às vezes; BotRadio segura o flood).
		if (best != null && _target == null && _radioQuiet > 6.0) BotRadio.Instance?.OnSpotted(this);
		if (best != null) _radioQuiet = 0;
		_target = best;
		_targetHealth = bestHealth;
	}

	/// <summary>Os outros corpos vivos da partida — dos dois times —, com
	/// a vida de cada um.</summary>
	private IEnumerable<(Node3D Body, Health Health)> OtherFighters()
	{
		foreach (string group in Groups.Fighters)
		{
			foreach (Node node in GetTree().GetNodesInGroup(group))
			{
				if (node == this || node is not Node3D body) continue;

				// Online: o boneco de quem ainda não chegou (ou já saiu) não está no mundo.
				if (body is Bot { RemoteAbsent: true }) continue;

				var health = body.GetNodeOrNull<Health>("Health");
				if (health == null || health.IsDead) continue;

				yield return (body, health);
			}
		}
	}

	/// <summary>Linha de visão até um corpo qualquer, da cabeça ao peito.</summary>
	private bool CanSee(Node3D body) => ClearShot(body);

	private bool HasLineOfSight() => _target != null && ClearShot(_target);

	/// <summary>Da cabeça ao peito do corpo, nada no meio — fora os aliados
	/// com o fogo amigo desligado: o tiro passa por eles (19/11), então o
	/// bot também não espera o aliado sair da frente para atirar.</summary>
	private bool ClearShot(Node3D body)
	{
		var query = PhysicsRayQueryParameters3D.Create(
			_head.GlobalPosition, body.GlobalPosition + Vector3.Up * 0.4f);

		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;

		for (int pass = 0; pass < 6; pass++)
		{
			var hit = space.IntersectRay(query);
			if (hit.Count == 0) return true;
			if (hit["collider"].Obj == body) return true;

			if (hit["collider"].Obj is CollisionObject3D other && other != body
				&& other.GetNodeOrNull<Health>("Health") is Health health
				&& _health != null && health.Team == _health.Team && !RoomConfig.Rules.FriendlyFire)
			{
				Godot.Collections.Array<Rid> exclude = query.Exclude;
				exclude.Add(other.GetRid());
				query.Exclude = exclude;
				continue;
			}

			return false;
		}

		return false;
	}

	/// <summary>
	/// Levou tiro. Não reage na hora: marca a origem e conta um tempo
	/// curto antes de se virar. Reação instantânea é desumana e anula
	/// o prêmio de atacar pelas costas.
	/// </summary>
	private void OnDamaged(float amount, Vector3 from)
	{
		// Lobo que apanha não fica plantado: troca o lado na hora.
		if (_form != null && _form.IsWolf) { _strafeSide = -_strafeSide; _strafeTimer = StrafeInterval * 0.5; }
		if (!_health.IsDead && _health.Hp < _health.MaxHp * 0.35f) BotRadio.Instance?.OnHurt(this);
		if (_health.IsDead || from == Vector3.Zero || _reacting) return;

		_threatAge = 0;
		_reacting = true;
		_reactionTimer = ReactionTime + _rng.Range(0.0, ReactionJitter);
		Believe(from, Memory);
		TeamIntel.Report(_health.Team, null, from);
	}

	private void UpdateReaction(double delta)
	{
		if (!_reacting) return;

		_reactionTimer -= delta;
		if (_reactionTimer > 0) return;

		_reacting = false;
		_alerted = true;

		// Virou-se. Agora vigia: enquanto este tempo corre, a crença
		// não expira, então o bot continua olhando e indo para onde o
		// tiro veio em vez de voltar à ronda.
		_vigilTimer = Vigilance;
	}

	/// <summary>Mantém viva a crença de onde veio o tiro.</summary>
	private void UpdateVigilance(double delta)
	{
		if (_vigilTimer <= 0) return;

		_vigilTimer -= delta;

		// Só sustenta enquanto não há alvo à vista: ver alguém é uma
		// informação melhor do que lembrar de onde veio um tiro.
		if (State != BotState.Combat && _hasBelief)
			_memoryTimer = Mathf.Max(_memoryTimer, 0.5f);
	}
}
