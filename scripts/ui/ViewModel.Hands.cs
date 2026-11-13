using Godot;

/// <summary>
/// As mãos na arma e o que elas fazem: a recarga (carregador ou cartucho
/// a cartucho), a bombeada da escopeta, o pino da granada, o golpe de
/// faca.
///
/// As duas mãos são filhas da arma: quando ela dá coice ou inclina, vão
/// junto. A direita não sai do punho. A esquerda é quem trabalha — anda
/// entre pontos calculados na própria arma (o fundo do carregador, o
/// ferrolho, a janela de carga), então a mesma coreografia serve para
/// qualquer arma que tenha essas peças.
/// </summary>
public partial class ViewModel
{
	/// <summary>Para onde a mão some com o carregador velho, a partir de
	/// onde ele estava (abaixo da tela, na direção do cinto).</summary>
	private static readonly Vector3 OutOfView = new(-0.05f, -0.42f, 0.16f);

	/// <summary>Curso do ferrolho, em metros, puxado à mão e no tiro.</summary>
	private const float BoltTravel = 0.045f;
	private const float PumpTravel = 0.075f;

	private Node3D _right;
	private Node3D _left;
	private Transform3D _leftRest;
	private Transform3D _leftAtMag;
	private Transform3D _leftOut;
	private Transform3D _leftAtBolt;
	private Transform3D _leftAtPort;
	private Transform3D _leftAtPin;

	private Node3D _magazine;
	private Vector3 _magRest;
	private Node3D _bolt;
	private Vector3 _boltRest;
	private Node3D _pump;
	private Vector3 _pumpRest;
	private Node3D _pin;
	private Vector3 _pinRest;

	private float _reloadPose;
	private bool _emptyReload;
	private bool _wasReloading;
	private float _boltKick;
	private float _pumpTime = -1f;
	private float _windup;
	private float _throw;
	private float _slash = 1f;

	private void ForgetHands()
	{
		_right = _left = _magazine = _bolt = _pump = _pin = null;
		_reloadPose = 0f;
		_windup = 0f;
		_throw = 0f;
		_slash = 1f;
		_pumpTime = -1f;
	}

	/// <summary>Põe as duas mãos na arma e calcula os pontos por onde a
	/// esquerda vai passar.</summary>
	private void BuildHands(string id)
	{
		_magazine = _held.GetNodeOrNull<Node3D>(WeaponModel.MagazineName);
		_bolt = _held.GetNodeOrNull<Node3D>(WeaponModel.BoltName);
		_pump = _held.GetNodeOrNull<Node3D>(WeaponModel.PumpName);
		_pin = _held.GetNodeOrNull<Node3D>(WeaponModel.PinName);
		if (_magazine != null) _magRest = _magazine.Position;
		if (_bolt != null) _boltRest = _bolt.Position;
		if (_pump != null) _pumpRest = _pump.Position;
		if (_pin != null) _pinRest = _pin.Position;

		_right = PlaceHand(id, right: true);
		_left = PlaceHand(id, right: false);
		_leftRest = _left.Transform;

		// A mão chega em cada ponto virada como está no descanso, com a
		// palma encostada nele.
		_leftAtMag = _magazine != null ? PalmOn(WeaponModel.Bottom(_magazine) + new Vector3(0, -0.012f, 0), 0f, 18f) : _leftRest;
		_leftOut = new Transform3D(_leftAtMag.Basis.Rotated(Vector3.Right, Mathf.DegToRad(-25f)), _leftAtMag.Origin + OutOfView);
		_leftAtBolt = _bolt != null ? PalmOn(WeaponModel.Bounds(_bolt).GetCenter() + new Vector3(0, 0.01f, 0), 0f, 0f) : _leftRest;

		Node3D port = _held.GetNodeOrNull<Node3D>(WeaponModel.LoadName);
		_leftAtPort = port != null ? PalmOn(port.Position, 0f, 10f) : _leftRest;
		_leftAtPin = _pin != null ? PalmOn(WeaponModel.Bounds(_pin).GetCenter(), 0f, 0f) : _leftRest;
	}

	private Node3D PlaceHand(string id, bool right)
	{
		WeaponModel.HandPlace place = WeaponModel.Hand(id, right);
		Node3D hand = WeaponModel.BuildHand(place.Pose, right);
		hand.Transform = place.Transform;
		_held.AddChild(hand);
		return hand;
	}

	/// <summary>A mão esquerda com a palma em <paramref name="point"/>
	/// (espaço da arma), girada um pouco a partir do descanso.</summary>
	private Transform3D PalmOn(Vector3 point, float yawDegrees, float rollDegrees)
	{
		// A mão esquerda é o espelho da direita: a palma dela fica do
		// mesmo lado (−Y), o resto troca de lado sozinho.
		Basis basis = _leftRest.Basis
			.Rotated(Vector3.Up, Mathf.DegToRad(yawDegrees))
			.Rotated(Vector3.Forward, Mathf.DegToRad(rollDegrees));
		return new Transform3D(basis, point - basis * WeaponModel.Palm);
	}

	private void OnAmmo(int inMag, int magSize, bool reloading)
	{
		if (reloading && !_wasReloading) _emptyReload = inMag == 0;
		_wasReloading = reloading;
	}

	private void UpdateHands(float dt)
	{
		if (_held == null) return;

		if (HasRig)
		{
			UpdateRig(dt);
			return;
		}

		bool reloading = _weapon.IsReloading && _weapon.Held != null && !_form.IsWolf;
		_reloadPose = Mathf.MoveToward(_reloadPose, reloading ? 1f : 0f, dt * (reloading ? 7f : 5f));
		_boltKick = Mathf.MoveToward(_boltKick, 0f, dt * 14f);
		_slash = Mathf.MoveToward(_slash, 1f, dt / 0.32f);
		_throw = Mathf.MoveToward(_throw, 0f, dt / 0.25f);

		float p = reloading ? _weapon.ReloadProgress : 1f;
		float jolt = 0f;
		Vector3 boltOffset = new(0, 0, BoltTravel * _boltKick);
		Transform3D left = _leftRest;

		if (_left != null)
		{
			if (_magazine != null) left = MagazineReload(reloading, p, ref jolt, ref boltOffset);
			else if (_weapon.Held is { ShellByShell: true }) left = ShellReload(reloading, p, ref jolt);

			left = Pump(dt, left);
			left = PullPin(dt, left);
			_left.Transform = left;
		}

		if (_bolt != null) _bolt.Position = _boltRest + boltOffset;

		// A pose da arma: inclinada na recarga, para trás armando a
		// granada, atravessando a tela no golpe de faca.
		float r = Smooth(_reloadPose);
		float w = Smooth(_windup);
		float t = Mathf.Sin(_throw * Mathf.Pi);
		// Na recarga o cano sobe e a arma gira para o carregador ficar de
		// frente para quem recarrega, no meio da tela.
		Vector3 rotation = new(16f * r + 3.5f * jolt + 38f * w - 30f * t, AimYaw + 14f * r - 10f * w, -24f * r - 18f * w);
		Vector3 position = Anchor(_heldId)
			+ new Vector3(-0.035f * r, 0.05f * r + 0.012f * jolt, 0.02f * r)
			+ new Vector3(0.01f * w, 0.1f * w, 0.1f * w)
			+ new Vector3(-0.03f * t, -0.03f * t, -0.2f * t);

		Slash(ref position, ref rotation);

		_held.RotationDegrees = rotation;
		_held.Position = position;
	}

	/// <summary>
	/// Troca de carregador. Os tempos são frações da recarga, então a
	/// vazia (mais longa, com o ferrolho no fim) tem o mesmo desenho, só
	/// mais devagar:
	///
	/// 0–10% a mão vai ao carregador · 10–18% agarra · 18–36% tira e some ·
	/// 36–50% busca outro · 50–66% encaixa · 66–72% bate por baixo ·
	/// pente vazio: 72–84% vai ao ferrolho, 84–92% puxa e solta ·
	/// depois volta ao guarda-mão.
	/// </summary>
	private Transform3D MagazineReload(bool reloading, float p, ref float jolt, ref Vector3 boltOffset)
	{
		if (!reloading)
		{
			_magazine.Position = _magRest;
			_magazine.Visible = true;
			return _leftRest;
		}

		Transform3D left;
		if (p < 0.10f) left = _leftRest.InterpolateWith(_leftAtMag, Span(p, 0f, 0.10f));
		else if (p < 0.18f) left = _leftAtMag;
		else if (p < 0.36f) left = _leftAtMag.InterpolateWith(_leftOut, Span(p, 0.18f, 0.36f));
		else if (p < 0.50f) left = _leftOut;
		else if (p < 0.66f) left = _leftOut.InterpolateWith(_leftAtMag, Span(p, 0.50f, 0.66f));
		else if (p < 0.72f)
		{
			left = _leftAtMag;
			jolt = Mathf.Sin((p - 0.66f) / 0.06f * Mathf.Pi);
		}
		else if (_emptyReload && _bolt != null)
		{
			if (p < 0.84f) left = _leftAtMag.InterpolateWith(_leftAtBolt, Span(p, 0.72f, 0.84f));
			else if (p < 0.92f)
			{
				// Puxa até o fim e solta: o ferrolho volta sozinho, de estalo.
				float pull = p < 0.9f ? Span(p, 0.84f, 0.9f) : 1f - Span(p, 0.9f, 0.92f);
				boltOffset = new Vector3(0, 0, BoltTravel * pull);
				left = _leftAtBolt.Translated(new Vector3(0, 0, BoltTravel * (p < 0.9f ? pull : 1f)));
				if (p >= 0.9f) jolt = 0.6f;
			}
			else left = _leftAtBolt.Translated(new Vector3(0, 0, BoltTravel)).InterpolateWith(_leftRest, Span(p, 0.92f, 1f));
		}
		else left = _leftAtMag.InterpolateWith(_leftRest, Span(p, 0.72f, 0.9f));

		// O carregador vai na mão do momento em que ela agarra até encaixar.
		bool inHand = p >= 0.18f && p < 0.66f;
		_magazine.Position = _magRest + (inHand ? left.Origin - _leftAtMag.Origin : Vector3.Zero);
		_magazine.Visible = !(p > 0.34f && p < 0.52f);
		return left;
	}

	/// <summary>Escopeta: a cada cartucho a mão sobe de baixo até a janela
	/// de carga, empurra e desce. O progresso é o do cartucho atual.</summary>
	private Transform3D ShellReload(bool reloading, float p, ref float jolt)
	{
		float blend = Smooth(_reloadPose);
		if (blend <= 0f) return _leftRest;

		Transform3D low = new(_leftAtPort.Basis, _leftAtPort.Origin + new Vector3(-0.03f, -0.2f, 0.08f));
		Transform3D cycle;
		if (!reloading) cycle = low;
		else if (p < 0.5f) cycle = low.InterpolateWith(_leftAtPort, Span(p, 0.05f, 0.5f));
		else if (p < 0.65f)
		{
			cycle = _leftAtPort.Translated(new Vector3(0, 0, -0.03f * Mathf.Sin((p - 0.5f) / 0.15f * Mathf.Pi)));
			jolt = Mathf.Sin((p - 0.5f) / 0.15f * Mathf.Pi) * 0.7f;
		}
		else cycle = _leftAtPort.InterpolateWith(low, Span(p, 0.65f, 1f));

		return _leftRest.InterpolateWith(cycle, blend);
	}

	/// <summary>A bombeada depois do tiro: a telha e a mão que a segura
	/// vão para trás e voltam, e a cápsula sai no meio.</summary>
	private Transform3D Pump(float dt, Transform3D left)
	{
		if (_pump == null) return left;

		float offset = 0f;
		if (_pumpTime >= 0f)
		{
			float before = _pumpTime;
			_pumpTime += dt;
			const float wait = 0.14f, length = 0.36f;
			float s = (_pumpTime - wait) / length;
			if (s >= 1f) _pumpTime = -1f;
			else if (s > 0f)
			{
				offset = PumpTravel * (s < 0.45f ? Span(s, 0f, 0.45f) : 1f - Span(s, 0.55f, 1f));
				if ((before - wait) / length < 0.45f && s >= 0.45f) EjectCasing();
			}
		}

		_pump.Position = _pumpRest + new Vector3(0, 0, offset);
		return _weapon.IsReloading ? left : left.Translated(new Vector3(0, 0, offset));
	}

	/// <summary>Granada: enquanto o botão está preso o braço arma o
	/// arremesso e a outra mão tira o pino; soltou, o braço lança.</summary>
	private Transform3D PullPin(float dt, Transform3D left)
	{
		bool winding = _weapon.IsGrenadeReady;
		if (!winding && _windup > 0.5f && _heldId == Arsenal.GrenadeId) _throw = 1f;
		_windup = Mathf.MoveToward(_windup, winding ? 1f : 0f, dt * (winding ? 4f : 10f));
		if (_pin == null) return left;

		float reach = Span(_windup, 0f, 0.45f);
		float pull = Span(_windup, 0.45f, 0.9f);
		Vector3 away = new Vector3(-0.16f, -0.02f, 0.03f) * pull;
		_pin.Position = _pinRest + away;
		return left.InterpolateWith(_leftAtPin, reach).Translated(away);
	}

	private void StartSlash() => _slash = 0f;

	/// <summary>O golpe de faca: recolhe um instante, corta da direita
	/// para a esquerda e volta.</summary>
	private void Slash(ref Vector3 position, ref Vector3 rotation)
	{
		if (_slash >= 1f) return;

		float back = Span(_slash, 0f, 0.16f) * (1f - Span(_slash, 0.16f, 0.5f));
		float cut = Span(_slash, 0.14f, 0.5f) * (1f - Span(_slash, 0.6f, 1f));
		position += new Vector3(0.05f * back - 0.3f * cut, 0.03f * back + 0.02f * cut, 0.05f * back - 0.14f * cut);
		rotation += new Vector3(-8f * back + 14f * cut, -18f * back + 62f * cut, -10f * back + 55f * cut);
	}
}
