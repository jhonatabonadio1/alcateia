using Godot;

/// <summary>
/// Minimapa no canto superior esquerdo.
///
/// Desenhado em 2D a partir das posições do mundo, não por câmera
/// ortogonal numa viewport. A diferença não é só custo: uma câmera de
/// cima desenharia o chão, as sombras e a iluminação, e nada disso
/// ajuda a ler posição. Um minimapa é um diagrama, não uma foto.
///
/// Gira com o jogador — o que está em cima do mapa é o que está à
/// frente. Minimapa fixo obriga o jogador a traduzir mentalmente
/// enquanto corre, e ninguém faz isso no meio de um tiroteio.
///
/// Só mostra quem está dentro do alcance. Saber onde está todo mundo
/// o tempo todo destruiria a razão de existir do agachar, do andar
/// devagar e da audição dos bots.
/// </summary>
public partial class Minimap : Control
{
	[Export] public float Range = 30f;

	/// <summary>Lado do quadrado, em pixels.</summary>
	[Export] public float Side = 168f;

	[Export] public Color BackColor = new Color(0.05f, 0.06f, 0.08f, 0.72f);
	[Export] public Color EdgeColor = new Color(1f, 1f, 1f, 0.22f);
	[Export] public Color WallColor = new Color(1f, 1f, 1f, 0.3f);
	[Export] public Color SelfColor = new Color(0.95f, 0.95f, 0.95f);

	/// <summary>Opacidade das peças do mapa. Abaixo de 1 para os pontos
	/// dos jogadores sempre se destacarem por cima.</summary>
	[Export] public float MapAlpha = 0.85f;

	private Node3D _player;
	private Health _playerHealth;

	/// <summary>Uma peça do mapa vista de cima: quatro cantos no mundo,
	/// a cor do material e a altura do topo.</summary>
	private struct Piece
	{
		public Vector3[] Corners;
		public Color Color;
		public float Top;
	}

	private readonly System.Collections.Generic.List<Piece> _pieces = new();
	private readonly System.Collections.Generic.List<(Vector3 Position, string Text, Color Color)> _marks = new();
	private bool _scanned;

	public override void _Ready()
	{
		_player = Groups.LocalPlayer(GetTree()) as Node3D;
		_playerHealth = _player?.GetNodeOrNull<Health>("Health");

		CustomMinimumSize = new Vector2(Side, Side);
		Size = CustomMinimumSize;
		ClipContents = true;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Process(double delta)
	{
		// A varredura sai no primeiro quadro e não no _Ready: o mapa é
		// uma cena irmã, e não vale apostar na ordem em que as duas
		// ficam prontas.
		if (!_scanned)
		{
			Scan();
			_scanned = true;
		}

		if (_player != null) QueueRedraw();
	}

	/// <summary>
	/// Lê a geometria do mapa uma vez.
	///
	/// Antes o minimapa procurava um nó chamado "Walls", que só existia
	/// na caixa cinza antiga — no Hangar ele não desenhava nada. Agora
	/// qualquer caixa do mapa entra, com a cor do material dela: o
	/// minimapa mostra o mesmo mapa que o jogador vê, base azul azul,
	/// base vermelha vermelha, e um mapa novo não precisa de nada
	/// para aparecer.
	/// </summary>
	private void Scan()
	{
		Node root = GetTree().CurrentScene?.GetNodeOrNull("Map") ?? GetTree().CurrentScene;
		if (root != null) Collect(root);

		// Mais baixo primeiro: o telhado pinta por cima do que está
		// embaixo dele, como visto do alto.
		_pieces.Sort((a, b) => a.Top.CompareTo(b.Top));
	}

	private void Collect(Node node)
	{
		// Corpo de gente não é mapa. Sem isto as garras do lobo, que
		// também são caixas, apareceriam como paredes andando.
		if (node is CharacterBody3D || node is RigidBody3D) return;

		if (node is MeshInstance3D mesh && mesh.Mesh is BoxMesh box)
			AddPiece(mesh, box.Size);

		// Letras curtas no mapa viram marcas no minimapa: é assim que
		// "A" e "B" aparecem sem o minimapa saber o que é uma base.
		if (node is Label3D label && label.Text.Length <= 2)
			_marks.Add((label.GlobalPosition, label.Text, label.Modulate));

		foreach (Node child in node.GetChildren())
			Collect(child);
	}

	private void AddPiece(MeshInstance3D mesh, Vector3 size)
	{
		// O chão cobre o mapa inteiro; desenhá-lo só escureceria tudo.
		if (size.X * size.Z > 1200f) return;

		Vector3 half = size * 0.5f;
		Transform3D xform = mesh.GlobalTransform;

		var corners = new Vector3[]
		{
			xform * new Vector3(-half.X, half.Y, -half.Z),
			xform * new Vector3(half.X, half.Y, -half.Z),
			xform * new Vector3(half.X, half.Y, half.Z),
			xform * new Vector3(-half.X, half.Y, half.Z)
		};

		float top = Mathf.Max(Mathf.Max(corners[0].Y, corners[1].Y), Mathf.Max(corners[2].Y, corners[3].Y));

		Color color = mesh.MaterialOverride is BaseMaterial3D material
			? material.AlbedoColor
			: new Color(0.6f, 0.6f, 0.62f);

		_pieces.Add(new Piece { Corners = corners, Color = color, Top = top });
	}

	public override void _Draw()
	{
		if (_player == null) return;

		var center = new Vector2(Side * 0.5f, Side * 0.5f);
		var frame = new Rect2(Vector2.Zero, new Vector2(Side, Side));

		DrawRect(frame, BackColor);
		DrawRect(frame, EdgeColor, false, 1.5f);

		// Cruz discreta nos eixos: dá referência de distância sem
		// competir com os pontos.
		var faint = new Color(EdgeColor, EdgeColor.A * 0.4f);
		DrawLine(new Vector2(center.X, 0), new Vector2(center.X, Side), faint, 1f);
		DrawLine(new Vector2(0, center.Y), new Vector2(Side, center.Y), faint, 1f);

		// O mundo é girado pelo inverso do olhar: o que está à frente
		// sobe na tela.
		float yaw = _player.Rotation.Y;

		DrawWalls(center, yaw);
		DrawActors(center, yaw);
		DrawSelf(center);
	}

	// ------------------------------------------------------------------

	/// <summary>Converte uma posição do mundo em ponto do minimapa, ou
	/// devolve falso se estiver fora do alcance.</summary>
	private bool ToMap(Vector3 world, Vector2 center, float yaw, out Vector2 point)
	{
		point = Vector2.Zero;

		Vector3 delta = world - _player.GlobalPosition;
		var flat = new Vector2(delta.X, delta.Z);

		if (flat.Length() > Range) return false;

		// Girar o mundo é o oposto de girar a câmera.
		Vector2 rotated = flat.Rotated(yaw);
		point = center + rotated / Range * (Side * 0.5f);

		return true;
	}

	private void DrawWalls(Vector2 center, float yaw)
	{
		float reach = Range * 1.6f;

		foreach (Piece piece in _pieces)
		{
			// Peça inteira longe demais não entra. Perto, entra mesmo com
			// cantos fora do quadro — o recorte do controle corta a sobra.
			Vector3 mid = (piece.Corners[0] + piece.Corners[2]) * 0.5f;
			Vector3 gap = mid - _player.GlobalPosition;
			if (new Vector2(gap.X, gap.Z).Length() > reach + 30f) continue;

			var points = new Vector2[4];
			for (int i = 0; i < 4; i++)
				ToMapUnclamped(piece.Corners[i], center, yaw, out points[i]);

			// Quem está bem acima do jogador aparece mais claro: é o
			// único jeito de um desenho plano dizer "isto é telhado".
			float above = piece.Top - _player.GlobalPosition.Y;
			Color fill = new Color(piece.Color, MapAlpha);
			if (above > 2.5f) fill = fill.Lightened(0.18f);

			DrawColoredPolygon(points, fill);
			DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] },
				new Color(0, 0, 0, 0.45f), 1f);
		}

		Font font = ThemeDB.FallbackFont;

		foreach (var mark in _marks)
		{
			ToMapUnclamped(mark.Position, center, yaw, out Vector2 at);
			if (at.X < -10 || at.Y < -10 || at.X > Side + 10 || at.Y > Side + 10) continue;

			DrawString(font, at + new Vector2(-12, 7), mark.Text, HorizontalAlignment.Center,
				24, 20, new Color(1, 1, 1, 0.95f));
		}
	}

	private void ToMapUnclamped(Vector3 world, Vector2 center, float yaw, out Vector2 point)
	{
		Vector3 delta = world - _player.GlobalPosition;
		Vector2 rotated = new Vector2(delta.X, delta.Z).Rotated(yaw);

		point = center + rotated / Range * (Side * 0.5f);
	}

	private void DrawActors(Vector2 center, float yaw)
	{
		foreach (Node node in GetTree().GetNodesInGroup(Groups.Bots))
		{
			if (node is not Node3D bot) continue;

			var health = bot.GetNodeOrNull<Health>("Health");
			if (health == null || health.IsDead) continue;

			if (!ToMap(bot.GlobalPosition, center, yaw, out Vector2 point)) continue;

			Color color = Teams.ColorOf(health.Team);

			// Altura relativa: quem está acima aparece maior. É a única
			// dica de verticalidade que um mapa de cima consegue dar.
			float height = bot.GlobalPosition.Y - _player.GlobalPosition.Y;
			float size = Mathf.Clamp(4f + height * 0.6f, 2.5f, 7f);

			DrawCircle(point, size, color);
		}
	}

	/// <summary>O jogador é um triângulo no centro, sempre apontando
	/// para cima: ele é a referência, não um ponto entre outros.</summary>
	private void DrawSelf(Vector2 center)
	{
		Color color = _playerHealth != null
			? Teams.ColorOf(_playerHealth.Team)
			: SelfColor;

		var points = new[]
		{
			center + new Vector2(0, -8),
			center + new Vector2(-5.5f, 6),
			center + new Vector2(5.5f, 6)
		};

		DrawColoredPolygon(points, color);
	}
}
