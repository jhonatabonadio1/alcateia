using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Alcateia.Dominio.Desafios;
using Alcateia.Dominio.Loja;

namespace Alcateia.Infra.Semente;

/// <summary>
/// O que veio com o jogo: catálogo, mapas e desafios, tirados do código do
/// jogo por <c>tools/servidor/exportar_dados.py</c> e embutidos aqui
/// (<c>jogo.json</c>). O servidor põe no banco só o que ainda não existe;
/// depois disso quem manda é o painel.
/// </summary>
public static class SeedData
{
	public sealed record Seed(IReadOnlyList<CatalogEntry> Catalog, IReadOnlyList<GameMap> Maps, IReadOnlyList<Challenge> Challenges);

	public static Seed Load()
	{
		using Stream stream = typeof(SeedData).Assembly.GetManifestResourceStream("Alcateia.Infra.Semente.jogo.json")
			?? throw new InvalidOperationException("a semente (jogo.json) não foi embutida no servidor");
		JsonNode root = JsonNode.Parse(stream);

		var catalog = root["catalog"].AsArray().Select(i => new CatalogEntry(
			Text(i, "id"), Text(i, "name"), Text(i, "type"), Text(i, "kind"), Int(i, "rarity"), Int(i, "minRank"), Text(i, "currency"),
			i["prices"].AsArray().Select(p => p.GetValue<int>()).ToArray(), Flag(i, "starter"), true, Flag(i, "hot"), Int(i, "team"),
			Text(i, "image"), Int(i, "sort")) { Consumable = Flag(i, "consumable") }).ToList();

		var maps = root["maps"].AsArray().Select(m => new GameMap(
			Text(m, "id"), Text(m, "name"), Text(m, "size"), Text(m, "description"),
			m["modes"].AsArray().Select(x => x.GetValue<string>()).ToArray(), Flag(m, "enabled"), Text(m, "image"), Int(m, "sort"))).ToList();

		var challenges = root["challenges"].AsArray().Select(c => new Challenge(
			Text(c, "id"), Text(c, "period"), Text(c, "text"), Text(c, "metric"), Int(c, "target"), Int(c, "presas"), Int(c, "xp"), 0,
			null, 0, c["title"]?.GetValue<string>(), true, null, null, Int(c, "sort"))).ToList();

		return new Seed(catalog, maps, challenges);
	}

	private static string Text(JsonNode node, string field) => node[field]?.GetValue<string>() ?? "";
	private static int Int(JsonNode node, string field) => node[field]?.GetValue<int>() ?? 0;
	private static bool Flag(JsonNode node, string field) => node[field]?.GetValue<bool>() ?? false;
}
