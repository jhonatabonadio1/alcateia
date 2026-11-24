/// <summary>
/// As regras de arma da sala aplicadas a um conjunto.
///
/// "Sem pesadas" proíbe escopeta (e, quando houver, sniper e
/// metralhadora); o conjunto que as usa fica indisponível na tela de
/// respawn. "Só pistolas" transforma todo conjunto em pistola e pistola,
/// sem granada.
/// </summary>
public static class RoomRules
{
	public static bool IsHeavy(WeaponDefinition def) =>
		def != null && (def.Class == WeaponClass.Shotgun || def.Class == WeaponClass.Sniper);

	/// <summary>O conjunto pode ser escolhido nesta sala?</summary>
	public static bool Allows(LoadoutSet set, out string why)
	{
		why = null;
		if (set == null || set.IsDefault) return true;

		if (RoomConfig.Rules.Weapons == WeaponRule.SemPesadas
			&& (IsHeavy(Arsenal.Get(set.Primary)) || IsHeavy(Arsenal.Get(set.Secondary))))
		{
			why = "Arma pesada proibida nesta sala";
			return false;
		}

		return true;
	}

	/// <summary>Equipa o conjunto respeitando a regra da sala.</summary>
	public static void Equip(Weapon weapon, LoadoutSet set)
	{
		if (RoomConfig.Rules.Weapons == WeaponRule.SoPistolas)
		{
			WeaponDefinition pistol = Arsenal.Get(set?.Secondary) ?? Arsenal.Get(Arsenal.DefaultSecondary);
			weapon.Equip(pistol, pistol, 0);
			return;
		}

		weapon.Equip(set);
	}

	/// <summary>Bot: fica com o padrão, a não ser que a sala mude as
	/// armas de todo mundo.</summary>
	public static void EquipBot(Weapon weapon)
	{
		if (RoomConfig.Rules.Weapons == WeaponRule.SoPistolas)
			Equip(weapon, null);
	}

	/// <summary>Frase curta para avisar a regra na tela de respawn.</summary>
	public static string Notice() => RoomConfig.Rules.Weapons switch
	{
		WeaponRule.SemPesadas => "Sala sem armas pesadas",
		WeaponRule.SoPistolas => "Sala só de pistolas: todo conjunto vira pistola",
		_ => null
	};
}
