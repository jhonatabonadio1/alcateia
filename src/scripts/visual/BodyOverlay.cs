using Godot;
using System.Collections.Generic;

/// <summary>
/// A passada por cima da malha (<see cref="GeometryInstance3D.MaterialOverlay"/>)
/// é uma só, e dois efeitos querem ela nos corpos: o brilho do lobo
/// (<see cref="WolfFx"/>, a "aura") e a borda (<see cref="Silhouette"/>,
/// do time ou de quem te matou). Cada um diz o seu aqui e esta classe
/// junta os dois — a borda vai como passada seguinte do brilho. Até
/// 19/11 o lobo, ligando e desligando o brilho, apagava a borda dele.
/// </summary>
public static class BodyOverlay
{
	private const string AuraKey = "overlay_aura", EdgeKey = "overlay_borda";
	private static readonly Dictionary<(ulong, ulong), Material> Mixed = new();

	public static void SetAura(GeometryInstance3D mesh, Material aura) => Set(mesh, AuraKey, aura);

	public static void SetEdge(GeometryInstance3D mesh, Material edge) => Set(mesh, EdgeKey, edge);

	public static Material Edge(GeometryInstance3D mesh) => Get(mesh, EdgeKey);

	public static Material Aura(GeometryInstance3D mesh) => Get(mesh, AuraKey);

	private static Material Get(GeometryInstance3D mesh, string key) =>
		mesh.HasMeta(key) ? mesh.GetMeta(key).As<Material>() : null;

	private static void Set(GeometryInstance3D mesh, string key, Material material)
	{
		if (!GodotObject.IsInstanceValid(mesh) || Get(mesh, key) == material) return;
		if (material == null) mesh.RemoveMeta(key);
		else mesh.SetMeta(key, material);

		Material aura = Get(mesh, AuraKey), edge = Get(mesh, EdgeKey);
		mesh.MaterialOverlay = aura == null ? edge : edge == null ? aura : Mix(aura, edge);
	}

	private static Material Mix(Material aura, Material edge)
	{
		var key = (aura.GetInstanceId(), edge.GetInstanceId());
		if (Mixed.TryGetValue(key, out Material mixed) && GodotObject.IsInstanceValid(mixed)) return mixed;

		mixed = (Material)aura.Duplicate();
		Material last = mixed;
		while (last.NextPass != null) last = last.NextPass = (Material)last.NextPass.Duplicate();
		last.NextPass = edge;
		Mixed[key] = mixed;
		return mixed;
	}
}
