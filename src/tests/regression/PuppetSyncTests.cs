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
			HasGear = true, GrenadeReady = true, Aiming = true, Primary = "ar15", Secondary = "deagle"
		};
		var stream = new System.IO.MemoryStream();
		sent.Write(new System.IO.BinaryWriter(stream));
		NetState got = NetState.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(stream.ToArray())));
		b.Check(got.Grounded && got.Sprinting && got.Crouching && got.Howling && got.Quiet && got.GrenadeReady && got.Aiming, "as marcas vão e voltam (a mira também)");
		b.Check(!got.Dead && !got.WallRunning, "sem mexer nos bits de antes");

		var old = new NetState { Grounded = true, HasGear = true, Primary = "ar15", Secondary = "deagle" };
		stream = new System.IO.MemoryStream();
		old.Write(new System.IO.BinaryWriter(stream));
		byte[] bytes = stream.ToArray();
		// A mensagem de antes de 21/11 terminava antes do byte da granada.
		got = NetState.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(bytes, 0, bytes.Length - 1)));
		b.Check(got.HasGear && !got.GrenadeReady && !got.Aiming && !got.Sprinting && !got.Howling && !got.Crouching, "a mensagem antiga é lida, tudo desligado");
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

		var puppet = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Bot>();
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
	public override string Description => "em terceira pessoa cada arma toca a animação feita no Blender a partir da primeira pessoa (animacoes/<arma>.json): as mãos onde as do conjunto estão (parado, mirando, recarregando), o cano para a mira, a mira dos outros pela rede; Eve e Barut";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		foreach (int team in new[] { Teams.Orange, Teams.Blue })
			await One(b, team);
	}

	private static async Task One(RegressionBench b, int team)
	{
		var puppet = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Bot>();
		puppet.Name = "Boneco";
		puppet.Team = team;
		puppet.Remote = true;
		b.GetTree().CurrentScene.AddChild(puppet);
		await b.Frames(2);
		Vector3 at = b.Player.GlobalPosition;
		string who = team == Teams.Orange ? "Barut" : "Eve";
		bool aiming = false;

		void Tell(Slot slot, float pitch)
		{
			puppet.RemoteUpdate(at, Vector3.Zero, 0f, pitch, true, Form.Human, slot, "ar15", "deagle", 200f, 200f, 0f, 0f, false);
			puppet.RemoteMotion(false, false, false, false, false, aiming);
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
		int FingerBone(string mixamo, string manny) { int b = Bone(mixamo); return b >= 0 ? b : skeleton.FindBone(manny); }
		int right = Bone("RightHand"), rightMiddle = FingerBone("RightHandMiddle1", "middle_01_r");
		int left = Bone("LeftHand"), leftMiddle = FingerBone("LeftHandMiddle1", "middle_01_l");
		// A palma: entre o pulso e o começo do médio, onde o Blender encosta
		// a palma do personagem na da primeira pessoa.
		Vector3 Palm(int wrist, int middle) => At(wrist).Lerp(At(middle), 0.8f);
		Vector3 RigPalm(HeldRig rig, bool rightSide)
		{
			int w = rightSide ? rig.HandR : rig.HandL;
			int m = (rightSide ? rig.FingersR : rig.FingersL)[2][0];
			return rig.At(w).Lerp(rig.At(m), 0.8f);
		}
		// As mãos do personagem onde as do conjunto estão (o Blender ajeita a
		// mão do personagem, que é maior, em até ~3 cm: a pele da palma, o giro
		// em volta do cabo).
		// A esquerda pode deslizar no guarda-mão: o braço do personagem é mais
		// curto que o da primeira pessoa e o Blender leva a mão para trás na
		// linha do cano (até ~10 cm). Conta o que sai da linha.
		float LeftGap(HeldRig rig)
		{
			Vector3 d = Palm(left, leftMiddle) - RigPalm(rig, false);
			Vector3 along = rig.GunForward();
			float s = d.Dot(along);
			return Mathf.Abs(s) > 0.12f ? d.Length() : (d - along * s).Length();
		}
		float Gap(HeldRig rig, bool rightSide) => rightSide ? Palm(right, rightMiddle).DistanceTo(RigPalm(rig, true)) : LeftGap(rig);

		async Task Check(Slot slot, string kind, bool twoHands)
		{
			await Hold(slot, 0f);
			HeldRig rig = Rig();
			b.Check(rig != null && held.Clips != null, $"{who}, {kind}: a animação do Blender carregou");
			if (rig == null || held.Clips == null) return;
			float r = Gap(rig, true), l = twoHands ? Gap(rig, false) : 0f;
			RegressionBench.Note($"{who}, {kind} ({rig.Playing}): mão direita a {r * 100f:0.0} cm da do conjunto" + (twoHands ? $", esquerda a {l * 100f:0.0} cm" : ""));
			b.AtMost(0.05f, r, $"{who}, {kind}: a mão direita na arma (m)");
			if (twoHands) b.AtMost(0.04f, l, $"{who}, {kind}: a mão esquerda na arma (m)");
		}

		await Check(Slot.Primary, "fuzil", true);
		float straight = Rig()?.GunForward().Dot(Aim(0f)) ?? 0f;
		b.Check(straight > 0.9f, $"{who}: o fuzil aponta para a frente ({straight:0.00})");
		await Hold(Slot.Primary, 0.5f);
		float up = Rig()?.GunForward().Dot(Aim(0.5f)) ?? 0f;
		b.Check(up > 0.9f, $"{who}: olhando para cima, o fuzil sobe junto ({up:0.00})");

		// A mira pela rede: o boneco sobe a arma ao olho.
		aiming = true;
		await Hold(Slot.Primary, 0f, 50);
		HeldRig aimed = Rig();
		b.Check(aimed != null && aimed.Playing is "mira" or "mira_andar", $"{who}: mirando, o boneco toca a mira ({aimed?.Playing})");
		if (aimed != null)
		{
			float r = Gap(aimed, true), l = Gap(aimed, false);
			float dir = aimed.GunForward().Dot(Aim(0f));
			RegressionBench.Note($"{who}, fuzil na mira: mãos a {r * 100f:0.0}/{l * 100f:0.0} cm, cano·mira {dir:0.00}");
			b.AtMost(0.05f, Mathf.Max(r, l), $"{who}: na mira, as mãos na arma (m)");
			b.Check(dir > 0.95f, $"{who}: na mira, o cano para a mira ({dir:0.00})");
		}
		aiming = false;
		await Hold(Slot.Primary, 0f, 30);
		b.Check(Rig()?.Playing is "parado" or "andar", $"{who}: soltou a mira, volta ao descanso ({Rig()?.Playing})");

		// A recarga: as mãos com as do conjunto. Quando o personagem troca o pente
		// (o clipe diz quando, HoldClip.MagDrop; Jhon, 23/11: "ela coloca o mesmo
		// carregador"), a esquerda sai de propósito: solta o pente velho (ele cai),
		// vai ao bolso e volta com o novo — aí só a direita fica com a de lá, e a
		// esquerda tem de ir mesmo (mais de 10 cm da de lá) e voltar.
		puppet.RemoteGear(0, true, 1, 0f, 0f);
		float worst = 0f, away = 0f;
		string playing = "";
		bool swaps = false;
		for (int i = 0; i < 90; i++)
		{
			Tell(Slot.Primary, 0f);
			puppet.RemoteGear(0, true, 1, 0f, 0f);
			await b.Frames(1);
			if (Rig() is HeldRig rr && rr.Playing.StartsWith("recarga") && rr.BlendWeight >= 1f)
			{
				playing = rr.Playing;
				HoldClip clip = held.Clips?.Get(rr.Playing);
				double t = rr.ClipTime;
				bool swapping = clip != null && clip.SwapsMagazine && t >= clip.MagDrop - 0.05 && t <= clip.MagSeat;
				swaps |= clip != null && clip.SwapsMagazine;
				worst = Mathf.Max(worst, swapping ? Gap(rr, true) : Mathf.Max(Gap(rr, true), Gap(rr, false)));
				if (swapping) away = Mathf.Max(away, Gap(rr, false));
			}
		}
		puppet.RemoteGear(30, false, 1, 0f, 0f);
		RegressionBench.Note($"{who}, recarga ({playing}): as mãos a no máximo {worst * 100f:0.0} cm das do conjunto"
			+ (swaps ? $"; a esquerda vai buscar o pente a {away * 100f:0.0} cm" : ""));
		b.Check(playing.StartsWith("recarga"), $"{who}: o boneco recarrega");
		b.AtMost(0.045f, worst, $"{who}: na recarga, as mãos com as do conjunto (m)");
		if (swaps) b.Check(away > 0.10f, $"{who}: na troca de pente, a esquerda vai à cintura ({away * 100f:0.0} cm)");

		await Check(Slot.Secondary, "pistola", true);
		float barrel = Rig()?.GunForward().Dot(Aim(0f)) ?? 0f;
		b.Check(barrel > 0.9f, $"{who}: a pistola aponta para onde ele mira ({barrel:0.00})");
		await Check(Slot.Knife, "faca", false);
		await Check(Slot.Grenade, "granada", false);

		puppet.QueueFree();
		await b.Frames(2);
	}
}
