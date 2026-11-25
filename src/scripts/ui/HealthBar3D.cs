using Godot;

/// <summary>
/// Barra de vida flutuando sobre um corpo — fina, inclinada e discreta
/// (pedido do Jhon, 18/11: "mais estilizada e mais discreta, e só aparece
/// ao atirar; a do aliado é branca; deve mostrar o escudo também").
///
/// **Só aparece quando interessa**: no inimigo, quando quem joga nesta
/// máquina acerta ele; no aliado, quando ele apanha. Fica
/// <see cref="ShowTime"/> segundos depois do último golpe e some num fade.
/// Barra em todo mundo o tempo todo era ruído e entregava quem estava
/// parado num canto.
///
/// O desenho é um quadro só, com shader: a vida embaixo, repartida em
/// gomos de <see cref="SegmentHp"/> pontos (vermelha no inimigo, branca no
/// aliado), o escudo numa faixa fina em cima (azul-claro), e o "fantasma"
/// do dano — o pedaço que acabou de sair fica claro por um instante e
/// encolhe, que é o que mostra o tamanho do golpe.
///
/// Fica no mundo e não no HUD: a informação pertence ao alvo. Sempre de
/// frente para a câmera, com teste de profundidade ligado — barra visível
/// através da parede entregaria posição de graça.
/// </summary>
public partial class HealthBar3D : Node3D
{
	[Export] public float Width = 0.78f;
	[Export] public float Height = 0.085f;
	[Export] public float HeightAbove = 1.32f;

	/// <summary>Além desta distância a barra não aparece. Zero: sem limite
	/// (Jhon, 25/11: a vida do inimigo aparece a qualquer distância).</summary>
	[Export] public float VisibleRange = 0f;

	/// <summary>Até esta distância a barra tem o tamanho de sempre; além
	/// dela cresce com a distância para continuar legível de longe, mas só
	/// até <see cref="MaxScale"/>, senão vira um letreiro (Jhon, 25/11).</summary>
	[Export] public float GrowFrom = 12f;
	[Export] public float MaxScale = 3.2f;

	/// <summary>Segundos na tela depois do último golpe, e o fade.</summary>
	[Export] public float ShowTime = 2.4f;
	[Export] public float FadeTime = 0.35f;

	/// <summary>Pontos de vida por gomo.</summary>
	[Export] public float SegmentHp = 25f;

	/// <summary>Inimigo: vermelha. Aliado: branca.</summary>
	[Export] public Color HpColor = new Color("ff3b3f");
	[Export] public Color AllyColor = new Color("f4f6fa");
	[Export] public Color ShieldColor = new Color("6fd3ff");

	private const string Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_never;

uniform vec4 fill : source_color = vec4(1.0, 0.23, 0.25, 1.0);
uniform vec4 shield_color : source_color = vec4(0.44, 0.83, 1.0, 1.0);
uniform float hp = 1.0;
uniform float ghost = 1.0;
uniform float shield = 0.0;
uniform float has_shield = 0.0;
uniform float segments = 8.0;
uniform float fade = 1.0;
uniform float skew = 0.22;

void vertex() {
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	// Paralelogramo: o topo anda para a direita.
	float x = (UV.x - (0.5 - UV.y) * skew * 0.12 - 0.06 * skew) / (1.0 - 0.12 * skew);
	if (x < 0.0 || x > 1.0) discard;
	float y = 1.0 - UV.y;

	vec3 color = vec3(0.02, 0.025, 0.035);
	float alpha = 0.0;

	// A vida: os 58% de baixo.
	if (y < 0.58) {
		float cell = fract(x * segments);
		float gap = step(cell, 0.045) + step(0.955, cell);
		alpha = 0.55;
		if (x <= hp) { color = fill.rgb; alpha = 0.95; }
		else if (x <= ghost) { color = mix(fill.rgb, vec3(1.0), 0.75); alpha = 0.85; }
		if (gap > 0.0 && x > 0.01 && x < 0.99) { color = vec3(0.02, 0.025, 0.035); alpha = 0.55; }
	}
	// O escudo: uma faixa fina em cima, com um respiro no meio.
	else if (y > 0.70 && has_shield > 0.5) {
		alpha = 0.45;
		if (x <= shield) { color = shield_color.rgb; alpha = 0.95; }
	}

	ALBEDO = color;
	ALPHA = alpha * fade;
}";

	private static Shader _shader;

	private Health _health;
	private ShaderMaterial _material;
	private int _shownAlly = -1;
	private double _colorCheck;
	private float _shownFor;
	private float _ghost = 1f, _ghostHold;
	private float _lastTotal = -1f;

	public override void _Ready()
	{
		_health = GetParent().GetNode<Health>("Health");
		Position = new Vector3(0, HeightAbove, 0);

		_shader ??= new Shader { Code = Code };
		_material = new ShaderMaterial { Shader = _shader };
		AddChild(new MeshInstance3D
		{
			Mesh = new QuadMesh { Size = new Vector2(Width, Height) },
			MaterialOverride = _material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			// O quadro gira no shader: sem folga, some na borda da tela.
			ExtraCullMargin = 1f
		});

		UpdateColor();
		_health.Changed += OnChanged;
		_health.Respawned += OnRespawned;
		OnChanged(_health.Hp, _health.MaxHp, _health.Shield);
		Visible = false;
	}

	/// <summary>O jogador desta máquina (o corpo), ou null.</summary>
	private Player LocalPlayer()
	{
		foreach (Node node in GetTree().GetNodesInGroup(Groups.Players))
			if (node is Player player && player.IsMultiplayerAuthority())
				return player;
		return null;
	}

	private int LocalTeam() =>
		LocalPlayer()?.GetNodeOrNull<Health>("Health") is Health health ? health.Team : RoomConfig.PlayerTeam;

	private bool IsAlly => _health.Team == LocalTeam();

	private void UpdateColor()
	{
		int ally = IsAlly ? 1 : 0;
		if (ally == _shownAlly) return;
		_shownAlly = ally;
		_material.SetShaderParameter("fill", ally == 1 ? AllyColor : HpColor);
		_material.SetShaderParameter("shield_color", ShieldColor);
	}

	/// <summary>A barra está na tela agora? Para os testes.</summary>
	public bool Showing => Visible && _shownFor > 0f;

	public override void _Process(double delta)
	{
		// O jogador local pode nascer depois da barra: confere de vez em quando.
		_colorCheck -= delta;
		if (_colorCheck <= 0)
		{
			_colorCheck = 0.5;
			UpdateColor();
		}

		if (_shownFor > 0f) _shownFor -= (float)delta;

		// O fantasma do dano espera um instante e encolhe até a vida.
		float hp = _health.MaxHp > 0 ? _health.Hp / _health.MaxHp : 0f;
		if (_ghost > hp)
		{
			_ghostHold -= (float)delta;
			if (_ghostHold <= 0f) _ghost = Mathf.MoveToward(_ghost, hp, (float)delta * 0.9f);
			_material.SetShaderParameter("ghost", _ghost);
		}

		Camera3D camera = GetViewport().GetCamera3D();
		bool on = _shownFor > 0f && camera != null && !_health.IsDead
			&& (VisibleRange <= 0f || camera.GlobalPosition.DistanceTo(GlobalPosition) <= VisibleRange);
		if (on != Visible) Visible = on;
		if (on)
		{
			float dist = camera.GlobalPosition.DistanceTo(GlobalPosition);
			Scale = Vector3.One * Mathf.Clamp(dist / Mathf.Max(GrowFrom, 0.01f), 1f, MaxScale);
		}
		if (on) _material.SetShaderParameter("fade", Mathf.Clamp(_shownFor / Mathf.Max(FadeTime, 0.01f), 0f, 1f));
	}

	private void OnRespawned()
	{
		_shownFor = 0f;
		_ghost = 1f;
		_lastTotal = _health.Hp + _health.Shield;
	}

	private void OnChanged(float hp, float maxHp, float shield)
	{
		float ratio = maxHp > 0 ? Mathf.Clamp(hp / maxHp, 0f, 1f) : 0f;
		_material.SetShaderParameter("hp", ratio);
		_material.SetShaderParameter("segments", Mathf.Max(1f, maxHp / Mathf.Max(SegmentHp, 1f)));
		_material.SetShaderParameter("has_shield", _health.MaxShield > 0 ? 1f : 0f);
		_material.SetShaderParameter("shield", _health.MaxShield > 0 ? Mathf.Clamp(shield / _health.MaxShield, 0f, 1f) : 0f);

		float total = hp + shield;
		bool hurt = _lastTotal >= 0f && total < _lastTotal - 0.01f;
		_lastTotal = total;

		if (ratio > _ghost) _ghost = ratio;
		if (!hurt) return;
		_ghostHold = 0.35f;

		// Aliado: apanhou, aparece. Inimigo: só se o golpe foi de quem joga aqui.
		if (IsAlly || (_health.LastAttacker == LocalPlayer() && _health.LastAttacker != null && _health.SinceLastAttack < 1.0))
			_shownFor = ShowTime;
	}
}
