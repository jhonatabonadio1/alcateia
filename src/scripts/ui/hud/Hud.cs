
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
/// | Centro | Mira, marcador de acerto, dano direcional, avisos, "ELIMINOU" |
/// | Inferior esquerdo | Vida, escudo e a forma atual |
/// | Inferior centro | O especial do lobo (o disco) e a barra de transformação |
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
	private KillBanner _killBanner;
	private KillBanner _streakCall;

	/// <summary>
	/// O HUD de outro corpo (a câmera de abate e o espectador,
	/// <see cref="KillCam"/>): o mesmo HUD do jogo, ligado à vida, à arma e
	/// à forma de quem se vê — o feed marca ele como "você". Definido antes
	/// de entrar na cena. Null: o HUD de quem joga.
	/// </summary>
	public Node3D Subject { get; set; }

	/// <summary>O que o HUD mostra agora: vida, munição no pente, WP e o
	/// estado da recarga. Para os testes.</summary>
	public string ShownHp => _hpNumber?.Text;
	public string ShownMag => _mag?.Text;
	public string ShownReload => _reloading?.Text;
	public int FeedShown => _feed?.GetChildCount() ?? 0;

	/// <summary>O feed está de fato na tela (a camada dele e ele visíveis). Para os testes.</summary>
	public bool FeedOnScreen => _feed != null && _feed.IsVisibleInTree() && (_feedLayer == null || _feedLayer.Visible);

	public override void _Ready()
	{
		Node player = Subject ?? Groups.LocalPlayer(GetTree()) ?? Nodes.FindFirst<Player>(GetTree().CurrentScene);
		if (player == null)
		{
			GD.PrintErr("Hud: no Player found in the scene.");
			return;
		}

		_player = player as Player;
		_health = player.GetNode<Health>("Health");
		_form = player.GetNode<FormController>("FormController");
		_weapon = player.GetNode<Weapon>("Weapon");

		_root = UiTheme.Apply(UiKit.Fill(new Control { MouseFilter = Control.MouseFilterEnum.Ignore }));
		AddChild(_root);
		// O DamageIndicator da cena fica por cima do resto. O HUD de outro
		// corpo traz o dele (de onde veio o tiro que ele levou).
		if (Subject != null) AddChild(new DamageIndicator { Name = "DamageIndicator", Subject = Subject });
		MoveChild(_root, 0);

		BuildVignette();
		BuildDanger();
		BuildCenter();
		BuildMinimap();
		BuildFeed();
		BuildVitals();
		BuildEquipment();
		BuildTransform();
		BuildGuide();
		BuildRadio();

		_health.Changed += OnHealthChanged;
		_health.Damaged += OnDamaged;
		_health.Died += OnDied;
		_health.Respawned += OnRespawned;
		_form.WpChanged += OnWpChanged;
		_form.FormChanged += OnFormChanged;
		_form.TransformDenied += OnTransformDenied;
		_form.TransformCooling += OnTransformCooling;
		_form.WolfTimeWarning += OnWolfTimeWarning;
		_form.WolfTimeUp += OnWolfTimeUp;
		_weapon.SlotChanged += OnSlotChanged;
		_weapon.GrenadesChanged += OnGrenadesChanged;
		_weapon.HitConfirmed += OnHitConfirmed;
		_weapon.AmmoChanged += OnAmmoChanged;
		_weapon.AttackCooldown += OnAttackCooldown;
		_weapon.PickedUp += OnPickedUp;
		_weapon.AmmoTaken += OnAmmoTaken;
		_weapon.Dropped += OnDropped;
		KillFeed.Reported += OnKill;
		if (Subject != null) PrefillFeed();

		// No fim da partida a tela de resultado assume a tela inteira.
		if (Match.Current != null)
		{
			_match = Match.Current;
			_match.PhaseChanged += OnMatchPhase;
			_match.HalfTime += OnHalfTime;
		}

		OnHealthChanged(_health.Hp, _health.MaxHp, _health.Shield);
		OnWpChanged(_form.Wp, _form.MaxWp);
		OnSlotChanged((int)_weapon.CurrentSlot);
		OnGrenadesChanged(_weapon.Grenades);
		OnAmmoChanged(_weapon.PrimaryAmmo, _weapon.PrimaryMag, false);
		OnFormChanged((int)_form.Current, animate: false);
	}

	private Match _match;

	private void OnPickedUp(string id) => Warn(Loc.F("Pegou: {0}", Arsenal.NameOf(id)), 1.6f, UiTokens.Success);
	private void OnAmmoTaken(string id) => Warn(Loc.F("Munição: {0}", Arsenal.NameOf(id)), 1.2f, UiTokens.Success);

	private void OnDropped(string id)
	{
		if (!_health.IsDead) Warn(Loc.F("Soltou: {0}", Arsenal.NameOf(id)), 1.2f, UiTokens.TextSecondary);
	}

	private void OnMatchPhase(int phase)
	{
		Visible = (MatchPhase)phase != MatchPhase.Ended && (Subject != null || !KillCam.Active);
		if (_feedLayer != null) _feedLayer.Visible = (MatchPhase)phase != MatchPhase.Ended;
	}

	private void OnHalfTime()
	{
		if (RoomConfig.Rules.Play == PlayMode.HumanosXLobos)
			Warn("METADE DO TEMPO · OS PAPÉIS TROCARAM", 3.5f, UiTokens.Accent);
	}

	/// <summary>
	/// Solta tudo o que ligou. O HUD de quem se vê (<see cref="Subject"/>)
	/// sai antes do corpo que ele mostra, e um sinal de lá chegando num HUD
	/// já desmontado derrubava o jogo.
	/// </summary>
	public override void _ExitTree()
	{
		KillFeed.Reported -= OnKill;
		Radio.Heard -= OnRadioHeard;
		if (_match != null && IsInstanceValid(_match))
		{
			_match.PhaseChanged -= OnMatchPhase;
			_match.HalfTime -= OnHalfTime;
		}
		if (_health != null && IsInstanceValid(_health))
		{
			_health.Changed -= OnHealthChanged;
			_health.Damaged -= OnDamaged;
			_health.Died -= OnDied;
			_health.Respawned -= OnRespawned;
		}
		if (_form != null && IsInstanceValid(_form))
		{
			_form.WpChanged -= OnWpChanged;
			_form.FormChanged -= OnFormChanged;
			_form.TransformDenied -= OnTransformDenied;
			_form.TransformCooling -= OnTransformCooling;
			_form.WolfTimeWarning -= OnWolfTimeWarning;
			_form.WolfTimeUp -= OnWolfTimeUp;
		}
		if (_weapon != null && IsInstanceValid(_weapon))
		{
			_weapon.SlotChanged -= OnSlotChanged;
			_weapon.GrenadesChanged -= OnGrenadesChanged;
			_weapon.HitConfirmed -= OnHitConfirmed;
			_weapon.AmmoChanged -= OnAmmoChanged;
			_weapon.AttackCooldown -= OnAttackCooldown;
			_weapon.PickedUp -= OnPickedUp;
			_weapon.AmmoTaken -= OnAmmoTaken;
			_weapon.Dropped -= OnDropped;
		}
		UnhookBreath();
	}

	private bool _watchingShown;

	/// <summary>
	/// Morto, a câmera de abate e o espectador põem na tela o HUD de quem
	/// se vê (outro <see cref="Hud"/>, com <see cref="Subject"/>); o de quem
	/// joga sai inteiro enquanto isso.
	/// </summary>
	private void UpdateWatching()
	{
		if (Subject != null) return;
		bool watching = KillCam.Active;
		if (watching == _watchingShown) return;
		_watchingShown = watching;
		Visible = !watching;
	}

	private bool _saidOnFire;

	/// <summary>
	/// O personagem fala ao pegar fogo ("I'm on fire!", Jhon, 23/11: "quero
	/// colocar para eu ouvir quando eu entrar em chamas"): só quem pegou
	/// ouve, como o resto do "em chamas". A fala é a do personagem dele
	/// (<c>voz_em_chamas_&lt;pasta&gt;</c>: barut, eve), e quem não tem a sua
	/// fica calado.
	/// </summary>
	private void SayOnFire(CombatantStats me)
	{
		bool on = me != null && me.IsLocal && me.Rage.OnFire;
		if (on && !_saidOnFire)
		{
			CharacterSkin skin = _player?.Character ?? CharacterSkin.DefaultFor(_health.Team);
			string who = skin?.ResourcePath.GetBaseDir().GetFile();
			string line = Sfx.First($"voz_em_chamas_{who}");
			Sfx.Local("em_chamas");   // o brilho de quem pegou fogo, junto com a fala
			if (line != null) Sfx.Local(line);
		}
		_saidOnFire = on;
	}

	public override void _Process(double delta)
	{
		if (_health == null) return;

		// Em chamas (rage): o fogo azul em volta da insígnia, e a borda de
		// quem está sendo visto (o próprio jogador, ou quem se assiste).
		if (_match != null && _formBadge != null)
		{
			CombatantStats me = null;
			foreach (CombatantStats c in _match.Combatants)
				if (c.Health == _health) { me = c; break; }
			_formBadge.Fire = me != null && me.Rage.OnFire ? me.Rage.Level : 0f;
			if (Subject == null) SayOnFire(me);
			if (me != null)
			{
				_formBadge.Rank = me.Rank;
				_formBadge.Photo = me.Photo;
			}
		}

		// Morto com a escolha de conjunto aberta: ela já diz "Abatido" e
		// de quê. Aqui só aparece depois de escolher, enquanto o tempo
		// mínimo para renascer não passou.
		_death.Visible = _health.IsDead && !UiState.AnyOpen;
		if (_death.Visible)
			_deathTimer.Text = _health.RespawnIn > 0.05f ? Loc.F("Renasce em {0:0.0} s", _health.RespawnIn) : "Escolha o conjunto";

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

		UpdateDanger((float)delta);
		UpdateWatching();
		UpdateRadio(delta);
		UpdateTransform();
		UpdateSpecial();
		UpdateWolf((float)delta);
		UpdateGuide();
	}

	private void Warn(string text, float seconds = 1.6f, Color? color = null)
	{
		_warning.Text = text;
		_warning.AddThemeColorOverride("font_color", color ?? UiTokens.Warning);
		_warningTimer = seconds;
	}
}
