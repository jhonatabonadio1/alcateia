using Godot;

/// <summary>
/// Os grupos da cena, num lugar só. É por eles que o HUD, o minimapa, os
/// menus e o som acham você (<see cref="Players"/>) e os bots
/// (<see cref="Bots"/>) — o nome do grupo é contrato com as cenas
/// (player.tscn, bot.tscn, main.tscn, treino.tscn).
/// </summary>
public static class Groups
{
	public const string Players = "player";
	public const string Bots = "bot";

	/// <summary>Quem luta: você e os bots, nessa ordem.</summary>
	public static readonly string[] Fighters = { Players, Bots };

	/// <summary>Os corpos do servidor de partida (os dos jogadores, ServerBody,
	/// e as cápsulas dos bots, BotProxies). Fora de <see cref="Fighters"/>:
	/// lá ninguém desenha nem mira; só o par de colisão do time os procura
	/// (TeamCollision), para o aliado atravessar no servidor como no jogo.</summary>
	public const string ServerBodies = "corpo_do_servidor";

	/// <summary>Todo corpo que entra no par de colisão do time.</summary>
	public static readonly string[] Bodies = { Players, Bots, ServerBodies };

	/// <summary>O jogador desta máquina, ou <c>null</c> (no lobby, ou
	/// antes de ele entrar na cena).</summary>
	public static Node LocalPlayer(SceneTree tree) => tree?.GetFirstNodeInGroup(Players);
}
