using Godot;

/// <summary>Movimentos de corpo que não são de ninguém em particular.</summary>
public static class BodyMotion
{
	/// <summary>O humano deste corpo está correndo (Shift)? O jogador
	/// decide o dele; o boneco de outro jogador mostra o que o dono contou.
	/// É o que o corpo animado, a arma na mão e os braços na tela perguntam.</summary>
	public static bool HumanSprinting(CharacterBody3D body) => body switch
	{
		Player player => player.IsHumanSprinting,
		Bot bot => bot.IsHumanSprinting,
		_ => false
	};

	/// <summary>Parado no lugar, com gravidade: contagem, fim de partida,
	/// menu aberto. Sem a gravidade quem estivesse no ar ficaria
	/// pendurado.</summary>
	public static void Hold(CharacterBody3D body, double delta, float gravity)
	{
		body.Velocity = new Vector3(0, body.IsOnFloor() ? 0 : body.Velocity.Y - gravity * (float)delta, 0);
		body.MoveAndSlide();
	}

	/// <summary>O mínimo que o corpo precisa conseguir subir, debaixo de um teto
	/// baixo, para tentar um degrau (m).</summary>
	public const float MinStepRoom = 0.06f;

	/// <summary>
	/// O degrau à frente: de <paramref name="from"/>, sobe <paramref name="up"/>,
	/// anda <paramref name="ahead"/> e desce até o chão. Verdadeiro, com o ponto
	/// em que o corpo pousa, se nada barra no caminho e o chão de lá é firme
	/// (não mais inclinado que <paramref name="floorMaxAngle"/>). Quem chama
	/// decide se o pouso é alto o bastante e o que fazer com o olho; o jogador
	/// e o bot sobem o degrau por aqui (Player.TryStepUp, Bot.TryStepUp).
	///
	/// Com teto baixo em cima (a verga de uma porta), sobe só o que couber
	/// (27/11): o lobo, que testa o degrau 45 cm acima, não cabia debaixo da
	/// porta das casas de escada do Deserto e a subida era recusada todo quadro
	/// — ficava parado no primeiro degrau, de 18 cm.
	/// </summary>
	public static bool ProbeStep(CharacterBody3D body, Vector3 from, Vector3 up, Vector3 ahead, float floorMaxAngle, out Vector3 landed)
	{
		landed = from;
		Transform3D at = body.GlobalTransform;
		at.Origin = from;
		var hit = new KinematicCollision3D();
		if (body.TestMove(at, up, hit))
		{
			// Teto em cima: até onde dá. Menos que um dedo, não há degrau que se suba.
			up = hit.GetTravel();
			if (up.Y < MinStepRoom) return false;
		}
		at.Origin += up;
		if (body.TestMove(at, ahead)) return false;       // mais alto que um degrau
		at.Origin += ahead;

		var floor = new KinematicCollision3D();
		if (!body.TestMove(at, -up - Vector3.Up * 0.05f, floor)) return false;
		if (floor.GetNormal().Dot(Vector3.Up) < Mathf.Cos(floorMaxAngle)) return false;

		landed = at.Origin + floor.GetTravel();
		return true;
	}

	/// <summary>
	/// Há um degrau para descer logo à frente? De onde o corpo está, anda
	/// <paramref name="ahead"/> (o raio dele: onde fica quando acaba de passar
	/// da quina) e desce: verdadeiro se pousa em chão firme, entre 3 cm e
	/// <paramref name="height"/> abaixo de onde está. No plano não há (pousa na
	/// mesma altura); na beira de uma laje também não (não pousa). Numa rampa
	/// há, e não faz mal: lá o corpo já anda grudado no chão.
	/// </summary>
	public static bool StepBelow(CharacterBody3D body, Vector3 ahead, float height, float floorMaxAngle)
	{
		Transform3D at = body.GlobalTransform;
		if (body.TestMove(at, ahead)) return false;       // parede à frente
		at.Origin += ahead;

		var floor = new KinematicCollision3D();
		if (!body.TestMove(at, Vector3.Down * (height + 0.05f), floor)) return false;
		float drop = -floor.GetTravel().Y;
		return drop > 0.03f && drop <= height && floor.GetNormal().Dot(Vector3.Up) >= Mathf.Cos(floorMaxAngle);
	}

	/// <summary>Quão à frente do corpo se procura o degrau de baixo (m): o raio
	/// da cápsula — é onde ela fica quando acaba de passar da quina.</summary>
	public const float StepReach = 0.3f;

	/// <summary>Descendo um degrau, até que inclinação a quina dele conta como
	/// chão (graus): a cápsula de 30 cm de raio rola pela quina de um degrau de
	/// 17 cm até uns 65° antes de assentar no de baixo.</summary>
	public const float StairFloorAngle = 72f;

	/// <summary>
	/// O <c>MoveAndSlide</c> de quem anda no chão, segurando o corpo na escada ao
	/// descer (27/11; Jhon: "subir qualquer tipo de escadaria, de qualquer mapa,
	/// tranquilamente, humano ou lobo. Subir e descer"). Com um degrau logo abaixo
	/// à frente (<see cref="StepBelow"/>), o corpo fica grudado no chão até lá (o
	/// ímã do Godot) e a quina do degrau conta como chão. Sem isto a cápsula,
	/// redonda embaixo, rolava pela quina até ela deixar de ser chão (45°), caía
	/// em queda livre e voava dois a três degraus de cada vez (medido no humano
	/// andando: 15 passos no ar, 45 cm de queda, a cada pulo); e o ímã sozinho não
	/// segurava — puxado para baixo, o corpo esbarra na própria quina, que não é
	/// chão. O ímã e o ângulo voltam ao que eram depois do passo: a corrida na
	/// parede e o resto contam com o valor do corpo.
	/// </summary>
	/// <param name="walking">O corpo começou o passo no chão e não está pulando.</param>
	/// <param name="direction">Para onde ele anda (no plano, tamanho 1), ou zero.</param>
	public static void MoveOnSteps(CharacterBody3D body, bool walking, Vector3 direction, float stepHeight)
	{
		float stair = Mathf.DegToRad(StairFloorAngle);
		bool held = walking && direction != Vector3.Zero && StepBelow(body, direction * StepReach, stepHeight, stair);
		float snap = body.FloorSnapLength, angle = body.FloorMaxAngle;
		if (held)
		{
			body.FloorSnapLength = Mathf.Max(snap, stepHeight);
			body.FloorMaxAngle = Mathf.Max(angle, stair);
		}
		body.MoveAndSlide();
		body.FloorSnapLength = snap;
		body.FloorMaxAngle = angle;
	}
}
