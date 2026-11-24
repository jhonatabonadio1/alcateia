using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O tiro cai dentro da cruz (Jhon, 21/11: "o tiro da sniper não está
/// coincidindo com o crosshair sem escopo"). A abertura da cruz é a conta
/// da câmera (<see cref="Crosshair.Opening"/>): com a SVD sem luneta (4°
/// de dispersão) ela abria metade do que o tiro espalhava. Atira 12 vezes
/// numa parede com a SVD, a AK-74 e a Desert Eagle e confere, na tela, que
/// nenhum furo cai fora da abertura e que não há desvio para um lado.
/// </summary>
public sealed class CrosshairMatchTest : RegressionTest
{
	public override string Name => "TiroNaCruz";
	public override string Description => "SVD sem luneta, AK-74 e Desert Eagle: todo furo cai dentro da abertura da cruz, sem desvio para um lado";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		foreach (string id in new[] { "svd", "ak74", "deagle" })
		{
			float yaw = -90f;
			await b.Place(new Vector3(-15, 1, 13), yaw, settle: 0.3);
			var space = b.Player.GetWorld3D().DirectSpaceState;
			for (int k = 0; k < 36; k++)
			{
				float y = k * 10f;
				Vector3 eye = b.Player.GlobalPosition + Vector3.Up * 0.6f;
				Vector3 dir = new Vector3(-Mathf.Sin(Mathf.DegToRad(y)), 0, -Mathf.Cos(Mathf.DegToRad(y)));
				var q = PhysicsRayQueryParameters3D.Create(eye, eye + dir * 25f); q.Exclude = new Godot.Collections.Array<Rid> { b.Player.GetRid() };
				var h = space.IntersectRay(q);
				if (h.Count > 0 && eye.DistanceTo((Vector3)h["position"]) > 8f) { yaw = y; break; }
			}
			await b.Place(new Vector3(-15, 1, 13), yaw, settle: 0.6);
			b.Weapon.Equip(Arsenal.Get(id), Arsenal.Get("deagle"), 0);
			await b.Seconds(0.8);
			Camera3D cam = b.Player.GetViewport().GetCamera3D();
			var crosshair = b.GetTree().Root.FindChildren("*", "", true, false).OfType<Crosshair>().FirstOrDefault();
			Vector2 center = cam.GetViewport().GetVisibleRect().Size / 2f;
			Vector3 impact = Vector3.Inf;
			b.Weapon.WorldHit += (p, n, c) => impact = p;
			float worstPx = 0, sumX = 0, sumY = 0; int n2 = 0; float spread = 0;
			for (int i = 0; i < 12; i++)
			{
				await b.Seconds(1.2);
				impact = Vector3.Inf;
				spread = b.Weapon.CurrentSpread;
				Vector2 at0 = center;
				Transform3D camT = cam.GlobalTransform;
				b.Weapon.TryAttack();
				await b.Frames(2);
				if (impact == Vector3.Inf) continue;
				// projeta com a câmera como estava no tiro
				Vector3 local = camT.AffineInverse() * impact;
				float tanHalf = Mathf.Tan(Mathf.DegToRad(cam.Fov * 0.5f));
				float py = cam.GetViewport().GetVisibleRect().Size.Y * 0.5f;
				Vector2 px = new Vector2(local.X / -local.Z, -local.Y / -local.Z) / tanHalf * py;
				worstPx = Mathf.Max(worstPx, px.Length()); sumX += px.X; sumY += px.Y; n2++;
			}
			float opening = crosshair?.Opening(spread) ?? 0f;
			b.Check(n2 >= 8, $"{id}: os tiros acertaram a parede ({n2} de 12)");
			b.Check(worstPx <= opening + 1.5f, $"{id}: o furo mais longe ({worstPx:0} px) cai dentro da abertura da cruz ({opening:0} px)");
			b.Check(Mathf.Abs(sumX / Math.Max(n2, 1)) < opening * 0.6f + 2f && Mathf.Abs(sumY / Math.Max(n2, 1)) < opening * 0.6f + 2f,
				$"{id}: sem desvio para um lado (média {sumX / Math.Max(n2, 1):0.0}, {sumY / Math.Max(n2, 1):0.0} px)");
			float gap = opening;
			GD.Print($"TiroNaCruz {id}: tela {cam.GetViewport().GetVisibleRect().Size} fov {cam.Fov:0.0} dispersão {spread:0.00}° tiros {n2} pior {worstPx:0} px média ({sumX / Math.Max(n2,1):0.0},{sumY / Math.Max(n2,1):0.0}) px; abertura da cruz {gap:0} px (dinâmica {Settings.Values.Crosshair.Dynamic}); real da dispersão {Mathf.Tan(Mathf.DegToRad(spread)) / Mathf.Tan(Mathf.DegToRad(cam.Fov * 0.5f)) * cam.GetViewport().GetVisibleRect().Size.Y * 0.5f:0} px");
		}
	}
}
