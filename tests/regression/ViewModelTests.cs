using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Os braços em primeira pessoa: cada arma de fogo usa o seu conjunto
/// animado (braços, arma, esqueleto), o tiro toca o disparo, a recarga
/// toca a recarga até o fim e depois tudo volta a ficar parado; a faca
/// alterna os dois golpes animados do conjunto e a granada é arremessada. Com tela (xvfb) e <c>--fotos=pasta</c>, fotografa cada arma
/// parada, atirando e recarregando — é como se confere o desenho sem
/// abrir o editor:
///
///   xvfb-run -a godot --path . --rendering-driver opengl3 --fixed-fps 60 \
///     res://tests/regression/bench.tscn -- --teste=BracosNaMao --fotos=/tmp/fotos
/// </summary>
public sealed class ViewModelTest : RegressionTest
{
	public override string Name => "BracosNaMao";
	public override string Description => "em primeira pessoa cada arma de fogo tem os braços animados, com tiro e recarga; a faca com os golpes animados e a granada arremessada";

	private string _photos = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--fotos="))?["--fotos=".Length..];

	// Para fotografar, a partida na Vila: tem sol. O teste em si roda no treino.
	public override string Scene => _photos == null ? RegressionBench.Training : RegressionBench.Match;

	public override void Prepare()
	{
		if (_photos == null) return;

		RoomConfig.Settings = new RoomSettings { Map = "armazem", TeamSize = 4 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Blue });
		RoomConfig.Active = true;
	}

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		Player p = b.Player;
		var view = p.GetNode<ViewModel>("Head/Camera3D/ViewModel");
		if (_photos != null)
		{
			// Na contagem a arma não atira: espera a partida valer.
			await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 20);
			await b.Place(new Vector3(-30, 1, 0), -90f, settle: 0.4);
		}
		else await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.4);

		// --armas=ak47,m9 fotografa só essas (a foto é lenta sem placa de vídeo).
		string only = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--armas="))?["--armas=".Length..];
		bool Wanted(string id) => only == null || only.Split(',').Contains(id);

		foreach (string id in Arsenal.Primaries.Append("m9").Where(Wanted))
		{
			bool pistol = Arsenal.IsSecondary(id);
			b.Weapon.Equip(pistol ? Arsenal.Get("m16a2") : Arsenal.Get(id), Arsenal.Get("m9"), 1);
			b.Weapon.SelectSlot(pistol ? Slot.Secondary : Slot.Primary);
			await b.Seconds(0.6);

			Node3D held = view.Held;
			b.Check(held != null && held.Name == id, $"{id}: o modelo da arma está na mão");
			if (held == null) continue;

			// Arma de fogo usa o conjunto animado: esqueleto, braços e arma.
			b.Check(view.HasRig, $"{id}: usa os braços animados");
			if (!view.HasRig) continue;
			b.Check(held.FindChildren("*", "Skeleton3D", true, false).Count >= 1
				&& held.FindChildren("*", "MeshInstance3D", true, false).Count >= 2, $"{id}: braços e arma do conjunto, com esqueleto");
			Node3D muzzle = held.GetNodeOrNull<Node3D>(WeaponModel.MuzzleName);
			float ahead = muzzle == null ? 0f : -view.ToLocal(muzzle.GlobalPosition).Z;
			b.Check(ahead > 0.15f && ahead < 0.7f, $"{id}: a boca do cano fica à frente do olho, sem atravessar a parede ({ahead:0.00} m)");
			await Photo(b, $"{id}_1_parada");

			// Parado, os braços ficam parados: a animação não recomeça nem
			// dá tranco na emenda da volta (o M16 pulava 10 cm a cada 0,9 s).
			if (_photos == null)
			{
				var camera = view.GetParent<Node3D>();
				var skeleton = (Skeleton3D)held.FindChildren("*", "Skeleton3D", true, false)[0];
				Vector3[] Pose()
				{
					var pose = new Vector3[skeleton.GetBoneCount()];
					for (int i = 0; i < pose.Length; i++)
						pose[i] = camera.ToLocal(skeleton.ToGlobal(skeleton.GetBoneGlobalPose(i).Origin));
					return pose;
				}

				// O saque termina primeiro (o do AK passa de 0,6 s).
				await b.Until(() => view.RigAnimation == "parado", 2.0);
				await b.Frames(15);

				int starts = view.RigStarts;
				float jump = 0f;
				Vector3[] last = Pose();
				for (int frame = 0; frame < 150; frame++)
				{
					await b.PhysicsFrame();
					Vector3[] now = Pose();
					for (int i = 0; i < now.Length; i++) jump = Mathf.Max(jump, now[i].DistanceTo(last[i]));
					last = now;
				}
				b.Check(view.RigStarts == starts, $"{id}: parado, a animação dos braços não recomeça ({view.RigStarts - starts} vezes em 2,5 s)");
				b.Check(jump < 0.01f, $"{id}: parado, os braços não dão tranco ({jump * 100f:0.0} cm num quadro)");
			}

			// Um tiro: toca o disparo, e o carregador deixa de estar cheio.
			RegressionBench.Press("attack");
			await b.Frames(3);
			b.Check(view.RigAnimation == "tiro", $"{id}: o tiro toca a animação de disparo (tocando: {view.RigAnimation})");
			await Photo(b, $"{id}_2_tiro");
			RegressionBench.Release("attack");
			await b.Seconds(id == "m3" ? 1.0 : 0.3);

			b.Check(b.Weapon.TryReload(), $"{id}: recarrega");
			bool animated = true;
			int shot = 0;
			float[] marks = { 0.14f, 0.3f, 0.58f, 0.7f, 0.88f };
			while (b.Weapon.IsReloading)
			{
				await b.PhysicsFrame();
				if (b.Weapon.IsReloading && !view.RigAnimation.StartsWith("recarga")) animated = false;
				if (shot < marks.Length && b.Weapon.ReloadProgress >= marks[shot])
				{
					await Photo(b, $"{id}_3_recarga_{shot + 1}");
					shot++;
				}
				if (id == "m3" && shot >= 3) break;
			}
			b.Check(animated, $"{id}: a recarga toca a animação de recarga do começo ao fim");

			await b.Seconds(id == "m3" ? 3.5 : 0.8);
			b.Check(view.RigAnimation == "parado", $"{id}: terminada a recarga, volta a ficar parado (tocando: {view.RigAnimation})");
		}

		// Faca e granada: também com o braço animado, e o golpe ou o
		// arremesso movem o conjunto.
		foreach ((Slot slot, string id) in new[] { (Slot.Knife, Arsenal.KnifeId), (Slot.Grenade, Arsenal.GrenadeId) })
		{
			if (!Wanted(id)) continue;
			b.Weapon.SelectSlot(slot);
			await b.Seconds(0.6);
			Node3D held = view.Held;
			b.Check(held != null && held.Name == id && view.HasRig, $"{id}: na mão, com o braço animado");
			if (held == null) continue;
			await Photo(b, $"{id}_1_parada");

			// Onde a peça está para quem olha: no espaço da câmera. A faca
			// anda pela animação do conjunto; a granada, pelo código, que
			// move o conjunto inteiro.
			var camera = view.GetParent<Node3D>();
			var piece = held.FindChild(slot == Slot.Knife ? "faca" : "granada", true, false) as Node3D ?? held;
			Vector3 Where() => camera.ToLocal(piece.GlobalPosition);
			Vector3 rest = Where();
			RegressionBench.Press("attack");
			float moved = 0f;
			for (int i = 0; i < (slot == Slot.Knife ? 8 : 30); i++)
			{
				await b.PhysicsFrame();
				moved = Mathf.Max(moved, Where().DistanceTo(rest));
			}
			await Photo(b, $"{id}_2_ataque");
			RegressionBench.Release("attack");
			b.AtLeast(0.04, moved, $"{id}: o {(slot == Slot.Knife ? "golpe" : "arremesso")} move a peça na mão (m)");
			if (slot == Slot.Knife)
			{
				b.Check(view.RigAnimation == "golpe", $"faca: o golpe toca a animação do conjunto (tocando: {view.RigAnimation})");
				await b.Seconds(0.7);
				RegressionBench.Press("attack");
				await b.Frames(3);
				RegressionBench.Release("attack");
				b.Check(view.RigAnimation == "golpe2", $"faca: o golpe seguinte é o outro (tocando: {view.RigAnimation})");
			}
			await b.Seconds(0.8);
		}
	}

	private async Task Photo(RegressionBench b, string name)
	{
		if (string.IsNullOrEmpty(_photos)) return;

		await b.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		DirAccess.MakeDirRecursiveAbsolute(_photos);
		b.GetViewport().GetTexture().GetImage().SaveJpg($"{_photos}/{name}.jpg", 0.9f);
	}
}

/// <summary>
/// O AR-15, o fuzil padrão (15/11): saca animado (é a única arma
/// principal que saca), guarda animado antes de a outra arma subir, e
/// recarrega com a animação do Jhon (16/11), com o carregador saindo e
/// voltando ao lugar.
/// </summary>
public sealed class Ar15ViewModelTest : RegressionTest
{
	public override string Name => "SacaEGuarda";
	public override string Description => "o AR-15 saca e guarda animado; a recarga animada tira o carregador e o põe de volta";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		Player p = b.Player;
		var view = p.GetNode<ViewModel>("Head/Camera3D/ViewModel");
		var camera = view.GetParent<Node3D>();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.4);

		b.Check(Arsenal.DefaultPrimary == "ar15", "o AR-15 é o fuzil padrão");
		b.Weapon.Equip(Arsenal.Get("ar15"), Arsenal.Get("m9"), 1);
		b.Weapon.SelectSlot(Slot.Secondary);
		await b.Seconds(0.6);

		b.Weapon.SelectSlot(Slot.Primary);
		await b.Frames(3);
		b.Check(view.Held?.Name == "ar15" && view.RigAnimation == "sacar", $"ao pegar, o AR-15 saca animado (tocando: {view.RigAnimation})");
		await b.Seconds(0.7);
		b.Check(view.RigAnimation == "parado", $"depois do saque, parado (tocando: {view.RigAnimation})");

		// Guarda antes de trocar, no tempo da animação do Jhon (pedido dele,
		// 15/11): o AR-15 continua na mão, e a pistola não atira até subir.
		float holster = Arsenal.Get("ar15").HolsterTime;
		b.Near(AnimationLength("ar15", "guardar"), holster, 0.001, "o tempo de guardar da arma é o da animação (s)");
		b.Weapon.SelectSlot(Slot.Secondary);
		await b.Frames(3);
		b.Check(view.Held?.Name == "ar15" && view.RigAnimation == "guardar", $"ao trocar, o AR-15 guarda primeiro (tocando: {view.RigAnimation})");
		await b.Seconds(holster - 0.15);
		b.Check(view.Held?.Name == "ar15", $"guardando ainda, o AR-15 na mão (na mão: {view.Held?.Name})");
		int ammo = b.Weapon.AmmoInMag;
		b.Weapon.TryAttack();
		b.Check(b.Weapon.AmmoInMag == ammo, "enquanto o AR-15 é guardado, a pistola não atira");
		await b.Seconds(0.3);
		b.Check(view.Held?.Name == "m9", $"guardado, a pistola sobe (na mão: {view.Held?.Name})");
		await b.Seconds(0.25);
		b.Weapon.TryAttack();
		b.Check(b.Weapon.AmmoInMag == ammo - 1, "e atira 0,2 s depois de subir");
		await b.Seconds(0.3);

		// De volta ao AR-15, e uma recarga.
		b.Weapon.SelectSlot(Slot.Primary);
		await b.Seconds(0.8);
		var skeleton = (Skeleton3D)view.Held.FindChildren("*", "Skeleton3D", true, false)[0];
		int magazine = skeleton.FindBone("Magazine");
		var clip = skeleton.FindChild("carregador", false, false) as Node3D;
		// A pose com o que o código mexeu só existe logo depois de os
		// modificadores do esqueleto rodarem: é lá que se mede.
		Vector3 sampled = Vector3.Zero, frame = Vector3.Zero;
		// O carregador em relação ao fuzil (o osso pai dele): longe do
		// lugar dele, saiu da arma — a animação troca o pente.
		int gun = skeleton.GetBoneParent(magazine);
		skeleton.SkeletonUpdated += () =>
		{
			sampled = camera.ToLocal(skeleton.ToGlobal(skeleton.GetBoneGlobalPose(magazine).Origin));
			frame = camera.ToLocal(skeleton.ToGlobal(skeleton.GetBoneGlobalPose(gun).Origin));
		};
		Vector3 Mag() => sampled;
		await b.Frames(2);
		Vector3 rest = Mag();
		float seated = rest.DistanceTo(frame), outOfGun = 0f;

		RegressionBench.Press("attack");
		await b.Frames(2);
		RegressionBench.Release("attack");
		await b.Seconds(0.7);
		b.Check(b.Weapon.TryReload(), "recarrega");
		float lowest = 0f;
		bool named = true;
		while (b.Weapon.IsReloading)
		{
			await b.PhysicsFrame();
			if (b.Weapon.IsReloading && view.RigAnimation != "recarga") named = false;
			lowest = Mathf.Max(lowest, rest.Y - Mag().Y);
			outOfGun = Mathf.Max(outOfGun, Mathf.Abs(Mag().DistanceTo(frame) - seated));
		}
		b.Check(named, "durante a recarga, os braços estão na recarga");
		b.AtLeast(0.05, lowest, "o carregador desce, na tela (m)");
		RegressionBench.Note($"recarga: o carregador desce {RegressionBench.F(lowest)} m na tela e sai {RegressionBench.F(outOfGun)} m da arma");
		b.AtLeast(0.1, outOfGun, "no meio da recarga o carregador sai da arma (m)");
		await b.Seconds(0.5);
		b.AtMost(0.005, Mag().DistanceTo(rest), "terminada a recarga, o carregador volta ao lugar (m)");
		b.Check(clip == null || clip.Visible, "e aparece de novo");
		b.Check(view.RigAnimation == "parado", $"e os braços voltam ao parado (tocando: {view.RigAnimation})");
	}

	/// <summary>A duração de uma animação do conjunto, na ficha bracos.json.</summary>
	private static float AnimationLength(string rig, string animation)
	{
		var sheet = Json.ParseString(FileAccess.GetFileAsString("res://assets/armas/fp/bracos.json")).AsGodotDictionary();
		var a = sheet[rig].AsGodotDictionary()["animacoes"].AsGodotDictionary()[animation].AsGodotDictionary();
		return (float)(a["ate"].AsDouble() - a["de"].AsDouble());
	}
}
