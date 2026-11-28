using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O lobo de quatro sobe e desce a escada sem tremer (alpha fechado, 26/11:
/// "tremor nas escadas no modo quadrúpede").
///
/// Mede, quadro a quadro, quanto o corpo sobe ou desce em cada passo da
/// física no meio da escada (a câmera vai presa nele) e o vaivém da distância
/// da câmera de terceira pessoa (encolher, esticar, encolher).
///
/// Medido em 26/11: nas escadas do Deserto as quinas dos degraus (a peça
/// Stairs_01R tem colisão de malha) encostavam na chapa lisa de subir, e o
/// corpo batia ora numa ora noutra — subia 3,2, 7,8, 3,6, 7,3 cm... por
/// quadro, um tranco de ±2 cm a 60 Hz. Com a chapa 3 cm acima das quinas
/// (Montagem.rampa, folga), sobe 5,7 cm em todo quadro. Na Estação, na Obra e
/// na Neon a subida já era constante (6,3, 5,7 e 7,0 cm por quadro).
/// </summary>
public abstract class WolfStairsTest : RegressionTest
{
	protected abstract string MapId { get; }
	public override string Scene => RegressionBench.Match;
	public override string Description => $"no mapa {Maps.Get(MapId).Name}, o lobo de quatro sobe e desce a escada sem tranco (o passo vertical constante) e a câmera não vai e volta";

	/// <summary>O quanto o passo vertical de cada quadro pode variar no meio da
	/// escada (desvio padrão, em metros por quadro).</summary>
	private const float Steady = 0.004f;

	/// <summary>Para descer, o lobo é posto no alto, isto para lá do último
	/// degrau (em metros). No Deserto de 27/11 (a cena de demonstração do kit)
	/// a escada de pedra acaba num patamar de 1,2 m encostado na casa, e a
	/// 1,2 m do último degrau já é parede: lá o lobo é posto no meio do patamar.</summary>
	protected virtual float TopRunUp => 1.2f;

	public override void Prepare()
	{
		RoomConfig.Settings = new RoomSettings { Map = MapId, TeamSize = 4 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Blue });
		RoomConfig.Active = true;
	}

	public override async Task Run(RegressionBench b)
	{
		b.CloseLoadout();
		b.RemoveBots();
		await b.Seconds(0.5);
		await b.BecomeWolf();
		Player p = b.Player;
		var camera = p.GetNode<Camera3D>("Head/SpringArm3D/WolfCamera");

		var ramps = b.Scene.FindChildren("pr*", "StaticBody3D", true, false).OfType<StaticBody3D>()
			.Where(body => System.Text.RegularExpressions.Regex.IsMatch(body.Name, "^pr\\d+$")).ToList();
		b.Check(ramps.Count > 0, $"o mapa tem escadas ({ramps.Count})");
		if (ramps.Count == 0) { RoomConfig.Active = false; return; }

		StaticBody3D ramp = StairPick.Open(ramps);
		Vector3 size = ((BoxShape3D)ramp.GetChild<CollisionShape3D>(0).Shape).Size;
		Basis basis = ramp.GlobalTransform.Basis;
		Vector3 up = basis.X.Normalized();
		// (um quarto para o lado: a escada da Estação tem o corrimão no meio)
		Vector3 top = ramp.GlobalPosition + basis.Y.Normalized() * size.Y * 0.5f + basis.Z.Normalized() * size.Z * 0.25f;
		Vector3 foot = top - up * size.X * 0.5f, high = top + up * size.X * 0.5f;
		Vector3 flat = new Vector3(up.X, 0, up.Z).Normalized();
		float yaw = Mathf.RadToDeg(Mathf.Atan2(-flat.X, -flat.Z));

		foreach (bool climbing in new[] { true, false })
		{
			string way = climbing ? "subindo" : "descendo";
			RegressionBench.ReleaseAll();
			if (climbing) await b.Place(foot - flat * 1.2f + Vector3.Up * 1.1f, yaw, settle: 0.6);
			else await b.Place(high + flat * TopRunUp + Vector3.Up * 1.1f, yaw + 180f, settle: 0.6);
			await b.Tap("move_forward");
			await b.Seconds(0.08);
			RegressionBench.Press("move_forward");

			var steps = new List<float>();
			float last = p.GlobalPosition.Y, lastDistance = camera.Position.Z, lastTurn = 0f;
			int flips = 0;
			for (int i = 0; i < 150; i++)
			{
				await b.PhysicsFrame();
				float y = p.GlobalPosition.Y, step = y - last;
				last = y;
				// Só em cima da chapa, longe das duas pontas (a entrada, a saída e o patamar).
				float along = (p.GlobalPosition - foot).Dot(flat), length = (high - foot).Dot(flat);
				if (along > 0.4f && along < length - 0.4f) steps.Add(Mathf.Abs(step));
				float distance = camera.Position.Z, turn = distance - lastDistance;
				if (Mathf.Abs(turn) > 0.005f)
				{
					if (lastTurn != 0f && Mathf.Sign(turn) != Mathf.Sign(lastTurn)) flips++;
					lastTurn = turn;
				}
				lastDistance = distance;
				if (climbing ? along > length + 0.5f : along < -0.5f) break;
			}
			RegressionBench.ReleaseAll();

			var middle = steps;
			float mean = middle.Count > 0 ? middle.Average() : 0f;
			float spread = middle.Count > 0 ? Mathf.Sqrt(middle.Average(s => (s - mean) * (s - mean))) : 0f;
			RegressionBench.Note($"{Maps.Get(MapId).Name}, {way}: {middle.Count} quadros na escada, {mean * 100:0.00} cm por quadro (de {middle.DefaultIfEmpty().Min() * 100:0.0} a {middle.DefaultIfEmpty().Max() * 100:0.0}), desvio {spread * 100:0.00} cm; a câmera foi e voltou {flips} vez(es)");
			b.Check(middle.Count > 20, $"{way}: o lobo de quatro anda pela escada ({middle.Count} quadros nela)");
			b.Check(spread <= Steady, $"{way}: sem tranco, o passo vertical constante (desvio {spread * 100:0.00} cm por quadro)");
			b.Check(flips <= 1, $"{way}: a câmera não vai e volta ({flips})");
		}
		RoomConfig.Active = false;
	}
}

public sealed class DesertoWolfStairsTest : WolfStairsTest
{
	public override string Name => "EscadaDeQuatroDeserto";
	protected override string MapId => "deserto";
	protected override float TopRunUp => 0.55f;
}

public sealed class EstacaoWolfStairsTest : WolfStairsTest
{
	public override string Name => "EscadaDeQuatroEstacao";
	protected override string MapId => "estacao";
}

/// <summary>
/// Qual escada do mapa os testes de escada usam: a primeira de entrada livre.
/// Os testes andam um quarto da largura para o lado do meio (a escada da Estação
/// tem o corrimão no meio); numa escada por dentro de uma casa — as casas de
/// escada do Deserto, que ganharam a chapa em 27/11 e passaram a vir primeiro —
/// esse lado é o batente da porta, e o corpo não entra. Livre: uma bola de 32 cm,
/// a 1 m do chão, vai de 1,2 m antes do pé até 60 cm escada adentro, por essa
/// linha, sem bater em nada.
/// </summary>
public static class StairPick
{
	public static StaticBody3D Open(List<StaticBody3D> ramps)
	{
		foreach (StaticBody3D ramp in ramps)
		{
			Vector3 size = ((BoxShape3D)ramp.GetChild<CollisionShape3D>(0).Shape).Size;
			Basis basis = ramp.GlobalTransform.Basis;
			Vector3 up = basis.X.Normalized();
			Vector3 top = ramp.GlobalPosition + basis.Y.Normalized() * size.Y * 0.5f + basis.Z.Normalized() * size.Z * 0.25f;
			Vector3 foot = top - up * size.X * 0.5f, flat = new Vector3(up.X, 0, up.Z).Normalized();
			var query = new PhysicsShapeQueryParameters3D
			{
				Shape = new SphereShape3D { Radius = 0.32f },
				Transform = new Transform3D(Basis.Identity, foot - flat * 1.2f + Vector3.Up * 1.0f),
				Motion = flat * 1.8f + Vector3.Up * 0.35f,
				CollisionMask = 1
			};
			float[] cast = ramp.GetWorld3D().DirectSpaceState.CastMotion(query);
			if (cast.Length == 0 || cast[0] >= 0.999f) return ramp;
		}
		return ramps[0];
	}
}
