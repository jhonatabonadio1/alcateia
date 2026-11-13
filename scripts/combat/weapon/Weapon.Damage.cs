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
	/// Altura da zona de cabeça, medida do centro do corpo para cima.
	/// A cápsula tem 1,75, então o topo fica em ~0,87: uma zona a
	/// partir de 0,72 deixa a cabeça com cerca de 15cm, proporcional a
	/// uma cabeça humana, e não o terço superior do corpo.
	/// </summary>
	[Export] public float HeadZone = 0.72f;

	/// <summary>Altura mínima de queda para contar como ataque aéreo.</summary>
	[Export] public float AirAttackMinFall = 2.5f;

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
			target.SendHit(amount, from, headshot, CauseOf(headshot), KillWeapon(), _body);
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
			net.LocalKill(target, _body, CauseOf(headshot), KillWeapon(), headshot);
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
			KillWeapon(),
			assisters.ToArray()));
	}

	/// <summary>Id do que matou, para o feed e as estatísticas.</summary>
	private string KillWeapon()
	{
		if (_form.IsWolf) return "lobo";
		if (HumanSlot == Slot.Knife) return Arsenal.KnifeId;
		return Held?.Id ?? "";
	}

	private KillCause CauseOf(bool headshot)
	{
		if (_form.IsWolf) return KillCause.Wolf;
		if (headshot) return KillCause.Headshot;

		return HumanSlot == Slot.Knife ? KillCause.Knife : KillCause.Gun;
	}

	/// <summary>
	/// Cabeça é a faixa superior do corpo. Sem hitbox dedicada, a
	/// altura do impacto resolve — e continua valendo quando o modelo
	/// chegar, porque a zona é medida do centro do corpo.
	/// </summary>
	private bool IsHeadshot(Node collider, Vector3 impact)
	{
		if (collider is not Node3D body) return false;

		return impact.Y - body.GlobalPosition.Y >= HeadZone;
	}
}
