using Godot;
using System.Collections.Generic;

/// <summary>
/// Inferior esquerdo e a tela: vida, escudo, a vinheta de vida baixa e de
/// dano, e o aviso de morte com o relógio de renascer.
/// </summary>
public partial class Hud
{
	/// <summary>Abaixo desta fração de vida, a barra pulsa e a vinheta
	/// vermelha entra.</summary>
	[ExportGroup("Vida")]
	[Export] public float LowHealth = 0.3f;

	private Label _hpNumber;
	private Label _hpMax;
	private SegmentBar _hpBar;
	private SegmentBar _shieldBar;
	private Label _shieldNumber;
	private HBoxContainer _shieldRow;
	private Tween _lowPulse;
	private bool? _lowShown;
	private TextureRect _vignette;
	private float _hurtFlash;
	private VBoxContainer _death;
	private Label _deathBy;
	private Label _deathTimer;

	private void OnHealthChanged(float hp, float maxHp, float shield)
	{
		if (_hpNumber == null) return;

		_hpNumber.Text = $"{Mathf.Ceil(Mathf.Max(hp, 0)):0}";
		_hpMax.Text = $"/ {maxHp:0}";
		_hpBar.Set(Mathf.Max(hp, 0), maxHp);

		_shieldRow.Visible = _health.MaxShield > 0;
		_shieldBar.Set(shield, Mathf.Max(_health.MaxShield, 1f));
		_shieldNumber.Text = $"{Mathf.Ceil(shield):0}";

		// Vida baixa: a barra pulsa e a vinheta entra.
		bool low = !_health.IsDead && maxHp > 0 && hp / maxHp < LowHealth;
		// Os estilos são criados uma vez: a vida muda a cada quadro (o
		// escudo do lobo escorre), e criar um estilo por quadro acumulava
		// objetos do lado C# que derrubavam o jogo ao fechar.
		if (low != _lowShown)
		{
			_lowShown = low;
			_hpBar.Fill = low ? UiTokens.Danger : UiTokens.TextPrimary;
			_hpBar.Trail = low ? UiTokens.Danger.Darkened(0.5f) : UiTokens.Danger;
			_hpBar.QueueRedraw();
			_hpNumber.AddThemeColorOverride("font_color", low ? UiTokens.Danger : UiTokens.TextPrimary);
		}

		if (low && _lowPulse == null) _lowPulse = Motion.Pulse(_hpBar, 0.35f, 0.7f);
		if (!low && _lowPulse != null)
		{
			_lowPulse.Kill();
			_lowPulse = null;
			_hpBar.Modulate = Colors.White;
		}

		UpdateVignette();
	}

	private void OnDamaged(float amount, Vector3 from)
	{
		if (_health.IsDead) return;
		_hurtFlash = Mathf.Min(1f, _hurtFlash + 0.45f);
		UpdateVignette();
	}

	private void UpdateVignette()
	{
		if (_vignette == null) return;
		// A vida baixa agora é do véu de perigo (UpdateDanger); a vinheta
		// simples fica só com o clarão do golpe.
		_vignette.Modulate = new Color(1, 1, 1, Mathf.Clamp(_hurtFlash * 0.6f, 0, 1));
	}

	// ------------------------------------------------------------ quase morrendo

	/// <summary>Abaixo desta fração de vida a tela começa a avermelhar;
	/// em <see cref="LowHealth"/> entra a respiração.</summary>
	[Export] public float DangerFrom = 0.55f;

	/// <summary>Volume da respiração ofegante, do começo ao quase morto.</summary>
	[Export] public float BreathQuietDb = -20f;
	[Export] public float BreathLoudDb = -5f;

	private const string DangerCode = @"
shader_type canvas_item;
uniform float danger = 0.0;
uniform float beat = 0.0;
uniform float flash = 0.0;
uniform sampler2D grain : repeat_enable, filter_linear;

void fragment() {
	vec2 p = UV - 0.5;
	float d = length(p * vec2(1.0, 1.15)) * 1.55;
	float n = texture(grain, UV * vec2(1.7, 1.0) + vec2(0.0, TIME * 0.012)).r;
	float n2 = texture(grain, UV * vec2(3.1, 2.3) - vec2(TIME * 0.02, 0.0)).r;

	// A borda vermelha fecha com o perigo e pulsa com o coração.
	float inner = mix(1.02, 0.34, danger) - beat * 0.07 * danger;
	float edge = smoothstep(inner, inner + 0.5, d + (n - 0.5) * 0.4 * danger);

	// Veios escuros na borda, como sangue na vista.
	float veins = smoothstep(0.52, 0.8, n2) * smoothstep(inner + 0.05, inner + 0.6, d) * danger;

	// O golpe: um clarão vermelho na tela toda, mais forte na borda.
	float hit = flash * (0.14 + 0.55 * smoothstep(0.25, 1.0, d));

	float a = edge * (0.3 + 0.62 * danger) + veins * 0.35 + hit + beat * danger * 0.05;
	vec3 c = mix(vec3(0.5, 0.0, 0.02), vec3(1.0, 0.06, 0.07), clamp(beat * 0.6 * danger + flash * 0.7, 0.0, 1.0));
	c = mix(c, vec3(0.16, 0.0, 0.0), veins * 0.8);
	COLOR = vec4(c, clamp(a, 0.0, 0.93));
}";

	private ColorRect _danger;
	private ShaderMaterial _dangerMaterial;
	private AudioStreamPlayer _breath;
	private float _dangerShown, _beatClock;

	private void BuildDanger()
	{
		var noise = new NoiseTexture2D
		{
			Width = 256, Height = 256, Seamless = true,
			Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = 0.03f, Seed = 7 }
		};
		_dangerMaterial = new ShaderMaterial { Shader = new Shader { Code = DangerCode } };
		_dangerMaterial.SetShaderParameter("grain", noise);
		_danger = UiKit.Fill(new ColorRect { Color = Colors.White, Material = _dangerMaterial, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false });
		_root.AddChild(_danger);
		_root.MoveChild(_danger, _vignette.GetIndex() + 1);
	}

	/// <summary>O perigo de agora, de 0 (inteiro) a 1 (quase morto). Para os testes.</summary>
	public float Danger => _dangerShown;
	public bool Breathing => _breath != null && IsInstanceValid(_breath) && _breath.Playing;

	/// <summary>
	/// Quase morrendo (pedido do Jhon, 18/11: "quando eu levar dano e
	/// estiver morrendo, quero mais sinais vermelhos na tela; também um
	/// barulho de respiração com falta de ar"): a borda da tela avermelha
	/// e fecha conforme a vida cai, com veios escuros, pulsando no ritmo de
	/// um coração que acelera; cada golpe dá um clarão vermelho; e, de
	/// <see cref="LowHealth"/> para baixo, a respiração ofegante, mais
	/// alta quanto menos vida.
	/// </summary>
	private void UpdateDanger(float delta)
	{
		if (_danger == null) return;

		float frac = _health.IsDead || _health.MaxHp <= 0 ? 1f : _health.Hp / _health.MaxHp;
		float target = Mathf.Clamp((DangerFrom - frac) / Mathf.Max(DangerFrom, 0.01f), 0f, 1f);
		// Sobe depressa (apanhou), desce devagar (a vida voltando).
		_dangerShown = Mathf.MoveToward(_dangerShown, target, delta * (target > _dangerShown ? 3f : 0.6f));

		bool on = _dangerShown > 0.005f || _hurtFlash > 0.01f;
		if (on != _danger.Visible) _danger.Visible = on;
		if (on)
		{
			_beatClock += delta * Mathf.Lerp(1.15f, 2.3f, _dangerShown);
			float phase = _beatClock - Mathf.Floor(_beatClock);
			float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * Mathf.Tau)), 6f)
				+ 0.6f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin((phase - 0.2f) * Mathf.Tau)), 6f);
			_dangerMaterial.SetShaderParameter("danger", _dangerShown);
			_dangerMaterial.SetShaderParameter("beat", Mathf.Min(beat, 1f));
			_dangerMaterial.SetShaderParameter("flash", _hurtFlash);
		}

		// A respiração: entra abaixo de LowHealth, com um pouco de folga para sair.
		bool gasping = !_health.IsDead && frac < (Breathing ? LowHealth + 0.08f : LowHealth) && !UiState.AnyOpen;
		if (gasping && !Breathing)
		{
			_breath = Sfx.Ambience(this, "respiracao_ofegante", -40f);
		}
		if (_breath != null && IsInstanceValid(_breath))
		{
			float wanted = gasping ? Mathf.Lerp(BreathQuietDb, BreathLoudDb, Mathf.Clamp(1f - frac / Mathf.Max(LowHealth, 0.01f), 0f, 1f)) : -50f;
			_breath.VolumeDb = Mathf.MoveToward(_breath.VolumeDb, wanted, delta * (gasping ? 40f : 22f));
			if (!gasping && _breath.VolumeDb <= -49f)
			{
				_breath.QueueFree();
				_breath = null;
			}
		}
	}

	private void OnDied(int killerId)
	{
		_death.Visible = true;
		_warning.Text = "";
		_warningTimer = 0;
		_crosshair.Visible = false;
		Motion.FadeIn(_death, UiTokens.Normal);
	}

	private void OnRespawned()
	{
		_death.Visible = false;
		_crosshair.Visible = true;
		_hurtFlash = 0;
		UpdateVignette();
	}
}
