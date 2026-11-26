using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// A ponte entre a conta no servidor e o que o jogo mostra.
///
/// Carteira, XP, itens, missões e títulos são do servidor (decisão 0015):
/// o jogo não soma Presas, não entrega item e não anda missão por conta
/// própria. Ele pede (comprar, descartar, resgatar, trocar o nome), o
/// servidor confere e decide, e este arquivo traz de volta o que a conta
/// tem agora e põe em <see cref="Profile"/> — que as telas leem.
///
/// Toda ação devolve <c>null</c> se deu certo, ou a frase do motivo.
/// </summary>
public static class AccountSync
{
	/// <summary>O item da loja que paga uma troca de nome.</summary>
	public const string RenameItem = "troca_nome";

	private static Task<bool> _refreshing;

	/// <summary>Patentes alcançadas que ainda não foram mostradas (a tela
	/// que estiver aberta mostra e esvazia).</summary>
	public static readonly List<int> PendingPromotions = new();

	/// <summary>Chegou uma patente nova para mostrar.</summary>
	public static event Action Promoted;

	/// <summary>Entrou na conta: o nome vale na hora; carteira, itens e
	/// missões vêm juntos em seguida. (Juntos de propósito: aplicar a
	/// carteira antes dos itens faria o jogo achar, por um instante, que a
	/// conta não tem nada — e limpar os conjuntos.)</summary>
	public static void SessionStarted(JsonObject profile)
	{
		Profile.UseAccountName(profile?["name"]?.GetValue<string>());
		_ = Refresh();
	}

	public static void SessionEnded()
	{
		PendingPromotions.Clear();
		Celebrated.Clear();
		Profile.ClearAccount();
	}

	/// <summary>Com um login guardado, confere se ainda vale e carrega a
	/// conta. É o que o jogo faz ao abrir.</summary>
	public static async Task<bool> Resume()
	{
		if (!Backend.LoggedIn) return false;
		Backend.Reply profile = await Backend.Resume();
		if (!profile.Ok) return false;
		return await Refresh();
	}

	/// <summary>Traz do servidor o que a conta tem agora. Pedidos ao mesmo
	/// tempo viram um só.</summary>
	public static Task<bool> Refresh() => _refreshing ??= Load();

	private static async Task<bool> Load()
	{
		try
		{
			if (!Backend.LoggedIn) return false;

			// Os dados do jogo vêm juntos: o preço que a loja mostra tem de
			// ser o que o servidor vai cobrar.
			Task data = ServerData.Refresh();
			Task<Backend.Reply> inventoryTask = Backend.Inventory(), challengesTask = Backend.Challenges();
			await data;
			Backend.Reply inventory = await inventoryTask, challenges = await challengesTask;
			if (!Backend.LoggedIn) return false;                     // saiu no meio
			if (inventory.Status == 401) { Backend.SessionExpired(); return false; }
			if (!inventory.Ok) return false;

			if (challenges.Ok)
			{
				Profile.Missions.Apply(challenges.Body);
				Profile.ApplyTitles((challenges.Body["titles"] as JsonArray)?.Select(t => t.GetValue<string>()));
			}

			var items = new List<OwnedItem>();
			foreach (JsonNode i in inventory.Body["items"] as JsonArray ?? new JsonArray())
				items.Add(new OwnedItem
				{
					Id = i["id"]?.GetValue<string>(),
					AcquiredUtc = Utc(i["acquiredAt"]) ?? DateTime.UtcNow,
					ExpiresUtc = Utc(i["expiresAt"]),
					Uses = i["uses"]?.GetValue<int>() ?? 0
				});

			Take(Profile.ApplyAccount(Long(inventory.Body, "xp"), Long(inventory.Body, "presas"), Long(inventory.Body, "sangue"), items));
			return true;
		}
		finally
		{
			_refreshing = null;
		}
	}

	/// <summary>Depois de uma partida online: o servidor paga quando o
	/// servidor de partida entrega o resultado, o que pode levar uns
	/// segundos. Pergunta de novo até o XP mudar.</summary>
	public static async Task RefreshAfterMatch()
	{
		long before = Profile.Xp;
		for (int attempt = 0; attempt < 6 && Backend.LoggedIn; attempt++)
		{
			await Task.Delay(attempt == 0 ? 600 : 2000);
			if (await Refresh() && Profile.Xp != before) return;
		}
	}

	// ------------------------------------------------------------------ ações

	/// <summary>Compra na loja. O servidor confere preço, desconto, patente e saldo.</summary>
	public static async Task<string> Buy(string itemId, Duration duration)
	{
		if (!Backend.LoggedIn) return NeedsAccount;
		// Um número por clique: se a rede mandar o pedido de novo, o servidor
		// devolve a mesma compra em vez de cobrar duas vezes (26/11).
		Backend.Reply reply = await Backend.Buy(itemId, duration, Guid.NewGuid().ToString("N"));
		if (!reply.Ok) return reply.Error;
		await Refresh();
		return null;
	}

	/// <summary>Joga um item fora (o que custou não volta).</summary>
	public static async Task<string> Discard(string itemId)
	{
		if (!Backend.LoggedIn) return NeedsAccount;
		Backend.Reply reply = await Backend.Discard(itemId);
		if (!reply.Ok) return reply.Error;
		await Refresh();
		return null;
	}

	/// <summary>Resgata o prêmio de uma missão completa.</summary>
	public static async Task<string> Claim(string missionId)
	{
		if (!Backend.LoggedIn) return NeedsAccount;
		Backend.Reply reply = await Backend.ClaimChallenge(missionId);
		if (!reply.Ok) return reply.Error;
		await Refresh();
		return null;
	}

	/// <summary>Troca o nome da conta, gastando um item Troca de nome.</summary>
	public static async Task<string> Rename(string name)
	{
		if (!Backend.LoggedIn) return NeedsAccount;
		Backend.Reply reply = await Backend.Rename(name);
		if (!reply.Ok) return reply.Error;
		Backend.Renamed(reply.Body?["name"]?.GetValue<string>() ?? name);
		Profile.UseAccountName(Backend.AccountName);
		await Refresh();
		return null;
	}

	public const string NeedsAccount = "Entre na sua conta primeiro";

	// ------------------------------------------------------------------ miúdos

	private static readonly HashSet<int> Celebrated = new();

	/// <summary>Estas promoções já foram mostradas (a tela de resultado as
	/// prevê e comemora na hora): quando o servidor confirmar, não se
	/// mostram de novo.</summary>
	public static void AlreadyShown(IEnumerable<int> ranks)
	{
		foreach (int rank in ranks) Celebrated.Add(rank);
	}

	private static void Take(List<int> promotions)
	{
		promotions.RemoveAll(rank => Celebrated.Remove(rank));
		if (promotions.Count == 0) return;
		PendingPromotions.AddRange(promotions);
		Promoted?.Invoke();
	}

	private static long Long(JsonObject body, string field) => body?[field]?.GetValue<long>() ?? 0;

	private static DateTime? Utc(JsonNode node) =>
		DateTime.TryParse(node?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime at) ? at : null;
}
