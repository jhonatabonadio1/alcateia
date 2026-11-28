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

	public string GameDir = AppInfo.DefaultGameDir;

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
			string dir = root["gameDir"]?.GetValue<string>();
			if (!string.IsNullOrWhiteSpace(dir)) s.GameDir = dir;
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

	public void Save()
	{
		var accounts = new JsonArray();
		foreach (SavedAccount a in Accounts)
			accounts.Add((JsonNode)new JsonObject { ["url"] = a.Url, ["id"] = a.Id, ["name"] = a.Name, ["tag"] = a.Tag, ["token"] = a.Token });
		var root = new JsonObject
		{
			["gameDir"] = GameDir, ["autoUpdate"] = AutoUpdate, ["closeToTray"] = CloseToTray,
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
