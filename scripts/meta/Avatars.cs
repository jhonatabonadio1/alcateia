using Godot;
using System.Linq;

/// <summary>
/// Fotos de perfil (Jhon, 21/11, "igual League of Legends"): o retrato que
/// vai dentro da borda de nível (<see cref="RankBadge"/>). Três vêm de graça
/// (Eve, Barut e o lobo do Barut, da arte dos personagens) e dez são da loja
/// (as armas e granadas raras, em assets/ui/avatares/).
///
/// As da loja são itens do catálogo (<c>foto_&lt;id&gt;</c>, tipo Photo,
/// só permanentes). A escolha fica em user://foto.cfg e vai para o saguão
/// (<see cref="LobbyLink.SetPhoto"/>), que confere se é sua e a põe na sala
/// para os outros verem (o "photo" de cada membro).
/// </summary>
public static class Avatars
{
	public sealed record Photo(string Id, string Name, int Price)
	{
		public bool Free => Price == 0;
		public string Path => $"res://assets/ui/avatares/{Id}.png";
	}

	public static readonly Photo[] All =
	{
		new("eve", "Eve", 0),
		new("barut", "Barut", 0),
		new("lobo_barut", "Fera do Barut", 0),
		new("faca_damasco", "Lâmina Damasco", 800),
		new("faca_obsidiana", "Obsidiana", 1200),
		new("choque_neon", "Neon", 800),
		new("choque_magma", "Magma", 1200),
		new("choque_ouro", "Ouro Puro", 2000),
		new("ar15_lava", "Lava", 1500),
		new("m4a4_dragao", "Dragão", 2000),
		new("deagle_fenix", "Fênix", 1500),
		new("scar_galaxia", "Galáxia", 2500),
		new("ak74_tigre", "Tigre", 1500),
	};

	public const string Default = "eve";
	private const string File = "user://foto.cfg";
	private static string _selected;

	public static Photo Get(string id) => All.FirstOrDefault(p => p.Id == id);

	/// <summary>O id da foto a partir do item da loja (<c>foto_eve</c> → <c>eve</c>).</summary>
	public static string IdOf(string itemId) => itemId != null && itemId.StartsWith("foto_") ? itemId[5..] : itemId;

	/// <summary>A foto é sua? As grátis sempre; as da loja quando o item
	/// <c>foto_&lt;id&gt;</c> estiver no inventário (quando o servidor vender).</summary>
	public static bool Owns(string id) => Get(id) is Photo p && (p.Free || Profile.Owns("foto_" + id));

	/// <summary>A foto escolhida (uma que é sua; senão, a padrão).</summary>
	public static string Selected
	{
		get
		{
			if (_selected == null)
			{
				var cfg = new ConfigFile();
				_selected = cfg.Load(File) == Error.Ok ? cfg.GetValue("perfil", "foto", Default).AsString() : Default;
			}
			return Owns(_selected) ? _selected : Default;
		}
		set
		{
			if (!Owns(value)) return;
			_selected = value;
			var cfg = new ConfigFile();
			cfg.SetValue("perfil", "foto", value);
			cfg.Save(File);
			if (LobbyLink.InRoom || Backend.LoggedIn) LobbyLink.Instance.SetPhoto(value);
		}
	}

	public static Texture2D PhotoOf(string id) => Get(id) is Photo p ? RankBadge.Load(p.Path) : null;
}
