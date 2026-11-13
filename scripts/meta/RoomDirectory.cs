using Godot;
using System.Collections.Generic;
using System.Linq;

public enum RoomState { Waiting, Playing, Full }

/// <summary>Uma linha da lista de salas.</summary>
public sealed class RoomListing
{
	public RoomSettings Settings;
	public List<RoomMember> Members = new();
	public RoomState State;
	/// <summary>Criada pelo próprio jogador nesta sessão.</summary>
	public bool Mine;

	public int Occupancy => Members.Count;
	public int Capacity => Settings.TeamSize * 2;
	public bool HasPassword => !string.IsNullOrEmpty(Settings.Password);

	public RoomState EffectiveState =>
		State == RoomState.Playing ? RoomState.Playing
		: Occupancy >= Capacity ? RoomState.Full
		: RoomState.Waiting;
}

/// <summary>
/// As salas de treino com bots, offline: só as que o jogador cria nesta
/// sessão. As salas de verdade, com outros jogadores, vêm do servidor
/// (<see cref="LobbyLink"/>, tela <see cref="OnlineScreen"/>).
///
/// Já houve aqui salas de mentira, montadas ao abrir o jogo para a lista
/// não nascer vazia; saíram quando a lista de verdade passou a existir.
/// </summary>
public static class RoomDirectory
{
	public const int PageSize = 10;

	private static readonly List<RoomListing> _rooms = new();

	/// <summary>A sala em que o jogador está agora, ou nenhuma. Sobrevive
	/// à partida: é para ela que o fim de partida volta.</summary>
	public static RoomListing Current { get; set; }
	private static int _nextNumber = 1;

	public static IReadOnlyList<RoomListing> Rooms
	{
		get => _rooms;
	}

	public static RoomListing Find(int number) => Rooms.FirstOrDefault(r => r.Settings.Number == number);

	/// <summary>Cria a sala do jogador a partir de regras dadas e a põe
	/// na lista. O criador é o mestre.</summary>
	public static RoomListing Create(RoomSettings settings)
	{
		_ = Rooms;
		settings.Number = _nextNumber++;
		var room = new RoomListing { Settings = settings, Mine = true };
		room.Members.Add(new RoomMember
		{
			Name = Profile.PlayerName,
			Team = Teams.Blue,
			IsHost = true,
			Rank = Profile.Rank
		});

		// Começa com metade dos lugares ocupada por bots, equilibrada —
		// ninguém quer criar sala e ter de adicionar oito bots à mão.
		int perTeam = Mathf.Max(settings.TeamSize / 2, 2);
		AddBots(room, Teams.Blue, perTeam - 1);
		AddBots(room, Teams.Orange, perTeam);

		_rooms.Insert(0, room);
		return room;
	}

	/// <summary>Entra numa sala existente: o jogador ocupa um lugar no
	/// time com menos gente e, sem outro humano na sala, vira o mestre.</summary>
	public static void Join(RoomListing room)
	{
		room.Members.RemoveAll(m => !m.IsBot);
		foreach (RoomMember m in room.Members) m.IsHost = false;

		int blue = room.Members.Count(m => m.Team == Teams.Blue);
		int orange = room.Members.Count(m => m.Team == Teams.Orange);
		int team = blue <= orange ? Teams.Blue : Teams.Orange;

		// Sala cheia: o jogador toma o lugar de um bot do time.
		if (room.Occupancy >= room.Capacity)
		{
			RoomMember bot = room.Members.LastOrDefault(m => m.IsBot && m.Team == team);
			if (bot != null) room.Members.Remove(bot);
		}

		room.Members.Insert(0, new RoomMember
		{
			Name = Profile.PlayerName,
			Team = team,
			IsHost = true,
			Rank = Profile.Rank
		});
		room.State = RoomState.Waiting;
	}

	public static void Remove(RoomListing room) => _rooms.Remove(room);

	public static void AddBots(RoomListing room, int team, int count)
	{
		for (int i = 0; i < count; i++)
		{
			if (room.Members.Count(m => m.Team == team) >= room.Settings.TeamSize) return;

			string name = RoomConfig.BotNames.FirstOrDefault(n => room.Members.All(m => m.Name != n))
						  ?? $"Bot {room.Members.Count + 1}";

			room.Members.Add(new RoomMember
			{
				Name = name,
				Team = team,
				IsBot = true,
				Ready = true,
				Rank = Ranks.ForBot(name, (int)room.Settings.Difficulty)
			});
		}
	}
}
