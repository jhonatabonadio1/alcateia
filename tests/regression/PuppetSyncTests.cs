using Godot;
using System.Threading.Tasks;

/// <summary>
/// O boneco de quem joga em outra máquina (Jhon, 21/11, jogando com dois
/// PCs: "o player está andando normal, mas na minha tela tá a animação de
/// quadrúpede"; "ao rugir, a animação não aparece para o oponente"; "ao me
/// transformar em lobo… rapidamente volta para humano e já volta para lobo";
/// "a animação do especial não é vista por outros players").
/// </summary>
public sealed class PuppetFlagsTest : RegressionTest
{
	public override string Name => "BonecoMarcas";
	public override string Description => "o estado leva correr de quatro, agachado, rugindo, passo quieto e granada armada; a mensagem antiga lê tudo desligado";

	public override async Task Run(RegressionBench b)
	{
		var sent = new NetState
		{
			Grounded = true, Sprinting = true, Crouching = true, Howling = true, Quiet = true,
			HasGear = true, GrenadeReady = true, Primary = "ar15", Secondary = "deagle"
		};
		var stream = new System.IO.MemoryStream();
		sent.Write(new System.IO.BinaryWriter(stream));
		NetState got = NetState.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(stream.ToArray())));
		b.Check(got.Grounded && got.Sprinting && got.Crouching && got.Howling && got.Quiet && got.GrenadeReady, "as marcas vão e voltam");
		b.Check(!got.Dead && !got.WallRunning, "sem mexer nos bits de antes");

		var old = new NetState { Grounded = true, HasGear = true, Primary = "ar15", Secondary = "deagle" };
		stream = new System.IO.MemoryStream();
		old.Write(new System.IO.BinaryWriter(stream));
		byte[] bytes = stream.ToArray();
		// A mensagem de antes de 21/11 terminava antes do byte da granada.
		got = NetState.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(bytes, 0, bytes.Length - 1)));
		b.Check(got.HasGear && !got.GrenadeReady && !got.Sprinting && !got.Howling && !got.Crouching, "a mensagem antiga é lida, tudo desligado");
		await b.Frames(1);
	}
}

public sealed class PuppetWolfTest : RegressionTest
{
	public override string Name => "BonecoLobo";
	public override string Description => "o boneco não volta a humano no fim da transformação, só galopa quando o dono galopa, ruge e salta o especial";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);

		var puppet = GD.Load<PackedScene>("res://scenes/bot.tscn").Instantiate<Bot>();
		puppet.Name = "Boneco";
		puppet.Team = Teams.Orange;
		puppet.Remote = true;
		b.GetTree().CurrentScene.AddChild(puppet);
		await b.Frames(2);

		var form = puppet.GetNode<FormController>("FormController");
		var weapon = puppet.GetNode<Weapon>("Weapon");
		var visual = puppet.GetNode<WolfVisual>("WolfVisual");
		Vector3 at = b.Player.GlobalPosition;

		// O que o NetMatch faz com cada estado que chega (Apply).
		void Tell(Form f, Vector3 velocity, bool sprinting = false, bool howling = false)
		{
			puppet.RemoteUpdate(at, velocity, 0f, 0f, true, f, Slot.Primary, "ar15", "deagle", 200f, 200f, 0f, 0f, false);
			puppet.RemoteMotion(sprinting, false, howling, false, false);
			puppet.CollisionLayer = 0;   // ocupa o lugar do jogador do teste
		}

		Tell(Form.Human, Vector3.Zero);
		await b.Frames(2);
		Tell(Form.ShiftingToWolf, Vector3.Zero);
		await b.Until(() => form.Current == Form.Wolf, 3);
		// O "ainda virando" que o dono mandou um instante antes de acabar.
		Tell(Form.ShiftingToWolf, Vector3.Zero);
		await b.Frames(3);
		b.Check(form.Current == Form.Wolf && form.ShowsWolf, "o estado velho não volta o boneco a humano no meio da fumaça");

		// Lobo rápido andando em pé (raça ou tatuagem de velocidade): sem galope.
		for (int i = 0; i < 40; i++)
		{
			Tell(Form.Wolf, new Vector3(0f, 0f, -4.6f));
			await b.Frames(1);
		}
		b.Check(!puppet.IsSprinting && visual.Gallop < 0.1f, $"andando em pé a 4,6 m/s o boneco anda em pé (galope {visual.Gallop:0.00})");

		for (int i = 0; i < 40; i++)
		{
			Tell(Form.Wolf, new Vector3(0f, 0f, -5.8f), sprinting: true);
			await b.Frames(1);
		}
		b.Check(puppet.IsSprinting && visual.Gallop > 0.9f, $"o dono correu de quatro: o boneco também (galope {visual.Gallop:0.00})");

		Tell(Form.Wolf, Vector3.Zero, howling: true);
		await b.Frames(3);
		b.Check(visual.Howling, "o dono rugiu: o boneco ruge");
		Tell(Form.Wolf, Vector3.Zero);
		await b.Frames(3);

		// O especial (o salto do Q): o boneco voa e toca o golpe.
		float skip = WolfMoves.StepOf(WolfMove.Pounce).Delay;
		puppet.RemoteAttack(Slot.Wolf, 0f, 0f, (int)WolfMove.Pounce + 1, skip, 2f);
		await b.Frames(3);
		b.Check(weapon.IsPounceFlying && weapon.IsStriking, "o salto do especial voa no boneco");
		await b.Until(() => !weapon.IsStriking, 4);
		b.Check(!weapon.IsStriking, "e acaba");

		puppet.QueueFree();
		await b.Frames(1);
	}
}

/// <summary>
/// As mãos na arma em terceira pessoa (Jhon, 21/11: "as armas, granadas,
/// pistola, faca não estão posicionadas corretamente nas mãos… o boneco
/// precisa segurar igual é na primeira pessoa"). Mede no boneco de outro
/// jogador: a pistola aponta para onde ele mira e as duas mãos estão nela;
/// a faca sai com a lâmina para a frente; o fuzil sobe quando ele olha para cima.
/// </summary>
public sealed class PuppetHandsTest : RegressionTest
{
	public override string Name => "BonecoMaos";
	public override string Description => "em terceira pessoa a pose de cada arma é a do Blender (poses_armas.json): as mãos e a arma onde o Blender deixou, o punho sem entortar, a granada na palma e o cano para a mira; Eve e Barut";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		foreach (int team in new[] { Teams.Orange, Teams.Blue })
			await One(b, team);
	}

	private static async Task One(RegressionBench b, int team)
	{
		var puppet = GD.Load<PackedScene>("res://scenes/bot.tscn").Instantiate<Bot>();
		puppet.Name = "Boneco";
		puppet.Team = team;
		puppet.Remote = true;
		b.GetTree().CurrentScene.AddChild(puppet);
		await b.Frames(2);
		Vector3 at = b.Player.GlobalPosition;
		string who = team == Teams.Orange ? "Barut" : "Eve";

		void Tell(Slot slot, float pitch)
		{
			puppet.RemoteUpdate(at, Vector3.Zero, 0f, pitch, true, Form.Human, slot, "ar15", "deagle", 200f, 200f, 0f, 0f, false);
			puppet.RemoteMotion(false, false, false, false, false);
			puppet.CollisionLayer = 0;
		}
		async Task Hold(Slot slot, float pitch, int frames = 40)
		{
			for (int i = 0; i < frames; i++)
			{
				Tell(slot, pitch);
				await b.Frames(1);
			}
		}

		var held = puppet.GetNode<HeldWeapon>("Human/HeldWeapon");
		var poses = HoldPoses.For(held.PosesPath);
		b.Check(poses != null && poses.Count == 4, $"{who}: as quatro poses do Blender carregaram ({held.PosesPath})");
		if (poses == null)
		{
			puppet.QueueFree();
			return;
		}

		Skeleton3D skeleton = AnimationKit.FindSkeleton(puppet.GetNode<Node3D>("Human"));
		int Bone(string end)
		{
			for (int i = 0; i < skeleton.GetBoneCount(); i++)
				if (skeleton.GetBoneName(i).EndsWith(end)) return i;
			return -1;
		}
		Vector3 At(int bone) => (skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone)).Origin;
		HeldRig Rig() => held.GetChildCount() > 0 ? held.GetChild(0) as HeldRig : null;
		Vector3 Aim(float pitch) => (new Basis(puppet.GlobalBasis.X.Normalized(), pitch) * -puppet.GlobalBasis.Z).Normalized();
		int rightArm = Bone("RightForeArm"), right = Bone("RightHand"), rightMiddle = Bone("RightHandMiddle1");
		int left = Bone("LeftHand"), head = Bone("_Head");
		Vector3 Palm(int wrist, int middle) => At(wrist).Lerp(At(middle), 0.6f);
		float Wrist() => Mathf.RadToDeg((At(right) - At(rightArm)).AngleTo(At(rightMiddle) - At(right)));
		Vector3 Gun(HeldRig rig) => rig.At(rig.Skeleton.FindBone("ik_hand_gun"));

		// As distâncias entre as mãos, a cabeça e a arma são as do Blender (o
		// peito vira para a mira e o quadril vem da animação, mas da cintura
		// para cima é a pose).
		void Same(string kind, HeldRig rig)
		{
			HoldPose pose = poses[kind];
			var here = new System.Collections.Generic.Dictionary<string, Vector3>
			{
				["RightHand"] = At(right), ["LeftHand"] = At(left), ["Head"] = At(head), ["arma"] = Gun(rig),
			};
			float worst = 0f;
			string where = "";
			foreach (var (a, b2) in new[] { ("RightHand", "arma"), ("LeftHand", "arma"), ("RightHand", "LeftHand"), ("Head", "arma"), ("Head", "RightHand") })
			{
				if (!pose.Check.ContainsKey(a) || !pose.Check.ContainsKey(b2)) continue;
				float want = pose.Check[a].DistanceTo(pose.Check[b2]);
				float got = here[a].DistanceTo(here[b2]);
				if (Mathf.Abs(got - want) > worst)
				{
					worst = Mathf.Abs(got - want);
					where = $"{a}–{b2} {got:0.000} m (Blender {want:0.000})";
				}
			}
			RegressionBench.Note($"{who}, {kind}: maior diferença para o Blender {worst * 100f:0.0} cm ({where}), punho {Wrist():0}°");
			b.AtMost(0.025f, worst, $"{who}, {kind}: mãos, cabeça e arma como no Blender (m)");
		}

		foreach (float pitch in new[] { 0f, 0.4f })
		{
			await Hold(Slot.Secondary, pitch);
			HeldRig pistol = Rig();
			b.Check(pistol != null, $"{who}: a pistola vem do conjunto da primeira pessoa");
			if (pistol == null) break;
			float barrel = pistol.GunForward().Dot(Aim(pitch));
			RegressionBench.Note($"{who}, pistola, mira {pitch:0.0} rad: cano·mira {barrel:0.00}");
			b.Check(barrel > 0.9f, $"{who}: a pistola aponta para onde ele mira ({barrel:0.00})");
			if (pitch == 0f) Same("pistola", pistol);
			b.AtMost(held.WristBend + 4f, Wrist(), $"{who}: o punho com a pistola não entorta (graus)");
		}

		await Hold(Slot.Knife, 0f);
		if (Rig() is HeldRig knife)
		{
			Same("faca", knife);
			b.AtMost(held.WristBend + 4f, Wrist(), $"{who}: o punho com a faca não entorta (graus)");
		}
		else b.Check(false, $"{who}: a faca vem do conjunto");

		await Hold(Slot.Grenade, 0f);
		if (Rig() is HeldRig grenade)
		{
			Same("granada", grenade);
			float gap = Palm(right, rightMiddle).DistanceTo(grenade.Center());
			int bolt = grenade.Skeleton.FindBone("Bolt");
			float size = grenade.Skeleton.GlobalTransform.Basis.Scale.X * grenade.Skeleton.GetBoneGlobalPose(bolt).Basis.Scale.X / 0.01f;
			RegressionBench.Note($"{who}, granada: o meio a {gap:0.000} m da palma, {size:0.00}× o tamanho do conjunto");
			b.AtMost(poses["granada"].Ball + 0.03f, gap, $"{who}: a granada encosta na palma (m)");
			b.Check(size > 1.15f, $"{who}: a granada cresce com a mão do personagem ({size:0.00}×)");
			b.AtMost(held.WristBend + 4f, Wrist(), $"{who}: o punho com a granada não entorta (graus)");
		}
		else b.Check(false, $"{who}: a granada vem do conjunto");

		await Hold(Slot.Primary, 0f);
		HeldRig rifle = Rig();
		b.Check(rifle != null, $"{who}: o fuzil vem do conjunto");
		float level = rifle?.GunForward().Y ?? 0f;
		float straight = rifle?.GunForward().Dot(Aim(0f)) ?? 0f;
		if (rifle != null) Same("fuzil", rifle);
		b.Check(straight > 0.93f, $"{who}: o fuzil aponta para a frente ({straight:0.00})");
		await Hold(Slot.Primary, 0.5f);
		float up = Rig()?.GunForward().Y ?? 0f;
		RegressionBench.Note($"{who}, fuzil: cano para cima {level:0.00} reto, {up:0.00} mirando 0,5 rad acima");
		b.Check(up > level + 0.2f, $"{who}: olhando para cima, o fuzil sobe junto");

		puppet.QueueFree();
		await b.Frames(2);
	}
}
