using Godot;
using System;

/// <summary>
/// Cobertura e recuo. Nenhum ponto é marcado no mapa: o bot descobre
/// parede testando — se de um ponto a linha até o alvo está bloqueada,
/// aquele ponto é cobertura. Funciona em qualquer geometria, inclusive num
/// .glb que ninguém anotou.
/// </summary>
public partial class Bot
{
	/// <summary>Quantas posições testar ao procurar parede.</summary>
	[ExportGroup("Cobertura")]
	[Export] public int CoverSamples = 12;

	/// <summary>Até onde procurar.</summary>
	[Export] public float CoverSearchRadius = 11f;

	/// <summary>Distância mínima do alvo que uma cobertura precisa ter.
	/// Parede colada no inimigo não protege de nada.</summary>
	[Export] public float CoverMinDistance = 6f;

	private Vector3 _cover;
	private bool _hasCover;

	/// <summary>
	/// Procura um ponto de onde o alvo não enxerga.
	///
	/// Testa posições num anel em volta: de cada candidato, dispara um
	/// raio até o peito do alvo. Bloqueado significa parede no meio, e
	/// parede no meio é cobertura — sem precisar que alguém tenha
	/// marcado nada no mapa.
	///
	/// Vence o candidato mais perto do bot: fugir para o outro lado do
	/// mapa não é se proteger, é desaparecer da luta.
	/// </summary>
	private bool FindCover(out Vector3 point)
	{
		point = Vector3.Zero;

		if (_target == null) return false;

		Vector3 chest = _target.GlobalPosition + Vector3.Up * 0.5f;
		float best = float.MaxValue;
		bool found = false;

		for (int i = 0; i < CoverSamples; i++)
		{
			float angle = Mathf.Tau * i / CoverSamples;
			float radius = CoverSearchRadius * (0.45f + 0.55f * ((i % 3) / 2f));

			Vector3 candidate = GlobalPosition + new Vector3(
				Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);

			if (candidate.DistanceTo(_target.GlobalPosition) < CoverMinDistance)
				continue;

			var query = PhysicsRayQueryParameters3D.Create(
				candidate + Vector3.Up * 0.5f, chest);

			var exclude = new Godot.Collections.Array<Rid> { GetRid() };

			if (_target is CollisionObject3D body)
				exclude.Add(body.GetRid());

			query.Exclude = exclude;

			// Linha livre significa que dali ainda se é visto.
			if (GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0)
				continue;

			float distance = GlobalPosition.DistanceTo(candidate);
			if (distance >= best) continue;

			best = distance;
			point = candidate;
			found = true;
		}

		return found;
	}

	private void RunTakeCover(double delta)
	{
		if (!_hasCover)
		{
			RunCombat(delta);
			return;
		}

		Vector3 step = PathTo(_cover);

		// Atira indo para a parede: correr para o esconderijo sem revidar
		// entregava o duelo. E parado sem caminho, ou "chegou" e ainda vê
		// o alvo, a parede não protege: desiste dela por um tempo e luta.
		// Era o defeito dos bots que paravam de atirar — dois bots a 3 m,
		// os dois "indo para a parede", parados e se encarando por meio
		// minuto.
		bool sees = _target != null && CanSeeTarget();
		if (sees)
		{
			UpdateBurst(delta);
			Vector3 to = _target.GlobalPosition - GlobalPosition;
			Engage(new Vector3(to.X, 0, to.Z).Length());
		}

		if (step == Vector3.Zero)
		{
			if (sees)
			{
				_noCoverTimer = NoCoverTime;
				_hasCover = false;
			}

			if (_target != null)
				FaceDirection(_target.GlobalPosition, delta);

			MoveTowards(Vector3.Zero, Speed, delta);
			return;
		}

		if (_target != null)
			FaceDirection(_target.GlobalPosition, delta);

		MoveTowards(step, Speed, delta);
	}

	/// <summary>Quanto tempo o bot esquece a ideia de parede depois de uma
	/// que não protegeu (sem caminho, ou chegou e ainda vê o alvo).</summary>
	[Export] public float NoCoverTime = 3f;

	private double _noCoverTimer;

	/// <summary>Recuar quebrando a linha de visão: afasta-se do alvo
	/// em vez de dar as costas em linha reta.</summary>
	private void RunRetreat(double delta)
	{
		if (_target == null)
		{
			RunPatrol(delta);
			return;
		}

		Vector3 away = GlobalPosition - _target.GlobalPosition;
		Vector3 flat = new Vector3(away.X, 0, away.Z).Normalized();

		FaceDirection(_target.GlobalPosition, delta);
		MoveTowards(Avoid(flat), Speed, delta);
	}
}
