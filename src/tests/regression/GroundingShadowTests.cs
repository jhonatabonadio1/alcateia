using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// A sombra de apoio dos personagens (<see cref="GroundingShadow"/>; pedido do
/// Jhon, 26/11: sombras mais precisas e definidas, mesmo no escuro, sem mexer
/// na saturação): um par de luzes que se anulam, em que só os personagens
/// fazem sombra.
/// </summary>
public sealed class GroundingShadowTest : RegressionTest
{
	public override string Name => "SombraDeApoio";
	public override string Description => "o mapa carregado tem a sombra de apoio: duas luzes direcionais iguais e opostas (uma soma, a outra subtrai — o mapa não muda de brilho nem de cor), só a que soma tem sombra e só os personagens (humano, lobo, robô, o corpo do dono) a fazem; cai para o lado do sol, mais a pino";
	public override string Scene => SetorZeroTest.Field;

	public override async Task Run(RegressionBench b)
	{
		Node map = b.Scene.GetNode("Map").GetChild(0);
		var shadow = map.GetNodeOrNull<GroundingShadow>(GroundingShadow.NodeName);
		b.Check(shadow != null, "o mapa carregado pelo MapSlot ganha a sombra de apoio");
		if (shadow == null) return;
		await b.Frames(2);

		DirectionalLight3D adds = shadow.Casting, takes = shadow.Taking;
		b.Check(adds != null && takes != null && !adds.LightNegative && takes.LightNegative, "são duas luzes: uma soma, a outra subtrai");
		b.Check(Mathf.IsEqualApprox(adds.LightEnergy, takes.LightEnergy) && adds.LightColor == takes.LightColor
			&& adds.GlobalBasis.IsEqualApprox(takes.GlobalBasis) && adds.LightCullMask == takes.LightCullMask,
			"iguais em tudo menos no sinal: no mapa inteiro uma anula a outra (o brilho e a saturação não mudam)");
		b.Check(adds.LightSpecular == 0f && takes.LightSpecular == 0f && adds.LightVolumetricFogEnergy == 0f && takes.LightVolumetricFogEnergy == 0f
			&& adds.SkyMode == DirectionalLight3D.SkyModeEnum.LightOnly && takes.SkyMode == DirectionalLight3D.SkyModeEnum.LightOnly,
			"sem brilho especular, sem entrar na névoa nem no céu");
		b.Check(adds.ShadowEnabled && !takes.ShadowEnabled, "só a que soma tem sombra: onde um corpo a tapa, sobra a que subtrai");
		b.Check(adds.ShadowCasterMask == GroundingShadow.Casters && (GroundingShadow.Casters & 1u) == 0, "e só as camadas dos personagens fazem sombra para ela (a camada 1, do mapa, não)");
		b.Check(adds.DirectionalShadowMaxDistance <= 40f && adds.LightAngularDistance == 0f, $"o mapa de sombra cobre só {RegressionBench.F(adds.DirectionalShadowMaxDistance)} m em volta da câmera, sem o tamanho do sol: é o que a deixa definida");

		// A direção: o rumo do sol do mapa, nunca mais deitada que o mínimo.
		var sun = map.GetNode<DirectionalLight3D>("Sol");
		Vector3 sunRay = -sun.GlobalBasis.Z.Normalized(), ray = -adds.GlobalBasis.Z.Normalized();
		float elevation = Mathf.RadToDeg(Mathf.Asin(-ray.Y)), sunElevation = Mathf.RadToDeg(Mathf.Asin(-sunRay.Y));
		RegressionBench.Note($"o sol do mapa cai a {RegressionBench.F(sunElevation)}°; a sombra de apoio, a {RegressionBench.F(elevation)}°");
		b.Check(elevation >= shadow.MinElevation - 0.5f, "cai mais a pino que o mínimo (curta, ao pé do corpo)");
		var flatSun = new Vector2(sunRay.X, sunRay.Z).Normalized();
		var flat = new Vector2(ray.X, ray.Z);
		b.Check(flat.Length() < 0.01f || flat.Normalized().Dot(flatSun) > 0.999f, "para o mesmo lado da sombra do sol");
		b.Check(GroundingShadow.DirectionFor((DirectionalLight3D)null, 64f).IsEqualApprox(Vector3.Down), "sem sol no mapa, cai a pino");
		Vector3 steep = GroundingShadow.DirectionFor(new Vector3(0.1f, -1f, 0f), 64f);
		b.Check(steep.IsEqualApprox(new Vector3(0.1f, -1f, 0f).Normalized()), "um sol já a pino é seguido como está");

		// Quem faz a sombra: os robôs (personagens) e o corpo do jogador; o mapa, não.
		bool Casts(Node root) => root.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
			.Any(m => (m.Layers & GroundingShadow.Casters) != 0 && m.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off);
		var robot = map.GetNode<Dummy>("Tutorial/Alvos/proximo_1");
		b.Check(Casts(robot.GetNode("Visual")), "o robô de treino faz a sombra de apoio");
		b.Check(Casts(b.Player), "o corpo do jogador faz a sombra de apoio (mesmo o que a câmera dele não desenha)");
		Node drawing = map.FindChild("Desenho", true, false);
		b.Check(drawing != null && !Casts(drawing), "o mapa não faz essa sombra (só a do sol dele)");

		// Na qualidade Baixa não entra; da Média para cima, sim.
		int before = Settings.Values.Quality;
		Settings.Values.Quality = 0;
		shadow.Apply();
		b.Check(!adds.Visible && !takes.Visible, "na qualidade Baixa as duas luzes saem juntas");
		Settings.Values.Quality = 2;
		shadow.Apply();
		b.Check(adds.Visible && takes.Visible, "na Alta, as duas entram");
		Settings.Values.Quality = before;
		shadow.Apply();
	}
}
