using Godot;
using System.Threading.Tasks;

/// <summary>
/// O som da granada (Jhon, 26/11: "som de explosão de granada melhor AAA+, com
/// fuse perceptível"). O pavio chia, preso à granada, do arremesso até a
/// explosão; no primeiro toque começa o fim do pavio (cliques que se aceleram),
/// no ritmo do <see cref="Grenade.Fuse"/>; e a explosão escolhe a versão de
/// perto, de longe ou debaixo de teto. Até 26/11 a granada voava muda e a
/// explosão era uma só (o "Sci-Fi" da Kenney). Sem tela não se ouve: o teste
/// confere que os tocadores existem, tocam, andam com a granada e somem com
/// ela, e que os arquivos são os de <c>src/tools/sons/granada.py</c>.
/// </summary>
public sealed class GrenadeSoundTest : RegressionTest
{
	public override string Name => "GranadaSom";
	public override string Description => "o pavio chia do arremesso até a explosão, o fim do pavio começa no primeiro toque, e a explosão tem versão de perto, de longe e debaixo de teto";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Place(new Vector3(-15, 1, 13), 0f);
		Vector3 start = b.Player.GlobalPosition + new Vector3(0, 1.2f, -1.5f);

		// No ar, sem tocar nada: só o chiado, em laço.
		var g = GD.Load<PackedScene>(Weapon.DefaultGrenadeScene).Instantiate<Grenade>();
		g.Thrower = b.Health;
		g.GravityScale = 0f;
		g.MaxFlight = 3f;
		b.Scene.AddChild(g);
		g.GlobalPosition = start;
		g.LinearVelocity = new Vector3(0, 0, -0.5f);
		await b.Seconds(1.6);
		var hiss = g.FusePlayer as AudioStreamPlayer3D;
		b.Check(hiss != null && hiss.GetParent() == g, "o pavio chia, preso à granada");
		b.Check(hiss != null && hiss.Playing, "e continua chiando 1,6 s depois do arremesso (em laço; o arquivo tem 1 s)");
		b.Check(GD.Load<AudioStreamWav>("res://src/assets/audio/granada_pavio.wav").LoopMode == AudioStreamWav.LoopModeEnum.Forward, "o arquivo do chiado está em laço");
		b.Check(g.FuseEndPlayer == null, "no ar, o fim do pavio ainda não começou (o pavio só conta depois do primeiro toque)");
		g.QueueFree();
		await b.Frames(2);

		// Cai no chão: o fim do pavio começa, no ritmo do pavio.
		var h = GD.Load<PackedScene>(Weapon.DefaultGrenadeScene).Instantiate<Grenade>();
		h.Thrower = b.Health;
		h.Fuse = 1.5f;
		b.Scene.AddChild(h);
		h.GlobalPosition = start;
		await b.Until(() => h.Landed, 3.0);
		await b.Frames(2);
		var end = h.FuseEndPlayer as AudioStreamPlayer3D;
		b.Check(end != null && end.Playing, "tocou o chão: o fim do pavio toca");
		b.Near(1.2 / 1.5, end?.PitchScale ?? 0, 0.01, "com o pavio de 1,5 s, o fim do pavio toca mais devagar, para acabar junto (fator)");
		b.Check(h.FusePlayer is AudioStreamPlayer3D { Playing: true }, "e o chiado segue até o fim");
		b.Health.SetPool(b.Health.MaxHp, b.Health.MaxShield, refill: true);
		await b.Until(() => !GodotObject.IsInstanceValid(h), 3.0);
		b.Check(!GodotObject.IsInstanceValid(h), "explodiu: o pavio some com a granada");
		b.Health.SetPool(b.Health.MaxHp, b.Health.MaxShield, refill: true);

		// A explosão: de perto, de longe, debaixo de teto.
		b.Check(WorldAudio.ExplosionName(6f, false) == "granada_explosao", "de perto, no aberto: a explosão de perto");
		b.Check(WorldAudio.ExplosionName(45f, false) == "granada_explosao_distante", "a 45 m: a de longe");
		b.Check(WorldAudio.ExplosionName(6f, true) == "granada_explosao_dentro", "debaixo de teto: a da sala");
		b.Check(WorldAudio.ExplosionName(45f, true) == "granada_explosao_distante", "longe, mesmo debaixo de teto: a de longe");
		foreach (string name in new[] { "granada_explosao", "granada_explosao_distante", "granada_explosao_dentro", "granada_pavio", "granada_pavio_fim" })
			b.Check(Sfx.Has(name), $"o arquivo {name} existe");
		b.Check(!ResourceLoader.Exists("res://src/assets/audio/granada_explosao_5.wav"), "a explosão antiga (o 5º arquivo da Kenney) saiu: não toca no meio das novas");

		PhysicsDirectSpaceState3D space = b.Player.GetWorld3D().DirectSpaceState;
		Vector3 open = b.Player.GlobalPosition + new Vector3(6, -0.8f, 0);
		b.Check(!WorldAudio.Covered(space, open, 6f), "no aberto não há teto");
		foreach (string name in new[] { "Teto", "Barreira" })
		{
			var roof = new StaticBody3D { Name = name };
			roof.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4, 0.3f, 4) } });
			b.Scene.AddChild(roof);
			roof.GlobalPosition = open + Vector3.Up * 3f;
			await b.PhysicsFrame();
			await b.PhysicsFrame();
			bool covered = WorldAudio.Covered(space, open, 6f);
			b.Check(name == "Teto" ? covered : !covered, name == "Teto" ? "com um teto a 3 m: debaixo de teto" : "uma parede invisível em cima não é teto");
			roof.QueueFree();
			await b.PhysicsFrame();
		}
	}
}
