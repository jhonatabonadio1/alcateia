using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// O que o painel administrativo muda no servidor chega ao jogo
/// (<see cref="ServerData"/>): mapa fora da rotação, preço novo, promoção
/// com hora marcada, aviso do saguão. Sem servidor, vale o que vem com o jogo.
/// </summary>
public sealed class ServerDataTests : RegressionTest
{
	public override string Name => "DadosDoServidor";
	public override string Description => "mapa desligado, preço, promoção e aviso do painel chegam ao jogo; sem servidor vale o do jogo";

	public override Task Run(RegressionBench b)
	{
		ServerData.Reset();
		// Dois itens à venda quaisquer: o teste não depende de qual arma está no catálogo.
		CatalogItem ak = Catalog.All.First(i => i.ForSale), other = Catalog.All.Last(i => i.ForSale);
		string first = Maps.All[0].Id, second = Maps.All.Count > 1 ? Maps.All[1].Id : "outro_mapa";

		b.Check(ServerData.MapOn(first) && ServerData.MapOn("mapa_que_nao_existe"), "sem servidor, todo mapa está em rotação");
		int builtIn = ak.Prices[(int)Duration.Day7], rarity = ak.Rarity;
		b.Check(Catalog.Price(ak, Duration.Day7) == builtIn && Catalog.Daily().Count == 0, "sem servidor, o preço é o do jogo e não há vitrine do dia");
		b.Check(ServerData.FirstMapOn() == first, "o primeiro mapa é o primeiro do jogo");

		var data = (JsonObject)JsonNode.Parse($$"""
		{
			"items": [ { "id": "{{ak.Id}}", "prices": [100, 500, 1500, 3300, 8000], "rarity": 4, "minRank": 7, "hot": true }, { "id": "{{other.Id}}", "prices": [-1, -1, -1, -1, 1500] } ],
			"maps": [ { "id": "{{first}}", "enabled": false }, { "id": "{{second}}", "enabled": true } ],
			"promotions": [
				{ "itemId": "{{ak.Id}}", "percent": 50, "startsAt": "2020-01-01T00:00:00Z", "endsAt": "2999-01-01T00:00:00Z" },
				{ "itemId": "{{ak.Id}}", "percent": 80, "startsAt": "2990-01-01T00:00:00Z", "endsAt": "2999-01-01T00:00:00Z" }
			],
			"dailyItems": [ "{{ak.Id}}" ],
			"settings": { "aviso": { "text": "Mapa novo no ar" }, "manutencao": { "on": false }, "bonus": { "xp": 2, "presas": 1 }, "versao_minima": { "version": "0.4.0" } }
		}
		""");
		ServerData.Apply(data);

		b.Check(!ServerData.MapOn(first), "o mapa que o painel desligou sai da rotação");
		b.Check(ServerData.MapOn(second), "os outros continuam");
		b.Check(ServerData.FirstMapOn() == (Maps.All.Count > 1 ? second : first), "sala nova cai no primeiro mapa ligado (com um mapa só, nele mesmo)");
		b.Check(ServerData.Discount(ak.Id) == 50, "vale a promoção em curso, não a que ainda vai começar");
		b.Check(Catalog.Price(ak, Duration.Day7) == 250, "o preço do jogo passa a ser o do servidor, com o desconto: 500 com 50%");
		b.Check(ak.Rarity == 4 && ak.MinRank == 7 && ak.Hot, "raridade, patente mínima e destaque também são os do servidor");
		b.Check(Catalog.IsDaily(ak) && Catalog.Daily().Count == 1, "o item do dia é o que o servidor sorteou");
		b.Check(Catalog.Price(other, Duration.Day1) == -1, "duração que não se vende continua não se vendendo");
		b.Check(ServerData.ItemOn(ak.Id) && !ServerData.ItemOn("item_que_o_servidor_nao_mandou"), "item que o servidor não manda está desligado");
		b.Check(ServerData.Notice == "Mapa novo no ar", "o aviso do saguão");
		b.Check(ServerData.XpBonus == 2 && ServerData.PresasBonus == 1, "o bônus de XP do evento");
		b.Check(ServerData.MinVersion == "0.4.0", "a versão mínima");

		ServerData.Reset();
		b.Check(ServerData.MapOn(first) && ServerData.Notice == "", "esquecido, volta ao que vem com o jogo");
		b.Check(Catalog.Price(ak, Duration.Day7) == builtIn && ak.Rarity == rarity && !Catalog.IsDaily(ak), "e o catálogo também: preço e raridade do jogo, sem vitrine");
		return Task.CompletedTask;
	}
}
