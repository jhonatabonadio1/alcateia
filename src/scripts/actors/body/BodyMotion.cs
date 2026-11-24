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
}
