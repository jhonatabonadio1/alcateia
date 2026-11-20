using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Granada de segurar e soltar (v0.26.9) e mais forte (v0.26.10):
/// nada sai enquanto segura; soltou, ela vai a uns 18 m olhando reto
/// (19/11: "pode ir mais longe também, não tanto"; eram 15,5).
///
/// E **sai da mão** (19/11): nasce quando a mão solta no arremesso (não no
/// clique), a um braço da câmera, **em cima da linha da cruz** e indo
/// exatamente para ela ("a granada tem que ser lançada no meio da linha da
/// mão, na direção exata do crosshair").</summary>
public sealed class GrenadeRangeTest : RegressionTest
{
	public override string Name => "Granada";
	public override string Description => "a granada arma enquanto segura, sai da mão no arremesso em cima da linha da cruz, vai reto para ela e cai a ~18 m";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Place(new Vector3(-15, 1, 13), -90f);
		b.Weapon.Equip(b.Weapon.PrimaryDef, b.Weapon.SecondaryDef, 2);
		await b.Tap("slot_5");
		// Da arma principal para a granada: o tempo de guardá-la (o AR-15
		// guarda no tempo da animação do Jhon) e os 0,2 s da troca.
		await b.Seconds(0.6 + b.Weapon.PrimaryDef.HolsterTime);

		RegressionBench.Press("attack");
		await b.Seconds(1.2);
		b.Check(b.Weapon.IsGrenadeReady, "segurando, a granada fica armada");
		b.Check(FindGrenade(b) == null, "nada sai enquanto segura");

		Vector3 from = b.Player.GlobalPosition;
		RegressionBench.Release("attack");

		await b.Frames(2);
		b.Check(b.Weapon.ThrowFromHand, "a mão animada é quem solta a granada");
		b.Check(FindGrenade(b) == null && b.Weapon.IsThrowPending, "soltou o botão: o braço lança e a granada ainda está na mão");

		Grenade grenade = null;
		double released = b.Time;
		await b.Until(() => (grenade = FindGrenade(b)) != null, 0.5);
		b.Check(grenade != null, "a mão soltou, a granada saiu");
		if (grenade == null) return;
		b.AtLeast(0.06, b.Time - released, "do botão solto à granada no ar: o tempo de o braço lançar (s)");
		b.AtMost(0.2, b.Time - released, "do botão solto à granada no ar: o tempo de o braço lançar (s)");

		// Nasce na mão, e a mão está em cima da linha da cruz: nem para um
		// lado nem para o outro, um pouco acima dela (para não a tapar).
		Camera3D cam = b.GetViewport().GetCamera3D();
		Vector3 born = cam.GlobalTransform.AffineInverse() * grenade.GlobalPosition;
		Vector3 going = cam.GlobalBasis.Inverse() * grenade.LinearVelocity;
		float aside = Mathf.RadToDeg(Mathf.Atan2(going.X, -going.Z));
		RegressionBench.Note($"nasceu a ({RegressionBench.F(born.X)}; {RegressionBench.F(born.Y)}; {RegressionBench.F(born.Z)}) m da câmera, a {RegressionBench.F(grenade.LinearVelocity.Length())} m/s, {RegressionBench.F(aside)}° para o lado da cruz");
		b.AtMost(0.03, Mathf.Abs(born.X), "nasce em cima da linha da cruz (m para o lado)");
		b.AtLeast(0.02, born.Y, "um pouco acima da cruz (m)");
		b.AtMost(0.3, born.Y, "um pouco acima da cruz (m)");
		b.AtLeast(0.25, -born.Z, "a um braço do olho (m à frente)");
		b.AtMost(1.1, -born.Z, "a um braço do olho (m à frente)");
		b.AtMost(1.0, Mathf.Abs(aside), "sai na direção exata da cruz (graus para o lado)");

		// E voa em cima da linha da cruz, do começo ao fim.
		Vector3 eye = cam.GlobalPosition, ahead = -cam.GlobalBasis.Z;
		var right = new Vector3(-ahead.Z, 0, ahead.X).Normalized();
		float landed = float.NaN, worst = Mathf.Abs(born.X), end = born.X;
		await b.Until(() =>
		{
			if (!GodotObject.IsInstanceValid(grenade)) return true;
			if (grenade.Landed || grenade.GlobalPosition.Y <= 0.15f)
			{
				landed = RegressionBench.Flat(grenade.GlobalPosition - from);
				return true;
			}
			end = (grenade.GlobalPosition - eye).Dot(right);
			worst = Mathf.Max(worst, Mathf.Abs(end));
			return false;
		}, 4.0);
		b.AtMost(0.03, worst, "no voo inteiro, fica em cima da linha da cruz (m para o lado, no máximo)");
		b.AtMost(0.03, Mathf.Abs(end), "chega ao chão em cima da linha da cruz (m para o lado)");

		b.Near(18.0, landed, 1.0, "onde a granada toca o chão (m)");
		b.Check(b.Weapon.Grenades == 1, $"sobrou uma granada (sobraram {b.Weapon.Grenades})");
	}

	private static Grenade FindGrenade(RegressionBench b) =>
		b.GetTree().Root.FindChildren("*", "RigidBody3D", true, false).OfType<Grenade>().FirstOrDefault();
}

/// <summary>
/// A granada cai onde a mira aponta: mirando o chão a 6 m e a 13 m, ela
/// explode a menos de 0,5 m do ponto (19/11: "extremamente preciso"; era 1,2); e a explosão deixa o efeito novo
/// (<see cref="Explosion"/>), com a granada de verdade no ar.
/// </summary>
public sealed class GrenadeAimTest : RegressionTest
{
	public override string Name => "GranadaNaMira";
	public override string Description => "a granada explode perto do ponto mirado (6 m e 13 m), voa com o modelo da granada e explode com o efeito novo";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		foreach (float pitch in new[] { -15f, -7f })
		{
			await b.Place(new Vector3(-15, 1, 13), -90f, pitch, settle: 0.5);
			b.Weapon.Equip(b.Weapon.PrimaryDef, b.Weapon.SecondaryDef, 2);
			b.Health.SetHp(b.Health.MaxHp);
			await b.Tap("slot_5");
			await b.Seconds(0.6 + b.Weapon.PrimaryDef.HolsterTime);

			Camera3D cam = b.GetViewport().GetCamera3D();
			var query = PhysicsRayQueryParameters3D.Create(cam.GlobalPosition, cam.GlobalPosition - cam.GlobalBasis.Z * 32f);
			query.Exclude = new Godot.Collections.Array<Rid> { b.Player.GetRid() };
			var hit = b.Player.GetWorld3D().DirectSpaceState.IntersectRay(query);
			b.Check(hit.Count > 0, $"mira a {pitch}°: há um ponto no chão");
			if (hit.Count == 0) continue;
			Vector3 aim = hit["position"].AsVector3();
			float far = RegressionBench.Flat(aim - b.Player.GlobalPosition);

			RegressionBench.Press("attack");
			await b.Seconds(0.3);
			RegressionBench.Release("attack");
			Grenade grenade = null;
			await b.Until(() => (grenade = b.GetTree().Root.FindChildren("*", "RigidBody3D", true, false).OfType<Grenade>().FirstOrDefault()) != null, 0.5);
			b.Check(grenade != null, "a granada saiu");
			if (grenade == null) continue;
			b.Check(grenade.GetNodeOrNull("Modelo") != null, "voa o modelo da granada, não a bola");

			Vector3 last = grenade.GlobalPosition;
			float fuse = grenade.Fuse;
			double flown = 0, fused = 0;
			await b.Until(() =>
			{
				if (!GodotObject.IsInstanceValid(grenade)) return true;
				last = grenade.GlobalPosition;
				if (grenade.Landed) fused += 1.0 / 60.0; else flown += 1.0 / 60.0;
				return false;
			}, 6.0);
			b.Check(flown > 0.15, $"no ar o pavio não conta ({RegressionBench.F(flown)} s de voo)");
			b.Near(fuse, fused, 0.2, "encostou, explode depois do pavio (s)");
			RegressionBench.Note($"mirou a {RegressionBench.F(far)} m; explodiu a {RegressionBench.F(RegressionBench.Flat(last - aim))} m do ponto");
			b.AtMost(0.5, RegressionBench.Flat(last - aim), $"a {RegressionBench.F(far)} m: distância entre a explosão e o ponto mirado (m)");
			b.Check(Explosion.Alive > 0, "a explosão está no ar (a granada é a explosiva)");
			b.Check(ShockBlast.Alive == 0, "e não o estouro de choque");
			await b.Seconds(2.3);
		}
		b.Check(Explosion.Alive == 0, "as explosões se desfazem sozinhas");
	}
}

/// <summary>
/// A granada **vai para onde a cruz aponta** (19/11: "granada vai na
/// direção do crosshair"): saindo da mão, o primeiro toque dela é no ponto
/// mirado — no chão perto e longe, numa parede (baixo, meio e alto),
/// andando de lado; mirando o céu, cai na linha da cruz. E colado numa
/// parede ela não a atravessa.
/// </summary>
public sealed class GrenadeCrosshairTest : RegressionTest
{
	public override string Name => "GranadaNaCruz";
	public override string Description => "saindo da mão, o primeiro toque da granada é no ponto que a cruz aponta (chão, parede, andando); colado na parede, não atravessa";

	private static readonly Vector3 Spot = new(-15, 1, 13);

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		int aimed = 0, walls = 0;
		float worst = 0f;

		// Uma parede de 6 × 4 m, a 9 m, para a segunda metade do teste.
		StaticBody3D wall = null;
		const float face = -6.2f;

		foreach ((float yaw, float pitch, string walk, bool walled, float x) in new (float, float, string, bool, float)[]
		{
			(-90f, -30f, "", false, Spot.X), (-90f, -10f, "", false, Spot.X), (-90f, -5.5f, "", false, Spot.X), (-90f, -4.9f, "", false, Spot.X),
			(0f, -12f, "", false, Spot.X), (90f, -20f, "", false, Spot.X), (0f, 8f, "", false, Spot.X),
			(-90f, -9f, "move_right", false, Spot.X), (-90f, -9f, "move_left", false, Spot.X),
			(-90f, -4f, "", true, Spot.X), (-90f, 3f, "", true, Spot.X), (-90f, 12f, "", true, Spot.X), (-65f, 5f, "", true, Spot.X),
			(-90f, 0f, "", true, face - 0.55f), (-90f, -40f, "", true, face - 0.55f), (-90f, 25f, "", true, face - 0.9f),
		})
		{
			if (walled && wall == null)
			{
				wall = new StaticBody3D { Name = "ParedeDoTeste" };
				wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.4f, 4f, 6f) } });
				b.GetTree().CurrentScene.AddChild(wall);
				wall.GlobalPosition = new Vector3(face + 0.2f, 2f, Spot.Z);
			}
			bool close = x != Spot.X;
			await b.Place(new Vector3(x, 1, Spot.Z), yaw, pitch, settle: 0.5);
			b.Weapon.Equip(b.Weapon.PrimaryDef, b.Weapon.SecondaryDef, 2);
			b.Health.SetHp(b.Health.MaxHp);
			await b.Tap("slot_5");
			await b.Seconds(0.6 + b.Weapon.PrimaryDef.HolsterTime);

			RegressionBench.Press("attack");
			if (walk != "") RegressionBench.Press(walk);
			await b.Seconds(0.45);

			// O ponto sob a cruz no instante em que a mão solta (andando, ele muda).
			Camera3D cam = b.GetViewport().GetCamera3D();
			RegressionBench.Release("attack");
			Grenade grenade = null;
			Vector3 eye = cam.GlobalPosition, ahead = -cam.GlobalBasis.Z;
			await b.Until(() =>
			{
				if (b.Weapon.IsThrowPending) { eye = cam.GlobalPosition; ahead = -cam.GlobalBasis.Z; }
				return (grenade = b.GetTree().Root.FindChildren("*", "RigidBody3D", true, false).OfType<Grenade>().FirstOrDefault()) != null;
			}, 0.6);
			float speed = RegressionBench.Flat(b.Player.Velocity);
			if (walk != "") RegressionBench.Release(walk);
			string name = $"{yaw}°/{pitch}°{(walk != "" ? $" andando a {RegressionBench.F(speed)} m/s" : "")}{(close ? " colado na parede" : "")}";
			b.Check(grenade != null, $"{name}: a granada saiu");
			if (grenade == null) continue;
			if (walk != "") b.AtLeast(2.0, speed, $"{name}: está mesmo andando (m/s)");

			var query = PhysicsRayQueryParameters3D.Create(eye, eye + ahead * b.Weapon.ThrowMaxRange);
			query.Exclude = new Godot.Collections.Array<Rid> { b.Player.GetRid(), grenade.GetRid() };
			var hit = b.Player.GetWorld3D().DirectSpaceState.IntersectRay(query);

			Vector3 touch = grenade.GlobalPosition;
			float through = -1f;
			bool landed = false;
			await b.Until(() =>
			{
				if (!GodotObject.IsInstanceValid(grenade)) return true;
				if (walled) through = Mathf.Max(through, grenade.GlobalPosition.X - face);
				if (!landed) touch = grenade.GlobalPosition;
				landed |= grenade.Landed;
				return landed && !close;
			}, close ? 0.9 : 5.0);
			// Colado na parede ela cai no pé de quem jogou: sai de cena antes de explodir.
			if (close && GodotObject.IsInstanceValid(grenade)) grenade.QueueFree();

			if (close)
			{
				RegressionBench.Note($"{name}: a granada chegou a {RegressionBench.F(-through)} m da face da parede, do lado de cá");
				b.AtMost(0.0, through, $"{name}: a granada não atravessa a parede (m além da face)");
			}
			else if (hit.Count > 0)
			{
				Vector3 point = hit["position"].AsVector3();
				bool onWall = Mathf.Abs(hit["normal"].AsVector3().Y) < 0.5f;
				float off = (touch - point).Length(), far = (point - eye).Length();
				RegressionBench.Note($"{name}: {(onWall ? "parede" : "chão")} a {RegressionBench.F(far)} m; tocou a {RegressionBench.F(off)} m do ponto");
				b.AtMost(walk != "" ? 0.6 : 0.35, off, $"{name}: do primeiro toque ao ponto da cruz (m)");
				aimed++;
				if (onWall) walls++;
				if (walk == "") worst = Mathf.Max(worst, off);
			}
			else
			{
				// Sem ponto ao alcance (o céu): vai reto na linha da cruz.
				var right = new Vector3(-ahead.Z, 0, ahead.X).Normalized();
				float side = Mathf.Abs((touch - eye).Dot(right)), far = RegressionBench.Flat(touch - eye);
				RegressionBench.Note($"{name}: sem ponto ao alcance; tocou a {RegressionBench.F(far)} m, {RegressionBench.F(side)} m fora da linha da cruz");
				b.AtMost(0.25, side, $"{name}: sem ponto na mira, cai na linha da cruz (m para o lado)");
			}
			await b.Until(() => !GodotObject.IsInstanceValid(grenade), 4.0);
			await b.Seconds(0.4);
		}
		wall?.QueueFree();
		RegressionBench.Note($"{aimed} arremessos com ponto na mira ({walls} em parede); o pior, parado: {RegressionBench.F(worst)} m");
		b.AtLeast(9, aimed, "arremessos com um ponto na mira");
		b.AtLeast(3, walls, "deles, em parede");
	}
}
