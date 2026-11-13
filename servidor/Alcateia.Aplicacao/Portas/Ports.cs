using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Partidas;

namespace Alcateia.Aplicacao.Portas;

/// <summary>Onde as contas, os logins e os perfis ficam guardados.</summary>
public interface IAccountRepository
{
	/// <summary>Cria a conta e o perfil juntos. Null se o nome já existe.</summary>
	Task<long?> TryCreate(string name, string nameKey, string passwordHash, long starterPresas);

	/// <summary>A conta com esta chave de nome e o hash da senha dela, ou null.</summary>
	Task<(Account Account, string PasswordHash)?> FindByName(string nameKey);

	Task MarkLogin(long accountId);

	/// <summary>A conta dona deste login (ainda válido), ou null.</summary>
	Task<Account> FindBySession(string tokenHash);

	/// <summary>Guarda um login novo e varre os vencidos da conta.</summary>
	Task AddSession(string tokenHash, long accountId, int days);

	Task RemoveSession(string tokenHash);

	/// <summary>O perfil da conta, ou null se ela não tem.</summary>
	Task<Profile> Profile(long accountId);

	Task SaveData(long accountId, string dataJson);

	Task<IReadOnlyList<RankingEntry>> Ranking(int limit);
}

/// <summary>Onde as partidas jogadas ficam guardadas.</summary>
public interface IMatchRepository
{
	/// <summary>Grava a partida, os jogadores e o prêmio no perfil de cada
	/// um, tudo junto. Falso se essa partida já tinha sido gravada.</summary>
	Task<bool> TrySave(SettledMatch match);

	Task<IReadOnlyList<MatchHistoryEntry>> Recent(long accountId, int limit);
}

/// <summary>Senha guardada sem ser a senha.</summary>
public interface IPasswordHasher
{
	string Hash(string password);
	bool Verify(string password, string stored);
}

/// <summary>Tokens de login: o jogador guarda o token, o servidor só o hash.</summary>
public interface ITokens
{
	string NewToken();
	string Hash(string token);
}

/// <summary>Assina o ingresso de entrada numa partida com o segredo da frota.</summary>
public interface ITicketSigner
{
	string Sign(string text);
}

public interface IClock
{
	DateTime UtcNow { get; }
}

/// <summary>A conexão viva de um jogador no saguão (o WebSocket, lá fora).</summary>
public interface ILobbyConnection
{
	bool IsOpen { get; }

	/// <summary>Põe uma mensagem na fila da conexão e volta logo, sem esperar
	/// a rede: um jogador lento não segura os outros. Quem caiu no meio só
	/// não recebe; quem não dá conta da própria fila é desligado.</summary>
	Task Send(string text);

	/// <summary>Fecha, avisando por quê.</summary>
	Task Close(string why);
}
