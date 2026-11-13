using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Os braços em primeira pessoa: cada arma de fogo usa o seu conjunto
/// animado (braços, arma, esqueleto), o tiro toca o disparo, a recarga
/// toca a recarga até o fim e depois tudo volta a ficar parado; faca e
/// granada ficam com as duas mãos de sempre. Com tela (xvfb) e <c>--fotos=pasta</c>, fotografa cada arma
/// parada, atirando e recarregando — é como se confere o desenho sem
/// abrir o editor:
///
///   xvfb-run -a godot --path . --rendering-driver opengl3 --fixed-fps 60 \
///     res://tests/regression/bench.tscn -- --teste=BracosNaMao --fotos=/tmp/fotos
/// </summary>
public sealed class ViewModelTest : RegressionTest
{
	public override string Name => "BracosNaMao";
	public override string Description => "em primeira pessoa cada arma de fogo tem os braços animados, com tiro e recarga; faca e granada, as duas mãos";

	private string _photos = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--fotos="))?["--fotos=".Length..];

	// Para fotografar, a partida na Vila: tem sol. O teste em si roda no treino.
	public override string Scene => _photos == null ? RegressionBench.Training : RegressionBench.Match;

	public override void Prepare()
	{
		if (_photos == null) return;

		RoomConfig.Settings = new RoomSettings { Map = "hangar", TeamSize = 4 };
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
			b.Check(muzzle != null && view.ToLocal(muzzle.GlobalPosition).Z < -0.3f, $"{id}: a boca do cano fica à frente da câmera");
			await Photo(b, $"{id}_1_parada");

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

		// Faca e granada: também na mão, com as duas mãos.
		foreach ((Slot slot, string id) in new[] { (Slot.Knife, Arsenal.KnifeId), (Slot.Grenade, Arsenal.GrenadeId) })
		{
			if (!Wanted(id)) continue;
			b.Weapon.SelectSlot(slot);
			await b.Seconds(0.6);
			Node3D held = view.Held;
			b.Check(held != null && held.Name == id && held.GetNodeOrNull("MaoDireita") != null && held.GetNodeOrNull("MaoEsquerda") != null,
				$"{id}: na mão, com as duas mãos");
			await Photo(b, $"{id}_1_parada");
			RegressionBench.Press("attack");
			await b.Seconds(slot == Slot.Knife ? 0.12 : 0.5);
			await Photo(b, $"{id}_2_ataque");
			RegressionBench.Release("attack");
			await b.Seconds(0.5);
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
