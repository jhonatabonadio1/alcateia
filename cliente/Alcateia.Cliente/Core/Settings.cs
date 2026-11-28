using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Alcateia.Cliente.Core;

/// <summary>Um acesso guardado neste computador: a conta, o servidor e o
/// login dela (o token, nunca a senha) — como o jogo guarda os dele.</summary>
public sealed record SavedAccount(string Url, long Id, string Name, string Tag, string Token);

/// <summary>
/// Os ajustes do cliente e as contas salvas, num JSON na pasta de dados
/// (<see cref="AppInfo.DataDir"/>). Lido uma vez ao abrir; cada mudança grava.
/// </summary>
public sealed class Settings
{
	/// <summary>Quantas contas ficam salvas (o mesmo teto do jogo).</summary>
	public const int MaxAccounts = 3;

	/// <summary>A pasta escolhida para cada jogo (pelo <see cref="GameEntry.Id"/>);
	/// jogo fora daqui vai na padrão (<see cref="AppInfo.DefaultGameDir"/>).</summary>
	private readonly Dictionary<string, string> _gameDirs = new();

	public string GameDir(GameEntry game) =>
		_gameDirs.TryGetValue(game.Id, out string dir) && !string.IsNullOrWhiteSpace(dir) ? dir : AppInfo.DefaultGameDir(game);

	public void SetGameDir(GameEntry game, string dir)
	{
		_gameDirs[game.Id] = dir;
		Save();
	}

	/// <summary>O idioma do launcher (as colunas de Assets/i18n/launcher.csv).
	/// Inglês por padrão (Jhon, 27/11: "o padrão do jogo e do launcher será em inglês").</summary>
	public string Language = Tr.Default;

	/// <summary>Versão nova do jogo: baixa sozinho, sem esperar o clique.</summary>
	public bool AutoUpdate = true;

	/// <summary>Fechar a janela deixa o cliente na bandeja (os amigos seguem
	/// vendo você, a atualização segue baixando).</summary>
	public bool CloseToTray = true;

	/// <summary>Ao abrir o jogo, a janela do cliente sai da frente.</summary>
	public bool MinimizeOnPlay = true;

	/// <summary>As contas salvas, a mais recente primeiro.</summary>
	public readonly List<SavedAccount> Accounts = new();

	/// <summary>Entra sozinho na conta mais recente ao abrir (o "lembrar").</summary>
	public bool AutoLogin = true;

	private static string FilePath => Path.Combine(AppInfo.DataDir, "settings.json");

	public static Settings Load()
	{
		var s = new Settings();
		try
		{
			if (!File.Exists(FilePath) || JsonNode.Parse(File.ReadAllText(FilePath)) is not JsonObject root) return s;
			if (root["games"] is JsonObject games)
				foreach ((string id, JsonNode node) in games)
				{
					string dir = node?["dir"]?.GetValue<string>();
					if (!string.IsNullOrWhiteSpace(dir)) s._gameDirs[id] = dir;
				}
			// Antes de 27/11 o cliente era só do WolfLeague: uma pasta só.
			string old = root["gameDir"]?.GetValue<string>();
			if (!string.IsNullOrWhiteSpace(old) && !s._gameDirs.ContainsKey(Games.WolfLeague.Id)) s._gameDirs[Games.WolfLeague.Id] = old;
			foreach (GameEntry game in Games.All)
				if (s._gameDirs.TryGetValue(game.Id, out string dir)) s._gameDirs[game.Id] = FromOldDefault(game, dir);
			s.Language = root["language"]?.GetValue<string>() ?? s.Language;
			s.AutoUpdate = root["autoUpdate"]?.GetValue<bool>() ?? s.AutoUpdate;
			s.CloseToTray = root["closeToTray"]?.GetValue<bool>() ?? s.CloseToTray;
			s.MinimizeOnPlay = root["minimizeOnPlay"]?.GetValue<bool>() ?? s.MinimizeOnPlay;
			s.AutoLogin = root["autoLogin"]?.GetValue<bool>() ?? s.AutoLogin;
			foreach (JsonNode a in root["accounts"] as JsonArray ?? new JsonArray())
			{
				string token = a?["token"]?.GetValue<string>() ?? "";
				if (token.Length == 0) continue;
				s.Accounts.Add(new SavedAccount(a["url"]?.GetValue<string>() ?? AppInfo.ServerUrl, a["id"]?.GetValue<long>() ?? 0,
					a["name"]?.GetValue<string>() ?? "", a["tag"]?.GetValue<string>() ?? "", token));
			}
		}
		catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
		{
			// Arquivo estragado: começa do padrão em vez de não abrir.
		}
		return s;
	}

	/// <summary>
	/// A pasta guardada, trocando uma padrão de antes (".../Jogo" do cliente
	/// 0.4) pela de agora (".../game/WolfLeague"). Estão no mesmo disco, então
	/// mudar é só renomear: o jogo baixado vai junto, sem baixar de novo. A
	/// pasta de dados pode ter acabado de mudar de nome (<see cref="AppInfo.DataDir"/>):
	/// o jogo está numa das padrões de antes, a que existir. Pasta escolhida
	/// pelo jogador fica como está. Sem conseguir renomear (o jogo aberto, a
	/// nova já existindo), fica a de antes, e tenta de novo na próxima vez.
	/// </summary>
	private static string FromOldDefault(GameEntry game, string dir)
	{
		string[] olds = AppInfo.OldDefaultGameDirs(game);
		if (!Array.Exists(olds, o => SamePath(dir, o))) return dir;
		string now = AppInfo.DefaultGameDir(game);
		string found = Array.Find(olds, Directory.Exists);
		if (found == null) return now;
		try
		{
			if (Directory.Exists(now)) return found;
			Directory.CreateDirectory(Path.GetDirectoryName(now));
			Directory.Move(found, now);
			return now;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return found;
		}
	}

	private static bool SamePath(string a, string b) =>
		string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
			AppInfo.IsMac ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

	public void Save()
	{
		var accounts = new JsonArray();
		foreach (SavedAccount a in Accounts)
			accounts.Add((JsonNode)new JsonObject { ["url"] = a.Url, ["id"] = a.Id, ["name"] = a.Name, ["tag"] = a.Tag, ["token"] = a.Token });
		var games = new JsonObject();
		foreach ((string id, string dir) in _gameDirs) games[id] = new JsonObject { ["dir"] = dir };
		var root = new JsonObject
		{
			["games"] = games, ["language"] = Language, ["autoUpdate"] = AutoUpdate, ["closeToTray"] = CloseToTray,
			["minimizeOnPlay"] = MinimizeOnPlay, ["autoLogin"] = AutoLogin, ["accounts"] = accounts
		};
		try
		{
			string temp = FilePath + ".tmp";
			File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
			File.Move(temp, FilePath, true);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
		}
	}

	/// <summary>Guarda (ou atualiza) um acesso, pondo-o na frente.</summary>
	public void Remember(SavedAccount account)
	{
		Forget(account.Url, account.Id, save: false);
		Accounts.Insert(0, account);
		if (Accounts.Count > MaxAccounts) Accounts.RemoveRange(MaxAccounts, Accounts.Count - MaxAccounts);
		Save();
	}

	public void Forget(string url, long id, bool save = true)
	{
		Accounts.RemoveAll(a => a.Id == id && a.Url == url);
		if (save) Save();
	}
}
