using Godot;
using System.Linq;

/// <summary>
/// Fotos de perfil (Jhon, 21/11, "igual League of Legends"): o retrato que
/// vai dentro da borda de nível (<see cref="RankBadge"/>). Vinte ícones
/// pintados, no estilo dos ícones de jogador dos MOBAs (gerados com IA no
/// Figma, arquivo "WolfLeague — ícones de jogador"; assets/ui/avatares/):
/// oito de graça e doze da loja.
///
/// Na partida a foto é outra: o retrato do personagem com que cada um joga
/// (<see cref="OfCharacter"/>, assets/personagens/&lt;nome&gt;/retrato.png).
///
/// As da loja são itens do catálogo (<c>foto_&lt;id&gt;</c>, tipo Photo,
/// só permanentes). A escolha fica em user://avatar.cfg e vai para o saguão
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
		// de graça
		new("lobo_cinza", "Lobo Cinzento", 0),
		new("filhote", "Filhote", 0),
		new("lua_cheia", "Lua Cheia", 0),
		new("coruja_piloto", "Coruja Piloto", 0),
		new("geleia", "Geleia", 0),
		new("drone", "Drone", 0),
		new("panda_vermelho", "Panda Vermelho", 0),
		new("corvo", "Corvo", 0),
		// da loja
		new("garras", "Garras", 800),
		new("capacete", "Capacete", 800),
		new("escorpiao", "Escorpião", 800),
		new("bau", "Baú", 800),
		new("urso", "Urso de Guerra", 1200),
		new("cristal_lua", "Cristal da Lua", 1200),
		new("coruja_neve", "Coruja da Neve", 1500),
		new("raposa_espirito", "Raposa Espírito", 1500),
		new("lobo_neon", "Lobo Neon", 2000),
		new("granada_ouro", "Granada de Ouro", 2000),
		new("lobo_gelo", "Lobo de Gelo", 2500),
		new("lobo_fogo", "Lobo de Fogo", 2500),
	};

	public const string Default = "lobo_cinza";
	private static string File => UserFiles.Resolve(UserFiles.Avatar, "user://foto.cfg");
	private static string _selected;

	public static Photo Get(string id) => All.FirstOrDefault(p => p.Id == id);

	/// <summary>O id da foto a partir do item da loja (<c>foto_lobo_fogo</c> → <c>lobo_fogo</c>).</summary>
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
				// [profile] avatar (até 21/11, [perfil] foto).
				_selected = cfg.Load(File) == Error.Ok
					? cfg.GetValue("profile", "avatar", cfg.GetValue("perfil", "foto", Default)).AsString()
					: Default;
			}
			return Owns(_selected) ? _selected : Default;
		}
		set
		{
			if (!Owns(value)) return;
			_selected = value;
			var cfg = new ConfigFile();
			cfg.SetValue("profile", "avatar", value);
			cfg.Save(File);
			if (LobbyLink.InRoom || Backend.LoggedIn) LobbyLink.Instance.SetPhoto(value);
		}
	}

	/// <summary>A imagem de uma foto de perfil ou de um personagem
	/// (<c>pers_&lt;nome&gt;</c>, o retrato dele).</summary>
	public static Texture2D PhotoOf(string id)
	{
		if (id != null && id.StartsWith("pers_")) return PortraitOf(id);
		return Get(id) is Photo p ? RankBadge.Load(p.Path) : null;
	}

	/// <summary>A foto de quem está na partida: o personagem escolhido, ou o
	/// de sempre do time (Eve no azul, Barut no laranja).</summary>
	public static string OfCharacter(CharacterSkin chosen, int team) =>
		(chosen ?? CharacterSkin.DefaultFor(team))?.Id;

	private static readonly System.Collections.Generic.Dictionary<string, Texture2D> Portraits = new();

	private static Texture2D PortraitOf(string id)
	{
		if (Portraits.TryGetValue(id, out Texture2D cached)) return cached;
		CharacterSkin skin = CharacterSkin.All().FirstOrDefault(s => s.Id == id);
		Texture2D portrait = skin == null ? null
			: ResourceLoader.Exists(skin.PortraitPath) ? GD.Load<Texture2D>(skin.PortraitPath) : skin.Thumbnail;
		Portraits[id] = portrait;
		return portrait;
	}
}
