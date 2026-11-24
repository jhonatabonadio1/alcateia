using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// As mãos de primeira pessoa do AR-15 são as mãos padrão do jogo
/// (<c>assets/armas/fp/maos/</c>), iguais para os dois times: entram no
/// esqueleto da arma no lugar dos braços do X Bot e seguem as animações.
/// Armas de outro esqueleto (a pistola) ficam com as mãos delas.
/// Com <c>--fotos=pasta</c> fotografa parado, atirando e de fora.
/// </summary>
public sealed class RigHandsTest : RegressionTest
{
	public override string Name => "MaosPadrao";
	public override string Description => "o AR-15 usa as mãos padrão (assets/armas/fp/maos), iguais nos dois times; a pistola também";

	private string _photos = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--fotos="))?["--fotos=".Length..];

	// Para fotografar, a partida na Vila: tem sol. O teste em si roda no treino.
	public override string Scene => _photos == null ? RegressionBench.Training : RegressionBench.Match;

	public override void Prepare()
	{
		if (_photos == null) return;

		RoomConfig.Settings = new RoomSettings { Map = "armazem_estendido", TeamSize = 4 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Blue });
		RoomConfig.Active = true;
	}

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		HideMenus(b);
		var view = b.Player.GetNode<ViewModel>("Head/Camera3D/ViewModel");
		var health = b.Player.GetNode<Health>("Health");
		if (_photos != null)
		{
			await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 20);
			await b.Place(new Vector3(-30, 1, 0), -90f, settle: 0.4);
		}
		else await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.4);

		b.Weapon.Equip(Arsenal.Get("ar15"), Arsenal.Get("deagle"), 1);
		b.Weapon.SelectSlot(Slot.Primary);
		await b.Seconds(0.8);

		Node3D held = view.Held;
		b.Check(held != null && view.HasRig, "ar15: braços animados na mão");
		if (held == null) return;

		var hands = held.FindChild("maos", true, false) as MeshInstance3D;
		b.Check(hands != null && view.HasRigHands && hands.GetParent() is Skeleton3D, "as mãos padrão entram no esqueleto da arma");
		if (hands == null) return;
		// As mãos novas (20/11, "luz, gráfico AAA+"): o material padrao.tres é
		// um shader (maos.gdshader) com cor, ORM, relevo e LED; eram de paleta
		// chapada, StandardMaterial3D, e com menos de 4 000 triângulos.
		b.Check(hands.MaterialOverride is ShaderMaterial { ResourcePath: "res://src/assets/armas/fp/maos/padrao.tres" },
			"as mãos usam o material padrao.tres");
		int faces = hands.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Index].AsInt32Array().Length / 3;
		b.Check(faces > 800 && faces < 12000, $"as mãos são leves ({faces} triângulos)");
		b.Check(held.FindChild("bracos", true, false) is Node3D { Visible: false }, "os braços do X Bot ficam escondidos");

		foreach (int team in new[] { Teams.Blue, Teams.Orange })
		{
			health.Team = team;
			await b.Frames(3);
			string tag = team == Teams.Blue ? "azul" : "laranja";
			b.Check(hands.IsVisibleInTree(), $"time {tag}: as mesmas mãos aparecem");
			if (team == Teams.Orange) break;
			await Photo(b, "1_parada");

			RegressionBench.Press("attack");
			await b.Frames(4);
			await Photo(b, "2_tiro");
			RegressionBench.Release("attack");
			await b.Seconds(0.3);
			b.Check(view.HasRig, "a animação segue tocando");
			await Orbit(b, view, held, "maos");
		}

		// A pistola (19/11) é do mesmo esqueleto: leva as mesmas mãos padrão.
		b.Weapon.SelectSlot(Slot.Secondary);
		await b.Seconds(0.8);
		b.Check(view.Held != null && view.HasRigHands && view.Held.FindChild("maos", true, false) != null,
			"a pistola usa as mesmas mãos padrão");
	}

	/// <summary>Fotos de fora, com uma câmera de teste em volta dos braços:
	/// é a única forma de ver as mãos, que de dentro ficam atrás da arma.</summary>
	private async Task Orbit(RegressionBench b, ViewModel view, Node3D held, string tag)
	{
		if (string.IsNullOrEmpty(_photos)) return;

		var own = b.Player.GetNode<Camera3D>("Head/Camera3D");
		var cam = new Camera3D { Near = 0.005f, Fov = 45f, CullMask = 0xFFFFFFFDu };   // sem a camada 2: o corpo de 3ª pessoa
		held.AddChild(cam);
		Vector3 center = held.GlobalPosition + held.GlobalBasis * new Vector3(0f, -0.1f, -0.22f);
		var views = new (string, Vector3)[]
		{
			("lado_dir", new Vector3(0.5f, 0.05f, 0f)),
			("lado_esq", new Vector3(-0.5f, 0.05f, 0f)),
			("frente", new Vector3(0.1f, 0.1f, -0.55f)),
			("cima", new Vector3(0f, 0.55f, 0.1f)),
			("tres_quartos", new Vector3(-0.35f, 0.2f, -0.35f)),
		};
		foreach (var (name, offset) in views)
		{
			cam.GlobalPosition = center + held.GlobalBasis * offset;
			cam.LookAt(center, Vector3.Up);
			cam.Current = true;
			await b.Frames(2);
			await Photo(b, $"{tag}_o_{name}");
		}
		own.Current = true;
		cam.QueueFree();
		await b.Frames(2);
	}

	private static void HideMenus(RegressionBench b)
	{
		foreach (var menu in b.GetTree().Root.FindChildren("*", "", true, false).OfType<DifficultyMenu>())
			menu.Visible = false;
	}

	private async Task Photo(RegressionBench b, string name)
	{
		if (string.IsNullOrEmpty(_photos)) return;

		await b.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		DirAccess.MakeDirRecursiveAbsolute(_photos);
		b.GetViewport().GetTexture().GetImage().SaveJpg($"{_photos}/{name}.jpg", 0.92f);
	}
}
