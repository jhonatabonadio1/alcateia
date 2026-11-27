using Godot;
using System.Collections.Generic;

/// <summary>
/// As nuvens do céu dos mapas a céu aberto: camadas grandes, deitadas, bem
/// acima do mapa, em que a nuvem é desenhada por ruído e anda com o vento
/// (<c>src/assets/mapas/detalhe/nuvens.gdshader</c> — o shader feito para o
/// Setor Zero). Pedido do Jhon (26/11): "coloque esses shaders de nuvens
/// nos mapas de dia e de noite também".
///
/// Cada mapa declara o clima dele em <see cref="MapInfo.Clouds"/>
/// (<c>Maps.cs</c>) e o <see cref="MapSlot"/> põe as camadas quando o mapa
/// carrega, num nó <c>Nuvens</c> — a cena do mapa não muda. Mapa que já
/// traz um nó <c>Nuvens</c> (o Setor Zero, que tem também o mar de nuvens
/// do precipício) fica como está; mapa fechado (o Armazém) não declara
/// clima.
///
/// São só desenho: sem colisão, sem sombra, sem luz (a nuvem tem a cor
/// dela). O servidor de partida não as põe (não carrega pelo MapSlot), e a
/// foto de capa (<c>src/tools/fotos_mapas.gd</c>) também não.
/// </summary>
public static class SkyClouds
{
	private const string ShaderPath = "res://src/assets/mapas/detalhe/nuvens.gdshader";

	/// <summary>Uma camada: a altura, o tamanho do desenho (metros por
	/// volta), quanto do céu cobre, a maciez da borda, o mais opaca que
	/// fica, o vento (m/s), a cor do alto e a da parte rala, e o brilho (a
	/// nuvem não leva luz: de dia vale perto de 2,3, para não sair cinza
	/// contra o céu de foto; de noite, menos de 1).</summary>
	public readonly record struct Layer(float Height, float Scale, float Cover, float Soft, float Opacity, Vector2 Wind, Color Top, Color Thin, float Glow = 2.3f);

	/// <summary>As camadas de cada clima. Os números saíram das fotos do
	/// Setor Zero; ajuste aqui (docs/AJUSTES.md, "Nuvens").</summary>
	public static readonly Dictionary<string, Layer[]> Climates = new()
	{
		// Céu de dia: bancos brancos a 620 m e um véu alto.
		["dia"] = new[]
		{
			new Layer(620f, 700f, 0.40f, 0.20f, 0.90f, new Vector2(6f, 2f), new Color(1f, 1f, 1f), new Color(0.76f, 0.83f, 0.95f)),
			new Layer(1050f, 1300f, 0.34f, 0.30f, 0.55f, new Vector2(9f, 3.5f), new Color(1f, 1f, 1f), new Color(0.86f, 0.91f, 0.99f)),
		},
		// O deserto: céu limpo, poucas nuvens, altas e ralas.
		["dia_seco"] = new[]
		{
			new Layer(900f, 1100f, 0.22f, 0.26f, 0.70f, new Vector2(8f, 2.5f), new Color(1f, 0.99f, 0.96f), new Color(0.90f, 0.88f, 0.84f), 2.5f),
			new Layer(1400f, 1700f, 0.26f, 0.34f, 0.40f, new Vector2(11f, 4f), new Color(1f, 1f, 1f), new Color(0.92f, 0.92f, 0.94f), 2.5f),
		},
		// Fim de tarde (a Estação de Trem): douradas em cima, rosadas onde são ralas.
		["tarde"] = new[]
		{
			new Layer(620f, 760f, 0.46f, 0.22f, 0.90f, new Vector2(5f, 1.8f), new Color(1f, 0.86f, 0.66f), new Color(0.82f, 0.56f, 0.52f), 1.9f),
			new Layer(1050f, 1400f, 0.36f, 0.30f, 0.55f, new Vector2(8f, 3f), new Color(1f, 0.90f, 0.78f), new Color(0.88f, 0.66f, 0.62f), 1.9f),
		},
		// Noite (o Distrito Neon): escuras, com o roxo e o rosa da cidade por baixo.
		["noite"] = new[]
		{
			new Layer(560f, 760f, 0.50f, 0.24f, 0.80f, new Vector2(5f, 2f), new Color(0.44f, 0.34f, 0.66f), new Color(0.14f, 0.12f, 0.28f), 0.8f),
			new Layer(980f, 1400f, 0.40f, 0.32f, 0.50f, new Vector2(8f, 3f), new Color(0.36f, 0.32f, 0.60f), new Color(0.10f, 0.10f, 0.24f), 0.8f),
		},
	};

	/// <summary>Lado do plano de cada camada, e de onde a onde ela se
	/// desfaz (metros do meio do mapa): a borda do plano não se vê.</summary>
	public const float Side = 9000f, FadeFrom = 2400f, FadeTo = 3800f;

	/// <summary>Põe as nuvens do <paramref name="climate"/> no mapa. Devolve
	/// o nó, ou <c>null</c> se não havia o que pôr (clima vazio ou
	/// desconhecido, mapa que já tem as dele, shader que não carregou).</summary>
	public static Node3D AddTo(Node map, string climate)
	{
		if (map == null || string.IsNullOrEmpty(climate)) return null;
		if (map.HasNode("Nuvens")) return null;
		if (!Climates.TryGetValue(climate, out Layer[] layers))
		{
			GD.PushWarning($"SkyClouds: clima de nuvens desconhecido '{climate}'.");
			return null;
		}

		var shader = ResourceLoader.Load<Shader>(ShaderPath);
		if (shader == null) return null;

		var root = new Node3D { Name = "Nuvens" };
		for (int i = 0; i < layers.Length; i++)
		{
			Layer layer = layers[i];
			var material = new ShaderMaterial { Shader = shader };
			material.SetShaderParameter("cor", layer.Top);
			material.SetShaderParameter("sombra", layer.Thin);
			material.SetShaderParameter("tamanho", layer.Scale);
			material.SetShaderParameter("cobertura", layer.Cover);
			material.SetShaderParameter("maciez", layer.Soft);
			material.SetShaderParameter("opacidade", layer.Opacity);
			material.SetShaderParameter("vento", layer.Wind);
			material.SetShaderParameter("some_de", FadeFrom);
			material.SetShaderParameter("some_ate", FadeTo);
			material.SetShaderParameter("semente", new Vector2(3.7f * i, 1.9f * i));
			material.SetShaderParameter("encosto", 60f);
			material.SetShaderParameter("brilho", layer.Glow);

			root.AddChild(new MeshInstance3D
			{
				Name = $"Camada{i + 1}",
				Mesh = new PlaneMesh { Size = new Vector2(Side, Side), Material = material },
				Position = new Vector3(0, layer.Height, 0),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				ExtraCullMargin = 16384f,
			});
		}
		map.AddChild(root);
		return root;
	}
}
