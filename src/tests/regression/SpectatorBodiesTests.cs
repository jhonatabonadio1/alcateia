using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// No espectador, só o corpo de quem se vê em primeira pessoa some — e por
/// inteiro (20/11, Jhon: "personagens com problemas de renderização,
/// partes do corpo sumindo, muitas vezes no espectador"). Troca de aliado
/// várias vezes, alterna primeira pessoa e vista de cima, e a cada passo
/// confere todos os corpos: os outros não podem ter nada escondido; o
/// visto, nada à mostra.
/// </summary>
public sealed class SpectatorBodiesTest : RegressionTest
{
	public override string Name => "EspectadorCorposInteiros";
	public override string Description => "trocando de aliado no espectador, ninguém fica com parte do corpo sumida";
	public override string Scene => RegressionBench.Match;

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => b.Bots().Length >= 4, 6.0);
		b.CloseLoadout();
		await b.Seconds(0.5);
		var bots = b.Bots();
		if (bots.Length < 3) { b.Check(false, $"bots suficientes ({bots.Length})"); return; }
		var killer = bots[0];
		killer.GetNode<Health>("Health").Team = b.Health.Team == Teams.Blue ? Teams.Orange : Teams.Blue;
		for (int i = 1; i < bots.Length; i++) bots[i].GetNode<Health>("Health").Team = b.Health.Team;
		RegressionBench.Freeze(killer);
		killer.GlobalPosition = new Vector3(0, -50, 0);
		b.Health.TrainingRespawnDelay = 60f;
		b.Health.NoteAttacker(killer);
		b.Health.TakeDamage(99999f);
		await b.Seconds(1.4);
		KillCam cam = b.GetTree().Root.FindChildren("*", "", true, false).OfType<KillCam>().FirstOrDefault();
		b.Check(cam != null, "espectador aberto");
		if (cam == null) return;

		int stuck = 0, shown = 0, steps = 0;
		string first = null;
		for (int i = 0; i < 24; i++)
		{
			await b.Tap(i % 6 == 5 ? "jump" : "attack");
			await b.Seconds(0.45);
			steps++;
			Node3D watched = cam.Mode == "primeira" ? cam.Watching : null;
			bool wolf = watched?.GetNodeOrNull<FormController>("FormController")?.ShowsWolf ?? false;
			foreach (var body in bots.Append(b.Player))
			{
				var visuals = body.FindChildren("*", nameof(VisualInstance3D), true, false).OfType<VisualInstance3D>()
					.Where(v => v.IsVisibleInTree() && v is not Light3D && !(v.GetParent() is ViewModel) && !IsLent(v)).ToList();
				if (body == watched && !wolf)
				{
					var visible = visuals.Where(v => (v.Layers & ~KillCam.WatchedLayer) != 0 && Under(v, body, "Human")).ToList();
					shown += visible.Count;
					if (visible.Count > 0) first ??= $"{body.Name} visto mostra {visible[0].Name} (camadas {visible[0].Layers})";
				}
				else
				{
					var hidden = visuals.Where(v => (v.Layers & KillCam.WatchedLayer) != 0).ToList();
					stuck += hidden.Count;
					if (hidden.Count > 0) first ??= $"{body.Name} (não visto) com {hidden[0].Name} escondido";
				}
			}
		}
		RegressionBench.Note($"{steps} passos; {first ?? "tudo certo"}");
		b.Check(stuck == 0, $"quem não se vê não tem parte escondida ({stuck})");
		b.Check(shown == 0, $"de quem se vê em primeira pessoa, nada do humano aparece ({shown})");
	}

	private static bool Under(Node node, Node body, string part) => body.GetNodeOrNull(part) is Node root && root.IsAncestorOf(node);

	private static bool IsLent(Node node)
	{
		for (Node n = node; n != null; n = n.GetParent()) if (n is ViewModel) return true;
		return false;
	}
}
