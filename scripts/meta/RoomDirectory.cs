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
/// As salas do canal. Sem servidor, o canal é local: algumas salas de
/// treino montadas com bots ao abrir o jogo, mais as que o jogador
/// criar na sessão.
///
/// Fase 2: a lista vem do servidor, e este é o ponto que troca de
/// fonte. A tela não sabe de onde as salas vêm.
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
        get
        {
            if (_rooms.Count == 0) Seed();
            return _rooms;
        }
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

    /// <summary>Salas de treino do canal local, variadas o bastante para
    /// os filtros terem o que filtrar.</summary>
    private static void Seed()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)System.DateTime.Now.Ticks };

        (string title, int size, int score, int minutes, WeaponRule weapons, TransformRule transform, BotLevel level, bool playing, string pass)[] seeds =
        {
            ("Treino rápido 4v4", 4, 20, 5, WeaponRule.Todas, TransformRule.Livre, BotLevel.Easy, false, ""),
            ("Hangar 8v8 lotado", 8, 40, 10, WeaponRule.Todas, TransformRule.Livre, BotLevel.Medium, false, ""),
            ("Duelo de pistolas", 6, 30, 10, WeaponRule.SoPistolas, TransformRule.Livre, BotLevel.Medium, false, ""),
            ("Humanos apenas", 6, 40, 10, WeaponRule.Todas, TransformRule.Bloqueada, BotLevel.Medium, true, ""),
            ("Bots difíceis", 8, 60, 15, WeaponRule.SemPesadas, TransformRule.Livre, BotLevel.Hard, false, ""),
            ("Sala do clã (senha)", 6, 40, 10, WeaponRule.Todas, TransformRule.Livre, BotLevel.Hard, false, "1234"),
        };

        foreach (var s in seeds)
        {
            var settings = new RoomSettings
            {
                Number = _nextNumber++,
                Title = s.title,
                TeamSize = s.size,
                ScoreToWin = s.score,
                Minutes = s.minutes,
                Weapons = s.weapons,
                Transform = s.transform,
                Difficulty = s.level,
                Password = s.pass
            };

            var room = new RoomListing { Settings = settings, State = s.playing ? RoomState.Playing : RoomState.Waiting };
            int fill = rng.RandiRange(s.size, s.size * 2);
            for (int i = 0; i < fill; i++)
                AddBots(room, i % 2 == 0 ? Teams.Blue : Teams.Orange, 1);

            _rooms.Add(room);
        }

        // Números saltados, como num canal de verdade.
        _nextNumber += rng.RandiRange(2, 6);
    }
}
