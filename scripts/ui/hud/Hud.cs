using Godot;
using System.Collections.Generic;

/// <summary>
/// O HUD de combate, pela regra dos cantos (docs/UI.md, "HUD de combate"):
///
/// | Canto | O quê |
/// |---|---|
/// | Superior esquerdo | Minimapa e nome do mapa |
/// | Superior centro | Placar e relógio (MatchHud) |
/// | Superior direito | Feed de abates |
/// | Centro | Mira, marcador de acerto, dano direcional, avisos |
/// | Inferior esquerdo | Vida, escudo e a forma atual |
/// | Inferior centro | Barra de transformação |
/// | Inferior direito | Arma, munição, granadas, slots e WP — ou, no lobo, as recargas dos ataques |
///
/// O centro fica livre: só mira e avisos críticos.
///
/// **O HUD troca de forma.** Humano mostra munição, pente e granadas;
/// lobo mostra a recarga da garrada, do salto, do bote e o uivo, e
/// nenhuma munição. São dois painéis distintos com transição, não dez
/// nós ligando e desligando.
///
/// Tudo chega por sinal (vida, munição, WP, forma, abates). Só o que é
/// contínuo por natureza — andamento da transformação, recarga de ataque
/// do lobo, uivo, relógio de renascer — é lido a cada quadro.
///
/// Nada aqui escala com o FOV nem balança com a câmera: CanvasLayer.
///
/// Um arquivo por canto: Hud.Layout monta tudo uma vez; Hud.Vitals,
/// Hud.Equipment, Hud.Form e Hud.Feed reagem aos sinais do seu canto.
/// </summary>
public partial class Hud : CanvasLayer
{
	private const int Edge = UiTokens.S5;
	private const int MinimapSide = 200;

	private Health _health;
	private FormController _form;
	private Weapon _weapon;
	private Player _player;
	private Control _root;

	// O centro: mira, marcador de acerto e avisos.
	private Crosshair _crosshair;
	private HitMarker _hitMarker;
	private Label _warning;
	private double _warningTimer;

	public override void _Ready()
	{
		Node player = Groups.LocalPlayer(GetTree()) ?? Nodes.FindFirst<Player>(GetTree().CurrentScene);
		if (player == null)
		{
			GD.PrintErr("Hud: nenhum Player encontrado na cena.");
			return;
		}

		_player = player as Player;
		_health = player.GetNode<Health>("Health");
		_form = player.GetNode<FormController>("FormController");
		_weapon = player.GetNode<Weapon>("Weapon");

		_root = UiTheme.Apply(UiKit.Fill(new Control { MouseFilter = Control.MouseFilterEnum.Ignore }));
		AddChild(_root);
		// O DamageIndicator da cena fica por cima do resto.
		MoveChild(_root, 0);

		BuildVignette();
		BuildCenter();
		BuildMinimap();
		BuildFeed();
		BuildVitals();
		BuildEquipment();
		BuildTransform();

		_health.Changed += OnHealthChanged;
		_health.Damaged += OnDamaged;
		_health.Died += OnDied;
		_health.Respawned += OnRespawned;
		_form.WpChanged += OnWpChanged;
		_form.FormChanged += OnFormChanged;
		_form.TransformDenied += OnTransformDenied;
		_weapon.SlotChanged += OnSlotChanged;
		_weapon.GrenadesChanged += OnGrenadesChanged;
		_weapon.HitConfirmed += OnHitConfirmed;
		_weapon.AmmoChanged += OnAmmoChanged;
		_weapon.AttackCooldown += OnAttackCooldown;
		_weapon.PickedUp += id => Warn($"Pegou: {Arsenal.NameOf(id)}", 1.6f, UiTokens.Success);
		_weapon.Dropped += id =>
		{
			if (!_health.IsDead) Warn($"Soltou: {Arsenal.NameOf(id)}", 1.2f, UiTokens.TextSecondary);
		};
		KillFeed.Reported += OnKill;

		// No fim da partida a tela de resultado assume a tela inteira.
		if (Match.Current != null)
			Match.Current.PhaseChanged += phase => Visible = (MatchPhase)phase != MatchPhase.Ended;

		OnHealthChanged(_health.Hp, _health.MaxHp, _health.Shield);
		OnWpChanged(_form.Wp, _form.MaxWp);
		OnSlotChanged((int)_weapon.CurrentSlot);
		OnGrenadesChanged(_weapon.Grenades);
		OnAmmoChanged(_weapon.PrimaryAmmo, _weapon.PrimaryMag, false);
		OnFormChanged((int)_form.Current, animate: false);
	}

	public override void _ExitTree() => KillFeed.Reported -= OnKill;

	public override void _Process(double delta)
	{
		if (_health == null) return;

		// Morto com a escolha de conjunto aberta: ela já diz "Abatido" e
		// de quê. Aqui só aparece depois de escolher, enquanto o tempo
		// mínimo para renascer não passou.
		_death.Visible = _health.IsDead && !UiState.AnyOpen;
		if (_death.Visible)
			_deathTimer.Text = _health.RespawnIn > 0.05f ? $"Renasce em {_health.RespawnIn:0.0} s" : "Escolha o conjunto";

		if (_warningTimer > 0)
		{
			_warningTimer -= delta;
			if (_warningTimer <= 0) _warning.Text = "";
		}

		if (_hurtFlash > 0)
		{
			_hurtFlash = Mathf.Max(0, _hurtFlash - (float)delta * 2.2f);
			UpdateVignette();
		}

		UpdateTransform();
		UpdateWolf((float)delta);
	}

	private void Warn(string text, float seconds = 1.6f, Color? color = null)
	{
		_warning.Text = text;
		_warning.AddThemeColorOverride("font_color", color ?? UiTokens.Warning);
		_warningTimer = seconds;
	}
}
