using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// As versões épicas e lendárias das armas novas (19/11; tools/armas/peles.py):
/// cada uma está no catálogo com a raridade certa, tem números (as de fogo),
/// modelo de mundo com a pintura própria, foto para a interface e conjunto
/// animado na mão; nenhuma é mais fraca que a arma de origem; e a faca
/// escolhida no conjunto é a que vai para a mão.
/// </summary>
public sealed class WeaponSkinsTest : RegressionTest
{
	public override string Name => "ArmasEpicasELendarias";
	public override string Description => "as 12 versões (M4A4, AK-74, SCAR-H, SVD, Desert Eagle, faca) existem no catálogo, no mundo, na mão e na interface; a faca do conjunto vai para a mão";

	private static readonly (string Id, string Base, int Rarity)[] Skins =
	{
		("m4a4_neon", "m4a4", 3), ("m4a4_dragao", "m4a4", 4), ("ak74_tigre", "ak74", 3), ("ak74_imperial", "ak74", 4),
		("scar_artico", "scar", 3), ("scar_galaxia", "scar", 4), ("svd_carbono", "svd", 3), ("svd_tempestade", "svd", 4),
		("deagle_cobra", "deagle", 3), ("deagle_fenix", "deagle", 4), ("faca_damasco", "faca", 3), ("faca_obsidiana", "faca", 4),
	};

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		string dir = OS.GetEnvironment("FOTO_DIR");
		if (!string.IsNullOrEmpty(dir)) foreach (Node n in b.GetTree().Root.FindChildren("*", "", true, false)) if (n is DifficultyMenu menu) menu.QueueFree();
		foreach (var (id, origin, rarity) in Skins)
		{
			CatalogItem item = Catalog.Get(id);
			b.Check(item != null && item.Rarity == rarity && item.ForSale, $"{id}: está no catálogo, à venda, com a raridade {rarity}");
			b.Check(ResourceLoader.Exists($"res://src/assets/ui/armas/{id}.png"), $"{id}: tem a foto da interface");

			Node3D model = WeaponModel.Build(id);
			var mesh = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault();
			var material = mesh?.MaterialOverride as StandardMaterial3D;
			b.Check(mesh != null && material?.AlbedoTexture != null && material.AlbedoTexture.ResourcePath.Contains($"/{id}/"), $"{id}: o modelo de mundo usa a pintura dela");
			model.Free();

			bool knife = origin == "faca";
			if (knife)
			{
				b.Check(Arsenal.IsKnife(id), $"{id}: é uma faca");
				var (damage, cooldown, _) = Arsenal.KnifeStats(id);
				b.Check(damage > 1f && cooldown < 1f, $"{id}: fere mais e bate mais rápido que a faca comum");
			}
			else
			{
				WeaponDefinition def = Arsenal.Get(id), from = Arsenal.Get(origin);
				b.Check(def != null && def.Id == id, $"{id}: tem os números dela (.tres)");
				if (def == null) continue;
				b.Check(def.Class == from.Class && Arsenal.IsSecondary(id) == Arsenal.IsSecondary(origin), $"{id}: mesma classe e mesmo espaço da {origin}");
				float dps = def.Damage * def.Rpm, was = from.Damage * from.Rpm;
				b.Check(dps >= was && def.Magazine >= from.Magazine && def.ReloadTactical <= from.ReloadTactical && def.RecoilVertical <= from.RecoilVertical,
					$"{id}: em nada pior que a {origin} (dano×cadência {RegressionBench.F(dps / was)}×, pente {def.Magazine}/{from.Magazine})");
				b.Check(def.WpCost > from.WpCost, $"{id}: custa mais WP que a {origin}");
			}

			// Na mão: o conjunto animado dela, com a pintura.
			if (knife)
			{
				var set = new LoadoutSet { Melee = id };
				b.Weapon.Equip(set);
				await b.Tap("slot_4");
			}
			else if (Arsenal.IsSecondary(id))
			{
				b.Weapon.Equip(b.Weapon.PrimaryDef, Arsenal.Get(id), 0);
				await b.Tap("slot_2");
			}
			else b.Weapon.Equip(Arsenal.Get(id), null, 0);
			await b.Seconds(0.9);
			b.Check(b.Weapon.HeldId == id, $"{id}: é o que está na mão ({b.Weapon.HeldId})");
			ViewModel view = b.Player.FindChildren("*", "", true, false).OfType<ViewModel>().FirstOrDefault();
			var held = view?.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
				.FirstOrDefault(m => m.IsVisibleInTree() && m.Mesh != null && m.Mesh.GetSurfaceCount() > 0 && m.Mesh.SurfaceGetMaterial(0) is BaseMaterial3D mat && mat.ResourceName == id);
			b.Check(held != null, $"{id}: na mão, a malha usa o material dela");
			if (!string.IsNullOrEmpty(dir))
			{
				await b.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				b.GetViewport().GetTexture().GetImage().SavePng($"{dir}/{id}.png");
			}
		}

		// A faca comum continua a de todo conjunto que não escolheu outra.
		b.Weapon.Equip(new LoadoutSet());
		await b.Tap("slot_4");
		await b.Seconds(0.5);
		b.Check(b.Weapon.HeldId == Arsenal.KnifeId, "conjunto sem faca escolhida: a faca comum");
		b.Check(!LoadoutSet.AllowedInDefault("faca_obsidiana") && LoadoutSet.AllowedInDefault(Arsenal.KnifeId), "no conjunto padrão só entra a faca comum");
	}
}
