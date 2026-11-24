using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Uma animação de terceira pessoa com arma, feita no Blender a partir da
/// mesma animação da primeira pessoa (<c>tools/personagens/blender/animacoes_armas.py</c>;
/// Jhon, 22/11: "vamos copiar exatamente as animações dos braços, tronco e
/// etc e vamos adaptá-las nos personagens, vamos usar o rig de cada arma para
/// isso"). Quadro a quadro: a rotação local de cada osso da cintura para cima
/// (o tronco, a cabeça, os braços e cada dedo) e onde o conjunto da primeira
/// pessoa (a arma, com o carregador e o ferrolho que a animação mexe) fica em
/// relação ao peito (o osso Spine2).
/// </summary>
public sealed class HoldClip
{
	public string Name = "";
	public int Frames;
	public float Fps = 30f;
	public bool Loop;
	public int BoneCount;

	/// <summary>A rotação de cada osso em cada quadro: [quadro * BoneCount + osso].</summary>
	public Quaternion[] Bones = Array.Empty<Quaternion>();

	/// <summary>O conjunto no espaço do Spine2, em cada quadro.</summary>
	public Transform3D[] Rig = Array.Empty<Transform3D>();

	/// <summary>A posição local de alguns ossos em cada quadro: [quadro * PosCount + osso].
	/// Na primeira pessoa a palma fecha deslizando os nós dos dedos (até 2,4 cm),
	/// e só rotação não faz isso (veja <see cref="HoldClipSet.PosNames"/>).</summary>
	public Vector3[] Pos = Array.Empty<Vector3>();
	public int PosCount;

	/// <summary>A troca de pente na recarga vista de fora (s do clipe; -1: não troca): a mão
	/// agarra o pente, solta-o já fora do encaixe (ele cai no chão), pega o novo no bolso e
	/// ele encaixa ("carregador" do arquivo; Jhon, 23/11: "a Eve tira o pente, ele cai no
	/// chão, tira outro pente do bolso").</summary>
	public float MagGrab = -1f, MagDrop = -1f, MagPick = -1f, MagSeat = -1f;
	public bool SwapsMagazine => MagGrab >= 0f && MagDrop >= MagGrab && MagPick > MagDrop && MagSeat > MagPick;

	/// <summary>O pino da granada no armar visto de fora (s do clipe; -1: não tem): quando ele sai
	/// da granada, no dedo da mão esquerda, e quando ela o larga e ele cai no chão ("pino" do
	/// arquivo; Jhon, 23/11: "o gatilho da granada deve cair no chão também").</summary>
	public float PinOut = -1f, PinDrop = -1f;
	public bool PullsPin => PinOut >= 0f && PinDrop > PinOut;

	/// <summary>A duração (s).</summary>
	public float Length => Frames > 1 ? (Frames - 1) / Fps : 0f;

	/// <summary>Os dois quadros em volta do instante t e quanto de um para o
	/// outro: em laço, volta ao começo; senão, para no último.</summary>
	private void At(double t, out int a, out int b, out float w)
	{
		if (Frames <= 1)
		{
			a = b = 0;
			w = 0f;
			return;
		}
		double f = t * Fps;
		if (Loop)
		{
			f %= Frames - 1;
			if (f < 0) f += Frames - 1;
		}
		else f = Math.Clamp(f, 0, Frames - 1);
		a = Math.Min((int)Math.Floor(f), Frames - 1);
		b = Math.Min(a + 1, Frames - 1);
		w = (float)(f - a);
	}

	/// <summary>A posição local de um osso da lista de posições no instante t.</summary>
	public Vector3 Position(int bone, double t)
	{
		At(t, out int a, out int b, out float w);
		Vector3 pa = Pos[a * PosCount + bone], pb = Pos[b * PosCount + bone];
		return w < 1e-4f ? pa : pa.Lerp(pb, w);
	}

	/// <summary>A rotação de um osso (índice da lista do arquivo) no instante t.</summary>
	public Quaternion Bone(int bone, double t)
	{
		At(t, out int a, out int b, out float w);
		Quaternion qa = Bones[a * BoneCount + bone], qb = Bones[b * BoneCount + bone];
		return w < 1e-4f ? qa : qa.Slerp(qb, w);
	}

	/// <summary>Onde o conjunto fica (no espaço do Spine2) no instante t.</summary>
	public Transform3D RigAt(double t)
	{
		At(t, out int a, out int b, out float w);
		return w < 1e-4f ? Rig[a] : HoldClips.Blend(Rig[a], Rig[b], w);
	}
}

/// <summary>As animações de uma arma num personagem.</summary>
public sealed class HoldClipSet
{
	/// <summary>Os ossos das trilhas, sem o prefixo do Mixamo ("Spine", "RightHandIndex1"…).</summary>
	public string[] BoneNames = Array.Empty<string>();

	/// <summary>Os ossos que também têm a posição no arquivo ("posicoes"; pode não ter).</summary>
	public string[] PosNames = Array.Empty<string>();
	public readonly Dictionary<string, HoldClip> Clips = new();

	/// <summary>Quantas vezes o conjunto (a arma) cresce neste personagem
	/// ("escala" do arquivo): a arma da primeira pessoa é do tamanho da mão
	/// de lá, e a de um personagem de mão maior fica de brinquedo sem isso.
	/// As animações foram feitas no Blender com a arma já deste tamanho.</summary>
	public float Scale = 1f;

	public HoldClip Get(string name) => name != null && Clips.TryGetValue(name, out HoldClip clip) ? clip : null;
}

/// <summary>
/// As animações de cada personagem, por arma
/// (<c>assets/personagens/&lt;p&gt;/animacoes/&lt;arma&gt;.json</c>), lidas uma vez e
/// guardadas. Cada pintura usa as do modelo dela (o mesmo conjunto); a faca e
/// a granada, as do tipo.
/// </summary>
public static class HoldClips
{
	private static readonly Dictionary<string, HoldClipSet> Cache = new();

	/// <summary>Qual arquivo de animações uma arma usa.</summary>
	public static string KeyOf(string weaponId) =>
		string.IsNullOrEmpty(weaponId) ? ""
		: Arsenal.IsKnife(weaponId) ? "faca"
		: Arsenal.IsGrenade(weaponId) ? "granada"
		: Sfx.ModelOf(weaponId);

	/// <summary>O arquivo de animações de uma arma, ao lado do modelo do personagem.</summary>
	public static string PathFor(string modelPath, string weaponId)
	{
		string key = KeyOf(weaponId);
		return string.IsNullOrEmpty(modelPath) || key == "" ? null : $"{modelPath.GetBaseDir()}/animacoes/{key}.json";
	}

	/// <summary>As animações de um arquivo; null se não existe.</summary>
	public static HoldClipSet For(string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		if (Cache.TryGetValue(path, out HoldClipSet known)) return known;
		HoldClipSet set = null;
		// Como as poses e a ficha da primeira pessoa: o .json entra no jogo
		// exportado como recurso.
		if (ResourceLoader.Exists(path))
		{
			Variant data = GD.Load<Json>(path)?.Data ?? default;
			if (data.VariantType == Variant.Type.Dictionary) set = Read(data.AsGodotDictionary());
			else GD.PushWarning($"HoldClips: could not read {path}.");
		}
		Cache[path] = set;
		return set;
	}

	private static HoldClipSet Read(Godot.Collections.Dictionary root)
	{
		var set = new HoldClipSet();
		float fps = root.TryGetValue("fps", out Variant f) ? f.AsSingle() : 30f;
		if (root.TryGetValue("escala", out Variant scale)) set.Scale = Mathf.Clamp(scale.AsSingle(), 0.5f, 2f);
		var names = root["ossos"].AsGodotArray();
		set.BoneNames = new string[names.Count];
		for (int i = 0; i < names.Count; i++) set.BoneNames[i] = names[i].AsString();
		int bones = set.BoneNames.Length;
		int per = bones * 4 + 7;
		if (root.TryGetValue("posicoes", out Variant posNames))
		{
			var list = posNames.AsGodotArray();
			set.PosNames = new string[list.Count];
			for (int i = 0; i < list.Count; i++) set.PosNames[i] = list[i].AsString();
		}
		int posCount = set.PosNames.Length;

		foreach (var (name, value) in root["clipes"].AsGodotDictionary())
		{
			var d = value.AsGodotDictionary();
			int frames = d["quadros"].AsInt32();
			byte[] raw = Convert.FromBase64String(d["dados"].AsString());
			if (raw.Length < frames * per * 2)
			{
				GD.PushWarning($"HoldClips: clip {name} is short ({raw.Length} bytes).");
				continue;
			}
			var clip = new HoldClip
			{
				Name = name.AsString(),
				Frames = frames,
				Fps = fps,
				Loop = d.TryGetValue("laco", out Variant loop) && loop.AsBool(),
				BoneCount = bones,
				Bones = new Quaternion[frames * bones],
				Rig = new Transform3D[frames],
			};
			for (int k = 0; k < frames; k++)
			{
				int at = k * per;
				for (int b = 0; b < bones; b++)
				{
					int i = (at + b * 4) * 2;
					clip.Bones[k * bones + b] = new Quaternion(Q(raw, i), Q(raw, i + 2), Q(raw, i + 4), Q(raw, i + 6)).Normalized();
				}
				int r = (at + bones * 4) * 2;
				var origin = new Vector3(P(raw, r), P(raw, r + 2), P(raw, r + 4));
				var rotation = new Quaternion(Q(raw, r + 6), Q(raw, r + 8), Q(raw, r + 10), Q(raw, r + 12)).Normalized();
				clip.Rig[k] = new Transform3D(new Basis(rotation), origin);
			}
			if (posCount > 0 && d.TryGetValue("pos", out Variant posData))
			{
				byte[] posRaw = Convert.FromBase64String(posData.AsString());
				if (posRaw.Length >= frames * posCount * 6)
				{
					clip.PosCount = posCount;
					clip.Pos = new Vector3[frames * posCount];
					for (int i = 0; i < clip.Pos.Length; i++)
						clip.Pos[i] = new Vector3(P(posRaw, i * 6), P(posRaw, i * 6 + 2), P(posRaw, i * 6 + 4));
				}
			}
			if (d.TryGetValue("carregador", out Variant mag) && mag.AsGodotArray().Count == 4)
			{
				var t = mag.AsGodotArray();
				clip.MagGrab = t[0].AsSingle();
				clip.MagDrop = t[1].AsSingle();
				clip.MagPick = t[2].AsSingle();
				clip.MagSeat = t[3].AsSingle();
			}
			if (d.TryGetValue("pino", out Variant pin) && pin.AsGodotArray().Count == 2)
			{
				clip.PinOut = pin.AsGodotArray()[0].AsSingle();
				clip.PinDrop = pin.AsGodotArray()[1].AsSingle();
			}
			set.Clips[clip.Name] = clip;
		}
		return set;
	}

	private static float Q(byte[] raw, int i) => BitConverter.ToInt16(raw, i) / 32767f;
	private static float P(byte[] raw, int i) => BitConverter.ToInt16(raw, i) / 10000f;

	/// <summary>De um para o outro, girando pelo caminho curto.</summary>
	public static Transform3D Blend(Transform3D a, Transform3D b, float w) =>
		new(new Basis(a.Basis.GetRotationQuaternion().Slerp(b.Basis.GetRotationQuaternion(), w)), a.Origin.Lerp(b.Origin, w));
}
