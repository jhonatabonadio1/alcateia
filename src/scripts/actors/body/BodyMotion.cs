using Godot;

/// <summary>Movimentos de corpo que não são de ninguém em particular.</summary>
public static class BodyMotion
{
	/// <summary>Parado no lugar, com gravidade: contagem, fim de partida,
	/// menu aberto. Sem a gravidade quem estivesse no ar ficaria
	/// pendurado.</summary>
	public static void Hold(CharacterBody3D body, double delta, float gravity)
	{
		body.Velocity = new Vector3(0, body.IsOnFloor() ? 0 : body.Velocity.Y - gravity * (float)delta, 0);
		body.MoveAndSlide();
	}

	/// <summary>
	/// O degrau à frente: de <paramref name="from"/>, sobe <paramref name="up"/>,
	/// anda <paramref name="ahead"/> e desce até o chão. Verdadeiro, com o ponto
	/// em que o corpo pousa, se nada barra no caminho e o chão de lá é firme
	/// (não mais inclinado que <paramref name="floorMaxAngle"/>). Quem chama
	/// decide se o pouso é alto o bastante e o que fazer com o olho; o jogador
	/// e o bot sobem o degrau por aqui (Player.TryStepUp, Bot.TryStepUp).
	/// </summary>
	public static bool ProbeStep(CharacterBody3D body, Vector3 from, Vector3 up, Vector3 ahead, float floorMaxAngle, out Vector3 landed)
	{
		landed = from;
		Transform3D at = body.GlobalTransform;
		at.Origin = from;
		if (body.TestMove(at, up)) return false;          // teto em cima
		at.Origin += up;
		if (body.TestMove(at, ahead)) return false;       // mais alto que um degrau
		at.Origin += ahead;

		var floor = new KinematicCollision3D();
		if (!body.TestMove(at, -up - Vector3.Up * 0.05f, floor)) return false;
		if (floor.GetNormal().Dot(Vector3.Up) < Mathf.Cos(floorMaxAngle)) return false;

		landed = at.Origin + floor.GetTravel();
		return true;
	}
}
