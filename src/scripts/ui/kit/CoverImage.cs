using Godot;

/// <summary>
/// Uma imagem que enche a área sem distorcer, cortando o que sobra — e
/// escolhendo de onde corta: <see cref="FocusY"/> 0 mantém o alto da
/// imagem (o rosto de um retrato), 0,5 o meio, 1 a base. O
/// TextureRect "coberto" do Godot só corta pelo meio, e num cartão largo
/// o retrato do personagem ficava sem cabeça.
/// </summary>
public partial class CoverImage : Control
{
	private Texture2D _texture;
	public Texture2D Texture { get => _texture; set { _texture = value; QueueRedraw(); } }

	/// <summary>De onde o corte vertical parte: 0 alto, 1 base.</summary>
	public float FocusY { get; set; } = 0.5f;

	/// <summary>O mesmo na horizontal: 0 esquerda, 1 direita.</summary>
	public float FocusX { get; set; } = 0.5f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Resized += QueueRedraw;
	}

	public override void _Draw()
	{
		if (_texture == null || Size.X <= 0 || Size.Y <= 0) return;
		Vector2 native = _texture.GetSize();
		float scale = Mathf.Max(Size.X / native.X, Size.Y / native.Y);
		Vector2 shown = Size / scale;
		var region = new Rect2((native.X - shown.X) * FocusX, (native.Y - shown.Y) * FocusY, shown.X, shown.Y);
		DrawTextureRectRegion(_texture, new Rect2(Vector2.Zero, Size), region);
	}
}
