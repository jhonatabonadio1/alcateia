using Godot;
using System;
using System.Collections.Generic;
using N = System.Numerics;

/// <summary>Um acerto como o jogo de quem atirou contou.</summary>
public struct ClaimedHit
{
	public long Shooter, Target;
	public float Amount;
	public bool Headshot;
	public KillCause Cause;
	public string Weapon;
	public Vector3 From;

	/// <summary>O comando do passo em que atirou, e o passo do servidor em
	/// que ele via os outros (desenhados 100 ms no passado). Zero e NaN no
	/// jogo antigo, que não conta.</summary>
	public uint Seq;
	public double ViewTick;

	/// <summary>A arma, sem as marcas do feed ("ak74#costas" → "ak74").</summary>
	public string WeaponId
	{
		get
		{
			string w = Weapon ?? "";
			int cut = w.IndexOf(Kill.MarkSeparator);
			return cut >= 0 ? w[..cut] : w;
		}
	}
}

/// <summary>
/// Confere, no servidor de partida, um acerto que um jogo diz que deu
/// (decisão 0016). Com as contas de <see cref="HitRules"/> e o mundo da
/// partida (<see cref="MatchWorld"/>): de onde saiu, a arma na mão, o
/// alcance, a parede no caminho (voltando o alvo no tempo até onde ele
/// aparecia na tela de quem atirou), a mira, o dano máximo e o ritmo.
///
/// Devolve <see cref="HitVerdict.Ok"/> com o dano que vale (pode ser menor
/// que o contado: acima do máximo da arma, o máximo; cabeça que não estava
/// à vista, vale corpo), ou o motivo da recusa.
/// </summary>
public sealed class HitCheck
{
	/// <summary>Até onde vai uma granada (o arremesso mais o rolar).</summary>
	public const float GrenadeReach = 45f;

	/// <summary>Alcance do golpe do lobo, contando o bote.</summary>
	public const float WolfReach = 6f;

	/// <summary>Folga a mais no dano, para arredondamento.</summary>
	private const float DamageSlack = 1.05f;

	private static float _grenadeDamage = -1, _grenadeRadius;

	private readonly MatchWorld _world;
	private readonly Func<IEnumerable<Rid>> _bodies;
	private readonly Godot.Collections.Array<Rid> _exclude = new();

	/// <param name="bodies">Os corpos de jogador desta partida: a linha de
	/// tiro passa por eles (só parede para).</param>
	public HitCheck(MatchWorld world, Func<IEnumerable<Rid>> bodies)
	{
		_world = world;
		_bodies = bodies;
	}

	/// <summary>Onde o alvo estava num passo: o meio do corpo e a altura.
	/// Falso se não se sabe.</summary>
	public delegate bool PoseAt(double tick, out Vector3 center, out float height);

	/// <summary>
	/// Confere um acerto de <paramref name="shot"/> (onde o atirador estava
	/// no comando do tiro) num alvo cuja pose em cada passo
	/// <paramref name="target"/> sabe dizer. <paramref name="window"/>: de
	/// que passo a que passo procurar o alvo.
	/// </summary>
	public HitVerdict Check(in ClaimedHit hit, in ServerBody.ShotPose shot, ServerBody shooter, PoseAt target,
		double firstTick, double lastTick, double now, out float amount, out bool headshot, bool checkAim = true)
	{
		amount = 0;
		headshot = false;
		// A granada de quem já morreu ainda explode (como no jogo).
		if (!shot.Valid || (!shot.Alive && hit.Cause != KillCause.Grenade)) return HitVerdict.ShooterGone;

		// A arma: a que estava na mão no comando do tiro.
		WeaponDefinition def = null;
		float range, max, spread = 0, headMultiplier = 1;
		bool sight = true;
		switch (hit.Cause)
		{
			case KillCause.Gun:
			case KillCause.Headshot:
				if (shot.Form != Form.Human || shot.Slot is Slot.Knife or Slot.Grenade) return HitVerdict.WrongWeapon;
				def = Arsenal.Get(hit.WeaponId);
				if (def == null || def.Id != shot.Held) return HitVerdict.WrongWeapon;
				range = def.Range;
				headMultiplier = def.HeadshotMultiplier;
				max = def.Damage * Math.Max(def.Pellets, 1) + 1f;
				spread = def.SpreadMax;
				break;

			case KillCause.Knife:
				if (shot.Form != Form.Human || shot.Slot != Slot.Knife) return HitVerdict.WrongWeapon;
				var knife = Arsenal.KnifeStats(shot.Held);
				range = shooter.Weapon.KnifeRange * knife.Range + 1f;
				headMultiplier = shooter.Weapon.HeadshotMultiplier;
				// o maior golpe da faca no corpo: a estocada pelas costas (24/11); a cabeça entra no teto, embaixo
				Weapon w = shooter.Weapon;
				max = Math.Max(w.KnifeDamage, w.KnifeStabDamage * w.KnifeStabBehind) * knife.Damage;
				checkAim = false;
				break;

			case KillCause.Wolf:
				if (shot.Form is not (Form.Wolf or Form.ShiftingToWolf)) return HitVerdict.WrongWeapon;
				range = WolfReach;
				max = WolfMax(shooter);
				sight = false;
				checkAim = false;
				break;

			case KillCause.Grenade:
				LoadGrenade();
				range = _grenadeRadius + 1.5f;
				max = _grenadeDamage;
				sight = false;
				checkAim = false;
				break;

			default:
				return HitVerdict.WrongWeapon;
		}

		// De onde saiu: do corpo de quem atirou (a granada, de onde explodiu,
		// a um arremesso dele).
		if (hit.Cause == KillCause.Grenade)
		{
			if (hit.From.DistanceTo(shot.Eye) > GrenadeReach) return HitVerdict.BadOrigin;
		}
		else if (HitRules.DistanceToSegment(hit.From.ToNumerics(), shot.Feet.ToNumerics(), shot.Eye.ToNumerics()) > HitRules.OriginSlack)
			return HitVerdict.BadOrigin;

		// O alvo, voltando no tempo: basta um passo da janela em que tudo bate.
		HitVerdict worst = HitVerdict.TargetGone;
		Span<N.Vector3> points = stackalloc N.Vector3[6];
		bool passed = false, headSeen = false;
		for (double tick = firstTick; tick <= lastTick + 0.001; tick += 1)
		{
			if (!target(tick, out Vector3 center, out float height)) continue;

			Vector3 origin = hit.From;
			float distance = origin.DistanceTo(center);
			if (distance > range + HitRules.RangeSlack)
			{
				worst = Worse(worst, HitVerdict.OutOfRange);
				continue;
			}

			int count = HitRules.TargetPoints(center.ToNumerics(), height, origin.ToNumerics(), points);

			if (checkAim)
			{
				float best = float.MaxValue;
				for (int i = 0; i < count; i++)
					best = Math.Min(best, HitRules.AimErrorDegrees(shot.Eye.ToNumerics(), shot.Yaw, shot.Pitch, points[i]));
				if (!HitRules.AimFits(best, distance, spread))
				{
					worst = Worse(worst, HitVerdict.BadAim);
					continue;
				}
			}

			if (sight)
			{
				bool any = false;
				for (int i = 0; i < count; i++)
				{
					if (!Clear(origin, points[i].ToGodot())) continue;
					any = true;
					if (i == 0) headSeen = true;
				}
				if (!any)
				{
					worst = Worse(worst, HitVerdict.Blocked);
					continue;
				}
			}
			else headSeen = true;

			passed = true;
			if (headSeen || !hit.Headshot) break;
		}

		if (!passed) return worst;

		// O dano: o contado, até o máximo da arma. Cabeça que não estava à
		// vista em nenhum instante vale corpo.
		headshot = hit.Headshot && headSeen && hit.Cause is KillCause.Gun or KillCause.Headshot or KillCause.Knife;
		amount = hit.Amount;
		if (hit.Headshot && !headshot && headMultiplier > 1) amount /= headMultiplier;
		float cap = max * (headshot ? headMultiplier : 1f) * DamageSlack;
		amount = Math.Min(amount, cap);
		if (!(amount > 0)) return HitVerdict.NotAllowed;

		// O ritmo: o dano por segundo que a arma dá.
		DamageBudget budget = hit.Cause switch
		{
			KillCause.Grenade => shooter.GrenadeBudget,
			KillCause.Knife or KillCause.Wolf => shooter.MeleeBudget,
			_ => shooter.GunBudget
		};
		ConfigureBudget(budget, hit.Cause, def, max, headMultiplier, shooter);
		return budget.Spend(amount, now) ? HitVerdict.Ok : HitVerdict.TooFast;
	}

	/// <summary>Só a linha: o alvo ao alcance de qualquer arma e, para tiro e
	/// faca, alguma parte dele à vista de onde o golpe saiu. É o que se
	/// confere no tiro de um bot (a máquina do dono o move e não manda os
	/// comandos dele).</summary>
	public HitVerdict CheckLine(in ClaimedHit hit, PoseAt target, double firstTick, double lastTick)
	{
		bool sight = hit.Cause is KillCause.Gun or KillCause.Headshot or KillCause.Knife;
		if (hit.Cause == KillCause.Grenade) LoadGrenade();
		// (a granada: o raio dela, que é o da cena — era 10 m fixo, menos que o raio de 9 m da de 20/11 com a folga)
		float reach = hit.Cause == KillCause.Grenade ? _grenadeRadius + 2f : hit.Cause == KillCause.Wolf ? WolfReach + 2f : 200f;
		HitVerdict worst = HitVerdict.TargetGone;
		Span<N.Vector3> points = stackalloc N.Vector3[6];
		for (double tick = firstTick; tick <= lastTick + 0.001; tick += 1)
		{
			if (!target(tick, out Vector3 center, out float height)) continue;
			if (hit.From.DistanceTo(center) > reach)
			{
				worst = Worse(worst, HitVerdict.OutOfRange);
				continue;
			}
			if (!sight) return HitVerdict.Ok;

			int count = HitRules.TargetPoints(center.ToNumerics(), height, hit.From.ToNumerics(), points);
			for (int i = 0; i < count; i++)
				if (Clear(hit.From, points[i].ToGodot())) return HitVerdict.Ok;
			worst = Worse(worst, HitVerdict.Blocked);
		}
		return worst;
	}

	/// <summary>
	/// Quanto cabe por segundo, e de uma vez. Folgado: arma com cadência
	/// mais 30%, todos na cabeça, e uns disparos de folga — o que passa
	/// disso é atirar mais rápido ou mais forte do que a arma deixa.
	/// </summary>
	private static void ConfigureBudget(DamageBudget budget, KillCause cause, WeaponDefinition def, float max, float headMultiplier, ServerBody shooter)
	{
		switch (cause)
		{
			case KillCause.Gun:
			case KillCause.Headshot:
				float perShot = max * headMultiplier;
				budget.Configure(perShot * Math.Max(def.Rpm, 60f) / 60f * 1.3f, perShot * 3f);
				break;
			case KillCause.Knife:
			case KillCause.Wolf:
				// Garra e salto pegam mais de um de uma vez; faca, um por golpe.
				float melee = Math.Max(max * headMultiplier, WolfMax(shooter));
				budget.Configure(melee * 3f, melee * 4f);
				break;
			default:
				// Granada: até quatro pegos por explosão, uma a cada dois segundos.
				budget.Configure(_grenadeDamage * 4f / 2f, _grenadeDamage * 8f);
				break;
		}
	}

	/// <summary>O golpe mais forte do lobo: o maior multiplicador (combo,
	/// salto ou bote) contra humano, caindo do alto.</summary>
	private static float WolfMax(ServerBody shooter)
	{
		Weapon w = shooter.Weapon;
		WolfBreed breed = shooter.Form.Breed;
		float attack = breed?.AttackDamage ?? 120f, air = breed?.AirAttackBonus ?? 1.5f;
		float combo = 1f;
		foreach (float c in w.ComboDamage ?? Array.Empty<float>()) combo = Math.Max(combo, c);
		float move = Math.Max(combo, Math.Max(w.LeapDamageBonus, w.HeavyDamageBonus));
		return attack * move * Math.Max(w.ClawVsHumanBonus, 1f) * Math.Max(air, 1f);
	}

	private static void LoadGrenade()
	{
		if (_grenadeDamage > 0) return;
		var grenade = GD.Load<PackedScene>(Weapon.DefaultGrenadeScene)?.Instantiate<Grenade>();
		_grenadeDamage = grenade?.MaxDamage ?? 420f;
		_grenadeRadius = grenade?.Radius ?? 9f;
		grenade?.Free();
	}

	/// <summary>Nada sólido do mapa entre os dois pontos (os corpos de
	/// jogador não contam: a bala do jogo passa por aliado).</summary>
	private bool Clear(Vector3 from, Vector3 to)
	{
		PhysicsDirectSpaceState3D space = _world.Space;
		if (space == null) return true;

		_exclude.Clear();
		foreach (Rid rid in _bodies()) _exclude.Add(rid);
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.Exclude = _exclude;
		query.CollideWithAreas = false;
		return space.IntersectRay(query).Count == 0;
	}

	/// <summary>Entre dois motivos de recusa, o que diz mais (a parede é
	/// mais grave que a distância, que é mais grave que não achar o alvo).</summary>
	private static HitVerdict Worse(HitVerdict a, HitVerdict b) => Rank(b) > Rank(a) ? b : a;

	private static int Rank(HitVerdict v) => v switch
	{
		HitVerdict.TargetGone => 0,
		HitVerdict.OutOfRange => 1,
		HitVerdict.BadAim => 2,
		HitVerdict.Blocked => 3,
		_ => 4
	};

	/// <summary>O motivo, para o registro do servidor.</summary>
	public static string Describe(HitVerdict v) => v switch
	{
		HitVerdict.Ok => "accepted",
		HitVerdict.NotAllowed => "not allowed by the referee",
		HitVerdict.ShooterGone => "shooter was dead or out of the world",
		HitVerdict.TargetGone => "target was not there",
		HitVerdict.BadOrigin => "shot started too far from the body",
		HitVerdict.OutOfRange => "out of the weapon's range",
		HitVerdict.Blocked => "wall in the way",
		HitVerdict.BadAim => "outside the aim",
		HitVerdict.TooFast => "more damage per second than the weapon deals",
		HitVerdict.WrongWeapon => "weapon not in hand",
		_ => v.ToString()
	};

	/// <summary>Recusa que só trapaça (ou um defeito) explica: conta contra quem atirou.</summary>
	public static bool Suspicious(HitVerdict v) =>
		v is HitVerdict.BadOrigin or HitVerdict.OutOfRange or HitVerdict.Blocked or HitVerdict.BadAim
			or HitVerdict.TooFast or HitVerdict.WrongWeapon;
}
