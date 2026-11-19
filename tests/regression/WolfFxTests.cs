using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Os efeitos do lobo (scripts/visual/fx): a fumaça cobre a troca do
/// corpo na metade da transformação; o vento aparece de quatro e some ao
/// frear; a garra risca na janela do golpe; a aura acende carregando o
/// salto e no rugido; a cor é a do personagem ou a do
/// time; e o efeito de um lobo não passa para o outro.
///
/// Com <c>-- --fotos=pasta</c> fotografa cada efeito de fora.
/// </summary>
public sealed class WolfFxTest : RegressionTest
{
	public override string Name => "EfeitosDoLobo";
	public override string Description => "fumaça na troca, vento de quatro, rastro da garra, aura no poder, cor do personagem, um lobo não afeta o outro";

	private readonly string _photos = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--fotos="))?["--fotos=".Length..];
	private Camera3D _camera;

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(keep: 1);
		var other = b.Bots()[0];
		RegressionBench.Freeze(other);
		other.GlobalPosition = new Vector3(-15, 1, 9);

		var p = b.Player;
		var form = b.Form;
		var fx = p.GetNodeOrNull<WolfFx>("WolfFx");
		b.Check(fx != null, "o corpo tem os efeitos do lobo (WolfFx)");
		if (fx == null) return;
		var human = p.GetNode<Node3D>("Human");
		var wolf = p.GetNode<WolfVisual>("WolfVisual");
		var meshes = wolf.Model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToList();

		// A cor: sem personagem, a do time.
		Color team = p.GetNode<Health>("Health").Team == Teams.Orange ? WolfFx.OrangeEffect : WolfFx.BlueEffect;
		b.Check(fx.EffectColor.IsEqualApprox(team), "sem personagem, os efeitos têm a cor do time");

		// ---- a transformação: fumaça, e o corpo troca na metade.
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		form.AddWp(5000);
		form.ToggleForm();
		await b.Frames(2);
		b.Check(form.Current == Form.ShiftingToWolf, "começou a virar lobo");
		b.Check(fx.Smoke.IsPlaying, "a fumaça sai no começo da transformação");
		b.Check(human.Visible && !wolf.Visible, "no começo da fumaça, ainda aparece o humano");
		await Photo(b, "1_fumaca_comeco", new Vector3(0, 0.9f, 3.8f));
		await b.Until(() => form.ShowsWolf, form.ShiftDuration);
		float swapAt = form.ShiftTotal - form.ShiftLeft;
		RegressionBench.Note($"o corpo trocou a {RegressionBench.F(swapAt)} s de {RegressionBench.F(form.ShiftDuration)} s");
		b.Near(form.ShiftDuration * 0.5, swapAt, 0.05, "o corpo troca na metade da transformação (s)");
		await b.Frames(1);
		b.Check(!human.Visible && wolf.Visible, "depois da metade, aparece o lobo");
		await Photo(b, "2_fumaca_troca", new Vector3(0, 0.9f, 3.8f));
		await b.Until(() => form.Current == Form.Wolf, 1.0);
		await b.Seconds(0.6);

		// ---- correndo: um pouco de vento; de quatro, cheio; parado, nada.
		b.AtMost(0.01, fx.AirAmount, "parado, sem vento");
		b.Check(meshes.All(m => BodyOverlay.Aura(m) == null), "parado e sem poder, nada por cima do lobo (não gasta placa)");
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.4);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.8);
		float walking = fx.AirAmount;
		RegressionBench.Note($"correndo normal a {RegressionBench.F(RegressionBench.Flat(p.Velocity))} m/s: vento {RegressionBench.F(walking)}");
		b.AtLeast(0.15, walking, "correndo normal, o vento aparece fraco");
		b.AtMost(0.6, walking, "correndo normal, o vento é mais fraco que de quatro");
		RegressionBench.ReleaseAll();
		await b.Seconds(0.8);
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.4);
		await b.Tap("move_forward");
		await b.Seconds(0.08);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.9);
		RegressionBench.Note($"de quatro a {RegressionBench.F(RegressionBench.Flat(p.Velocity))} m/s: vento {RegressionBench.F(fx.AirAmount)}");
		b.AtLeast(0.95, fx.AirAmount, "de quatro, o vento é cheio");
		b.Check(meshes.All(m => BodyOverlay.Aura(m) != null), "com vento, o material vai por cima do lobo");
		await Photo(b, "3_vento_de_quatro", new Vector3(0, 0.8f, 3.4f), follow: true);
		RegressionBench.ReleaseAll();
		await b.Seconds(0.8);
		RegressionBench.Note($"freou: {RegressionBench.F(p.Velocity.Length())} m/s, vento {RegressionBench.F(fx.AirAmount)}");
		b.AtMost(0.05, fx.AirAmount, "freou: o vento some");

		// ---- garrada: as garras riscam na janela do golpe.
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.5);
		bool sawTrail = false, photographed = false;
		float strongest = 0f;
		RegressionBench.Press("attack");
		double end = b.Time + 0.7;
		while (b.Time < end)
		{
			await b.Frames(1);
			sawTrail |= fx.TrailsActive;
			strongest = Mathf.Max(strongest, Mathf.Max(fx.LeftTrail?.Strongest ?? 0f, fx.RightTrail?.Strongest ?? 0f));
			if (strongest > 0.9f && !photographed)
			{
				photographed = true;
				await Photo(b, "4_garrada", new Vector3(1.4f, 1.1f, -1.6f));
			}
		}
		RegressionBench.Release("attack");
		b.Check(sawTrail, "garrada: as garras riscam");
		b.AtLeast(0.9, strongest, "no golpe, a garra que bate risca forte");
		await b.Seconds(0.8);
		b.Check(!fx.TrailsActive, "fora do golpe, as garras não riscam");

		// ---- o especial (Q): a aura acende no voo; a do outro lobo não.
		var otherFx = other.GetNodeOrNull<WolfFx>("WolfFx");
		b.Weapon.FillSpecial();
		await b.Tap("pounce");
		await b.Tap("attack"); // Q arma, o clique salta (18/11)
		await b.Until(() => b.Weapon.IsPounceFlying, 1.5);
		float aura = 0f, mine = 0f, theirsMax = 0f;
		var theirMesh = otherFx != null
			? other.GetNode<WolfVisual>("WolfVisual").Model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().First()
			: null;
		while (b.Weapon.IsPounceFlying)
		{
			await b.PhysicsFrame();
			aura = Mathf.Max(aura, fx.AuraAmount);
			mine = Mathf.Max(mine, meshes[0].GetInstanceShaderParameter("aura_amount").AsSingle());
			if (theirMesh != null) theirsMax = Mathf.Max(theirsMax, theirMesh.GetInstanceShaderParameter("aura_amount").AsSingle());
		}
		// A aura cheia é a do efeito (PowerAura; 0,7 desde 18/11: só o contorno).
		b.AtLeast(0.9 * fx.PowerAura, aura, "no especial, a aura acende");
		b.AtLeast(0.9 * fx.PowerAura, mine, "a aura chega à malha do lobo (instance uniform)");
		if (theirMesh != null)
			b.AtMost(0.01, theirsMax, "a aura de um lobo não acende a do outro");
		await Photo(b, "5_aura_carga", new Vector3(0.8f, 1.0f, 3.2f));
		RegressionBench.ReleaseAll();
		await b.Seconds(2.5);

		// ---- o uivo (R): a luz de regeneração acende, os arcos saltam, e o
		// escudo cheio fecha com o anel. Soltar no meio não dá o anel.
		b.Check(fx.Roar.Glow <= 0.01f && !fx.Roar.Sweeping, "sem rugir, nada da luz do uivo");
		Input.ActionPress("howl");
		await b.Seconds(0.5);
		b.Check(fx.Roar.Active, "segurando R, o rugido aparece");
		b.AtLeast(0.5, fx.Roar.Glow, "a luz de regeneração acende durante o uivo");
		int arcs = 0;
		for (int i = 0; i < 30; i++)
		{
			await b.PhysicsFrame();
			arcs = Mathf.Max(arcs, fx.Roar.Arcs);
		}
		b.AtLeast(1, arcs, "arcos elétricos saltam para o corpo no uivo");
		b.AtMost(2.5, RoarBurst.ShaftTop, "a luz do uivo é baixa (não passa de 2,5 m dos pés)");
		Input.ActionRelease("howl");
		await b.Seconds(0.1);
		b.Check(!fx.Roar.Sweeping, "soltou no meio: sem o anel de escudo cheio");
		b.Check(!fx.EnergyPlaying, "soltou no meio: o som da energia é cortado");
		await b.Seconds(1.0);

		bool swept = false;
		Input.ActionPress("howl");
		for (int i = 0; i < 150 && !swept; i++)
		{
			await b.PhysicsFrame();
			swept = fx.Roar.Sweeping;
		}
		Input.ActionRelease("howl");
		b.Check(swept, "rugiu até o fim: o anel de escudo cheio sobe");
		await b.Seconds(1.2);
		b.Check(fx.Roar.Glow <= 0.01f && fx.Roar.Arcs == 0, "acabou o uivo, a luz e os arcos somem");

		// ---- voltando a humano: fumaça de novo, curta.
		form.ToggleForm();
		await b.Frames(2);
		b.Check(fx.Smoke.IsPlaying, "voltando a humano, sai fumaça");
		await b.Until(() => form.Current == Form.Human, 1.0);
		await b.Frames(2);
		b.Check(human.Visible && !wolf.Visible, "de humano, o lobo some");
	}

	private async Task Photo(RegressionBench b, string name, Vector3 offset, bool follow = false)
	{
		if (string.IsNullOrEmpty(_photos)) return;
		DirAccess.MakeDirRecursiveAbsolute(_photos);
		_camera ??= new Camera3D { Fov = 50f, Near = 0.05f, CullMask = 0xFFFFFFFFu };
		if (_camera.GetParent() == null) b.AddChild(_camera);
		Vector3 at = b.Player.GlobalPosition;
		Basis basis = follow ? b.Player.GlobalBasis : Basis.Identity;
		_camera.GlobalPosition = at + basis * offset;
		_camera.LookAt(at + Vector3.Up * 0.1f);
		_camera.MakeCurrent();
		var layers = b.GetTree().Root.FindChildren("*", "CanvasLayer", true, false).OfType<CanvasLayer>().Where(l => l.Visible).ToList();
		foreach (var layer in layers) layer.Visible = false;
		await b.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		b.GetViewport().GetTexture().GetImage().SaveJpg($"{_photos}/{name}.jpg", 0.9f);
		foreach (var layer in layers) layer.Visible = true;
		_camera.ClearCurrent();
	}
}
