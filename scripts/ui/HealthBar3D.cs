using Godot;

/// <summary>
/// Barra de vida flutuando sobre um corpo.
///
/// Fica no mundo e não no HUD: a informação pertence ao alvo, então
/// acompanha o alvo. Billboard para sempre encarar a câmera, e com
/// teste de profundidade ligado — uma barra visível através da parede
/// entregaria posição de graça.
/// </summary>
public partial class HealthBar3D : Node3D
{
	[Export] public float Width = 1.1f;
	[Export] public float Height = 0.11f;
	[Export] public float HeightAbove = 1.35f;

	/// <summary>Além desta distância a barra some: poluição visual em
	/// alvo que o jogador nem está enfrentando.</summary>
	[Export] public float VisibleRange = 45f;

	[Export] public Color BackColor = new Color(0.05f, 0.05f, 0.06f, 0.75f);
	/// <summary>Inimigo: sempre vermelha.</summary>
	[Export] public Color HpColor = new Color(0.9f, 0.2f, 0.15f);

	/// <summary>Aliado: sempre verde.</summary>
	[Export] public Color AllyColor = new Color(0.25f, 0.85f, 0.35f);
	[Export] public Color ShieldColor = new Color(1f, 0.55f, 0.15f);

	private Health _health;
	private MeshInstance3D _hpFill;
	private MeshInstance3D _shieldFill;
	private StandardMaterial3D _hpMaterial;
	private int _shownAlly = -1;
	private double _colorCheck;

	public override void _Ready()
	{
		_health = GetParent().GetNode<Health>("Health");

		Position = new Vector3(0, HeightAbove, 0);

		// Verde no aliado, vermelha no inimigo, sempre (decisão do Jhon,
		// 17/11) — para quem olha, seja qual for o time dele. A cor do
		// time (azul, laranja) continua no corpo e no placar.
		Make(BackColor, Width, Height, 0f, out _);
		Make(HpColor, Width, Height, 0.001f, out _hpFill);
		_hpMaterial = (StandardMaterial3D)_hpFill.MaterialOverride;
		UpdateColor();
		Make(ShieldColor, Width, Height * 0.55f, 0.002f, out _shieldFill);

		_shieldFill.Position = new Vector3(0, -Height * 0.85f, 0.002f);

		_health.Changed += OnChanged;
		OnChanged(_health.Hp, _health.MaxHp, _health.Shield);
	}

	/// <summary>O time de quem está jogando nesta máquina: o do jogador
	/// local na cena; sem ele (ainda), o da sala.</summary>
	private int LocalTeam()
	{
		foreach (Node node in GetTree().GetNodesInGroup(Groups.Players))
			if (node is Player player && player.GetNodeOrNull<Health>("Health") is Health health)
				return health.Team;
		return RoomConfig.PlayerTeam;
	}

	private void UpdateColor()
	{
		int ally = _health.Team == LocalTeam() ? 1 : 0;
		if (ally == _shownAlly) return;
		_shownAlly = ally;
		_hpMaterial.AlbedoColor = ally == 1 ? AllyColor : HpColor;
	}

	public override void _Process(double delta)
	{
		// O jogador local pode nascer depois da barra: confere de vez em quando.
		_colorCheck -= delta;
		if (_colorCheck <= 0)
		{
			_colorCheck = 0.5;
			UpdateColor();
		}

		Camera3D camera = GetViewport().GetCamera3D();

		Visible = camera != null
			&& !_health.IsDead
			&& camera.GlobalPosition.DistanceTo(GlobalPosition) <= VisibleRange;
	}

	private void Make(Color color, float width, float height, float depth, out MeshInstance3D node)
	{
		node = new MeshInstance3D
		{
			Mesh = new QuadMesh { Size = new Vector2(width, height) },
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = color,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
				BillboardKeepScale = true
			},
			Position = new Vector3(0, 0, depth)
		};

		AddChild(node);
	}

	private void OnChanged(float hp, float maxHp, float shield)
	{
		// Encolhe pela esquerda: a escala sozinha encolheria dos dois
		// lados e a barra pareceria flutuar no meio.
		Fill(_hpFill, maxHp > 0 ? hp / maxHp : 0f);
		Fill(_shieldFill, _health.MaxShield > 0 ? shield / _health.MaxShield : 0f);

		_shieldFill.Visible = _health.MaxShield > 0 && shield > 0;
	}

	private void Fill(MeshInstance3D node, float ratio)
	{
		ratio = Mathf.Clamp(ratio, 0f, 1f);

		node.Scale = new Vector3(Mathf.Max(ratio, 0.0001f), 1, 1);

		Vector3 position = node.Position;
		position.X = -Width * (1f - ratio) * 0.5f;
		node.Position = position;
	}
}
