using Godot;
using System.Collections.Generic;

/// <summary>
/// Uma pose de terceira pessoa com arma, feita no Blender
/// (<c>tools/personagens/blender/poses_armas.py</c>, Jhon, 22/11: "use o
/// blender para isso"): a rotação local de cada osso da cintura para cima
/// (o tronco, a cabeça, os braços e cada dedo), em chaves que respiram, e
/// a rotação do peito no corpo.
/// </summary>
public sealed class HoldPose
{
	/// <summary>O conjunto da primeira pessoa com que a pose foi feita.</summary>
	public string Kit = "";

	/// <summary>O que a mão esquerda faz: "arma" (segura a arma), "guarda"
	/// (punho fechado na frente do peito) ou "solta" (ao lado do corpo).</summary>
	public string Left = "arma";

	/// <summary>Quanto a mão esquerda desliza para trás na arma (m): o braço
	/// não alcançava onde a da primeira pessoa segura.</summary>
	public float Slide;

	/// <summary>Quantas vezes a arma cresce na mão (a granada: a mão do
	/// personagem é maior que a do conjunto).</summary>
	public float Grow = 1f;

	/// <summary>O raio da granada na palma (m); 0 se não é uma bola.</summary>
	public float Ball;

	/// <summary>Onde o Blender deixou a mão direita, a esquerda, a cabeça e a
	/// arma (no corpo em pé), para os testes conferirem.</summary>
	public readonly Dictionary<string, Vector3> Check = new();

	public float[] Times = { 0f };
	public readonly Dictionary<string, Quaternion[]> Bones = new();
	public Quaternion[] Chest = { Quaternion.Identity };

	/// <summary>A duração de uma volta (a última chave).</summary>
	public float Length => Times[^1];

	/// <summary>Uma trilha no instante <paramref name="t"/> (em laço).</summary>
	public Quaternion Sample(Quaternion[] keys, double t)
	{
		if (keys.Length == 1 || Length <= 0f) return keys[0];
		float at = (float)(t % Length);
		for (int i = 0; i < Times.Length - 1 && i < keys.Length - 1; i++)
		{
			if (at > Times[i + 1]) continue;
			float span = Times[i + 1] - Times[i];
			float w = span > 1e-4f ? (at - Times[i]) / span : 0f;
			// Suave nas pontas: o peito para em cima e embaixo.
			w = w * w * (3f - 2f * w);
			return keys[i].Slerp(keys[i + 1], w);
		}
		return keys[^1];
	}
}

/// <summary>
/// As poses de cada personagem (<c>poses_armas.json</c>, na pasta dele), lidas
/// uma vez e guardadas.
/// </summary>
public static class HoldPoses
{
	private static readonly Dictionary<string, Dictionary<string, HoldPose>> Cache = new();

	/// <summary>O tipo de pose de uma arma.</summary>
	public static string KindOf(string weaponId) =>
		Arsenal.IsKnife(weaponId) ? "faca"
		: Arsenal.IsGrenade(weaponId) ? "granada"
		: WeaponModel.IsPistol(weaponId) ? "pistola"
		: "fuzil";

	/// <summary>O arquivo de poses ao lado do modelo do personagem.</summary>
	public static string PathFor(string modelPath) =>
		string.IsNullOrEmpty(modelPath) ? null : modelPath.GetBaseDir() + "/poses_armas.json";

	/// <summary>As poses de um personagem, por tipo; null se ele não tem.</summary>
	public static Dictionary<string, HoldPose> For(string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		if (Cache.TryGetValue(path, out var known)) return known;
		Dictionary<string, HoldPose> poses = null;
		// Como a ficha da primeira pessoa (ViewModel.RigSheet): o .json entra
		// no jogo exportado como recurso.
		if (ResourceLoader.Exists(path))
		{
			Variant data = GD.Load<Json>(path)?.Data ?? default;
			if (data.VariantType == Variant.Type.Dictionary) poses = Read(data.AsGodotDictionary());
			else GD.PushWarning($"HoldPoses: could not read {path}.");
		}
		Cache[path] = poses;
		return poses;
	}

	private static Dictionary<string, HoldPose> Read(Godot.Collections.Dictionary root)
	{
		var result = new Dictionary<string, HoldPose>();
		float[] times = { 0f };
		if (root.TryGetValue("chaves", out Variant keys))
		{
			var list = keys.AsGodotArray();
			times = new float[list.Count];
			for (int i = 0; i < list.Count; i++) times[i] = list[i].AsSingle();
		}
		if (!root.TryGetValue("poses", out Variant posesVariant)) return result;
		foreach (var (kind, value) in posesVariant.AsGodotDictionary())
		{
			var d = value.AsGodotDictionary();
			var pose = new HoldPose
			{
				Times = times,
				Kit = d.TryGetValue("conjunto", out Variant kit) ? kit.AsString() : "",
				Left = d.TryGetValue("esquerda", out Variant left) ? left.AsString() : "arma",
				Slide = d.TryGetValue("recuo", out Variant slide) ? slide.AsSingle() : 0f,
				Grow = d.TryGetValue("cresce", out Variant grow) ? grow.AsSingle() : 1f,
				Ball = d.TryGetValue("raio", out Variant ball) ? ball.AsSingle() : 0f,
			};
			foreach (var (bone, track) in d["ossos"].AsGodotDictionary())
				pose.Bones[bone.AsString()] = Keys(track.AsGodotArray());
			if (d.TryGetValue("peito", out Variant chest)) pose.Chest = Keys(chest.AsGodotArray());
			if (d.TryGetValue("conferir", out Variant check))
				foreach (var (what, at) in check.AsGodotDictionary())
				{
					var v = at.AsGodotArray();
					pose.Check[what.AsString()] = new Vector3(v[0].AsSingle(), v[1].AsSingle(), v[2].AsSingle());
				}
			result[kind.AsString()] = pose;
		}
		return result;
	}

	private static Quaternion[] Keys(Godot.Collections.Array list)
	{
		var keys = new Quaternion[list.Count];
		for (int i = 0; i < list.Count; i++)
		{
			var q = list[i].AsGodotArray();
			keys[i] = new Quaternion(q[0].AsSingle(), q[1].AsSingle(), q[2].AsSingle(), q[3].AsSingle()).Normalized();
		}
		return keys;
	}
}
