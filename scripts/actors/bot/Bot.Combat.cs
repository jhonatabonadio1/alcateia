using Godot;
using System;

/// <summary>
/// Combate à distância: rajadas, strafe, distância preferida e a mira
/// que erra de propósito (na medida da dificuldade).
/// </summary>
public partial class Bot
{
	/// <summary>Distância que tenta manter do alvo na forma humana.</summary>
	[ExportGroup("Combate")]
	[Export] public float PreferredRange = 12f;
	[Export] public float BurstTime = 0.7f;
	[Export] public float BurstPause = 0.6f;
	[Export] public float EngageRange = 45f;
	[Export] public float StrafeFactor = 0.8f;
	[Export] public float StrafeInterval = 1.4f;

	/// <summary>
	/// Quanto o bot avança em vez de circular. Deathmatch não premia
	/// quem administra distância: premia quem vai atrás. Em 1 ele
	/// fecha a distância enquanto atira, mantendo o strafe só como
	/// movimento lateral.
	/// </summary>
	[Export] public float Aggression = 0.75f;

	/// <summary>Distância abaixo da qual ele para de avançar. Colar no
	/// alvo com arma de fogo é pior que manter um palmo.</summary>
	[Export] public float MinRange = 4f;

	/// <summary>Erro de mira em graus. Zero faz um bot desumano.</summary>
	[Export] public float AimError = 4f;

	/// <summary>Fração dos tiros que erra de propósito, com desvio bem
	/// maior. Nem no difícil vai a zero: um bot que acerta sempre não
	/// é difícil, é injusto.</summary>
	[Export] public float MissChance = 0.2f;
	[Export] public float MissSpread = 14f;

	private double _burstTimer;
	private bool _firing;
	private double _strafeTimer;
	private int _strafeSide = 1;
	private Vector3 _aimOffset;

	private void RunCombat(double delta)
	{
		if (_target == null)
		{
			RunPatrol(delta);
			return;
		}

		UpdateBurst(delta);

		Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
		float distance = new Vector3(toTarget.X, 0, toTarget.Z).Length();

		FaceDirection(_target.GlobalPosition, delta);
		Maneuver(distance, toTarget, delta);

		// O especial vem antes da garra: arranhar no meio do pulo ou já
		// em alcance deixava o golpe em recarga e o bote não saía.
		if (_form.IsWolf)
		{
			WolfTactics(distance, delta);
			if (_wantDive || _weapon.IsChargingPounce || _weapon.IsPounceFlying || _weapon.IsDiving) return;
		}

		Engage(distance);
	}

	private void Maneuver(float distance, Vector3 toTarget, double delta)
	{
		Vector3 flat = new Vector3(toTarget.X, 0, toTarget.Z).Normalized();
		Vector3 side = flat.Cross(Vector3.Up).Normalized() * _strafeSide;
		Vector3 desired;

		if (_form.IsWolf)
		{
			desired = flat;
		}
		else if (distance < MinRange)
		{
			// Perto demais até para um bot agressivo: recua de lado.
			desired = (-flat * 0.6f + side).Normalized();
		}
		else
		{
			// Avança enquanto circula. O peso da agressão decide o
			// quanto da intenção é ir para cima e o quanto é lateral.
			float push = Action == BotAction.Push
				? 1f
				: distance > PreferredRange ? 1f : Aggression;

			desired = (flat * push + side * StrafeFactor).Normalized();
		}

		float speed = Speed * (_form.IsWolf ? _form.Breed.SpeedMultiplier : 1f);

		// Longe do alvo, segue o caminho: ir em linha reta esbarra em
		// parede. Perto, manda a intenção direta, porque navegação
		// ponto a ponto estraga strafe e recuo.
		if (distance > PreferredRange * 1.5f)
		{
			Vector3 step = PathTo(_target.GlobalPosition);

			if (step != Vector3.Zero)
			{
				MoveTowards(step, speed, delta);
				return;
			}
		}

		MoveTowards(Avoid(desired), speed, delta);
	}

	/// <summary>Rajada e pausa. Fogo contínuo não deixa o jogador
	/// respirar e esconde de onde vem o tiro.</summary>
	private void UpdateBurst(double delta)
	{
		_burstTimer -= delta;
		if (_burstTimer > 0) return;

		_firing = !_firing;
		_burstTimer = _firing ? BurstTime : BurstPause;
	}

	private void UpdateStrafe(double delta)
	{
		_strafeTimer -= delta;
		if (_strafeTimer > 0) return;

		_strafeSide = GD.Randf() < 0.5f ? -1 : 1;
		_strafeTimer = StrafeInterval * GD.RandRange(0.6, 1.4);
	}

	/// <summary>
	/// Sorteia o desvio da mira. Na maior parte das vezes é o erro
	/// normal; de vez em quando é um tiro jogado fora de propósito.
	///
	/// Errar explicitamente é melhor que só aumentar o erro médio:
	/// mantém os acertos convincentes e dá ao jogador janelas claras
	/// em vez de um chuvisco de quase-acertos.
	/// </summary>
	private void ShuffleAim()
	{
		bool miss = GD.Randf() < MissChance;
		float spread = Mathf.DegToRad(miss ? MissSpread : AimError);

		_aimOffset = new Vector3(
			(float)GD.RandRange(-spread, spread),
			(float)GD.RandRange(-spread, spread),
			0);
	}

	private void Engage(float distance)
	{
		if (!_form.CanAttack) return;

		// Recarregando ou recuando não se atira. É o cérebro que
		// decide, e a ação em curso é a decisão. Indo para a parede, sim:
		// é fogo de cobertura.
		if (Action == BotAction.Reload || Action == BotAction.Retreat) return;

		float reach = _form.IsWolf
			? _form.Breed.AttackRange
			: Mathf.Min(EngageRange, _weapon.PrimaryDef?.Range ?? EngageRange);

		if (distance > reach) return;
		if (!_form.IsWolf && !_firing) return;
		if (!HasLineOfSight()) return;

		_weapon.TryAttack();
	}
}
