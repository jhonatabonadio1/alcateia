using Godot;
using System.Collections.Generic;

/// <summary>
/// Aplicar dano: cabeça ou corpo, fogo amigo, crédito do abate e o
/// retorno para quem acertou (marcador, WP). Toda arma e todo golpe
/// terminam aqui.
/// </summary>
public partial class Weapon
{
	/// <summary>Multiplicador de dano na cabeça.</summary>
	[ExportGroup("Dano")]
	[Export] public float HeadshotMultiplier = 2.5f;

	/// <summary>
	/// Altura da zona de cabeça, medida do centro do corpo para cima, para
	/// quem não tem modelo à vista (o boneco do treino, o corpo no
	/// servidor). Quem tem leva tiro na cabeça pela cabeça do modelo
	/// (<see cref="HeadHitbox"/>). A cápsula tem 1,75, então o topo fica
	/// em ~0,87: uma zona a partir de 0,72 deixa a cabeça com cerca de
	/// 15cm, e não o terço superior do corpo.
	/// </summary>
	[Export] public float HeadZone = 0.72f;

	/// <summary>Altura mínima de queda para contar como ataque aéreo.</summary>
	[Export] public float AirAttackMinFall = 2.5f;

	/// <summary>
	/// Este acerto tira sangue? Só o que de fato fere: vivo, fora da
	/// proteção de quem acabou de nascer, e não aliado com o fogo amigo
	/// desligado (o tiro passa sem efeito, então sem sangue também).
	/// </summary>
	private bool Bleeds(Health target)
	{
		if (target == null || target.IsDead || target.IsProtected) return false;
		if (_health != null && target.Team == _health.Team && target != _health
			&& !RoomConfig.Rules.FriendlyFire) return false;
		return true;
	}

	/// <summary>O som do golpe entrando no corpo (pedido do Jhon, 18/11:
	/// "tem que ter o barulho de sangue ao atirar e acertar alguém,
	/// barulho de hit também"). Toca no ponto do acerto, para quem está
	/// por perto; de longe chega fraco. Não é o marcador de acerto, que é
	/// só de quem atirou (<see cref="CombatantAudio"/>).</summary>
	[Export] public string FleshSound = "impacto_carne";
	[Export] public float FleshRadius = 26f;
	[Export] public float FleshReach = 60f;

	/// <summary>Uma rajada não empilha dez impactos no mesmo instante.</summary>
	private const ulong FleshGapMsec = 45;
	private static ulong _lastFlesh;

	/// <summary>O acerto no corpo: o som do impacto e o sangue
	/// (<see cref="Blood"/>).</summary>
	private void Bleed(Health target, Vector3 at, Vector3 direction, float size)
	{
		if (!Bleeds(target)) return;

		ulong now = Time.GetTicksMsec();
		// Quem deu o golpe já ouve o marcador de acerto (CombatantAudio), a
		// cada bala: o impacto no mundo é para os outros, senão dobraria.
		bool mine = _body == Groups.LocalPlayer(_body.GetTree());
		if (!mine && now - _lastFlesh >= FleshGapMsec)
		{
			_lastFlesh = now;
			Sfx.At(FleshSound, at, FleshRadius, size > 1.2f ? 0f : -2f, FleshReach);
		}

		Blood.Spray(_body.GetParent(), at, direction, size, target.GetParent() as CollisionObject3D);
	}

	private static bool IsHumanForm(Node collider)
	{
		FormController form = collider?.GetNodeOrNull<FormController>("FormController");

		return form == null || !form.IsWolf;
	}

	/// <summary>
	/// Aplica o dano e, se o alvo cair, credita o abate. O crédito
	/// sai daqui porque é aqui que se sabe quem bateu — fazer a vítima
	/// descobrir o autor acopla Health a quem atira.
	/// </summary>
	/// <summary>
	/// Um acerto que não saiu da arma na mão (a granada que este jogador
	/// lançou): o mesmo retorno de um tiro — o som de acerto, a marca na
	/// mira e a carga do especial (Jhon, 20/11: "se der dano em inimigo,
	/// tem que dar o barulho de dano igual da arma").
	/// </summary>
	public void ConfirmHit(float amount, bool headshot = false)
	{
		if (Puppet || amount <= 0) return;
		EmitSignal(SignalName.DamageDealt, amount);
		EmitSignal(SignalName.HitConfirmed, headshot);
		_form.OnDamageDealt(amount);
	}

	private void Damage(Health target, float amount, Vector3 from, bool headshot)
	{
		// O boneco de outro jogador só encena o ataque: quem acerta é a
		// máquina de quem atirou.
		if (Puppet) return;

		// Fogo amigo desligado por padrão: num jogo de duas formas,
		// confundir aliado com inimigo já acontece demais pela silhueta.
		// A sala pode ligar.
		if (_health != null && target.Team == _health.Team && target != _health
			&& !RoomConfig.Rules.FriendlyFire) return;

		// Online: o corpo de outro jogador não leva dano aqui. O acerto
		// vai para a máquina dele; o abate, se houver, volta pelo servidor.
		if (target.IsRemote)
		{
			if (target.IsDead) return;
			// Só para a barra de vida do alvo aparecer na tela de quem acertou.
			target.NoteAttacker(_body);
			target.SendHit(amount, from, headshot, CauseOf(headshot), KillWeapon(target), _body);
			// O que se vê não espera a rede: a barra desce e o alvo reage já.
			target.PredictHit(amount, from, headshot);
			EmitSignal(SignalName.DamageDealt, amount);
			EmitSignal(SignalName.HitConfirmed, headshot);
			_form.OnDamageDealt(amount);
			return;
		}

		bool wasAlive = !target.IsDead;

		// Online, dois corpos desta máquina (o jogador e os bots que ela
		// move): o dano é aplicado aqui, mas o juiz fica sabendo da autoria.
		NetMatch net = NetMatch.Current;
		net?.BeginLocalHit(_body, target, amount);
		target.NoteAttacker(_body);
		target.TakeDamage(amount, 0, from, headshot);
		net?.EndLocalHit();

		EmitSignal(SignalName.DamageDealt, amount);
		EmitSignal(SignalName.HitConfirmed, headshot);
		_form.OnDamageDealt(amount);

		if (!wasAlive || !target.IsDead) return;

		// Online o abate é do servidor: a vítima conta que morreu, e o
		// feed, o placar e o WP voltam com a resposta dele.
		if (net != null)
		{
			net.LocalKill(target, _body, CauseOf(headshot), KillWeapon(target), headshot);
			return;
		}

		_form.OnKill(_form.IsWolf);

		// Quem ajudou nos últimos segundos também ganha. Sem assistência,
		// quem tira 90% da vida e perde o abate para um tiro alheio sai
		// sem nada — e o jogo passa a premiar roubar o último tiro.
		var assisters = new System.Collections.Generic.List<string>();
		foreach (Node helper in target.TakeAssists(_body))
		{
			helper.GetNodeOrNull<FormController>("FormController")?.OnAssist();
			if (helper.GetNodeOrNull<Health>("Health") is Health h) assisters.Add(h.PublicName);
		}

		KillFeed.Report(new Kill(
			_health?.PublicName ?? _body.Name.ToString(),
			_health?.Team ?? 0,
			target.PublicName,
			target.Team,
			CauseOf(headshot),
			KillWeapon(target),
			assisters.ToArray()));
	}

	/// <summary>Atrás é quando a vítima olha para longe de quem bate: o
	/// quanto a frente dela aponta na direção do golpe (1 = de costas).</summary>
	[Export] public float BehindDot = 0.45f;

	/// <summary>De cima: quem bate está pelo menos isto acima da vítima.</summary>
	[Export] public float AboveHeight = 1.8f;

	/// <summary>O golpe do lobo em curso (<see cref="Kill.WolfClawId"/>…):
	/// quem chama a garra diz qual é.</summary>
	private string _wolfHit = Kill.WolfClawId;

	/// <summary>Id do que matou, para o feed e as estatísticas, com as
	/// marcas do golpe: pelas costas, de cima (<see cref="Kill"/>).</summary>
	private string KillWeapon(Health target)
	{
		string id = _form.IsWolf ? _wolfHit
			: HumanSlot == Slot.Knife ? Arsenal.KnifeId
			: Held?.Id ?? "";

		string marks = "";
		if (target?.GetParent() is Node3D victim && _body != null && victim != _body)
		{
			Vector3 toVictim = victim.GlobalPosition - _body.GlobalPosition;
			if (-toVictim.Y >= AboveHeight) marks += Kill.AboveMark;

			toVictim.Y = 0;
			Vector3 facing = -victim.GlobalTransform.Basis.Z;
			facing.Y = 0;
			if (toVictim.LengthSquared() > 0.01f && facing.LengthSquared() > 0.01f
				&& facing.Normalized().Dot(toVictim.Normalized()) >= BehindDot)
				marks += Kill.BehindMark;
		}

		return marks == "" ? id : id + Kill.MarkSeparator + marks;
	}

	private KillCause CauseOf(bool headshot)
	{
		if (_form.IsWolf) return KillCause.Wolf;
		if (headshot) return KillCause.Headshot;

		return HumanSlot == Slot.Knife ? KillCause.Knife : KillCause.Gun;
	}

	/// <summary>
	/// Sem modelo à vista, cabeça é a faixa superior da cápsula: a altura
	/// do impacto resolve.
	/// </summary>
	private bool IsHeadshot(Node collider, Vector3 impact)
	{
		if (collider is not Node3D body) return false;

		return impact.Y - body.GlobalPosition.Y >= HeadZone;
	}

	/// <summary>Um corpo com modelo no caminho dos tiros: de quem, a vida, as
	/// peças agora (<see cref="BodyHitbox"/>) e a bola que contém todas (o
	/// teste rápido antes de olhar peça por peça).</summary>
	private readonly record struct Target(Node3D Body, Health Health, BodyHitbox.Part[] Parts,
		System.Numerics.Vector3 Center, float Reach);

	private readonly List<Target> _targets = new();
	private readonly List<BodyHitbox.Part> _parts = new();

	/// <summary>Os corpos com modelo de quem está vivo, menos o de quem atira e
	/// os dos aliados (o tiro atravessa o time). Uma vez por disparo: os
	/// chumbos da escopeta usam a mesma lista.</summary>
	private List<Target> Targets()
	{
		_targets.Clear();
		SceneTree tree = _body.GetTree();
		foreach (string group in Groups.Fighters)
		{
			foreach (Node node in tree.GetNodesInGroup(group))
			{
				if (node == _body || node is not Node3D body) continue;
				if (body.GetNodeOrNull<Health>("Health") is not Health health || health.IsDead) continue;
				// O aliado também não segura o tiro.
				if (PassesThrough(health)) continue;
				if (!BodyHitbox.TryGet(body, _parts)) continue;
				System.Numerics.Vector3 center = N(body.GlobalPosition);
				float reach = 0f;
				foreach (BodyHitbox.Part part in _parts)
					reach = Mathf.Max(reach, System.Numerics.Vector3.Distance(center, part.Shape.Center) + part.Shape.Reach);
				_targets.Add(new Target(body, health, _parts.ToArray(), center, reach));
			}
		}

		return _targets;
	}

	/// <summary>
	/// Um raio de ataque. Com <paramref name="precise"/> (as armas de fogo),
	/// quem tem modelo leva tiro pelas peças do corpo (<see cref="BodyHitbox"/>):
	/// o raio da física acha só a parede e quem não tem modelo, e a peça vale
	/// se vier antes da parede; cabeça é a peça da cabeça. Sem ele (faca,
	/// coronhada, de perto), o corpo é a cápsula de movimento e a cabeça é a
	/// peça da cabeça, mesmo um palmo dentro da cápsula (<see cref="HeadHit.Counts"/>).
	/// Quem não tem modelo leva tiro na cápsula, e na cabeça pela faixa de
	/// cima dela (<see cref="IsHeadshot"/>).
	/// </summary>
	private Health Trace(Vector3 from, Vector3 to, List<Target> targets, bool precise,
		out Vector3 impact, out Node collider, out bool headshot)
	{
		Health hit = Raycast(from, to, out impact, out collider, precise ? targets : null);

		float range = from.DistanceTo(to);
		Vector3 direction = (to - from) / Mathf.Max(range, 1e-4f);
		float? at = collider != null || impact != to ? from.DistanceTo(impact) : null;
		System.Numerics.Vector3 f = N(from), d = N(direction);

		Target best = default;
		bool bestHead = false;
		float bestAt = float.MaxValue;
		foreach (Target target in targets)
		{
			if (HeadHit.Enter(f, d, target.Center, target.Reach) is not float near || near > range || near >= bestAt) continue;
			foreach (BodyHitbox.Part part in target.Parts)
			{
				if (!precise && !part.Head) continue;
				if (BodyHit.Enter(f, d, part.Shape) is not float t || t > range || t >= bestAt) continue;
				bool counts = precise ? at is not float wall || t <= wall : HeadHit.Counts(t, at, collider == target.Body);
				if (!counts) continue;
				best = target;
				bestHead = part.Head;
				bestAt = t;
			}
		}

		if (best.Body != null)
		{
			impact = from + direction * bestAt;
			collider = best.Body;
			headshot = bestHead;
			return best.Health;
		}

		headshot = hit != null && collider is Node3D body && !BodyHitbox.Has(body) && IsHeadshot(collider, impact);
		return hit;
	}

	private static System.Numerics.Vector3 N(Vector3 v) => new(v.X, v.Y, v.Z);
}
