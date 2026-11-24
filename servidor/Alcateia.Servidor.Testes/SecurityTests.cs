using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Seguranca;
using Alcateia.Infra.Seguranca;

namespace Alcateia.Servidor.Testes;

/// <summary>O escudo: blacklist de IP, endereço real, varredura, injeção,
/// força bruta, freio — e as regras puras por trás (faixas, TOTP, cifra).</summary>
public static class SecurityTests
{
	private static async Task<int> Get(World w, string path, params (string Name, string Value)[] headers)
	{
		var request = new HttpRequestMessage(HttpMethod.Get, w.Url + path);
		foreach (var (name, value) in headers) request.Headers.TryAddWithoutValidation(name, value);
		return (int)(await w.Http.SendAsync(request)).StatusCode;
	}

	private static async Task<string[]> Kinds(World w)
	{
		await w.Services.Security.Flush();
		return (await w.Services.Security.Events(new EventFilter(Limit: 200))).Select(e => e.Kind).ToArray();
	}

	private static Task<(int Status, JsonObject Body)> Login(World w, string name, string password) =>
		w.Server().Call(HttpMethod.Post, "/v1/sessions", new JsonObject { ["name"] = name, ["password"] = password });

	public static readonly (string, Func<Task>)[] All =
	{
		("segurança (regras): faixa de IP, código TOTP, cifra, padrões de ataque e patente", () =>
		{
			IpRange net = IpRange.Parse("203.0.113.0/24");
			Check.True(net.Contains(IPAddress.Parse("203.0.113.77")), "o endereço está na faixa");
			Check.True(!net.Contains(IPAddress.Parse("203.0.114.1")), "o vizinho não está");
			Check.True(net.Contains(IPAddress.Parse("::ffff:203.0.113.9")), "IPv4 embrulhado em IPv6 é o mesmo endereço");
			Check.Equal("203.0.113.0/24", IpRange.Parse("203.0.113.55/24").Text, "a faixa é guardada pela rede");
			Check.True(IpRange.Parse("2001:db8::/32").Contains(IPAddress.Parse("2001:db8:1::5")), "faixa IPv6");
			Check.True(!IpRange.TryParse("0.0.0.0/0", out _), "bloquear a internet inteira por engano não passa");
			Check.True(!IpRange.TryParse("casa", out _) && !IpRange.TryParse("1.2.3.4/40", out _), "texto que não é endereço");

			// O vetor de teste da RFC 6238 (segredo "12345678901234567890", instante 59 s).
			const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
			DateTime at = DateTime.UnixEpoch.AddSeconds(59);
			Check.Equal("287082", Totp.Code(secret, at), "o código da RFC 6238");
			Check.True(Totp.Verify(secret, "287082", at.AddSeconds(25)), "vale no passo seguinte (relógio adiantado)");
			Check.True(!Totp.Verify(secret, "287082", at.AddMinutes(5)), "cinco minutos depois não vale");
			Check.True(!Totp.Verify(secret, "000000", at) && !Totp.Verify(secret, "", at), "código errado ou vazio");
			Check.Equal(32, Totp.NewSecret().Length, "segredo novo de 160 bits");

			var cipher = new AesCipher("uma-chave");
			string locked = cipher.Encrypt(secret);
			Check.True(!locked.Contains(secret), "o segredo não fica legível");
			Check.Equal(secret, cipher.Decrypt(locked), "com a chave, abre");
			Check.True(locked != cipher.Encrypt(secret), "cifrar de novo dá outro texto");
			bool opened = true;
			try { new AesCipher("outra-chave").Decrypt(locked); }
			catch (System.Security.Cryptography.CryptographicException) { opened = false; }
			Check.True(!opened, "com outra chave, não abre");

			Check.True(ThreatRules.LooksLikeScanner("/.env") && ThreatRules.LooksLikeScanner("/wp-admin/setup.php") && ThreatRules.LooksLikeScanner("/.git/config"), "rotas de scanner");
			Check.True(!ThreatRules.LooksLikeScanner("/v1/rooms") && !ThreatRules.LooksLikeScanner("/health"), "as rotas do jogo não são");
			Check.True(ThreatRules.LooksLikeInjection("/v1/ranking?limit=1' OR 1=1") && ThreatRules.LooksLikeInjection("/x?q=1 UNION SELECT pass_hash")
				&& ThreatRules.LooksLikeInjection("/v1/../../etc/passwd") && ThreatRules.LooksLikeInjection("/?a=${jndi:ldap://x}"), "padrões de injeção");
			Check.True(!ThreatRules.LooksLikeInjection("/v1/ranking?limit=50"), "pedido normal não é");

			// níveis (rank 0 = nível 1); os mesmos degraus de scripts/meta/Ranks.cs
			Check.Equal(0L, RankRule.XpFor(0), "o nível 1 começa em zero");
			Check.Equal(100L, RankRule.XpFor(1), "o nível 2, igual ao do jogo");
			Check.Equal(210L, RankRule.XpFor(2), "o nível 3, igual ao do jogo");
			Check.Equal(49050L, RankRule.XpFor(90), "fim da rampa: daí em diante, 1.000 por nível");
			Check.Equal(58050L, RankRule.XpFor(99), "o nível 100, o primeiro bronze");
			Check.Equal(1, RankRule.RankFor(209), "quase no nível 3");
			Check.Equal(2, RankRule.RankFor(210), "no nível 3");
			Check.Equal(99, RankRule.RankFor(58050), "nível 100");
			Check.Equal(RankRule.Count - 1, RankRule.RankFor(long.MaxValue / 4), "o teto é o nível 3000");
			Check.Equal(1, RankRule.Family(99), "o nível 100 é bronze");
			Check.Equal(100L, RankRule.PromotionPresas(RankRule.XpFor(8), RankRule.XpFor(9)), "o nível 10 é borda nova: 100 Presas");

			// rage: tempo em chamas que os abates não explicam vira evento de segurança
			var fogo = new Alcateia.Dominio.Partidas.MatchReport("k", "s", 0, 0, new[]
			{
				// três abates: até 26 s em chamas (Rage.MaxFireSeconds, regras de 23/11)
				new Alcateia.Dominio.Partidas.ReportedLine(1, 3, 0, 0, 0, 0) { FireSeconds = 26 },
				new Alcateia.Dominio.Partidas.ReportedLine(2, 0, 0, 0, 0, 0) { FireSeconds = 200 }
			});
			var estranho = Alcateia.Dominio.Partidas.Plausibility.Flags(fogo, 10);
			Check.True(estranho.All(f => f.AccountId != 1), "26 s em chamas com três abates é possível");
			Check.True(estranho.Any(f => f.AccountId == 2 && f.What.Contains("em chamas")), "200 s em chamas sem abate é denunciado");

			// fotos de perfil: grátis sempre; da loja, só com o item
			Check.True(PhotoRule.Allowed("lobo_cinza", Array.Empty<string>()), "o lobo cinzento é grátis");
			Check.True(!PhotoRule.Allowed("lobo_fogo", Array.Empty<string>()), "a da loja sem o item, não");
			Check.True(PhotoRule.Allowed("lobo_fogo", new[] { "foto_lobo_fogo" }), "com o item, sim");
			Check.True(!PhotoRule.Allowed("inventada", new[] { "foto_inventada" }), "foto que não existe, nunca");
			return Task.CompletedTask;
		}),

		("segurança: endereço na blacklist recebe 403 em tudo; fora dela, volta; bloqueio com prazo vence", async () =>
		{
			await using World w = await World.Start();
			Check.Equal(200, await Get(w, "/health"), "antes, responde");

			IpBlock block = await w.Services.Security.Block("127.0.0.1", "teste", 0, "admin", "chefe");
			Check.Equal(403, await Get(w, "/health"), "bloqueado, nem a saúde responde");
			Check.Equal(403, (await Login(w, "Ninguem", "senha-qualquer")).Status, "nem o login");

			await w.Services.Security.Unblock(block.Id);
			Check.Equal(200, await Get(w, "/health"), "desbloqueado, volta");

			await w.Services.Security.Block("127.0.0.0/8", "faixa inteira", 0, "admin", "chefe");
			Check.Equal(403, await Get(w, "/health"), "faixa bloqueada pega o endereço");
			await w.Db.Exec("update ip_blocks set until = now() - interval '1 minute'");
			await w.Services.Security.Reload();
			Check.Equal(200, await Get(w, "/health"), "bloqueio vencido sai sozinho");
			Check.True((await Kinds(w)).Contains(EventKind.Blocked), "o pedido recusado fica registrado");
		}),

		("segurança: o endereço de verdade só vem de proxy de confiança", async () =>
		{
			await using (World alone = await World.Start())
			{
				await alone.Services.Security.Block("198.51.100.7", "atacante", 0, "admin", "chefe");
				Check.Equal(200, await Get(alone, "/health", ("X-Forwarded-For", "198.51.100.7")),
					"sem proxy de confiança o cabeçalho é ignorado (senão qualquer um se passava por outro)");
				Check.Equal(200, await Get(alone, "/health", ("Fly-Client-IP", "198.51.100.7")), "o do Fly também");
			}

			await using World w = await World.Start(c => c.TrustedProxies.Add(IpRange.Parse("127.0.0.0/8", true)));
			await w.Services.Security.Block("198.51.100.7", "atacante", 0, "admin", "chefe");
			Check.Equal(200, await Get(w, "/health", ("X-Forwarded-For", "203.0.113.5")), "cliente limpo atrás do proxy passa");
			Check.Equal(403, await Get(w, "/health", ("X-Forwarded-For", "198.51.100.7")), "o bloqueado atrás do proxy não");
			Check.Equal(403, await Get(w, "/health", ("X-Forwarded-For", "203.0.113.5, 198.51.100.7")),
				"vale o último endereço antes do proxy: o que vem antes o cliente pode ter inventado");
			Check.Equal(403, await Get(w, "/health", ("CF-Connecting-IP", "198.51.100.7")), "o cabeçalho do Cloudflare também vale");
			Check.Equal(403, await Get(w, "/health", ("Fly-Client-IP", "198.51.100.7"), ("X-Forwarded-For", "198.51.100.7, 66.241.124.10")),
				"no Fly vale o Fly-Client-IP (o último do X-Forwarded-For lá é o IP do próprio app)");
		}),

		("segurança: varredura e injeção são recusadas e registradas; a terceira bloqueia o endereço", async () =>
		{
			await using World w = await World.Start(c => c.Security.PoliceInternal = true);
			Check.Equal(404, await Get(w, "/.env"), "arquivo de segredos: não existe");
			Check.Equal(400, await Get(w, "/v1/ranking?limit=1%27%20OR%201=1"), "injeção no endereço");
			Check.Equal(200, await Get(w, "/health"), "duas ocorrências ainda não bloqueiam");
			Check.Equal(404, await Get(w, "/wp-admin/install.php"), "a terceira");

			for (int i = 0; i < 100 && w.Services.Security.Blocks.Count == 0; i++) await Task.Delay(20);
			Check.Equal(403, await Get(w, "/health"), "bloqueado sozinho");
			Check.Equal("auto", w.Services.Security.Blocks[0].Source, "bloqueio automático");
			Check.True(w.Services.Security.Blocks[0].Until != null, "e com prazo");

			string[] kinds = await Kinds(w);
			Check.True(kinds.Contains(EventKind.Scanner) && kinds.Contains(EventKind.Injection) && kinds.Contains(EventKind.AutoBlock),
				"varredura, injeção e bloqueio no registro: " + string.Join(", ", kinds));
		}),

		("segurança: o painel não existe na porta do jogo", async () =>
		{
			await using World w = await World.Start();
			Check.Equal(404, await Get(w, "/admin/v1/me"), "a rota do painel na porta do jogo");
			Check.Equal(404, await Get(w, "/metrics"), "as métricas também não");
			var (status, _) = await w.Server().Call(HttpMethod.Post, "/admin/v1/login",
				new JsonObject { ["name"] = World.AdminName, ["password"] = World.AdminPassword });
			Check.Equal(404, status, "nem com a senha certa");
			Check.True((await Kinds(w)).Contains(EventKind.Scanner), "procurar o painel ali é varredura");
		}),

		("segurança: login errado em série bloqueia o endereço; login certo depois de erros fica marcado", async () =>
		{
			await using World w = await World.Start(c => { c.Security.PoliceInternal = true; c.Security.LoginFailuresToBlock = 6; });
			await using Player p = await w.NewPlayer("Alvo");

			for (int i = 0; i < 3; i++) Check.Equal(401, (await Login(w, "Alvo", "senha-errada-" + i)).Status, "senha errada");
			Check.Equal(200, (await Login(w, "Alvo", "senha-boa-123")).Status, "a certa entra");
			string[] kinds = await Kinds(w);
			Check.True(kinds.Contains(EventKind.LoginFailed) && kinds.Contains(EventKind.LoginOk), "erros e acerto no registro");
			Check.True(kinds.Contains(EventKind.LoginAfterFailures), "acerto depois de três erros é um evento à parte");

			for (int i = 0; i < 6; i++) await Login(w, "Alvo", "outra-errada-" + i);
			for (int i = 0; i < 100 && w.Services.Security.Blocks.Count == 0; i++) await Task.Delay(20);
			Check.Equal(403, (await Login(w, "Alvo", "senha-boa-123")).Status, "depois de seis erros seguidos, nem a senha certa entra daquele endereço");
			Check.True((await Kinds(w)).Contains(EventKind.BruteForce), "força bruta no registro");

			var events = await w.Services.Security.Events(new EventFilter(KindPrefix: "login."));
			Check.True(events.All(e => !e.Detail.Contains("senha") && !e.Actor.Contains("errada")), "a senha tentada nunca vai para o registro");
		}),

		("segurança: a mesma senha em várias contas (spraying) bloqueia", async () =>
		{
			await using World w = await World.Start(c => { c.Security.PoliceInternal = true; c.Security.SprayedNamesToBlock = 4; });
			for (int i = 0; i < 4; i++) await Login(w, "Conta" + i, "Senha@2026");
			for (int i = 0; i < 100 && w.Services.Security.Blocks.Count == 0; i++) await Task.Delay(20);
			Check.Equal(403, await Get(w, "/health"), "quatro nomes diferentes do mesmo endereço: bloqueado");
			Check.True((await Kinds(w)).Contains(EventKind.Spraying), "no registro como uma senha em várias contas");
		}),

		("segurança: freio de pedidos por minuto, por endereço — e o servidor de partida não entra nele", async () =>
		{
			await using World w = await World.Start(c => { c.Security.PoliceInternal = true; c.Security.RequestsPerMinute = 15; c.Security.AutoBlock = false; });
			for (int i = 0; i < 15; i++) Check.Equal(200, await Get(w, "/health"), "dentro do limite");
			Check.Equal(429, await Get(w, "/health"), "passou do limite");

			var (status, _) = await w.Server().Call(HttpMethod.Post, "/v1/fleet/heartbeat", MatchTests.Beat("gs-1"), w.Config.FleetKey);
			Check.Equal(200, status, "a batida da frota passa mesmo assim");
			Check.True((await Kinds(w)).Contains(EventKind.RateLimited), "o freio fica registrado (uma vez por minuto)");
		}),

		("segurança: respostas saem com os cabeçalhos de proteção e sem dizer que servidor é", async () =>
		{
			await using World w = await World.Start();
			HttpResponseMessage r = await w.Http.GetAsync(w.Url + "/health");
			Check.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").First(), "nosniff");
			Check.Equal("DENY", r.Headers.GetValues("X-Frame-Options").First(), "sem moldura");
			Check.True(!r.Headers.Contains("Server"), "sem o cabeçalho Server");

			var (status, body) = await w.Server().Call(HttpMethod.Post, "/v1/fleet/heartbeat", MatchTests.Beat("gs-1"), "chave-errada");
			Check.Equal(401, status, "chave da frota errada");
			Check.True((await Kinds(w)).Contains(EventKind.FleetKey), "e fica registrado");
		})
	};
}
