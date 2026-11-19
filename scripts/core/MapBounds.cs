using Godot;
using System.Collections.Generic;

/// <summary>
/// O espaço jogável do mapa, como uma caixa: é até onde a câmera livre do
/// espectador pode ir (pedido do Jhon, 18/11: "no modo espectador não posso
/// sair do espaço do mapa; configure no Armazém e em todos os mapas
/// futuros").
///
/// **A regra para os mapas** (docs/MAPAS.md): o mapa fecha o espaço com
/// paredes de colisão invisíveis chamadas <c>Limite…</c> — LimiteOeste,
/// LimiteLeste, LimiteNorte, LimiteSul e LimiteTeto, como os dois
/// Armazéns. A caixa é o lado de dentro delas: cada parede fina corta a
/// caixa no lado em que está, e o teto corta em cima. O chão é o topo do
/// corpo <c>Piso</c> (ou <c>Chao</c>), se houver.
///
/// Mapa sem paredes de limite (os de antes, a sala de treino) cai numa
/// caixa tirada de toda a colisão do mapa, com 6 m de folga em cima.
/// </summary>
public static class MapBounds
{
	private static ulong _scene;
	private static Aabb _cached;
	private static bool _fromLimits;

	/// <summary>A caixa veio das paredes <c>Limite…</c> (e não da colisão
	/// toda). Para os testes.</summary>
	public static bool FromLimits => _fromLimits;

	/// <summary>A caixa do mapa da cena atual (lida uma vez por cena).</summary>
	public static Aabb Of(SceneTree tree)
	{
		Node root = tree.CurrentScene;
		if (root == null) return new Aabb(new Vector3(-200, -20, -200), new Vector3(400, 80, 400));
		if (root.GetInstanceId() == _scene && _cached.Size != Vector3.Zero) return _cached;

		_scene = root.GetInstanceId();
		_cached = Measure(root, out _fromLimits);
		return _cached;
	}

	/// <summary>Mede a caixa de um mapa (o nó raiz dele ou a cena que o contém).</summary>
	public static Aabb Measure(Node root, out bool fromLimits)
	{
		var limits = new List<Aabb>();
		var all = new List<Aabb>();
		float? floor = null;

		foreach (Node node in root.FindChildren("*", nameof(StaticBody3D), true, false))
		{
			var body = (StaticBody3D)node;
			if (!Box(body, out Aabb box)) continue;
			string name = body.Name;
			if (name.StartsWith("Limite")) limits.Add(box);
			else
			{
				all.Add(box);
				if (name is "Piso" or "Chao") floor = floor.HasValue ? Mathf.Max(floor.Value, box.End.Y) : box.End.Y;
			}
		}

		fromLimits = limits.Count >= 4;
		if (!fromLimits)
		{
			if (all.Count == 0) return new Aabb(new Vector3(-200, -20, -200), new Vector3(400, 80, 400));
			Aabb union = all[0];
			foreach (Aabb b in all) union = union.Merge(b);
			return new Aabb(union.Position, union.Size + new Vector3(0, 6f, 0));
		}

		Aabb outer = limits[0];
		foreach (Aabb b in limits) outer = outer.Merge(b);
		Vector3 min = outer.Position, max = outer.End, mid = outer.GetCenter();

		foreach (Aabb wall in limits)
		{
			Vector3 size = wall.Size, center = wall.GetCenter();
			// O eixo mais fino é o da espessura da parede.
			if (size.Y <= size.X && size.Y <= size.Z)
			{
				if (center.Y > mid.Y) max.Y = Mathf.Min(max.Y, wall.Position.Y);
				else min.Y = Mathf.Max(min.Y, wall.End.Y);
			}
			else if (size.X <= size.Z)
			{
				if (center.X < mid.X) min.X = Mathf.Max(min.X, wall.End.X);
				else max.X = Mathf.Min(max.X, wall.Position.X);
			}
			else
			{
				if (center.Z < mid.Z) min.Z = Mathf.Max(min.Z, wall.End.Z);
				else max.Z = Mathf.Min(max.Z, wall.Position.Z);
			}
		}

		if (floor.HasValue) min.Y = Mathf.Max(min.Y, floor.Value);
		return new Aabb(min, max - min);
	}

	/// <summary>Um ponto dentro da caixa, a <paramref name="margin"/> das bordas.</summary>
	public static Vector3 Clamp(SceneTree tree, Vector3 point, float margin = 0.4f)
	{
		Aabb box = Of(tree);
		Vector3 min = box.Position + Vector3.One * margin, max = box.End - Vector3.One * margin;
		return new Vector3(
			Mathf.Clamp(point.X, min.X, Mathf.Max(min.X, max.X)),
			Mathf.Clamp(point.Y, min.Y, Mathf.Max(min.Y, max.Y)),
			Mathf.Clamp(point.Z, min.Z, Mathf.Max(min.Z, max.Z)));
	}

	private static bool Box(StaticBody3D body, out Aabb box)
	{
		box = default;
		bool any = false;
		foreach (Node child in body.GetChildren())
		{
			if (child is not CollisionShape3D { Shape: not null, Disabled: false } shape) continue;
			Aabb local = shape.Shape is BoxShape3D cube
				? new Aabb(-cube.Size / 2f, cube.Size)
				: shape.Shape.GetDebugMesh().GetAabb();
			Aabb world = shape.GlobalTransform * local;
			box = any ? box.Merge(world) : world;
			any = true;
		}
		return any;
	}
}
