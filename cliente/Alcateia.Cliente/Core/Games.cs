using System.Collections.Generic;

namespace Alcateia.Cliente.Core;

/// <summary>
/// Um jogo da biblioteca do Initech Launcher: o nome, o produto (o nome dos
/// manifestos dele no balde: <c>&lt;produto&gt;-&lt;plataforma&gt;.manifest.json</c>),
/// a pasta dentro de <c>game/</c> e a arte da página dele.
///
/// O launcher é da Initech Games, não de um jogo só (Jhon, 27/11: "não é
/// exclusivo do wolfleague [...] futuramente teremos outros jogos"): jogo
/// novo é uma linha em <see cref="Games.All"/>, a arte em Assets/jogos/ e os
/// manifestos dele publicados no mesmo balde.
/// </summary>
public sealed record GameEntry(
	string Id,
	string Name,
	string Product,
	string Folder,
	string Stage,
	string Art,
	string Logo,
	string Icon);

public static class Games
{
	public static readonly GameEntry WolfLeague = new(
		Id: "wolfleague",
		Name: "WolfLeague",
		Product: "wolfleague",
		Folder: "WolfLeague",
		Stage: "ALPHA",
		Art: "avares://WolfLeagueLauncher/Assets/jogos/wolfleague/arte.jpg",
		Logo: "avares://WolfLeagueLauncher/Assets/jogos/wolfleague/logo.png",
		Icon: "avares://WolfLeagueLauncher/Assets/jogos/wolfleague/icone.png");

	/// <summary>A biblioteca, na ordem do menu da esquerda.</summary>
	public static readonly IReadOnlyList<GameEntry> All = new[] { WolfLeague };
}
