using Godot;
using System.IO;

/// <summary>
/// A arma no chão na partida online (Jhon, 26/11: "arma largada não aparece
/// pros outros jogadores do mapa, amigo e inimigo... devo conseguir pegar a
/// arma de qualquer jogador na partida"). Até aqui a arma caía só na tela de
/// quem a soltou — nada ia pela rede —, e o boneco de outra máquina que
/// morria soltava uma cópia só desta tela. Agora:
///
/// - **soltar**: o corpo desta máquina (o jogador ou um bot dela) solta, e a
///   arma vai ao servidor (<c>Drop</c>), que passa a todos;
/// - **ver**: a arma de outra máquina cai aqui de onde saiu, com a mesma
///   força e a mesma munição (<see cref="WeaponPickup.SpawnRemote"/>);
/// - **pegar**: só o corpo desta máquina pede (<c>Take</c>); o servidor dá
///   ao primeiro pedido e avisa todos (<c>Taken</c>), e a arma some de todas
///   as telas.
///
/// O que é regra (quem cabe a arma, a munição que entra) continua no
/// <see cref="Weapon"/>; aqui só vai e volta o aviso.
/// </summary>
public partial class NetMatch : WeaponPickup.IPickupLink
{
	private long _dropCount;

	public bool Controls(Node3D body) =>
		body != null && (body == _player || (body is Bot bot && !bot.Remote && _owned.ContainsKey(bot.NetId)));

	public long Announce(WeaponPickup pickup, Node3D dropper, Vector3 from, Vector3 velocity)
	{
		long owner = IdOf(dropper);
		if (!Joined || owner == 0 || !Controls(dropper) || pickup?.Definition == null) return 0;

		long id = OnlineMatch.AccountId * NetWire.DropIdBase + (++_dropCount % NetWire.DropIdBase);
		BinaryWriter w = NetWire.Begin(NetWire.Drop, out MemoryStream stream);
		w.Write(id);
		w.Write(owner);
		w.Write(pickup.Definition.Id);
		w.Write((short)Mathf.Clamp(pickup.Ammo, 0, short.MaxValue));
		w.Write((short)Mathf.Clamp(pickup.Reserve, 0, short.MaxValue));
		w.Write(from);
		w.Write(velocity);
		Send(stream);
		return id;
	}

	public void Claim(WeaponPickup pickup, Node3D taker)
	{
		long id = IdOf(taker);
		if (!Joined || id == 0 || pickup == null || pickup.NetId == 0) return;
		BinaryWriter w = NetWire.Begin(NetWire.Take, out MemoryStream stream);
		w.Write(pickup.NetId);
		w.Write(id);
		Send(stream);
	}

	/// <summary>Uma arma que caiu em outra máquina (ou que já estava no chão
	/// quando entrei).</summary>
	private void ReadDrop(BinaryReader r)
	{
		long id = r.ReadInt64(), owner = r.ReadInt64();
		WeaponDefinition def = Arsenal.Get(r.ReadString());
		int ammo = r.ReadInt16(), reserve = r.ReadInt16();
		Vector3 from = r.ReadVector(), velocity = r.ReadVector();
		double age = r.BaseStream.Length - r.BaseStream.Position >= 4 ? r.ReadSingle() : 0;
		if (def == null || !from.IsFinite() || !velocity.IsFinite()) return;

		Node3D dropper = _puppets.TryGetValue(owner, out Bot puppet) ? puppet : LocalBody(owner) as Node3D;
		WeaponPickup.SpawnRemote(GetTree(), id, def, ammo, reserve, from, velocity, age, dropper);
	}

	/// <summary>O servidor decidiu quem levou a arma (0: sumiu no chão).</summary>
	private void ReadTaken(BinaryReader r)
	{
		long id = r.ReadInt64(), taker = r.ReadInt64();
		WeaponDefinition def = Arsenal.Get(r.ReadString());
		int ammo = r.ReadInt16(), reserve = r.ReadInt16();
		WeaponPickup.Grant(id, taker == 0 ? null : LocalBody(taker) as Node3D, def, ammo, reserve);
	}
}
