using Godot;

/// <summary>
/// A foto de perfil sozinha: quadrada, sem a borda do nível (Jhon, 21/11:
/// no cabeçalho e nas listas a borda fica "pequena e sem espaço; aqui é só
/// foto de perfil, sem moldura, maior"). A borda do nível com a foto dentro
/// é o <see cref="RankBadge"/>, para onde há espaço (perfil, TAB, HUD).
/// Sem foto (bot), fica o quadrado escuro.
/// </summary>
public partial class PhotoTile : Control
{
	private string _photo;

	/// <summary>O id da foto (<see cref="Avatars"/>), ou nulo.</summary>
	public string Photo
	{
		get => _photo;
		set
		{
			if (_photo == value) return;
			_photo = value;
			QueueRedraw();
		}
	}

	private Color _edge = new(1f, 1f, 1f, 0.22f);

	/// <summary>O filete em volta (a cor do metal do nível, a do time…).</summary>
	public Color Edge
	{
		get => _edge;
		set { _edge = value; QueueRedraw(); }
	}

	public static PhotoTile Make(string photo, int size) => new()
	{
		CustomMinimumSize = new Vector2(size, size),
		SizeFlagsVertical = SizeFlags.ShrinkCenter,
		MouseFilter = MouseFilterEnum.Ignore,
		TextureFilter = TextureFilterEnum.LinearWithMipmaps,
		_photo = photo
	};

	public override void _Draw()
	{
		float side = Mathf.Min(Size.X, Size.Y);
		if (side <= 0) return;
		var rect = new Rect2((Size - new Vector2(side, side)) / 2f, new Vector2(side, side));
		if (Avatars.PhotoOf(_photo) is Texture2D photo) DrawTextureRect(photo, rect, false);
		else DrawRect(rect, new Color("0a1119"));
		DrawRect(rect.Grow(-0.5f), _edge, false, side >= 40f ? 2f : 1f);
	}
}
