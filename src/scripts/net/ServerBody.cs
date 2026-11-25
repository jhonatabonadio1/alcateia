using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O corpo de um jogador no servidor de partida (modo autoritativo,
/// decisões 0011 e 0016): o <see cref="Player"/> de sempre, movido pelos
/// comandos que chegam (<see cref="Commands"/>), um por passo, com o mesmo
/// código que o jogo do jogador usou para prever.
///
/// **A vida é daqui** (decisão 0016): o <see cref="Health"/> deste corpo é
/// o que vale. O acerto que outro jogador diz que deu é conferido
/// (<see cref="HitCheck"/>) e aplicado aqui; a morte é decidida aqui; o
/// jogo só pode contar que tem **menos** vida (dano que só ele viu: queda,
/// a própria granada, um bot dele) — nunca mais. Renascer só depois da
/// espera, e num ponto de nascimento do time.
///
/// O que ainda vem do jogo: a forma e a arma na mão (no comando), as armas
/// do conjunto e a guarda do especial do lobo (Status), a classe de lobo e
/// as melhorias (Loadout).
/// </summary>
public sealed class ServerBody
{
	/// <summary>Folga na espera para renascer, em segundos: a morte chega
	/// ao jogo meia ida e volta depois de acontecer aqui.</summary>
	public const double RespawnSlack = 0.75;

	/// <summary>Até quantos metros do ponto de nascimento do time vale
	/// renascer (o jogo sorteia uns metros em volta).</summary>
	public const float SpawnReach = 9f;

	/// <summary>Só para o teste de ponta a ponta: deixa o robô "renascer"
	/// vivo, em qualquer lugar, para se pôr de frente para o alvo. Em
	/// produção fica desligado.</summary>
	public static bool AllowTeleport { get; set; }

	public readonly long Account;
	public readonly byte Index;
	public readonly Player Body;
	public readonly CommandBuffer Commands = new();
	public readonly PoseHistory History = new();

	/// <summary>O ritmo de dano de quem atira, por tipo de golpe.</summary>
	public readonly DamageBudget GunBudget = new(), MeleeBudget = new(), GrenadeBudget = new();

	public Health Health { get; }
	public FormController Form { get; }
	public Weapon Weapon { get; }

	/// <summary>O corpo de outro jogador pelo lugar dele na escalação
	/// (<see cref="Index"/>): é por ele que chega o alvo travado do especial.</summary>
	public System.Func<byte, Player> Resolve { get; set; }

	/// <summary>Os pontos onde o time deste corpo pode renascer (vazio: não confere).</summary>
	public List<Vector3> SpawnPoints { get; } = new();

	private string _primary = "", _secondary = "";
	private readonly Node3D _head;

	/// <summary>A vida do jogador em que o corpo está (dá a volta em 256).</summary>
	public byte Epoch { get; private set; }

	/// <summary>Já sabe onde o jogador nasceu e está no mundo.</summary>
	public bool Placed { get; private set; }

	public bool Alive => Placed && !Health.IsDead;

	public int Simulated { get; private set; }

	/// <summary>Renascimentos recusados (cedo demais, ou longe da base).</summary>
	public int RefusedSpawns { get; private set; }

	/// <summary>Onde o corpo estava logo depois de cada comando: é de onde
	/// sai o tiro que o jogo deu naquele comando.</summary>
	public struct ShotPose
	{
		public uint Seq, Tick;
		public Vector3 Eye, Feet;
		public float Yaw, Pitch;
		public bool Alive;
		public Form Form;
		public Slot Slot;
		public string Held;
		public bool Valid;
	}

	private readonly ShotPose[] _shots = new ShotPose[NetRates.HistoryTicks];
	private ShotPose _latest;

	public ServerBody(long account, byte index, Player body)
	{
		Account = account;
		Index = index;
		Body = body;
		Health = body.GetNode<Health>("Health");
		Form = body.GetNode<FormController>("FormController");
		Weapon = body.GetNode<Weapon>("Weapon");
		_head = body.GetNode<Node3D>("Head");
		Health.MatchTimings = true;
		Health.AutoRespawn = false;
		Hide();
	}

	/// <summary>Um passo do servidor: consome o comando deste passo (ou
	/// dois, se o jogo está à frente) e guarda onde o corpo ficou.</summary>
	public void Step(uint tick, double delta)
	{
		// Aliado não é parede, aqui também (TeamCollision): o jogo deixa o
		// lobo atravessar o boneco do aliado, e o corpo daqui batia nele — a
		// correção puxava o jogo de volta. O passo de física do corpo está
		// desligado (Player.ReadyDriven): o par se refaz daqui.
		TeamCollision.Keep(Body, Health, ref _collisionTeam);

		int wanted = Commands.WantedThisTick;
		for (int i = 0; i < wanted; i++)
		{
			if (!Commands.Next(out InputCommand command, out _)) break;
			Apply(command, tick, delta);
		}

		if (Placed)
			History.Record(tick, Body.GlobalPosition.ToNumerics(), Body.Rotation.Y, Body.HeadPitch, Body.IsCrouching ? Body.CrouchHeight : Body.StandHeight);
	}

	/// <summary>Quantos passos seguidos o jogo conta que está na parede e
	/// este corpo não, antes de voltar para ela.</summary>
	public const int WallBehindSteps = 3;
	private int _collisionTeam = TeamCollision.None;
	private int _wallBehind;
	private bool _wallWas;

	/// <summary>Quantas vezes voltou para a parede pelo que o jogo contou.</summary>
	public int WallResumes { get; private set; }

	private void Apply(in InputCommand c, uint tick, double delta)
	{
		// Vida nova (ou a primeira): o corpo vai para onde o jogo diz que
		// nasceu — se já pode renascer, e se é na base do time. Sem a
		// posição ainda, espera: comando de outra vida não move o corpo desta.
		if (!Placed || c.Epoch != Epoch)
		{
			if (!c.HasSpawn || !MayRespawn(c.Spawn.ToGodot())) return;
			Epoch = c.Epoch;
			if (Health.IsDead) Health.Mirror(Health.MaxHp, Health.MaxHp, Health.MaxShield, Health.MaxShield, false);
			Show();
			Body.PlaceDriven(c.Spawn.ToGodot());
		}

		// Morto aqui, o corpo não anda (o jogo pode não ter sabido ainda).
		if (!Health.IsDead)
		{
			Form form = System.Enum.IsDefined(typeof(Form), (int)c.Form) ? (Form)c.Form : global::Form.Human;
			Form.RemoteSet(form);
			if (!Form.IsWolf) Weapon.RemoteHold(_primary, _secondary, (Slot)c.HeldSlot);
			TakeSpecial(c);
			Body.Simulate(NetCommands.ToIntent(c), Quant.Yaw(c.Yaw), Quant.Pitch(c.Pitch), delta, c.Frozen);
			Weapon.RemoteAim = null;
			Weapon.RemoteAimNone = false;
			Simulated++;

			// O jogo segue na parede e este corpo saiu dela: um passo de
			// diferença no grudar é normal (o próximo acerta), três seguidos
			// não — volta para a parede (InputCommand.OnWall).
			_wallBehind = c.OnWall && !Body.IsWallRunning ? _wallBehind + 1 : 0;
			if (_wallBehind >= WallBehindSteps)
			{
				if (Body.ResumeWall("the client is on the wall: the server goes back to it"))
				{
					WallResumes++;
					_wallBehind = 0;
				}
				else if (_wallBehind == WallBehindSteps)
					GD.Print($"wall server: {Account} says it is on the wall, this body can't go back ({Body.WallResumeBlock})");
			}
			if (Body.IsWallRunning != _wallWas)
			{
				_wallWas = Body.IsWallRunning;
				Vector3 at = Body.GlobalPosition;
				GD.Print($"wall server: {Account} {(_wallWas ? "on" : "off")} the wall at ({at.X:0.0}, {at.Y:0.0}, {at.Z:0.0}); client on the wall: {c.OnWall}");
			}
		}

		RecordShot(c.Seq, tick);
	}

	/// <summary>Quantos comandos do especial vieram com o alvo ou o ponto
	/// recusados (longe demais, aliado, morto): para o relatório e o teste.</summary>
	public int RefusedAims { get; private set; }

	/// <summary>Folga, em metros, entre onde o jogo diz que via o alvo e onde
	/// ele esteve aqui: o jogo o vê uns 100 ms atrás, e um lobo de quatro
	/// anda 0,8 m nesse tempo.</summary>
	public const float FocusSlack = 3f;

	/// <summary>
	/// O alvo travado e o ponto mirado do especial, como o jogo os tinha
	/// neste comando (<see cref="InputCommand.HasFocus"/>, <see cref="InputCommand.HasAim"/>):
	/// o corpo daqui não tem câmera nem trava, e sem isto o salto dele ia
	/// para outro lugar e a correção puxava o lobo do jogador para trás
	/// (Jhon, 25/11: "o lobo não avança e cai e fica, indo e voltando").
	/// Não acredita em tudo: o alvo tem de ser inimigo, vivo e ao alcance da
	/// trava, e o ponto, ao alcance do salto.
	/// </summary>
	private void TakeSpecial(in InputCommand c)
	{
		Player focus = null;
		(Vector3, Vector3)? view = null;
		if (c.HasFocus)
		{
			Player them = Resolve?.Invoke(c.Focus);
			Health health = them?.GetNodeOrNull<Health>("Health");
			float reach = Body.LockKeepRange + 2f;
			if (them != null && them != Body && health != null && !health.IsDead && health.Team != Health.Team
				&& Flat(them.GlobalPosition - Body.GlobalPosition) <= Mathf.Max(reach, Weapon.PounceChaseRange))
			{
				focus = them;
				Vector3 at = c.FocusAt.ToGodot(), velocity = c.FocusVelocity.ToGodot();
				if (at.DistanceTo(them.GlobalPosition) <= FocusSlack && velocity.Length() <= 20f) view = (at, velocity);
				else RefusedAims++;
			}
			else if (them == null && Flat(c.FocusAt.ToGodot() - Body.GlobalPosition) <= Weapon.PounceChaseRange && c.FocusVelocity.Length() <= 20f)
			{
				// Um bot (a máquina do dono o move; aqui não há corpo) ou um alvo
				// ainda sem lugar: vale só onde o jogo o via — o mesmo que um ponto
				// mirado, que o jogo já pode escolher dentro do alcance.
				view = (c.FocusAt.ToGodot(), c.FocusVelocity.ToGodot());
			}
			else RefusedAims++;
		}
		Weapon.RemoteFocus = focus;
		Weapon.RemoteFocusView = view;

		Weapon.RemoteAim = null;
		Weapon.RemoteAimNone = c.HasAim && c.AimNone;
		if (c.HasAim && !c.AimNone)
		{
			Vector3 aim = c.Aim.ToGodot();
			Vector3 from = Body.GlobalPosition;
			if (Flat(aim - from) <= Weapon.PounceMaxDistance + 4f && aim.Y - from.Y <= Weapon.PounceMaxClimb + 2f && from.Y - aim.Y <= Weapon.PounceMaxDrop + 3f)
				Weapon.RemoteAim = aim;
			else RefusedAims++;
		}
	}

	private static float Flat(Vector3 v) => new Vector2(v.X, v.Z).Length();

	/// <summary>Pode renascer agora, ali? A primeira vez, sempre (é a
	/// largada); depois, só morto, passada a espera, e perto de um ponto de
	/// nascimento do time.</summary>
	private bool MayRespawn(Vector3 at)
	{
		if (!Placed || AllowTeleport) return true;

		bool ok = Health.IsDead && Health.RespawnIn <= RespawnSlack && NearSpawn(at);
		if (!ok) RefusedSpawns++;
		return ok;
	}

	private bool NearSpawn(Vector3 at)
	{
		if (SpawnPoints.Count == 0) return true;
		foreach (Vector3 point in SpawnPoints)
			if (new Vector2(point.X - at.X, point.Z - at.Z).Length() <= SpawnReach && Mathf.Abs(point.Y - at.Y) <= 4f) return true;
		return false;
	}

	private void RecordShot(uint seq, uint tick)
	{
		_latest = new ShotPose
		{
			Seq = seq, Tick = tick,
			Eye = _head.GlobalPosition,
			Feet = Body.GlobalPosition - Vector3.Up * (Body.StandHeight * 0.5f),
			Yaw = Body.Rotation.Y, Pitch = Body.HeadPitch,
			Alive = Alive,
			Form = Form.Current,
			Slot = Weapon.HumanSlot,
			Held = Form.IsWolf ? "" : Weapon.HumanSlot == Slot.Knife ? Weapon.MeleeId : Weapon.Held?.Id ?? "",
			Valid = true
		};
		_shots[seq % (uint)_shots.Length] = _latest;
	}

	/// <summary>Onde o corpo estava logo depois do comando <paramref name="seq"/>.
	/// Sem ele (já saiu do anel), o mais recente.</summary>
	public ShotPose ShotAt(uint seq)
	{
		ShotPose pose = _shots[seq % (uint)_shots.Length];
		return pose.Valid && pose.Seq == seq ? pose : _latest;
	}

	/// <summary>
	/// Vida, escudo, armas e a guarda do especial, como o jogo conta
	/// (Status). Da vida e do escudo só vale o que desce (decisão 0016).
	/// </summary>
	public void ApplyStatus(float hp, float shield, string primary, string secondary, bool guard, float charge = -1f)
	{
		_primary = primary ?? "";
		_secondary = secondary ?? "";
		// A carga do especial (Weapon.MirrorCharge): até o que as regras deram aqui.
		if (charge >= 0f) Weapon.MirrorCharge(charge);
		if (!Alive) return;

		Health.LowerTo(hp, shield);
		// A guarda do especial é da arma do lobo, que só roda no jogo: ele
		// conta, e ela só vale em lobo (é uma redução do dano, com teto).
		Weapon.GuardOverride = guard && Form.IsWolf;
	}

	/// <summary>O jogo contou que morreu sem ser por acerto de outra máquina
	/// (queda, a própria granada, um bot dele).</summary>
	public void SelfDeath()
	{
		if (Alive) Health.Mirror(0, Health.MaxHp, 0, Health.MaxShield, true);
	}

	/// <summary>A pontuação e a duração da sala: a carga do especial deste
	/// corpo enche no mesmo ritmo da do jogo (<see cref="Weapon.KillFill"/>).</summary>
	public void SetRules(int scoreToWin, double duration)
	{
		Weapon.RulesScoreToWin = scoreToWin;
		Weapon.RulesDuration = (float)duration;
	}

	/// <summary>O servidor decidiu um abate (ou uma assistência) deste
	/// jogador: a carga do especial dele enche aqui também.</summary>
	public void Scored(bool kill, bool asWolf = false)
	{
		if (kill) Form.OnKill(asWolf);
		else Form.OnAssist();
	}

	/// <summary>A classe de lobo e as melhorias (Loadout): mudam a
	/// velocidade e o pulo do lobo, então o servidor precisa delas.</summary>
	public void ApplyLoadout(string breed, IEnumerable<string> tattoos)
	{
		var valid = tattoos.Where(id => Catalog.Get(id)?.Kind == ItemKind.Tattoo).Distinct().Take(Profile.MaxTattoos);
		WolfBreed built = Profile.BuildBreed(breed, valid);
		if (built != null) Form.Breed = built;
	}

	/// <summary>O corpo no estado do mundo.</summary>
	public EntityState State()
	{
		var flags = EntityFlags.None;
		if (Placed) flags |= EntityFlags.Present;
		if (Body.IsOnFloor()) flags |= EntityFlags.Grounded;
		if (Body.IsCrouching) flags |= EntityFlags.Crouching;
		if (Health.IsDead) flags |= EntityFlags.Dead;
		if (Body.IsWallRunning) flags |= EntityFlags.WallRunning;
		if (Body.IsSprinting) flags |= EntityFlags.Sprinting;
		if (Weapon.IsAiming) flags |= EntityFlags.Aiming;
		if (Body.HowlProgress > 0f) flags |= EntityFlags.Howling;

		return new EntityState
		{
			Index = Index,
			Flags = flags,
			Form = (byte)Form.Current,
			Slot = (byte)Weapon.HumanSlot,
			Epoch = Epoch,
			Position = Body.GlobalPosition.ToNumerics(),
			Velocity = Body.Velocity.ToNumerics(),
			Yaw = Quant.Yaw(Body.Rotation.Y),
			Pitch = Quant.Pitch(Body.HeadPitch)
		};
	}

	private uint _layer, _mask;

	/// <summary>Fora do mundo até saber onde nasceu: sem colisão.</summary>
	private void Hide()
	{
		_layer = Body.CollisionLayer;
		_mask = Body.CollisionMask;
		Body.CollisionLayer = 0;
		Placed = false;
	}

	private void Show()
	{
		Body.CollisionLayer = _layer;
		Body.CollisionMask = _mask;
		Placed = true;
	}
}
