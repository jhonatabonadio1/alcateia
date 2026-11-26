using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// A arma no chão, do lado do servidor (26/11, nos dois modos): a partida
/// guarda as que estão no chão, passa a todos as que caem, decide quem pega
/// e conta a quem entra no meio as que já estavam lá.
///
/// - **Soltar** (<c>Drop</c>): só vale do corpo de quem fala (a conta, ou um
///   bot dela), com número dela (<see cref="NetWire.DropIdBase"/>), arma que
///   existe e munição dentro do pente e da reserva; na autoritativa, a arma
///   tem de ser uma das que o corpo carrega (o <c>Status</c>). Passa a todos
///   menos a quem soltou (que já a tem na tela).
/// - **Pegar** (<c>Take</c>): o primeiro pedido leva; o pedido de uma arma
///   que já não está no chão é ignorado (o <c>Taken</c> de quem levou já
///   saiu). O <c>Taken</c> vai a todos, inclusive a quem levou: é ele que
///   põe a arma na mão.
/// - **Some** sozinha depois de <see cref="WeaponPickup.Lifetime"/> s; com mais de
///   <see cref="WeaponPickup.MaxOnMap"/> no chão, a mais velha some
///   (<c>Taken</c> com ninguém).
/// </summary>
public sealed partial class HostedMatch
{
	private sealed class LooseWeapon
	{
		public long Id, Owner;
		public string Weapon;
		public short Ammo, Reserve;
		public Vector3 From, Velocity;
		public double Born;
	}

	private readonly Dictionary<long, LooseWeapon> _loose = new();
	private readonly List<long> _looseOrder = new();

	/// <summary>As armas que cada conta pegou do chão nesta partida: valem
	/// no <c>Status</c> dela mesmo sem ser da loja dela.</summary>
	private readonly Dictionary<long, HashSet<string>> _pickedUp = new();

	/// <summary>Armas no chão agora (para os testes).</summary>
	public int LooseWeapons => _loose.Count;

	/// <summary>Pegou do chão nesta partida (vale no <see cref="Allowed"/>).</summary>
	private bool PickedUp(long account, string id) =>
		_pickedUp.TryGetValue(account, out HashSet<string> set) && set.Contains(id);

	private void ReadDrop(long peer, long account, BinaryReader r)
	{
		long id = r.ReadInt64(), owner = r.ReadInt64();
		string weapon = r.ReadString();
		short ammo = r.ReadInt16(), reserve = r.ReadInt16();
		Vector3 from = r.ReadVector(), velocity = r.ReadVector();

		WeaponDefinition def = Arsenal.Get(weapon);
		if (id / NetWire.DropIdBase != account || id % NetWire.DropIdBase == 0 || _loose.ContainsKey(id)) return;
		if (!Referee.Speaks(account, owner) || def == null) return;
		if (!from.IsFinite() || !velocity.IsFinite() || velocity.Length() > 40f) return;
		// Arma que o corpo não carrega não sai do nada (a autoritativa sabe o que ele tem).
		if (Authoritative && _bodies.TryGetValue(owner, out ServerBody body) && body.Placed && !body.Carries(weapon))
		{
			GD.Print($"Match {Key}: account {account} dropped {weapon}, which {owner} does not carry; ignored.");
			return;
		}

		var loose = new LooseWeapon
		{
			Id = id, Owner = owner, Weapon = weapon, Born = _now,
			Ammo = (short)Math.Clamp((int)ammo, 0, def.Magazine),
			Reserve = (short)Math.Clamp((int)reserve, 0, def.Reserve),
			From = from, Velocity = velocity
		};
		_loose[id] = loose;
		_looseOrder.Add(id);
		Broadcast(DropPacket(loose), except: peer);

		while (_looseOrder.Count > WeaponPickup.MaxOnMap) Vanish(_looseOrder[0]);
	}

	private void ReadTake(long account, BinaryReader r)
	{
		long id = r.ReadInt64(), taker = r.ReadInt64();
		if (!Referee.Speaks(account, taker) || !_loose.TryGetValue(id, out LooseWeapon loose)) return;
		// Morto não pega (na autoritativa o servidor sabe quem está vivo).
		if (Authoritative && _bodies.TryGetValue(taker, out ServerBody body) && body.Placed && !body.Alive) return;

		_loose.Remove(id);
		_looseOrder.Remove(id);
		if (!_pickedUp.TryGetValue(taker, out HashSet<string> set)) _pickedUp[taker] = set = new HashSet<string>();
		set.Add(loose.Weapon);
		Broadcast(TakenPacket(loose, taker));
	}

	/// <summary>Passa o tempo das armas no chão: a que passou do tempo some.</summary>
	private void ExpireLoose()
	{
		while (_looseOrder.Count > 0 && _now - _loose[_looseOrder[0]].Born > WeaponPickup.Lifetime)
			Vanish(_looseOrder[0]);
	}

	/// <summary>A arma some do chão sem ninguém levar.</summary>
	private void Vanish(long id)
	{
		_looseOrder.Remove(id);
		if (_loose.Remove(id, out LooseWeapon loose)) Broadcast(TakenPacket(loose, 0));
	}

	/// <summary>Quem acabou de entrar recebe as armas que estão no chão.</summary>
	private void SendLooseTo(long peer)
	{
		foreach (long id in _looseOrder) _link.Send(peer, DropPacket(_loose[id]), true);
	}

	private byte[] DropPacket(LooseWeapon loose)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Drop, out MemoryStream stream);
		w.Write(loose.Id);
		w.Write(loose.Owner);
		w.Write(loose.Weapon);
		w.Write(loose.Ammo);
		w.Write(loose.Reserve);
		w.Write(loose.From);
		w.Write(loose.Velocity);
		w.Write((float)(_now - loose.Born));
		return stream.ToArray();
	}

	private static byte[] TakenPacket(LooseWeapon loose, long taker)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Taken, out MemoryStream stream);
		w.Write(loose.Id);
		w.Write(taker);
		w.Write(loose.Weapon);
		w.Write(loose.Ammo);
		w.Write(loose.Reserve);
		return stream.ToArray();
	}
}
