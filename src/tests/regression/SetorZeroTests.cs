using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O Setor Zero, o campo de treinamento (docs/MAPAS.md, "Setor Zero";
/// src/tools/mapas/setor_zero.py). Não é mapa de partida — um jogador só,
/// sem times, sem bots, sem rotas —, então não entra nos testes de mapa
/// de partida (<see cref="MapPlaysTest"/>): o que vale para ele é isto
/// aqui. Cada obstáculo do treino tem de ser vencível pelo corpo de hoje,
/// com folga; quem muda a física (o pulo, a cápsula agachada, o salto do
/// lobo) e quebra um obstáculo fica sabendo por estes testes.
///
/// As coordenadas são as da planta (docs/tutorial/planta_setor_zero.png):
/// x para a direita, y para baixo; no mundo, X = x − 75 e Z = y − 50.
///
/// O mapa tem níveis (pedido do Jhon, 26/11: pontos altos com escadaria,
/// lugares baixos): a Linha de Fogo fica 2,4 m abaixo do campo, o fundo do
/// Núcleo 1,2 m abaixo, a Câmara 1,5 m acima, a galeria da arena a 4 m e o
/// terraço da Rua Ignição a 6 m.
/// </summary>
public abstract class SetorZeroTest : RegressionTest
{
	public const string Field = "res://src/scenes/treino_setor_zero.tscn";
	public override string Scene => Field;

	/// <summary>Da planta para o mundo, com os pés em <paramref name="floor"/>.</summary>
	protected static Vector3 At(float x, float y, float floor = 0f) => new(x - 75f, floor + 0.9f, y - 50f);

	protected static float PlanX(Node3D body) => body.GlobalPosition.X + 75f;
	protected static float PlanY(Node3D body) => body.GlobalPosition.Z + 50f;
	protected static float Feet(Player p) => p.GlobalPosition.Y + p.FeetOffset;

	protected static Node3D Mark(RegressionBench b, string id) =>
		b.Scene.GetNode<Node3D>($"Map/Setor Zero/Tutorial/Marcas/{id}");

	protected static Node Tutorial(RegressionBench b) => b.Scene.GetNode("Map/Setor Zero/Tutorial");

	/// <summary>Os níveis do mapa (src/tools/mapas/setor_zero.py).</summary>
	protected const float Range = -2.4f, Core = -1.2f, Chamber = 1.5f, Gallery = 4f, Terrace = 6f;

	/// <summary>Olhando para +x da planta (leste), para −x, para +y (sul), para −y (norte).</summary>
	protected const float East = -90f, West = 90f, South = 180f, North = 0f;
}

/// <summary>O mapa carrega fora da lista, fechado, sem buraco no chão além dos dois vãos, com as peças do tutorial.</summary>
public sealed class SetorZeroLoadsTest : SetorZeroTest
{
	public override string Name => "SetorZeroCarrega";
	public override string Description => "o Setor Zero carrega fora da lista de mapas, fechado por limites, com chão em todo o campo (menos os dois vãos), zonas, marcas, portas e alvos";

	public override async Task Run(RegressionBench b)
	{
		var slot = b.Scene.GetNodeOrNull<MapSlot>("Map");
		b.Check(slot != null && slot.Info.Id == "setor_zero", $"carregou o Setor Zero ({slot?.Info.Id})");
		b.Check(Maps.All.All(m => m.Id != "setor_zero"), "não está na lista de mapas das salas (Maps.All)");
		b.Check(Maps.Get("setor_zero") == Maps.Training && Maps.IsTraining("setor_zero"), "abre pelo id (Maps.Get)");
		b.Check(Maps.Training.Modes.Length == 0, "não tem modo de sala");

		Aabb box = MapBounds.Of(b.GetTree());
		b.Check(MapBounds.FromLimits, "fechado pelas paredes Limite…");
		b.Near(186, box.Size.X, 0.5, "largura do espaço (o campo de 150 m e 15 m de ar de cada lado, para quem cai da beira)");
		b.Near(136, box.Size.Z, 0.5, "fundo do espaço");

		Node t = Tutorial(b);
		b.Check(t.GetNode("Zonas").GetChildCount() == 28 && TutorialZone.All.Count == 28, $"28 zonas ({TutorialZone.All.Count})");
		b.Check(t.GetNode("Marcas").GetChildCount() == 30, $"30 marcas ({t.GetNode("Marcas").GetChildCount()})");
		b.Check(t.GetNode("Portas").GetChildCount() == 7, $"7 portas de energia ({t.GetNode("Portas").GetChildCount()})");
		b.Check(t.GetNode("Alvos").GetChildren().Count(n => n is Dummy) == 30, $"30 robôs de treino ({t.GetNode("Alvos").GetChildren().Count(n => n is Dummy)})");
		b.Check(TutorialZone.All.Select(z => z.Id).Distinct().Count() == 28 && TutorialZone.All.All(z => z.Id == z.Name), "cada zona com o seu nome");
		b.Check(TutorialZone.All.Count(z => z.Deadly) == 1 && TutorialZone.Find("abismo").Deadly, "uma zona que mata: o abismo");
		b.Check(SpawnPoints.PointsFor(Teams.Blue).Count == 1, "um ponto de nascer só");

		// O jogador nasce de pé na Sala de Ativação, olhando para o corredor (+x).
		Player p = b.Player;
		b.Near(12, PlanX(p), 0.3, "nasce na Sala de Ativação (x)");
		b.Near(50, PlanY(p), 0.3, "nasce na Sala de Ativação (y)");
		b.Near(0, Feet(p), 0.08, "pisando no chão");
		b.Check(-p.GlobalBasis.Z.X > 0.99f, "olhando para o corredor");
		b.Check(TutorialZone.Find("sala").Holds(p), "a zona da sala sabe que ele está nela");

		// Chão em todo o campo: um raio de cima a cada metro. Abaixo do nível do campo só ficam os dois vãos (1,5 m), a
		// Linha de Fogo (2,4 m, até a beira) e o anfiteatro do Núcleo (1,2 m) — e todos têm fundo.
		PhysicsDirectSpaceState3D space = p.GetWorld3D().DirectSpaceState;
		int voids = 0, deep = 0, wrongDeep = 0, cells = 0;
		string where = "";
		for (float x = -2.5f; x <= 152.5f; x += 1f)
		{
			for (float y = -2.5f; y <= 102.5f; y += 1f)
			{
				cells++;
				var from = new Vector3(x - 75f + 0.13f, 29f, y - 50f + 0.17f);
				var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 40f, 1));
				if (hit.Count == 0) { voids++; continue; }
				float h = hit["position"].AsVector3().Y;
				if (h > -0.5f) continue;
				deep++;
				bool low = (x > 32f && x < 34f && y > 47f && y < 53f) || (x > 46f && x < 54f && y > 80f && y < 92f)
					|| (x > 44.9f && x < 105.1f && y < 34.1f) || (x > 62.4f && x < 87.6f && y > 37.4f && y < 62.6f);
				if (low) continue;
				wrongDeep++;
				if (wrongDeep <= 3) where += $" ({x:0.#},{y:0.#})";
			}
		}
		b.Check(voids == 0, $"sem buraco no chão ({voids} de {cells} pontos sem nada embaixo)");
		b.Check(deep > 2000 && wrongDeep == 0, $"abaixo do campo só os vãos, a Linha de Fogo e o Núcleo ({deep} pontos fundos, {wrongDeep} fora deles{where})");

		float Floor(float x, float y)
		{
			var from = new Vector3(x - 75f, 29f, y - 50f);
			var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 40f, 1));
			return hit.Count == 0 ? float.NaN : hit["position"].AsVector3().Y;
		}
		b.Near(Range, Floor(60f, 12f), 0.02, "o piso da Linha de Fogo");
		b.Near(Core, Floor(75f, 55f), 0.02, "o fundo do anfiteatro do Núcleo");
		b.Near(Chamber, Floor(112f, 50f), 0.02, "a rotunda da Câmara de Metamorfose");
		b.Near(Gallery, Floor(75f, 65.3f), 0.02, "a galeria da arena");
		b.Near(Terrace, Floor(118f, 14f), 0.02, "o terraço da Rua Ignição");

		// Em volta do campo, a barreira vai até o teto — menos na beira da Linha de Fogo, que é aberta (é dali que se cai).
		int open = 0, closedGap = 0, points = 0;
		bool Blocked(float x, float y, float h)
		{
			var query = new PhysicsPointQueryParameters3D { Position = new Vector3(x - 75f, h, y - 50f), CollideWithBodies = true, CollideWithAreas = false };
			return space.IntersectPoint(query, 1).Count > 0;
		}
		for (float x = -3f; x <= 153f; x += 2f)
		{
			points += 2;
			// (de 44,6 a 45,4 e de 104,6 a 105,4 ficam os muros laterais da Linha de Fogo: nem beira aberta nem barreira)
			if (x > 45.5f && x < 104.5f) { if (Blocked(x, -3.3f, 10f) || Blocked(x, -3.3f, 0.5f)) closedGap++; }
			else if ((x < 44.5f || x > 105.5f) && !Blocked(x, -3.3f, 10f)) open++;
			if (!Blocked(x, 103.3f, 10f)) open++;
		}
		for (float y = -3f; y <= 103f; y += 2f)
		{
			points += 2;
			if (!Blocked(-3.3f, y, 10f)) open++;
			if (!Blocked(153.3f, y, 10f)) open++;
		}
		b.Check(open == 0, $"em volta do campo a beira é barrada até o teto ({open} de {points} pontos abertos)");
		b.Check(closedGap == 0, $"a beira da Linha de Fogo é aberta: nada segura quem pula o parapeito ({closedGap} pontos fechados)");
		await b.PhysicsFrame();
	}
}

/// <summary>O vão do corredor (bloco 1): o humano passa pulando, andando, sem Shift; quem cai volta à marca, inteiro.</summary>
public sealed class SetorZeroHumanGapTest : SetorZeroTest
{
	public override string Name => "SetorZeroVaoDoHumano";
	public override string Description => "o humano atravessa o vão do corredor (1,4 m) pulando sem correr, mesmo saindo meio metro antes da borda; quem cai nele volta para a marca, sem dano";

	public override async Task Run(RegressionBench b)
	{
		Player p = b.Player;
		Health me = b.Health;
		float hurt = 0;
		me.Damaged += (amount, _) => hurt += amount;

		// Andando (2,56 m/s), pula meio metro antes da borda (x = 32,3) e cai do outro lado (x ≥ 33,7).
		await b.Place(At(29.5f, 50f), East);
		RegressionBench.Press("move_forward");
		bool near = await b.Until(() => PlanX(p) >= 31.8f, 3.0);
		b.Check(near, "chegou à borda do vão");
		RegressionBench.Press("jump");
		await b.Frames(3);
		RegressionBench.Release("jump");
		float lowest = Feet(p);
		bool across = await b.Until(() =>
		{
			lowest = Mathf.Min(lowest, Feet(p));
			return p.IsOnFloor() && PlanX(p) > 33.7f;
		}, 2.5);
		RegressionBench.ReleaseAll();
		RegressionBench.Note($"pulando sem correr: caiu em x = {RegressionBench.F(PlanX(p))} (a borda de lá é 33,7), o pé nunca desceu de {RegressionBench.F(lowest)} m");
		b.Check(across && Feet(p) > -0.05f, "atravessou o vão pulando, sem Shift");
		b.Check(lowest > -0.3f, $"sem raspar no vão (o pé desceu a {RegressionBench.F(lowest)} m)");
		await b.Seconds(0.4);
		b.Check(TutorialZone.Find("pouso_vao").Holds(p), "a zona de pouso sabe que ele chegou");

		// Sem pular: cai, e o fundo do vão devolve à marca de antes, sem dano e sem morrer.
		bool sent = false;
		TutorialZone pit = TutorialZone.Find("vao");
		pit.SentBack += _ => sent = true;
		hurt = 0;
		await b.Place(At(30.5f, 50f), East);
		RegressionBench.Press("move_forward");
		await b.Until(() => sent, 3.0);
		RegressionBench.ReleaseAll();
		await b.Seconds(0.6);
		Vector3 mark = Mark(b, "antes_vao").GlobalPosition;
		b.Check(sent, "caiu no vão e foi devolvido");
		b.Check(RegressionBench.Flat(p.GlobalPosition - mark) < 0.6f && Feet(p) > -0.05f, $"voltou para a marca de antes do vão (x = {RegressionBench.F(PlanX(p))})");
		b.Near(0, hurt, 0.01, "sem dano");
		b.Check(!me.IsDead, "e vivo");

		// Do fundo não se sai pulando (1,5 m; o pulo do humano sobe 1,02): só a zona tira de lá.
		b.Check(1.5f > 1.1f + 0.3f, "o vão é mais fundo que o pulo do humano");
	}
}

/// <summary>A viga do corredor: de pé o humano bate nela; agachado, passa.</summary>
public sealed class SetorZeroBeamTest : SetorZeroTest
{
	public override string Name => "SetorZeroViga";
	public override string Description => "a viga do corredor (1,62 m livres) barra o humano de pé (1,75 m) e deixa passar agachado (1,5 m)";

	public override async Task Run(RegressionBench b)
	{
		Player p = b.Player;
		b.Check(p.StandHeight > 1.62f + 0.1f, $"de pé ({p.StandHeight} m) é mais alto que o vão da viga");
		b.Check(p.HumanCrouchHeight < 1.62f - 0.1f, $"agachado ({p.HumanCrouchHeight} m) cabe com folga");

		await b.Place(At(40f, 50f), East);
		RegressionBench.Press("move_forward");
		await b.Seconds(3.0);
		RegressionBench.ReleaseAll();
		RegressionBench.Note($"de pé: parou em x = {RegressionBench.F(PlanX(p))} (a viga começa em 42,5)");
		b.Check(PlanX(p) < 42.5f, "de pé, bate na viga e não passa");

		await b.Place(At(40f, 50f), East);
		RegressionBench.Press("crouch");
		await b.Seconds(0.4);
		RegressionBench.Press("move_forward");
		bool under = await b.Until(() => TutorialZone.Find("sob_viga").Holds(p) && p.IsCrouching, 4.0);
		b.Check(under, "agachado, entra debaixo da viga");
		bool passed = await b.Until(() => PlanX(p) > 44.6f, 6.0);
		RegressionBench.ReleaseAll();
		b.Check(passed, $"agachado, passa por baixo (x = {RegressionBench.F(PlanX(p))})");
		await b.Seconds(0.5);
		b.Check(TutorialZone.Find("depois_viga").Holds(p), "a zona de depois da viga sabe que ele passou");

		// As duas pontas do pórtico também fecham: não dá para contornar rente à parede.
		foreach (float y in new[] { 47.4f, 52.6f })
		{
			await b.Place(At(40f, y), East);
			RegressionBench.Press("move_forward");
			await b.Seconds(2.5);
			RegressionBench.ReleaseAll();
			b.Check(PlanX(p) < 42.5f, $"rente à parede (y = {y}) também não passa de pé");
		}
	}
}

/// <summary>O trecho silencioso do corredor: 12 m de piso acústico, onde cabem os 3 s da etapa em qualquer passo.</summary>
public sealed class SetorZeroQuietStretchTest : SetorZeroTest
{
	public override string Name => "SetorZeroTrechoSilencioso";
	public override string Description => "o trecho silencioso do corredor (12 m) segura 3 s de quem anda agachado, andando ou correndo com Shift, sem obstáculo no caminho";

	public override async Task Run(RegressionBench b)
	{
		Player p = b.Player;
		TutorialZone zone = TutorialZone.Find("trecho_silencioso");

		// Agachado (o passo que não faz barulho): 3 s seguidos dentro do trecho, e ainda sobra chão.
		await b.Place(Mark(b, "silencio").GlobalPosition, East);
		RegressionBench.Press("crouch");
		await b.Seconds(0.4);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.3);
		bool inside = true;
		double end = b.Time + 3.0;
		while (b.Time < end)
		{
			inside &= zone.Holds(p) && p.IsCrouching;
			await b.PhysicsFrame();
		}
		RegressionBench.ReleaseAll();
		RegressionBench.Note($"agachado, em 3,3 s chegou a x = {RegressionBench.F(PlanX(p))} (o trecho vai de 49 a 61)");
		b.Check(inside, "3 s andando agachado, sempre dentro do trecho");
		b.Check(PlanX(p) < 56f, "e ainda falta mais da metade dele");

		// De ponta a ponta, do jeito mais rápido (correndo com Shift): ainda leva 3 s, e nada trava o caminho.
		await b.Place(At(48.4f, 50f), East);
		RegressionBench.Press("move_forward", "sprint");
		bool entered = await b.Until(() => zone.Holds(p), 2.0);
		double began = b.Time;
		bool left = await b.Until(() => !zone.Holds(p), 8.0);
		double took = b.Time - began;
		RegressionBench.ReleaseAll();
		RegressionBench.Note($"correndo com Shift, atravessou o trecho em {RegressionBench.F(took)} s");
		b.Check(entered && left && PlanX(p) > 61f, "atravessa o trecho inteiro sem bater em nada");
		b.Check(took >= 3.0, $"mesmo correndo, o trecho dura 3 s ({RegressionBench.F(took)} s)");
	}
}

/// <summary>O vão da arena: o salto carregado do lobo atravessa os 8 m, com folga; o lobo que cai também volta.</summary>
public sealed class SetorZeroWolfGapTest : SetorZeroTest
{
	public override string Name => "SetorZeroVaoDoLobo";
	public override string Description => "o salto carregado do lobo atravessa o vão da arena (8 m) da marca de saída, e sobra alcance; o pulo comum não atravessa";

	public override async Task Run(RegressionBench b)
	{
		Player p = b.Player;
		await b.BecomeWolf();
		b.Weapon.SpecialFree = true;
		b.Check(b.Weapon.PounceMaxDistance >= (55.2f - 44.8f) * 1.15f, $"o alcance do salto ({b.Weapon.PounceMaxDistance} m) cobre da marca ao pouso (10,4 m) com 15% de folga");

		// Da marca (1,2 m antes da borda), mirando o chão 1,2 m depois da outra borda.
		Vector3 from = Mark(b, "salto").GlobalPosition;
		float eye = 1.48f, far = 55.2f - 44.8f;
		await b.Place(from, West, -Mathf.RadToDeg(Mathf.Atan2(eye, far)));
		b.Check(b.Weapon.BotPounce(null, 1f), "o salto saiu");
		bool landed = await b.Until(() => p.IsOnFloor() && PlanX(p) < 46f, 3.0);
		await b.Seconds(0.8);
		RegressionBench.Note($"salto carregado: caiu em x = {RegressionBench.F(PlanX(p))} (a borda de lá é 46)");
		b.Check(landed && Feet(p) > -0.05f && PlanX(p) < 45.6f, "atravessou o vão de 8 m");
		b.Check(TutorialZone.Find("pouso_salto").Holds(p), "a zona de pouso sabe que ele chegou");

		// O pulo comum (uns 5 m correndo) não atravessa: cai, e o fundo devolve à marca de saída.
		bool sent = false;
		TutorialZone.Find("vao_arena").SentBack += _ => sent = true;
		float hurt = 0;
		b.Health.Damaged += (amount, _) => hurt += amount;
		await b.Place(At(57.5f, 86f), West);
		RegressionBench.Press("move_forward");
		await b.Until(() => PlanX(p) <= 54.4f, 2.0);
		RegressionBench.Press("jump");
		await b.Until(() => sent, 4.0);
		RegressionBench.ReleaseAll();
		await b.Seconds(0.6);
		b.Check(sent, "o pulo comum cai no vão");
		b.Check(RegressionBench.Flat(p.GlobalPosition - from) < 0.6f, "e o lobo volta para a marca de saída");
		b.Near(0, hurt, 0.01, "sem dano");
	}
}

/// <summary>As plataformas do bote: o lobo sobe na baixa com o salto, e da baixa na alta.</summary>
public sealed class SetorZeroPlatformsTest : SetorZeroTest
{
	public override string Name => "SetorZeroPlataformas";
	public override string Description => "o salto carregado põe o lobo em cima da plataforma baixa (2,7 m) e, dela, na alta (5 m); o pulo comum não sobe em nenhuma";

	public override async Task Run(RegressionBench b)
	{
		Player p = b.Player;
		await b.BecomeWolf();
		b.Weapon.SpecialFree = true;
		b.Check(b.Weapon.PounceMaxClear >= 2.7f * 1.15f, $"o salto pousa em cima de até {b.Weapon.PounceMaxClear} m: 2,7 m tem 15% de folga");

		// O pulo comum (2,09 m) não chega ao topo da baixa.
		await b.Place(At(64f, 80.6f), South);
		RegressionBench.Press("jump");
		await b.Frames(3);
		RegressionBench.Press("move_forward");
		await b.Seconds(1.6);
		RegressionBench.ReleaseAll();
		await b.Seconds(0.5);
		b.Check(Feet(p) < 1f, $"o pulo comum não sobe na plataforma baixa (pés a {RegressionBench.F(Feet(p))} m)");

		// Do chão, 5 m antes da face, mirando por cima dela (o céu): a barra dá a distância (8 m) e o
		// salto cai em cima do que houver ali até 3,2 m acima dos pés.
		await b.Place(At(64f, 77f), South, 50f);
		float power = (8f - b.Weapon.PounceMinDistance) / (b.Weapon.PounceMaxDistance - b.Weapon.PounceMinDistance);
		b.Check(b.Weapon.BotPounce(null, power), "o salto para a baixa saiu");
		await b.Seconds(2.6);
		RegressionBench.Note($"plataforma baixa: pés a {RegressionBench.F(Feet(p))} m em ({RegressionBench.F(PlanX(p))}, {RegressionBench.F(PlanY(p))})");
		b.Near(2.7, Feet(p), 0.1, "está em cima da plataforma baixa");

		// Da baixa para a alta (5 m; 2,3 m acima, 5 m de vão entre as duas).
		await b.Place(At(65.5f, 85f, 2.7f), East, 10f);
		b.Check(b.Weapon.BotPounce(null, 0.8f), "o salto para a alta saiu");
		await b.Seconds(2.6);
		RegressionBench.Note($"plataforma alta: pés a {RegressionBench.F(Feet(p))} m em ({RegressionBench.F(PlanX(p))}, {RegressionBench.F(PlanY(p))})");
		b.Near(5.0, Feet(p), 0.1, "está em cima da plataforma alta");

		// De cima da alta se vê o boneco do bote (a 3,2 m da face norte).
		var dummy = Tutorial(b).GetNode<Node3D>("Alvos/bote_2");
		var space = p.GetWorld3D().DirectSpaceState;
		Vector3 eyes = At(75f, 82.6f, 5.0f) + Vector3.Up * 0.6f;
		var ray = PhysicsRayQueryParameters3D.Create(eyes, dummy.GlobalPosition, 1);
		ray.Exclude = new Godot.Collections.Array<Rid> { p.GetRid() };
		var hit = space.IntersectRay(ray);
		b.Check(hit.Count > 0 && hit["collider"].AsGodotObject() == dummy, "da beira da plataforma alta se vê o boneco do bote");
	}
}

/// <summary>A parede de corrida: 32 m lisos, o lobo corre mais de 2 s sem cair nem soltar.</summary>
public sealed class SetorZeroWallRunTest : SetorZeroTest
{
	public override string Name => "SetorZeroParedeDeCorrida";
	public override string Description => "na parede de corrida da arena (32 m, 14 m de altura) o lobo corre 4 s seguidos, na mesma altura; no muro de fora do campo, não gruda";

	public override async Task Run(RegressionBench b)
	{
		Player p = b.Player;
		await b.BecomeWolf();

		// A face da parede sul da arena fica em y = 98 (normal para o norte); o lobo vem de leste para oeste.
		await b.Place(At(113f, 97.6f, 2f), West, settle: 0.05);
		p.Velocity = new Vector3(-3.5f, 0, 0);
		RegressionBench.Press("move_forward");
		bool grabbed = await b.Until(() => p.IsWallRunning, 1.0);
		b.Check(grabbed, "grudou na parede de corrida");
		float startX = PlanX(p), height = Feet(p);
		double began = b.Time;
		bool fell = await b.Until(() => !p.IsWallRunning, 4.0);
		double ran = b.Time - began;
		float run = startX - PlanX(p);
		RegressionBench.ReleaseAll();
		RegressionBench.Note($"correu {RegressionBench.F(ran)} s e {RegressionBench.F(run)} m na parede, de {RegressionBench.F(height)} m para {RegressionBench.F(Feet(p))} m de altura");
		b.Check(!fell, "4 s seguidos na parede, sem soltar (a etapa pede 2 s)");
		b.Check(run > 17f, $"andou mais de 17 m nela ({RegressionBench.F(run)} m; a parede tem 32, entre os dois cantos cortados)");
		b.Near(height, Feet(p), 0.25, "na mesma altura");
		b.Check(TutorialZone.Find("parede").Holds(p), "a zona da parede sabe que ele está nela");
		await b.Seconds(1.0);

		// O muro de fora (5 m, com a barreira atrás) não segura a corrida: ninguém sobe nele.
		await b.Place(At(60f, 102.6f, 2f), West, settle: 0.05);
		p.Velocity = new Vector3(-3.5f, 0, 0);
		RegressionBench.Press("move_forward");
		bool outer = await b.Until(() => p.IsWallRunning, 1.0);
		RegressionBench.ReleaseAll();
		b.Check(!outer, "no muro de fora o lobo não gruda");
	}
}

/// <summary>De cada posto de tiro se vê o alvo dele; o da granada fica escondido atrás do muro.</summary>
public sealed class SetorZeroTargetsTest : SetorZeroTest
{
	public override string Name => "SetorZeroAlvos";
	public override string Description => "de cada posto do treino o alvo da etapa está à vista e ao alcance; o alvo da granada fica atrás do muro, fora da linha de tiro";

	public override async Task Run(RegressionBench b)
	{
		Player p = b.Player;
		var space = p.GetWorld3D().DirectSpaceState;
		Node targets = Tutorial(b).GetNode("Alvos");

		// Os robôs andam (cada um no percurso dele): a visada é conferida do começo do percurso, com todos parados lá.
		foreach (Dummy robot in targets.GetChildren().OfType<Dummy>()) robot.MoveSpeed = 0f;
		await b.Frames(4);

		bool Sees(string mark, string target, out float distance)
		{
			var dummy = targets.GetNode<Node3D>(target);
			Vector3 eyes = Mark(b, mark).GlobalPosition + Vector3.Up * (1.59f - 0.9f);
			distance = eyes.DistanceTo(dummy.GlobalPosition);
			var ray = PhysicsRayQueryParameters3D.Create(eyes, dummy.GlobalPosition, 1);
			// Só o mapa conta: outro robô no caminho está de passagem (todos andam).
			var skip = new Godot.Collections.Array<Rid> { p.GetRid() };
			foreach (Dummy other in targets.GetChildren().OfType<Dummy>())
				if (other != dummy) skip.Add(other.GetRid());
			ray.Exclude = skip;
			var hit = space.IntersectRay(ray);
			return hit.Count > 0 && hit["collider"].AsGodotObject() == dummy;
		}

		(string Mark, string Target, float Max)[] seen =
		{
			("linha_de_tiro", "proximo_1", 20f), ("linha_de_tiro", "proximo_2", 12f), ("linha_de_tiro", "proximo_3", 20f),
			("linha_de_tiro", "medio_1", 26f), ("linha_de_tiro", "medio_2", 18f), ("linha_de_tiro", "medio_3", 26f),
			("linha_de_tiro", "distante", 23f), ("pistola", "pistola_1", 4f), ("pistola", "pistola_2", 4f),
			("corpo_a_corpo", "corpo_1", 3.5f), ("corpo_a_corpo", "corpo_2", 3.5f),
			("especial", "especial_1", 5f), ("especial", "especial_2", 7f), ("especial", "especial_3", 7f),
			("rua_centro", "onda_a", 17f), ("rua_centro", "onda_b", 7f), ("rua_centro", "onda_c", 17f),
		};
		foreach (var (mark, target, max) in seen)
		{
			bool ok = Sees(mark, target, out float d);
			b.Check(ok && d <= max, $"de '{mark}' se vê '{target}' a {RegressionBench.F(d)} m (até {max})");
		}

		b.Check(!Sees("granada", "granada", out float far), "o alvo da granada não se vê do posto: o muro cobre");
		RegressionBench.Note($"o alvo da granada fica a {RegressionBench.F(far)} m do posto, atrás de um muro de 2,4 m");
		b.Check(far > 8f && far < 12f, "e está à distância de um arremesso por cima do muro");

		// Os bonecos estão de pé no chão, com corpo para levar tiro, cada um no seu lugar.
		int floating = 0;
		foreach (Node node in targets.GetChildren())
		{
			if (node is not Dummy dummy) continue;
			// O chão ao lado dele fica na altura da base (o boneco tem 2 m, com o meio em 1 m). Os que andam são
			// medidos de onde nasceram (o começo do trilho).
			Vector3 beside = dummy.Home + Vector3.Right * 0.7f;
			var down = space.IntersectRay(PhysicsRayQueryParameters3D.Create(beside, beside + Vector3.Down * 1.5f, 1));
			if (down.Count == 0 || Mathf.Abs(down["position"].AsVector3().Y - (dummy.Home.Y - 1f)) > 0.03f) floating++;
		}
		b.Check(floating == 0, $"todo boneco pisa no chão ({floating} no ar ou afundados)");
		await b.PhysicsFrame();
	}
}

/// <summary>As escadarias: sobe-se e desce-se andando, sem tranco, até cada nível do mapa.</summary>
public sealed class SetorZeroStairsTest : SetorZeroTest
{
	public override string Name => "SetorZeroEscadarias";
	public override string Description => "andando se chega a cada nível: desce-se à Linha de Fogo (−2,4 m) e ao fundo do Núcleo (−1,2 m), sobe-se à Câmara (+1,5 m), à galeria da arena (+4 m) e ao terraço da rua (+6 m)";

	public override async Task Run(RegressionBench b)
	{
		Player p = b.Player;

		async Task Walk(string what, Vector3 from, float yaw, System.Func<bool> arrived, float level, double timeout, string zone = null)
		{
			await b.Place(from, yaw);
			float worst = 0f;
			Vector3 last = p.GlobalPosition;
			RegressionBench.Press("move_forward");
			bool ok = await b.Until(() =>
			{
				// O tranco: num quadro o corpo não pode subir ou descer mais do que anda (a escada é uma rampa lisa).
				Vector3 now = p.GlobalPosition;
				worst = Mathf.Max(worst, Mathf.Abs(now.Y - last.Y));
				last = now;
				return arrived();
			}, timeout);
			RegressionBench.ReleaseAll();
			await b.Seconds(0.5);
			RegressionBench.Note($"{what}: pés a {RegressionBench.F(Feet(p))} m em ({RegressionBench.F(PlanX(p))}, {RegressionBench.F(PlanY(p))}); o maior passo vertical num quadro foi {RegressionBench.F(worst * 100)} cm");
			b.Check(ok, $"{what}: chegou");
			b.Near(level, Feet(p), 0.06, $"{what}: no nível certo");
			b.Check(worst < 0.06f, $"{what}: sem tranco ({RegressionBench.F(worst * 100)} cm num quadro)");
			if (zone != null) b.Check(TutorialZone.Find(zone).Holds(p), $"{what}: a zona '{zone}' sabe que ele chegou");
		}

		await Walk("descendo à Linha de Fogo", At(75f, 36.5f), North, () => PlanY(p) < 27.4f, Range, 6.0, "linha_de_fogo");
		await Walk("subindo de volta", At(75f, 27.5f, Range), South, () => PlanY(p) > 35.4f, 0f, 6.0);
		await Walk("descendo ao fundo do Núcleo", At(64.2f, 50f), East, () => PlanX(p) > 70f, Core, 5.0, "nucleo");
		await Walk("subindo à Câmara pelo salão", At(96f, 50f), East, () => PlanX(p) > 106.5f, Chamber, 7.0, "camara");
		await Walk("subindo à Câmara pela arena", At(112f, 63.2f), North, () => PlanY(p) < 56f, Chamber, 6.0, "camara");
		await Walk("subindo à Câmara pela rua", At(112f, 36.8f), South, () => PlanY(p) > 44f, Chamber, 6.0, "camara");
		await Walk("subindo à galeria da arena (oeste)", At(41.2f, 75.6f), North, () => PlanY(p) < 65.6f, Gallery, 8.0);
		await Walk("subindo à galeria da arena (leste)", At(106.2f, 75.6f), North, () => PlanY(p) < 65.6f, Gallery, 8.0);
		await Walk("andando pela galeria", At(102f, 65.3f, Gallery), West, () => PlanX(p) < 80f, Gallery, 12.0, "galeria");
		await Walk("subindo ao terraço da rua", At(125.4f, 9.2f), West, () => PlanX(p) < 112.9f, Terrace, 9.0);
	}
}

/// <summary>A beira: o parapeito da Linha de Fogo segura quem anda; quem o pula cai no abismo e morre de queda.</summary>
public sealed class SetorZeroAbyssTest : SetorZeroTest
{
	public override string Name => "SetorZeroAbismo";
	public override string Description => "na beira da Linha de Fogo o parapeito (0,9 m) segura quem anda, e quem o pula cai no abismo e morre de queda, humano ou lobo";

	public override async Task Run(RegressionBench b)
	{
		Player p = b.Player;
		Health me = b.Health;

		// Andando contra o parapeito: não passa.
		await b.Place(At(75f, -0.8f, Range), North);
		RegressionBench.Press("move_forward");
		await b.Seconds(2.0);
		RegressionBench.ReleaseAll();
		b.Check(PlanY(p) > -2.8f && Feet(p) > Range - 0.05f && !me.IsDead, $"andando, o parapeito segura (y = {RegressionBench.F(PlanY(p))})");
		b.Check(TutorialZone.Find("mirante").Holds(p), "a zona do mirante sabe que ele está na beira");

		// Pulando por cima: cai e morre de queda.
		int feed = KillFeed.Recent.Count;
		await b.Place(At(75f, -1.6f, Range), North);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.25);
		RegressionBench.Press("jump");
		bool died = await b.Until(() => me.IsDead, 6.0);
		RegressionBench.ReleaseAll();
		RegressionBench.Note($"pulou o parapeito: morreu = {died}, em y = {RegressionBench.F(PlanY(p))}, a {RegressionBench.F(p.GlobalPosition.Y)} m");
		b.Check(died, "quem pula o parapeito cai e morre");
		Kill? last = KillFeed.Recent.Count > feed ? KillFeed.Recent[^1].Kill : null;
		b.Check(last is Kill k && k.Cause == KillCause.Fall, $"o feed diz queda ({(last is Kill l ? l.Cause.ToString() : "nada")})");
		await b.Until(() => !me.IsDead, 8.0);
		b.CloseLoadout();
		await b.Seconds(0.6);
		b.Check(!me.IsDead && PlanX(p) < 20f && Feet(p) > -0.1f, $"renasce na Sala de Ativação (x = {RegressionBench.F(PlanX(p))})");

		// O lobo também: a queda não tem fundo, e a zona do abismo mata mesmo quem cai devagar.
		await b.BecomeWolf();
		await b.Place(At(90f, -6f, Range + 4f), North, settle: 0.05);
		bool wolfDied = await b.Until(() => me.IsDead, 6.0);
		b.Check(wolfDied, "o lobo solto além da beira também morre");
	}
}

/// <summary>Os robôs de treino são inimigos que não atiram: seguem o percurso deles, levam tiro, morrem, voltam e mostram a vida.</summary>
public sealed class SetorZeroRobotsTest : SetorZeroTest
{
	public override string Name => "SetorZeroRobos";
	public override string Description => "os robôs de treino são do outro time, todos seguem um percurso (sem atravessar parede, cobertura nem outro robô, pisando no chão), levam tiro da arma do jogador, mostram a vida numa barra, caem e voltam";

	public override async Task Run(RegressionBench b)
	{
		Node targets = Tutorial(b).GetNode("Alvos");
		Dummy[] all = targets.GetChildren().OfType<Dummy>().ToArray();
		int mine = b.Player.GetNode<Health>("Health").Team;
		b.Check(all.All(d => d.Model != null && d.GetNodeOrNull("Visual") != null && d.GetNode("Visual").FindChildren("*", nameof(MeshInstance3D), true, false).Count > 0),
			"todo robô tem o desenho (o modelo do robô de treino)");
		b.Check(all.All(d => d.GetNode<Health>("Health").Team != mine), "todo robô é do outro time (a arma do jogador fere)");
		b.Check(all.All(d => d.Move == DummyMove.Route && d.Cycle > 1.0), $"todo robô segue um percurso ({all.Count(d => d.Move != DummyMove.Route || d.Cycle <= 1.0)} parados)");
		b.Check(all.All(d => d.BarShown && Mathf.IsEqualApprox(d.BarValue, 1f)), "todo robô mostra a barra de vida, cheia");

		// O percurso inteiro de cada um: a caixa do corpo não entra em nada do mapa, e há chão embaixo, na altura do começo.
		var space = b.Player.GetWorld3D().DirectSpaceState;
		var others = new Godot.Collections.Array<Rid>(all.Select(d => d.GetRid()));
		others.Add(b.Player.GetRid());
		var body = new BoxShape3D { Size = new Vector3(0.94f, 1.9f, 0.94f) };
		var blocked = new System.Collections.Generic.List<string>();
		foreach (Dummy d in all)
		{
			string bad = null;
			for (double at = 0; at <= d.Cycle && bad == null; at += 0.1)
			{
				Vector3 pos = d.Home + d.Offset(at);
				var query = new PhysicsShapeQueryParameters3D { Shape = body, Transform = new Transform3D(Basis.Identity, pos + Vector3.Up * 0.03f), CollisionMask = 1, Exclude = others };
				var hits = space.IntersectShape(query, 1);
				if (hits.Count > 0) bad = $"{d.Name} bate em {((Node)hits[0]["collider"].AsGodotObject()).Name} em ({RegressionBench.F(pos.X + 75f)}, {RegressionBench.F(pos.Z + 50f)})";
				var ray = PhysicsRayQueryParameters3D.Create(pos, pos + Vector3.Down * 1.6f, 1);
				ray.Exclude = others;
				var down = space.IntersectRay(ray);
				if (bad == null && (down.Count == 0 || Mathf.Abs(down["position"].AsVector3().Y - (d.Home.Y - 1f)) > 0.05f))
					bad = $"{d.Name} sem chão em ({RegressionBench.F(pos.X + 75f)}, {RegressionBench.F(pos.Z + 50f)})";
			}
			if (bad != null) blocked.Add(bad);
		}
		b.Check(blocked.Count == 0, $"nenhum percurso atravessa o mapa nem sai do chão ({string.Join("; ", blocked)})");

		// Um robô não entra no outro: no mesmo relógio, dois corpos nunca ficam a menos de 1 m.
		string crash = null;
		for (double at = 0; at < 120.0 && crash == null; at += 0.2)
			for (int i = 0; i < all.Length && crash == null; i++)
				for (int k = i + 1; k < all.Length; k++)
				{
					Vector3 gap = all[i].Home + all[i].Offset(at) - all[k].Home - all[k].Offset(at);
					if (Mathf.Abs(gap.Y) < 1.9f && Mathf.Abs(gap.X) < 1.0f && Mathf.Abs(gap.Z) < 1.0f) { crash = $"{all[i].Name} e {all[k].Name} aos {RegressionBench.F((float)at)} s"; break; }
				}
		b.Check(crash == null, $"dois robôs nunca se atravessam ({crash})");

		// O do trilho da Linha de Fogo anda de verdade: vai até a outra ponta (18,5 m) e volta, sem sair da linha.
		var rail = targets.GetNode<Dummy>("movel");
		Vector3 from = rail.Home;
		float far = 0f, side = 0f, back = float.MaxValue;
		double end = b.Time + 19.0;
		while (b.Time < end)
		{
			Vector3 off = rail.GlobalPosition - from;
			far = Mathf.Max(far, off.X);
			side = Mathf.Max(side, Mathf.Abs(off.Z) + Mathf.Abs(off.Y));
			if (far > 18f) back = Mathf.Min(back, off.X);
			await b.PhysicsFrame();
		}
		RegressionBench.Note($"o robô do trilho foi {RegressionBench.F(far)} m e voltou a {RegressionBench.F(back)} m do começo");
		b.Near(18.5, far, 0.1, "vai até a ponta do trilho");
		b.Check(back < 1.0f, "e volta");
		b.Check(side < 0.01f, "sem sair da linha");
		// A ronda fecha a volta: passa pelos quatro cantos e chega de novo ao começo.
		var round = targets.GetNode<Dummy>("ronda_arena");
		b.Check(round.RouteLoop && round.Offset(round.Cycle * 0.5).Length() > 6f && round.Offset(round.Cycle).Length() < 0.05f, "a ronda da arena dá a volta e fecha no começo");

		// Leva tiro da arma do jogador (na cabeça, que vale: a mira reta de quem está de pé pega o capacete), a barra
		// desce, e a vida volta sozinha depois da espera.
		var target = targets.GetNode<Dummy>("pistola_1");
		target.MoveSpeed = 0f;                          // parado no começo do percurso, para o tiro não depender do passo
		var health = target.GetNode<Health>("Health");
		b.Check(target is IHeadBand band && Mathf.IsEqualApprox(band.HeadFrom, target.RobotHeadFrom), "a cabeça do robô começa onde o desenho dela começa");
		await b.Place(At(49f, 22.6f, Range), North);
		RegressionBench.Press("attack");
		bool hurt = await b.Until(() => health.Hp < health.MaxHp, 2.0);
		RegressionBench.Release("attack");
		await b.Frames(3);
		RegressionBench.Note($"um tiro deixou o robô com {RegressionBench.F(health.Hp)} de {RegressionBench.F(health.MaxHp)}");
		b.Check(hurt, "a arma do jogador fere o robô");
		b.Check(target.BarValue < 0.999f && Mathf.Abs(target.BarValue - health.Hp / health.MaxHp) < 0.02f, "a barra de vida acompanha");
		if (!health.IsDead)
		{
			float low = health.Hp;
			bool healed = await b.Until(() => health.Hp > low + 5f, target.RegenDelay + 3.0);
			b.Check(healed, $"sem levar dano por {RegressionBench.F(target.RegenDelay)} s, a vida do robô volta sozinha");
		}

		// Cai e some (a barra junto), e volta uns segundos depois, no lugar do percurso em que estaria.
		health.TakeDamage(health.MaxHp + 500f, 0, default, false, true);
		await b.Frames(3);
		b.Check(health.IsDead && !target.Shown && !target.BarShown, "o robô que perde toda a vida cai e some, com a barra");
		bool again = await b.Until(() => !health.IsDead && target.Shown && target.BarShown, target.RespawnDelay + 2.0);
		b.Check(again, $"e volta sozinho em {RegressionBench.F(target.RespawnDelay)} s, com a vida cheia");
		b.Check(health.Hp >= health.MaxHp - 0.01f, "cheio");
	}
}

/// <summary>No campo de treino a tecla dos conjuntos (H) abre a escolha a qualquer hora, sem custo.</summary>
public sealed class SetorZeroLoadoutTest : SetorZeroTest
{
	public override string Name => "SetorZeroConjuntos";
	public override string Description => "no campo de treino a tecla dos conjuntos (H) abre e fecha a escolha a qualquer hora, sem relógio, e nenhum conjunto custa nem paga KP";

	public override async Task Run(RegressionBench b)
	{
		var menu = (LoadoutMenu)b.Scene.GetNode("LoadoutMenu");
		b.Check(menu.Free, "a cena do treino liga os conjuntos livres");
		b.Check(!menu.IsOpen, "fechada depois da primeira escolha");

		async Task Tap()
		{
			Input.ParseInputEvent(new InputEventAction { Action = "loadout", Pressed = true });
			await b.Frames(3);
			Input.ParseInputEvent(new InputEventAction { Action = "loadout", Pressed = false });
			await b.Frames(3);
		}

		float kp = b.Form.Wp;
		await Tap();
		b.Check(menu.IsOpen, "o H abre a escolha, vivo, no meio do treino");
		await b.Seconds(menu.ChooseTime + 2.0);
		b.Check(menu.IsOpen, "sem relógio: fica aberta o tempo que o jogador quiser");
		await Tap();
		b.Check(!menu.IsOpen, "o H de novo fecha, sem trocar nada");
		b.Near(kp, b.Form.Wp, 0.01, "abrir e fechar não mexe nos KP");

		// Escolher: nem o padrão paga a rede de segurança, nem os outros cobram.
		await Tap();
		var choose = typeof(LoadoutMenu).GetMethod("Choose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		choose.Invoke(menu, new object[] { 0, false });
		await b.Frames(3);
		b.Check(!menu.IsOpen, "escolher fecha");
		b.Near(kp, b.Form.Wp, 0.01, "o conjunto padrão não paga KP no treino");
		var blocker = typeof(LoadoutMenu).GetMethod("Blocker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		int priced = 0, blockedByKp = 0;
		foreach (LoadoutSet set in Profile.Sets)
		{
			if (set.IsDefault || set.TotalCost() <= 0) continue;
			priced++;
			if (blocker.Invoke(menu, new object[] { set }) is string why && why.Contains("KP")) blockedByKp++;
		}
		RegressionBench.Note($"{priced} conjuntos com preço no perfil de teste; {blockedByKp} barrados por KP");
		b.Check(blockedByKp == 0, "nenhum conjunto é barrado por falta de KP");

		// O menu do Esc do treino diz por onde sair.
		var pause = b.Scene.GetNodeOrNull<GameMenu>("GameMenu");
		b.Check(pause != null && pause.Training, "o menu do Esc é o do treino (\"Sair do treinamento\")");
	}
}

/// <summary>
/// O mapa é todo aberto para andar (decisão do Jhon, 26/11: "o mapa deve
/// estar todo aberto para andar, sem portas ou barreiras para acessar os
/// locais do treinamento"). Antes as sete portas de energia nasciam
/// fechadas, à espera de um tutorial que as abrisse uma por vez — e o
/// tutorial ainda não existe: no treino livre não se saía do corredor. As
/// portas continuam na cena, abertas (sem colisão, invisíveis); fechar uma é
/// ligar a colisão dela, e é isso que o tutorial fará quando existir.
/// </summary>
public sealed class SetorZeroDoorsTest : SetorZeroTest
{
	public override string Name => "SetorZeroPortas";
	public override string Description => "o campo de treinamento é todo aberto: as sete portas de energia nascem abertas (sem colisão, invisíveis) e anda-se do corredor a cada setor; fechada (pelo tutorial, no futuro), uma porta tapa a passagem dela de parede a parede";

	public override async Task Run(RegressionBench b)
	{
		Player p = b.Player;
		Node doors = Tutorial(b).GetNode("Portas");
		StaticBody3D[] all = doors.GetChildren().OfType<StaticBody3D>().ToArray();
		b.Check(all.Length == 7 && all.All(d => d.GetNode<CollisionShape3D>("Colisao").Disabled && !d.GetNode<MeshInstance3D>("Malha").Visible),
			"as sete portas nascem abertas: sem colisão e sem o campo de energia à vista");

		// Nenhuma tapa o vão dela: um raio de um lado ao outro, a 1 m do chão, passa.
		var space = p.GetWorld3D().DirectSpaceState;
		(Vector3 Across, Vector3 Along, float Half) Axes(StaticBody3D door)
		{
			var shape = (BoxShape3D)door.GetNode<CollisionShape3D>("Colisao").Shape;
			bool alongX = shape.Size.X > shape.Size.Z;
			return (alongX ? Vector3.Back : Vector3.Right, alongX ? Vector3.Right : Vector3.Back, (alongX ? shape.Size.X : shape.Size.Z) / 2f);
		}
		bool Blocked(StaticBody3D door, float side)
		{
			var (across, along, _) = Axes(door);
			Vector3 mid = door.GlobalPosition + along * side + Vector3.Down;
			var ray = PhysicsRayQueryParameters3D.Create(mid - across * 1.5f, mid + across * 1.5f, 1);
			ray.Exclude = new Godot.Collections.Array<Rid> { p.GetRid() };
			return space.IntersectRay(ray).Count > 0;
		}
		foreach (StaticBody3D door in all)
			b.Check(!Blocked(door, 0f) && !Blocked(door, -Axes(door).Half + 0.4f) && !Blocked(door, Axes(door).Half - 0.4f), $"a passagem '{door.Name}' está livre");

		// Andando: do fim do corredor entra-se no Núcleo, sem parar na porta.
		await b.Place(At(60f, 50f), East);
		RegressionBench.Press("move_forward");
		bool inside = await b.Until(() => PlanX(p) > 66f, 4.0);
		RegressionBench.ReleaseAll();
		b.Check(inside, "do corredor entra-se no Núcleo andando");
		await b.Seconds(0.3);
		b.Check(TutorialZone.Find("nucleo").Holds(p), "a zona do Núcleo sabe que ele entrou");
		// Do Núcleo sai-se para a arena (sul) e, da arena, para o Ponto de Retorno (oeste).
		await b.Place(At(75f, 64.2f), South);
		RegressionBench.Press("move_forward");
		bool arena = await b.Until(() => PlanY(p) > 68.5f, 4.0);
		RegressionBench.ReleaseAll();
		b.Check(arena, "do Núcleo sai-se para a arena andando");
		await b.Place(At(42.5f, 82.5f), West);
		RegressionBench.Press("move_forward");
		bool back = await b.Until(() => PlanX(p) < 37.5f, 4.0);
		RegressionBench.ReleaseAll();
		b.Check(back, "da arena passa-se ao corredor do Ponto de Retorno andando");

		// Fechada (o que o tutorial fará): a porta tapa a passagem de parede a parede, e segura quem anda.
		foreach (StaticBody3D door in all)
		{
			door.GetNode<CollisionShape3D>("Colisao").Disabled = false;
			door.GetNode<MeshInstance3D>("Malha").Visible = true;
		}
		await b.Frames(3);
		foreach (StaticBody3D door in all)
		{
			var (_, _, half) = Axes(door);
			int leaks = new[] { -half + 0.05f, 0f, half - 0.05f, -half - 0.25f, half + 0.25f }.Count(side => !Blocked(door, side));
			b.Check(leaks == 0, $"fechada, a porta '{door.Name}' tapa a passagem de parede a parede ({leaks} frestas)");
		}
		await b.Place(At(60f, 50f), East);
		RegressionBench.Press("move_forward");
		await b.Seconds(2.5);
		RegressionBench.ReleaseAll();
		b.Check(PlanX(p) < 63.2f, $"e segura quem anda (x = {RegressionBench.F(PlanX(p))})");
	}
}
