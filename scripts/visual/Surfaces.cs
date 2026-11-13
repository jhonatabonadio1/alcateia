using Godot;

/// <summary>
/// Do que é feito o chão ou a parede: <c>metal</c>, <c>neve</c>,
/// <c>madeira</c>, <c>terra</c> — ou vazio, que é concreto, o padrão.
///
/// O mapa diz, num metadado <c>piso</c> no corpo de colisão (o gerador de
/// mapas põe sozinho, pelo material da peça — tools/mapas/kit.py). É só
/// enfeite: escolhe o som do passo e o da bala na parede. Nenhuma regra
/// do jogo lê isto.
/// </summary>
public static class Surfaces
{
	public const string Meta = "piso";

	public static string Of(Node collider)
	{
		if (collider == null || !collider.HasMeta(Meta)) return "";
		return collider.GetMeta(Meta).AsString();
	}

	/// <summary>O que há debaixo de <paramref name="position"/> (os pés
	/// de alguém), ignorando o corpo de <paramref name="ignore"/>.</summary>
	public static string Under(Vector3 position, Node ignore)
	{
		if (ignore is not Node3D node || !node.IsInsideTree()) return "";

		var query = PhysicsRayQueryParameters3D.Create(position + Vector3.Up * 0.6f, position + Vector3.Down * 1.2f);
		query.CollideWithAreas = false;
		if (ignore is CollisionObject3D body)
			query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };

		var hit = node.GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 && hit["collider"].Obj is Node found ? Of(found) : "";
	}
}
