using Godot;
using System;

/// <summary>
/// Ronda e busca. Bot parado no spawn denuncia que é script; andar sem
/// rumo também é informação para o jogador: um bot caminhando longe é uma
/// oportunidade, e um bot chegando é uma ameaça.
/// </summary>
public partial class Bot
{
	/// <summary>Tempo olhando em volta ao chegar no destino.</summary>
	[ExportGroup("Ronda e busca")]
	[Export] public float ScanTime = 2.5f;

	/// <summary>Raio da área patrulhada, medido do centro do mapa.</summary>
	[Export] public float PatrolRadius = 16f;

	/// <summary>Centro da área patrulhada.</summary>
	[Export] public Vector3 PatrolCenter = Vector3.Zero;

	/// <summary>Pausa entre um destino e o próximo.</summary>
	[Export] public float PatrolPause = 1.5f;

	/// <summary>Fração da velocidade ao patrulhar. Andar devagar faz
	/// menos barulho e deixa o bot menos previsível de ouvir.</summary>
	[Export] public float PatrolSpeedFactor = 0.55f;

	/// <summary>Rota de ronda atual e em que ponto dela o bot está.
	/// −1 significa que ainda não pegou nenhuma.</summary>
	private int _route = -1;
	private int _leg;
	private int _legStep = 1;
	private double _scanTimer;
	private float _scanAngle;
	private Vector3 _patrolTarget;
	private bool _hasPatrolTarget;
	private double _patrolPause;

	/// <summary>
	/// Vai até a última posição conhecida e olha em volta. Se não achar
	/// nada, desiste. É o que transforma um som em comportamento
	/// legível: o jogador vê o bot indo aonde ele estava.
	/// </summary>
	private void RunSearch(double delta)
	{
		// Procurando e deu de cara com o alvo: luta, não termina a busca.
		if (_target != null && CanSeeTarget())
		{
			RunCombat(delta);
			return;
		}

		float distance = new Vector3(
			_lastKnown.X - GlobalPosition.X, 0,
			_lastKnown.Z - GlobalPosition.Z).Length();

		if (distance > ArriveRadius)
		{
			Vector3 step = PathTo(_lastKnown);

			FaceDirection(_lastKnown, delta);
			MoveTowards(step, Speed * 0.85f, delta);
			return;
		}

		// Chegou: varre o entorno girando devagar.
		_scanTimer -= delta;

		if (_scanTimer <= 0)
		{
			_hasBelief = false;
			_memoryTimer = 0;
			return;
		}

		_scanAngle += (float)delta * 1.8f;
		Rotation = new Vector3(0, _scanAngle, 0);

		MoveTowards(Vector3.Zero, Speed, delta);
	}

	/// <summary>
	/// Patrulha: escolhe um ponto, vai até ele, pausa e escolhe outro.
	///
	/// Sem navmesh não há garantia de que o ponto seja alcançável, e é
	/// por isso que existe o detector de travamento: em vez de tentar
	/// provar que o caminho existe, o bot tenta e desiste quando para
	/// de progredir. É mais barato e falha de forma visível, em vez de
	/// empurrar a parede indefinidamente.
	/// </summary>
	private void RunPatrol(double delta)
	{
		if (_target != null && CanSeeTarget())
		{
			RunCombat(delta);
			return;
		}

		if (_patrolPause > 0)
		{
			_patrolPause -= delta;
			MoveTowards(Vector3.Zero, Speed, delta);
			return;
		}

		if (!_hasPatrolTarget)
		{
			PickPatrolTarget();
			return;
		}

		Vector3 flat = new Vector3(
			_patrolTarget.X - GlobalPosition.X, 0,
			_patrolTarget.Z - GlobalPosition.Z);

		if (flat.Length() <= ArriveRadius)
		{
			_hasPatrolTarget = false;
			_patrolPause = PatrolPause * _rng.Range(0.6, 1.6);
			return;
		}

		// Progrediu? Se não, o caminho não serve.
		if (GlobalPosition.DistanceTo(_lastPosition) < 0.04f)
		{
			_stuckTimer += delta;

			if (_stuckTimer >= StuckTime)
			{
				if (Debug) GD.Print($"{Name}: travado, trocando de destino");
				_hasPatrolTarget = false;
				_stuckTimer = 0;
				return;
			}
		}
		else
		{
			_stuckTimer = 0;
		}

		_lastPosition = GlobalPosition;

		Vector3 step = PathTo(_patrolTarget);

		if (step == Vector3.Zero)
		{
			_hasPatrolTarget = false;
			return;
		}

		FaceDirection(GlobalPosition + step * 4f, delta);
		MoveTowards(step, Speed * PatrolSpeedFactor, delta);
	}

	private void PickPatrolTarget()
	{
		if (PickRouteTarget()) return;

		float angle = _rng.Range(0f, Mathf.Tau);

		// Raiz quadrada distribui por área; elevar o expoente puxa os
		// destinos para o meio do mapa, que é onde a luta acontece.
		// Patrulha que vive na borda é patrulha que ninguém encontra.
		float radius = PatrolRadius * Mathf.Pow(_rng.Unit(), 0.85f);

		_patrolTarget = PatrolCenter + new Vector3(
			Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);

		_hasPatrolTarget = true;
		_stuckTimer = 0;
		_lastPosition = GlobalPosition;
	}

	/// <summary>
	/// Próximo ponto da rota, pegando uma nova quando a atual acaba.
	///
	/// Entra pelo ponto mais próximo, não pelo primeiro: rota é um
	/// caminho, não uma fila com começo obrigatório, e fazer o bot
	/// voltar ao ponto um antes de andar é a travessia boba que
	/// denuncia o roteiro.
	///
	/// O sentido é sorteado, então a mesma rota não vira um trilho
	/// onde dá para esperar o bot aparecer sempre pelo mesmo lado.
	/// </summary>
	private bool PickRouteTarget()
	{
		if (PatrolRoutes.RouteCount == 0) return false;

		if (_route < 0 || _leg < 0 || _leg >= PatrolRoutes.Length(_route))
		{
			_route = PatrolRoutes.Nearest(GlobalPosition);
			if (_route < 0) return false;

			PatrolRoutes.ClosestPoint(_route, GlobalPosition, out _leg);
			_legStep = _rng.Sign();

			// Entrando pela ponta, metade dos sorteios apontava para
			// fora da rota: o bot pegava um ponto só, a rota acabava
			// e ele sorteava outra. De longe isso é idêntico a não ter
			// rota nenhuma — ia parecer que o recurso não existe.
			if (_leg + _legStep < 0 || _leg + _legStep >= PatrolRoutes.Length(_route))
				_legStep = -_legStep;
		}

		_patrolTarget = PatrolRoutes.Point(_route, _leg);
		_leg += _legStep;

		if (Debug)
			GD.Print($"{Name}: rota {_route} ponto {_leg - _legStep} -> {_patrolTarget}");

		// Acabou a rota: na próxima vez pega outra.
		if (_leg < 0 || _leg >= PatrolRoutes.Length(_route))
			_route = -1;

		_hasPatrolTarget = true;
		_stuckTimer = 0;
		_lastPosition = GlobalPosition;
		return true;
	}
}
