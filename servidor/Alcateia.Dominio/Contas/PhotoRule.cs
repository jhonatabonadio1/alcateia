using System;
using System.Collections.Generic;
using System.Linq;

namespace Alcateia.Dominio.Contas;

/// <summary>
/// As fotos de perfil (a mesma lista de <c>scripts/meta/Avatars.cs</c>):
/// três de graça para todo mundo; as outras são itens da loja
/// <c>foto_&lt;id&gt;</c> (tipo <c>photo</c>). O saguão só mostra para os
/// outros uma foto que a conta pode usar.
/// </summary>
public static class PhotoRule
{
	public const string Default = "eve";
	public const string ItemPrefix = "foto_";

	public static readonly string[] Free = { "eve", "barut", "lobo_barut" };

	public static readonly string[] Sold =
	{
		"faca_damasco", "faca_obsidiana", "choque_neon", "choque_magma", "choque_ouro",
		"ar15_lava", "m4a4_dragao", "deagle_fenix", "scar_galaxia", "ak74_tigre"
	};

	public static bool Exists(string id) => Free.Contains(id) || Sold.Contains(id);

	/// <summary>A conta pode usar a foto? As grátis sempre; as da loja se o
	/// item está entre os que ela tem.</summary>
	public static bool Allowed(string id, IEnumerable<string> ownedItemIds) =>
		Free.Contains(id) || Sold.Contains(id) && ownedItemIds.Contains(ItemPrefix + id);

	/// <summary>As fotos da loja que a conta tem, a partir dos itens.</summary>
	public static HashSet<string> Owned(IEnumerable<string> ownedItemIds) =>
		ownedItemIds.Where(i => i.StartsWith(ItemPrefix, StringComparison.Ordinal)).Select(i => i[ItemPrefix.Length..]).Where(Exists).ToHashSet();
}
