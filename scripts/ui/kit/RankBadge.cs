using Godot;
using System.Collections.Generic;

/// <summary>
/// Insígnia de patente. Nunca texto: com trinta patentes, a forma e a cor
/// dizem a família, e os losangos dizem o grau.
///
/// O desenho é uma imagem por patente (assets/ui/patentes/NN.png, 256 px;
/// pedido do Jhon, 17/11): um brasão facetado em metal da cor da família,
/// miolo de esmalte escuro e o emblema no meio — pata, divisas, mira,
/// ponta de flecha, torre, lua crescente, presas, lua cheia, estrela e a
/// cabeça de lobo. O brasão cresce com a família (ombros, asas, coroa).
/// Quem gera as imagens é tools/patentes.py.
///
/// O miolo é sempre escuro, então a insígnia vale no menu claro e na
/// partida, sem moldura por fora.
/// </summary>
public partial class RankBadge : Control
{
	private const string ArtDir = "res://assets/ui/patentes/";
	private static readonly Dictionary<int, Texture2D> Art = new();

	private int _rank;

	public int Rank
	{
		get => _rank;
		set
		{
			// Sem patente (bot): o espaço fica, a insígnia não.
			_rank = value < 0 ? Ranks.None : Ranks.Clamp(value);
			TooltipText = _rank < 0 ? "Bot — sem patente" : Ranks.NameOf(_rank);
			QueueRedraw();
		}
	}

	/// <summary>Sem moldura. Ficou do desenho antigo (a marca solta, para
	/// linha de placar); a imagem já não tem moldura.</summary>
	public bool Bare { get; set; }

	public static RankBadge Make(int rank, int size = 32, bool bare = false)
	{
		var badge = new RankBadge
		{
			CustomMinimumSize = new Vector2(size, size),
			Bare = bare,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Pass,
			// 256 px quase sempre mostrados com 20 a 48.
			TextureFilter = TextureFilterEnum.LinearWithMipmaps
		};
		badge.Rank = rank;
		return badge;
	}

	private static Texture2D ArtOf(int rank)
	{
		if (Art.TryGetValue(rank, out Texture2D texture)) return texture;
		string path = $"{ArtDir}{rank:00}.png";
		texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		if (texture == null) GD.PrintErr($"RankBadge: não achei {path}");
		Art[rank] = texture;
		return texture;
	}

	public override void _Draw()
	{
		float side = Mathf.Min(Size.X, Size.Y);
		if (side <= 0 || _rank < 0) return;
		if (ArtOf(_rank) is not Texture2D art) return;

		// As asas e a coroa passam um pouco do brasão: a imagem ocupa o
		// quadrado inteiro, e um pouco mais nas linhas pequenas, onde o
		// espaço dado é justo.
		float grow = side < 30 ? 1.15f : 1f;
		Vector2 size = new Vector2(side, side) * grow;
		DrawTextureRect(art, new Rect2((Size - size) / 2f, size), false);
	}
}
