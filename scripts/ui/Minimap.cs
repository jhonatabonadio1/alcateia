using Godot;

/// <summary>
/// Minimapa no canto superior esquerdo.
///
/// Desenhado em 2D a partir das posições do mundo, não por câmera
/// ortogonal numa viewport. A diferença não é só custo: uma câmera de
/// cima desenharia o chão, as sombras e a iluminação, e nada disso
/// ajuda a ler posição. Um minimapa é um diagrama, não uma foto.
///
/// **Fixo e cheio (decisão do Jhon, 17/11):** o norte do mapa fica para
/// cima, quem gira é a seta do jogador, e o mapa ocupa o quadrado
/// inteiro. A janela tem o lado menor do mapa (32 m no Armazém), segue o
/// jogador e para na borda do mapa — perto de um canto a seta sai do
/// centro, mas o quadrado nunca fica vazio. Antes ele girava com o
/// jogador centrado, e no canto de nascer três quartos do quadrado eram
/// fundo. <see cref="Rotate"/> volta ao jeito antigo.
///
/// Só mostra quem está dentro do alcance. Saber onde está todo mundo
/// o tempo todo destruiria a razão de existir do agachar, do andar
/// devagar e da audição dos bots.
/// </summary>
public partial class Minimap : Control
{
	/// <summary>Até onde se vê os outros, em metros a partir do
	/// jogador. No modo girando, também é meio lado do quadrado.</summary>
	[Export] public float Range = 30f;

	/// <summary>O jeito antigo: o mundo gira em volta do jogador, que fica
	/// no centro apontando para cima.</summary>
	[Export] public bool Rotate = false;

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

		/// <summary>Contorno escuro em volta: só nas peças grandes. Nas
		/// pequenas ele cobriria a cor.</summary>
		public bool Outline;
	}

	private readonly System.Collections.Generic.List<Piece> _pieces = new();

	/// <summary>O retângulo do mapa visto de cima (x, z), das peças lidas.</summary>
	private Rect2 _bounds;

	// A janela deste quadro: o ponto do mundo no centro do quadrado, meio
	// lado em metros e o giro.
	private Vector2 _viewCenter;
	private float _viewHalf = 30f;
	private float _viewYaw;
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
	///
	/// Mapa feito de modelo pronto (o Armazém) não tem caixa desenhada:
	/// o que se vê é um GLB, e as caixas são só colisão. Essas entram
	/// também, pela forma de colisão, com a cor que o mapa gravou na
	/// caixa (<c>metadata/mapa_cor</c>, a cor da peça no modelo). Sem
	/// isso o minimapa do Armazém ficava vazio (print do Jhon, 17/11).
	/// </summary>
	private void Scan()
	{
		Node root = GetTree().CurrentScene?.GetNodeOrNull("Map") ?? GetTree().CurrentScene;
		if (root != null) Collect(root);

		// Mais baixo primeiro: o telhado pinta por cima do que está
		// embaixo dele, como visto do alto.
		_pieces.Sort((a, b) => a.Top.CompareTo(b.Top));

		bool first = true;
		foreach (Piece piece in _pieces)
			foreach (Vector3 c in piece.Corners)
			{
				var at = new Vector2(c.X, c.Z);
				_bounds = first ? new Rect2(at, Vector2.Zero) : _bounds.Expand(at);
				first = false;
			}
	}

	private void Collect(Node node)
	{
		// Corpo de gente não é mapa. Sem isto as garras do lobo, que
		// também são caixas, apareceriam como paredes andando.
		if (node is CharacterBody3D || node is RigidBody3D) return;

		if (node is MeshInstance3D mesh && mesh.Mesh is BoxMesh box)
			AddPiece(mesh.GlobalTransform, box.Size, mesh.MaterialOverride is BaseMaterial3D material
				? material.AlbedoColor
				: DefaultPiece);
		else if (node is CollisionShape3D { Shape: BoxShape3D shape } hidden && !hidden.Disabled
			&& hidden.GetParent() is StaticBody3D body && !HasDrawnBox(body))
			AddPiece(hidden.GlobalTransform, shape.Size, body.HasMeta(ColorMeta)
				? body.GetMeta(ColorMeta).AsColor()
				: DefaultPiece);

		// Letras curtas no mapa viram marcas no minimapa: é assim que
		// "A" e "B" aparecem sem o minimapa saber o que é uma base.
		if (node is Label3D label && label.Text.Length <= 2)
			_marks.Add((label.GlobalPosition, label.Text, label.Modulate));

		foreach (Node child in node.GetChildren())
			Collect(child);
	}

	/// <summary>Onde o mapa grava a cor da caixa só de colisão
	/// (<c>kit.caixa(..., cor=)</c> em tools/mapas).</summary>
	private const string ColorMeta = "mapa_cor";

	private static readonly Color DefaultPiece = new Color(0.6f, 0.6f, 0.62f);

	/// <summary>Menor área vista de cima que entra, em m²: garrafa, lata
	/// e ferramenta solta só sujariam um quadro de 168 px.</summary>
	[Export] public float SmallestPiece = 0.12f;

	/// <summary>A caixa já tem malha de caixa ao lado da colisão? Então
	/// ela entrou pela malha, com a cor do material.</summary>
	private static bool HasDrawnBox(Node body)
	{
		foreach (Node child in body.GetChildren())
			if (child is MeshInstance3D { Mesh: BoxMesh }) return true;
		return false;
	}

	private void AddPiece(Transform3D xform, Vector3 size, Color color)
	{
		// O chão cobre o mapa inteiro; desenhá-lo só escureceria tudo.
		if (size.X * size.Z > 1200f) return;

		Vector3 half = size * 0.5f;

		var corners = new Vector3[]
		{
			xform * new Vector3(-half.X, half.Y, -half.Z),
			xform * new Vector3(half.X, half.Y, -half.Z),
			xform * new Vector3(half.X, half.Y, half.Z),
			xform * new Vector3(-half.X, half.Y, half.Z)
		};

		float top = Mathf.Max(Mathf.Max(corners[0].Y, corners[1].Y), Mathf.Max(corners[2].Y, corners[3].Y));

		// Caixa deitada de lado ou em pé (uma tábua encostada, uma porta
		// aberta): o que conta é a sombra dela no chão, não o tamanho.
		Vector2 a = new Vector2(corners[1].X - corners[0].X, corners[1].Z - corners[0].Z);
		Vector2 b = new Vector2(corners[3].X - corners[0].X, corners[3].Z - corners[0].Z);
		float footprint = Mathf.Abs(a.Cross(b));
		if (footprint < SmallestPiece) return;

		_pieces.Add(new Piece { Corners = corners, Color = color, Top = top, Outline = footprint >= 0.6f });
	}

	public override void _Draw()
	{
		if (_player == null) return;

		var frame = new Rect2(Vector2.Zero, new Vector2(Side, Side));

		DrawRect(frame, BackColor);
		DrawRect(frame, EdgeColor, false, 1.5f);

		var me = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);
		if (Rotate || _bounds.Size.X < 1f || _bounds.Size.Y < 1f)
		{
			// O mundo é girado pelo inverso do olhar: o que está à frente
			// sobe na tela.
			_viewCenter = me;
			_viewHalf = Range;
			_viewYaw = _player.Rotation.Y;
		}
		else
		{
			// O lado menor do mapa enche o quadrado; a janela segue o
			// jogador e para na borda.
			_viewHalf = Mathf.Min(Mathf.Min(_bounds.Size.X, _bounds.Size.Y) * 0.5f, Range);
			_viewCenter = new Vector2(Follow(me.X, _bounds.Position.X, _bounds.End.X),
				Follow(me.Y, _bounds.Position.Y, _bounds.End.Y));
			_viewYaw = 0f;
		}

		// Cruz discreta passando pelo jogador: dá referência sem competir
		// com os pontos.
		Vector2 self = ToMapUnclamped(_player.GlobalPosition);
		var faint = new Color(EdgeColor, EdgeColor.A * 0.4f);
		DrawLine(new Vector2(self.X, 0), new Vector2(self.X, Side), faint, 1f);
		DrawLine(new Vector2(0, self.Y), new Vector2(Side, self.Y), faint, 1f);

		DrawWalls();
		DrawActors();
		DrawSelf(self, Rotate ? 0f : _player.Rotation.Y);
	}

	/// <summary>O centro da janela num eixo: o jogador, sem deixar a
	/// janela passar da borda do mapa.</summary>
	private float Follow(float at, float min, float max)
	{
		if (max - min <= _viewHalf * 2f) return (min + max) * 0.5f;
		return Mathf.Clamp(at, min + _viewHalf, max - _viewHalf);
	}

	// ------------------------------------------------------------------

	/// <summary>Converte uma posição do mundo em ponto do minimapa, ou
	/// devolve falso se estiver longe demais do jogador para ser visto.</summary>
	private bool ToMap(Vector3 world, out Vector2 point)
	{
		point = Vector2.Zero;

		Vector3 delta = world - _player.GlobalPosition;
		if (new Vector2(delta.X, delta.Z).Length() > Range) return false;

		point = ToMapUnclamped(world);
		return true;
	}

	private void DrawWalls()
	{
		float reach = _viewHalf * 1.6f;
		var view = new Vector3(_viewCenter.X, _player.GlobalPosition.Y, _viewCenter.Y);

		foreach (Piece piece in _pieces)
		{
			// Peça inteira longe demais não entra. Perto, entra mesmo com
			// cantos fora do quadro — o recorte do controle corta a sobra.
			Vector3 mid = (piece.Corners[0] + piece.Corners[2]) * 0.5f;
			Vector3 gap = mid - view;
			if (new Vector2(gap.X, gap.Z).Length() > reach + 30f) continue;

			// Os mesmos dois vetores para todas as peças: o Armazém tem
			// centenas, e um par novo por peça por quadro era lixo para o
			// coletor. O desenho copia os pontos na chamada.
			Vector2[] points = _quad;
			bool inside = false;
			for (int i = 0; i < 4; i++)
			{
				points[i] = ToMapUnclamped(piece.Corners[i]);
				inside |= points[i].X > -Side && points[i].X < Side * 2f && points[i].Y > -Side && points[i].Y < Side * 2f;
			}
			if (!inside) continue;

			// Quem está bem acima do jogador aparece mais claro: é o
			// único jeito de um desenho plano dizer "isto é telhado".
			float above = piece.Top - _player.GlobalPosition.Y;
			Color fill = new Color(piece.Color, MapAlpha);
			if (above > 2.5f) fill = fill.Lightened(0.18f);

			// Quadrilátero direto, sem triangulação: uma tábua vista de
			// quina vira um risco de área quase zero, e o polígono comum
			// falharia nela com um erro por quadro.
			_fill[0] = fill;
			DrawPrimitive(points, _fill, _noUv);
			if (!piece.Outline) continue;

			for (int i = 0; i < 4; i++) _loop[i] = points[i];
			_loop[4] = points[0];
			DrawPolyline(_loop, new Color(0, 0, 0, 0.45f), 1f);
		}

		Font font = ThemeDB.FallbackFont;

		foreach (var mark in _marks)
		{
			Vector2 at = ToMapUnclamped(mark.Position);
			if (at.X < -10 || at.Y < -10 || at.X > Side + 10 || at.Y > Side + 10) continue;

			DrawString(font, at + new Vector2(-12, 7), mark.Text, HorizontalAlignment.Center,
				24, 20, new Color(1, 1, 1, 0.95f));
		}
	}

	private readonly Vector2[] _quad = new Vector2[4];
	private readonly Vector2[] _loop = new Vector2[5];
	private readonly Color[] _fill = new Color[1];
	private static readonly Vector2[] _noUv = System.Array.Empty<Vector2>();

	/// <summary>Ponto do minimapa de uma posição do mundo, dentro ou fora
	/// do quadrado. Girar o mundo é o oposto de girar a câmera.</summary>
	private Vector2 ToMapUnclamped(Vector3 world)
	{
		var flat = new Vector2(world.X - _viewCenter.X, world.Z - _viewCenter.Y);
		if (_viewYaw != 0f) flat = flat.Rotated(_viewYaw);
		return new Vector2(Side * 0.5f, Side * 0.5f) + flat / _viewHalf * (Side * 0.5f);
	}

	private void DrawActors()
	{
		foreach (Node node in GetTree().GetNodesInGroup(Groups.Bots))
		{
			if (node is not Node3D bot) continue;

			var health = bot.GetNodeOrNull<Health>("Health");
			if (health == null || health.IsDead) continue;

			if (!ToMap(bot.GlobalPosition, out Vector2 point)) continue;

			Color color = Teams.ColorOf(health.Team);

			// Altura relativa: quem está acima aparece maior. É a única
			// dica de verticalidade que um mapa de cima consegue dar.
			float height = bot.GlobalPosition.Y - _player.GlobalPosition.Y;
			float size = Mathf.Clamp(4f + height * 0.6f, 2.5f, 7f);

			DrawCircle(point, size, color);
		}
	}

	/// <summary>O jogador é um triângulo apontando para onde ele olha
	/// (no modo girando, sempre para cima, no centro): ele é a referência,
	/// não um ponto entre outros.</summary>
	private void DrawSelf(Vector2 center, float yaw)
	{
		Color color = _playerHealth != null
			? Teams.ColorOf(_playerHealth.Team)
			: SelfColor;

		// O olhar do jogador é -Z girado pelo yaw; no quadrado, z cresce
		// para baixo, então girar pelo inverso aponta a seta certa.
		var points = new[]
		{
			center + new Vector2(0, -8).Rotated(-yaw),
			center + new Vector2(-5.5f, 6).Rotated(-yaw),
			center + new Vector2(5.5f, 6).Rotated(-yaw)
		};

		DrawColoredPolygon(points, color);
	}
}
