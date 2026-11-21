using Godot;
using System;

/// <summary>Textos e estados de item que inventário, loja e detalhe
/// mostram igual.</summary>
public static class ItemInfo
{
	/// <summary>Faltando menos que isto, o item aparece como "expirando".</summary>
	public static readonly TimeSpan ExpiringSoon = TimeSpan.FromDays(3);

	public static bool IsBreedUnlocked(CatalogItem item) =>
		item.Kind == ItemKind.Breed && Profile.Rank >= item.MinRank;

	/// <summary>O jogador pode usar o item agora?</summary>
	public static bool Usable(CatalogItem item) =>
		item.Kind == ItemKind.Breed ? IsBreedUnlocked(item) : Profile.Owns(item.Id);

	public static bool Locked(CatalogItem item) => Profile.Rank < item.MinRank;

	/// <summary>Onde o item vai: "Primária", "Secundária"...</summary>
	public static string SlotName(CatalogItem item)
	{
		if (item.Kind == ItemKind.Weapon)
		{
			if (Arsenal.IsKnife(item.Id)) return "Corpo a corpo";
			return item.Weapon?.Class == WeaponClass.Pistol ? "Secundária" : "Primária";
		}

		return item.Kind switch
		{
			ItemKind.Utility => "Granada",
			ItemKind.Tattoo => "Melhoria",
			ItemKind.Breed => "Classe",
			ItemKind.Character => "Personagem",
			ItemKind.Photo => "Foto de perfil",
			_ => "Serviço"
		};
	}

	public static bool IsPrimary(CatalogItem item) => SlotName(item) == "Primária";
	public static bool IsSecondary(CatalogItem item) => SlotName(item) == "Secundária";

	/// <summary>Duração escrita por extenso: "Permanente", "6 d 4 h",
	/// "Expirado". Nunca só número.</summary>
	public static string Duration(CatalogItem item, out Color color)
	{
		color = UiTokens.TextSecondary;

		if (item.Starter) return "Permanente";
		if (item.Kind == ItemKind.Breed)
		{
			if (IsBreedUnlocked(item)) return "Permanente";
			color = UiTokens.TextDisabled;
			return Loc.F("Destrava em {0}", Ranks.NameOf(item.MinRank));
		}

		OwnedItem owned = Profile.Find(item.Id);
		if (owned == null) return "";

		if (item.Consumable) return owned.Uses == 1 ? "1 uso" : Loc.F("{0} usos", owned.Uses);
		if (owned.IsPermanent) return "Permanente";

		if (owned.IsExpired)
		{
			color = UiTokens.Danger;
			return "Expirado";
		}

		TimeSpan left = owned.Remaining;
		if (left < ExpiringSoon) color = UiTokens.Warning;

		if (left.TotalDays >= 1) return Loc.F("{0} d {1} h", (int)left.TotalDays, left.Hours);
		if (left.TotalHours >= 1) return Loc.F("{0} h {1} min", (int)left.TotalHours, left.Minutes);
		return Loc.F("{0} min", Math.Max(1, left.Minutes));
	}

	/// <summary>Custo do item na partida, em WP (arma e granada).</summary>
	public static string WpCost(CatalogItem item)
	{
		if (item.Kind != ItemKind.Weapon && item.Kind != ItemKind.Utility) return "";
		int cost = Arsenal.CostOf(item.Id);
		return Arsenal.IsKnife(item.Id) ? "" : Loc.F("WP {0}", cost);
	}

	public static Color RarityColor(CatalogItem item) => UiTokens.Rarity[Mathf.Clamp(item.Rarity, 0, 4)];
	public static string RarityName(CatalogItem item) => UiTokens.RarityNames[Mathf.Clamp(item.Rarity, 0, 4)];

	/// <summary>"Especial": moldura colorida com brilho. Épico para cima.</summary>
	public static bool IsSpecial(CatalogItem item) => item.Rarity >= 3;
}
