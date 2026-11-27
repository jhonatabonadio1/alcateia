using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using N = System.Numerics;

/// <summary>
/// O que cada jogador recebe dos inimigos (27/11, contra wallhack; as regras
/// em <see cref="SightRules"/>). Até aqui o servidor mandava todos os corpos a
/// todos, e um jogo adulterado desenhava o inimigo através da parede. Agora,
/// 10 vezes por segundo, o servidor confere para cada jogador quem ele pode
/// ver (linha de visão no mapa do servidor, com onde cada um vai estar em
/// 0,3 s) ou ouvir (o passo, o rugido, o tiro), e:
///
/// - no estado do mundo, o inimigo que ele não pode saber vai sem o lugar
///   (sem <see cref="EntityFlags.Present"/>, zerado) — o jogo esconde o boneco;
/// - o estado e o ataque de um bot inimigo não são repassados a ele, e o
///   boneco some (<c>Gone</c>).
///
/// Sempre recebe: o próprio time, quem morreu (o corpo no chão), e tudo se é
/// o dono dos bots da sala — a máquina dele move os bots, que precisam saber
/// onde todos estão (é o buraco que fecha quando os bots forem do servidor).
/// </summary>
public sealed partial class HostedMatch
{
	/// <summary>Liga e desliga o filtro (o teste de ponta a ponta antigo e a
	/// variável <c>ALCATEIA_NEBLINA=0</c> desligam).</summary>
	public bool Fog { get; set; } = OS.GetEnvironment("ALCATEIA_NEBLINA").Trim() != "0";

	/// <summary>Em que passo cada jogador pôde saber de cada outro corpo pela última vez.</summary>
	private readonly Dictionary<(long Receiver, long Target), uint> _knownAt = new();

	/// <summary>Em que passo cada corpo atacou pela última vez (ouve-se de longe).</summary>
	private readonly Dictionary<long, uint> _loudAt = new();

	/// <summary>Os bots que cada jogador não está recebendo (já avisado com <c>Gone</c>).</summary>
	private readonly HashSet<(long Receiver, long Bot)> _botHidden = new();

	/// <summary>Estados de inimigo mandados sem o lugar (para o relatório e o teste).</summary>
	public long HiddenStates { get; private set; }

	/// <summary>Raios de linha de visão dados (para medir o custo).</summary>
	public long SightRays { get; private set; }

	private void MarkLoud(long who) => _loudAt[who] = _tick == 0 ? 1 : _tick;

	/// <summary>Este jogador pode saber, agora, onde este corpo está?</summary>
	public bool Knows(long receiver, long target)
	{
		if (!Fog || !Authoritative || receiver == target) return true;
		Referee.Seat mine = Referee.Find(receiver), theirs = Referee.Find(target);
		if (mine == null || theirs == null || mine.Team == theirs.Team) return true;
		// O dono dos bots move os bots: precisa de todos.
		if (_botOwners.Contains(receiver)) return true;
		// Quem ainda não tem corpo no mundo (entrando) ou o alvo que caiu.
		if (!_bodies.TryGetValue(receiver, out ServerBody body) || !body.Placed) return true;
		if (!theirs.IsBot && (!_bodies.TryGetValue(target, out ServerBody them) || !them.Alive)) return true;

		if (body.Alive) return SightRules.Fresh(_knownAt.GetValueOrDefault((receiver, target)), _tick);

		// Morto, assiste os aliados: sabe o que algum deles sabe (sem aliado vivo, tudo).
		bool anyAlive = false;
		foreach (ServerBody ally in _bodyList)
		{
			if (ally == body || !ally.Alive || Referee.Find(ally.Account)?.Team != mine.Team) continue;
			anyAlive = true;
			if (SightRules.Fresh(_knownAt.GetValueOrDefault((ally.Account, target)), _tick)) return true;
		}
		return !anyAlive;
	}

	/// <summary>Refaz, para cada jogador vivo, quem ele vê ou ouve.</summary>
	private void UpdateSight(uint tick)
	{
		if (!Fog || _hits == null) return;

		_botOwners.Clear();
		foreach (Referee.Seat s in Referee.Seats)
			if (s.IsBot && s.Connected) _botOwners.Add(s.Owner);

		foreach (ServerBody viewer in _bodyList)
		{
			if (!viewer.Alive) continue;
			Referee.Seat seat = Referee.Find(viewer.Account);
			if (seat == null) continue;

			foreach (ServerBody other in _bodyList)
			{
				if (other == viewer || !other.Alive || Referee.Find(other.Account)?.Team == seat.Team) continue;
				Player b = other.Body;
				bool running = b.IsSprinting || b.IsHumanSprinting || other.Form.IsWolf;
				float hear = SightRules.HearingRadius(b.Velocity.Length(), b.IsCrouching, running, b.HowlProgress > 0f);
				if (Perceives(viewer, other.Account, b.GlobalPosition, b.Velocity, b.BodyHeight, hear, tick))
					_knownAt[(viewer.Account, other.Account)] = tick;
			}

			foreach (var (bot, pose) in _botPoses)
			{
				if (Referee.Find(bot) is not { } botSeat || botSeat.Team == seat.Team || _botDead.Contains(bot)) continue;
				// O bot é da máquina do dono: do passo dele o servidor só tem o lugar. Ouve-se como quem corre.
				if (Perceives(viewer, bot, pose.Position, Vector3.Zero, 1.75f, SightRules.RunRadius, tick))
					_knownAt[(viewer.Account, bot)] = tick;
			}
		}

		// O bot que um jogador deixou de poder saber some no jogo dele.
		foreach (var (peer, account) in _accountOf)
			foreach (long bot in _botPoses.Keys)
			{
				bool knows = Knows(account, bot);
				if (knows) _botHidden.Remove((account, bot));
				else if (_botHidden.Add((account, bot)))
				{
					BinaryWriter w = NetWire.Begin(NetWire.Gone, out MemoryStream stream);
					w.Write(bot);
					_link.Send(peer, stream.ToArray(), true);
				}
			}
	}

	/// <summary>O jogador vê ou ouve este corpo neste passo?</summary>
	private bool Perceives(ServerBody viewer, long target, Vector3 center, Vector3 velocity, float height, float hear, uint tick)
	{
		if (SightRules.Fresh(_loudAt.GetValueOrDefault(target), tick, SightRules.LoudTicks)) return true;

		Player me = viewer.Body;
		Vector3 eye = me.GlobalPosition + Vector3.Up * (me.BodyHeight * 0.5f - 0.15f);
		if (eye.DistanceTo(center) <= hear) return true;

		// De onde se olha: o olho, o olho daqui a pouco, e por cima (o pulo, e a
		// câmera de terceira pessoa do lobo, que fica atrás e acima do corpo).
		Vector3 back = new Vector3(Mathf.Sin(me.Rotation.Y), 0, Mathf.Cos(me.Rotation.Y));
		Span<Vector3> from = stackalloc Vector3[4];
		from[0] = eye;
		from[1] = eye + me.Velocity * SightRules.LookAhead;
		from[2] = eye + Vector3.Up * 2.2f;
		from[3] = eye + back * 2.5f + Vector3.Up * 1.2f;
		int origins = viewer.Form.IsWolf ? 4 : 3;

		// Para onde: os pontos do corpo que o tiro confere, agora e daqui a pouco.
		Span<N.Vector3> points = stackalloc N.Vector3[6];
		Vector3 ahead = center + velocity * SightRules.LookAhead;
		for (int o = 0; o < origins; o++)
		{
			int count = HitRules.TargetPoints(center.ToNumerics(), height, from[o].ToNumerics(), points);
			for (int i = 0; i < count; i++)
			{
				SightRays++;
				if (_hits.ClearLine(from[o], points[i].ToGodot())) return true;
			}
			SightRays += 2;
			if (_hits.ClearLine(from[o], ahead) || _hits.ClearLine(from[o], ahead + Vector3.Up * (height * 0.5f - 0.15f))) return true;
		}
		return false;
	}

	/// <summary>O estado do mundo de um jogador: o corpo de quem ele não pode saber vai sem o lugar.</summary>
	private int WriteSnapshotFor(long account, int count, Span<byte> buffer)
	{
		if (_filtered.Length < count) _filtered = new EntityState[count * 2];
		bool any = false;
		for (int i = 0; i < count; i++)
		{
			EntityState s = _states[i];
			long id = _stateOwner[i];
			if (id != account && s.Has(EntityFlags.Present) && !Knows(account, id))
			{
				s = new EntityState { Index = s.Index, Epoch = s.Epoch };
				any = true;
				HiddenStates++;
			}
			_filtered[i] = s;
		}
		return any ? SnapshotWire.WriteBody(buffer, _filtered.AsSpan(0, count)) : -1;
	}

	/// <summary>Quem move bots nesta partida (refeito a cada conta).</summary>
	private readonly HashSet<long> _botOwners = new();

	private EntityState[] _filtered = new EntityState[16];
	private long[] _stateOwner = new long[16];
	private byte[] _ownBody = new byte[1 + 16 * EntityState.Size];
}
