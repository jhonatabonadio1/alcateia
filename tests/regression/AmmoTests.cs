using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// A munição acaba (decisão do Jhon, 13/11): recarregar tira da reserva,
/// sem reserva não recarrega e o gatilho dá o clique seco; a arma igual
/// caída no chão junta a munição dela; renascer enche tudo.
/// </summary>
public sealed class FiniteAmmoTest : RegressionTest
{
	public override string Name => "MunicaoFinita";
	public override string Description => "recarregar gasta a reserva; sem reserva, clique seco e nada de recarga; arma igual no chão e renascer devolvem munição";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.4);
		Weapon w = b.Weapon;
		WeaponDefinition m16 = Arsenal.Get("m16a2");

		w.Equip(m16, Arsenal.Get("m9"), 1);
		w.SelectSlot(Slot.Primary);
		await b.Seconds(0.6);
		b.Check(w.AmmoInMag == m16.Magazine && w.Reserve == m16.Reserve, $"nasce com o pente e a reserva cheios ({w.AmmoInMag} / {w.Reserve})");

		// Pouca munição: solta a arma e pega de volta com 5 no pente e 10 na reserva.
		b.Check(w.TryDrop(), "solta a arma");
		foreach (WeaponPickup dropped in b.GetTree().Root.FindChildren("*", "", true, false).OfType<WeaponPickup>())
			dropped.QueueFree();
		b.Check(w.TryPickUp(m16, 5, 10), "pega a arma de novo, com 5 e 10");
		w.SelectSlot(Slot.Primary);
		await b.Seconds(0.6);

		b.Check(w.TryReload(), "recarrega com 10 na reserva");
		await b.Until(() => !w.IsReloading, 5);
		b.Check(w.AmmoInMag == 15 && w.Reserve == 0, $"a recarga passa só o que havia na reserva ({w.AmmoInMag} / {w.Reserve})");

		RegressionBench.Press("attack");
		await b.Until(() => w.AmmoInMag == 0, 5);
		RegressionBench.Release("attack");
		await b.Seconds(0.3);
		b.Check(!w.TryReload() && !w.IsReloading, "sem reserva, não recarrega");

		int clicks = 0;
		w.DryFired += () => clicks++;
		await b.Tap("attack");
		await b.Seconds(0.4);
		b.Check(clicks == 1 && !w.IsReloading && w.AmmoInMag == 0, $"o gatilho dá o clique seco ({clicks} cliques)");

		// A mesma arma, caída: a munição dela entra na reserva, até o teto.
		b.Check(w.CanTake(m16) && w.TryPickUp(m16, 30, 90), "junta a munição de um M16 caído");
		b.Check(w.Reserve == m16.Reserve && w.AmmoInMag == 0, $"a reserva enche até o teto ({w.Reserve})");
		b.Check(!w.CanTake(m16), "com a reserva cheia, outro M16 fica no chão");
		b.Check(!w.CanTake(Arsenal.Get("ak47")), "arma diferente com o espaço ocupado fica no chão");

		// Renascer enche tudo.
		w.TryPickUp(m16, 0, 0);
		b.Health.TakeDamage(99999f);
		await b.Seconds(b.Health.RespawnDelay + 0.2);
		b.Health.TryRespawn();
		await b.Seconds(0.3);
		w.SelectSlot(Slot.Primary);
		await b.Seconds(0.6);
		b.Check(w.PrimaryAmmo == w.PrimaryMag && w.PrimaryReserve == (w.PrimaryDef?.Reserve ?? -1)
			&& w.SecondaryAmmo == w.SecondaryMag && w.SecondaryReserve == (w.SecondaryDef?.Reserve ?? -1),
			$"renasce com pente e reserva cheios nas duas armas ({w.PrimaryAmmo}/{w.PrimaryReserve}, {w.SecondaryAmmo}/{w.SecondaryReserve})");
	}
}

/// <summary>
/// Botão direito, de humano: zoom com mira circular (pedido do Jhon,
/// 13/11). A câmera fecha, a arma fica onde está, o tiro fica mais preciso
/// e o passo mais lento; soltar volta. De lobo, o botão direito continua
/// sendo o salto. Com <c>--fotos=pasta</c> (e tela), fotografa cada arma
/// sem e com zoom.
/// </summary>
public sealed class AimZoomTest : RegressionTest
{
	public override string Name => "ZoomNaMira";
	public override string Description => "segurar o botão direito dá zoom: câmera fecha, a arma fica no lugar, menos dispersão e passo mais lento; recarregar e o lobo não dão zoom";

	private readonly string _photos = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--fotos="))?["--fotos=".Length..];

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.4);

		// Na foto, o menu de nível dos bots do treino tapa a arma.
		if (_photos != null)
			foreach (CanvasLayer menu in b.GetTree().Root.FindChildren("*", "", true, false).OfType<DifficultyMenu>())
				menu.Visible = false;

		Weapon w = b.Weapon;
		Player p = b.Player;
		var camera = p.GetNode<Camera3D>("Head/Camera3D");
		var view = p.GetNode<ViewModel>("Head/Camera3D/ViewModel");
		float baseFov = camera.Fov;

		string only = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--armas="))?["--armas=".Length..];
		foreach (string id in Arsenal.Primaries.Append("m9").Where(i => only == null || only.Split(',').Contains(i)))
		{
			bool pistol = Arsenal.IsSecondary(id);
			WeaponDefinition def = Arsenal.Get(id);
			w.Equip(pistol ? Arsenal.Get("m16a2") : def, Arsenal.Get("m9"), 1);
			w.SelectSlot(pistol ? Slot.Secondary : Slot.Primary);
			await b.Seconds(0.8);

			float hipSpread = w.CurrentSpread, hipSpeed = w.MoveSpeedMultiplier;
			Vector2 hipMuzzle = OnScreen(view, camera);
			await Photo(b, $"{id}_1_cintura");

			RegressionBench.Press("special");
			await b.Seconds(0.4);
			b.Check(w.IsAiming && w.AimBlend >= 1f, $"{id}: segurar o botão direito mira");
			b.Near(def.AimFov, camera.Fov, 0.1, $"{id}: a câmera fecha na abertura da arma (graus)");
			b.Near(hipSpread * def.AimSpreadMultiplier, w.CurrentSpread, 0.01, $"{id}: a dispersão fecha (graus)");
			b.Near(hipSpeed * def.AimSpeedMultiplier, w.MoveSpeedMultiplier, 0.001, $"{id}: o passo fica mais lento");

			// A arma não vem para o olho nem cresce com o zoom: fica no
			// mesmo lugar da tela (quem mira é a mira circular).
			b.AtMost(0.03, (OnScreen(view, camera) - hipMuzzle).Length(), $"{id}: a arma fica no lugar da tela no zoom (fração da tela)");
			await Photo(b, $"{id}_2_zoom");

			// Recarregar tira da mira; acabou a recarga, volta.
			RegressionBench.Press("attack");
			await b.Frames(3);
			RegressionBench.Release("attack");
			await b.Seconds(id == "m3" ? 1.0 : 0.3);
			b.Check(w.TryReload(), $"{id}: recarrega mirando");
			await b.Seconds(0.3);
			b.Check(!w.IsAiming, $"{id}: recarregando, sai da mira");
			await b.Until(() => !w.IsReloading, 5);
			await b.Seconds(0.4);
			b.Check(w.IsAiming, $"{id}: sem recarga, volta à mira");

			RegressionBench.Release("special");
			await b.Seconds(0.4);
			b.Check(w.AimBlend <= 0f, $"{id}: soltar sai da mira");
			b.Near(baseFov, camera.Fov, 0.1, $"{id}: a câmera volta a abrir (graus)");
		}

		// De lobo o botão direito é o salto, não a mira.
		await b.BecomeWolf();
		RegressionBench.Press("special");
		await b.Seconds(0.3);
		b.Check(w.AimBlend <= 0f, "de lobo, o botão direito não mira");
		RegressionBench.Release("special");
		await b.Seconds(0.5);
	}

	/// <summary>Onde a boca do cano aparece na tela (0 a 1).</summary>
	private static Vector2 OnScreen(ViewModel view, Camera3D camera)
	{
		if (!view.TryGetMuzzle(out Vector3 at)) return Vector2.Zero;
		Vector2 size = camera.GetViewport().GetVisibleRect().Size;
		return camera.UnprojectPosition(at) / size;
	}

	private async Task Photo(RegressionBench b, string name)
	{
		if (string.IsNullOrEmpty(_photos)) return;
		await b.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		DirAccess.MakeDirRecursiveAbsolute(_photos);
		b.GetViewport().GetTexture().GetImage().SaveJpg($"{_photos}/{name}.jpg", 0.9f);
	}
}
