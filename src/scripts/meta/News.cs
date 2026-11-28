using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// O que a tela inicial anuncia (Jhon, 27/11: "a tela inicial ao logar não
/// será mais salas, e sim uma área de banners e notícias"): os banners
/// grandes, que giram sozinhos, e as notícias curtas embaixo.
///
/// **Quem publica é o painel** (Jhon, 27/11: "essas notícias precisam estar
/// no backoffice para publicarmos: título, descrição, categoria"): a página
/// Notícias do painel grava a configuração <c>noticias</c> do servidor, que
/// chega ao jogo com o resto dos dados (<see cref="ServerData.News"/>, em
/// <c>/v1/gamedata</c>). Cada item tem categoria, título, descrição, imagem
/// (o id de um mapa: a capa dele) e se está publicado; o banner tem ainda o
/// texto do botão e para onde ele leva.
///
/// Sem nada publicado (ou sem servidor), valem as daqui, para a tela nunca
/// abrir vazia.
/// </summary>
public static class News
{
	/// <summary>Para onde o botão de um banner leva.</summary>
	public enum Target { Play, Shop, Inventory, Missions, None }

	public sealed record Banner(string Tag, string Title, string Body, string Image, string Cta, Target Go);

	public sealed record Story(string Tag, string Title, string Body, string Image, string Date);

	private static readonly Banner[] DefaultBanners =
	{
		new("Alpha 16", "Seis campos de caça",
			"Do galpão de contêineres às ruas de neon: seis mapas na rotação. Escolha o seu ao criar a sala.",
			"neon", "Jogar agora", Target.Play),
		new("Lobo", "Trinta segundos de fera",
			"Virar lobo não custa nada e mata em duas garradas. São 30 segundos de cada vez: faça valer.",
			"cidade", "Ver as melhorias de lobo", Target.Inventory),
		new("Corrida", "Fôlego no Shift",
			"O humano corre quatro segundos e precisa respirar. A corrida longa continua sendo do lobo.",
			"deserto", "Ver as salas", Target.Play),
		new("Loja", "Vitrine do dia",
			"Armas, visuais e fotos de perfil em promoção, trocando todo dia.",
			"estacao", "Abrir a loja", Target.Shop),
	};

	private static readonly Story[] DefaultStories =
	{
		new("Som", "Granada com som de bomba",
			"As dez explosões foram refeitas: de perto, de longe e de dentro de casa.", "obra", "27/11"),
		new("Mapas", "Cidade com calçadas novas",
			"As calçadas em cacos saíram e as seis capas dos mapas foram fotografadas de novo.", "cidade", "27/11"),
		new("Partida", "Espectador dentro do mapa",
			"Quem cai do precipício continua caindo, e a câmera de quem assiste não sai mais dos limites.", "armazem_estendido", "27/11"),
	};

	/// <summary>Quantos banners e quantas notícias a tela mostra, no máximo.</summary>
	public const int MaxBanners = 6, MaxStories = 3;

	/// <summary>Os banners publicados no painel; sem nenhum, os daqui.</summary>
	public static Banner[] Banners
	{
		get
		{
			Banner[] published = Published("banners").Take(MaxBanners).Select(n => new Banner(
				Text(n, "category"), Text(n, "title"), Text(n, "body"), Text(n, "image"), Text(n, "cta"),
				Text(n, "go") switch
				{
					"jogar" => Target.Play,
					"loja" => Target.Shop,
					"inventario" => Target.Inventory,
					"missoes" => Target.Missions,
					_ => Target.None
				})).ToArray();
			return published.Length > 0 ? published : DefaultBanners;
		}
	}

	/// <summary>As notícias publicadas no painel, as mais novas primeiro
	/// (a ordem do painel); sem nenhuma, as daqui.</summary>
	public static Story[] Stories
	{
		get
		{
			Story[] published = Published("noticias").Take(MaxStories).Select(n => new Story(
				Text(n, "category"), Text(n, "title"), Text(n, "body"), Text(n, "image"), Text(n, "date"))).ToArray();
			return published.Length > 0 ? published : DefaultStories;
		}
	}

	/// <summary>Os itens publicados de uma das listas: ligados e com título.</summary>
	private static IEnumerable<JsonNode> Published(string list) =>
		(ServerData.News?[list] as JsonArray ?? new JsonArray())
			.Where(n => n != null && (n["on"]?.GetValue<bool>() ?? true) && Text(n, "title").Length > 0);

	private static string Text(JsonNode node, string field)
	{
		// O painel grava texto; um valor de outro tipo (arquivo editado à mão) vira vazio.
		try { return node[field]?.GetValue<string>()?.Trim() ?? ""; }
		catch (Exception e) when (e is InvalidOperationException or FormatException) { return ""; }
	}

	/// <summary>O arquivo da imagem de um item: a capa do mapa com esse id
	/// (sem id, ou id que não existe mais: a do primeiro mapa).</summary>
	public static string Picture(string image) =>
		!string.IsNullOrEmpty(image) && image.StartsWith("res://") ? image : Maps.Get(image).Picture;
}
