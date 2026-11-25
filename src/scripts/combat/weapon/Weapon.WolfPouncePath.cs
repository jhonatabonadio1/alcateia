using Godot;

/// <summary>
/// O caminho do salto do lobo (o especial e o salto carregado): o voo em
/// que o corpo inteiro passa sem bater em nada, e onde cair em volta do
/// alvo travado.
///
/// Antes (até 25/11) o salto media o caminho por uma linha só, no meio do
/// corpo, a cada 40 cm, e subia o arco pelo mais alto que a linha cruzasse.
/// Na Cidade isso dava os defeitos do vídeo do Jhon ("vai e volta no ar e
/// acaba errando o alvo"; "sobe muito e fica no alto"):
///   - o lado do corpo batia no que a linha não via — a quina de um carro,
///     uma barreira, um poste meio palmo ao lado — e o lobo escorregava de
///     lado no ar ou parava no meio, caindo longe do alvo;
///   - o que ficava logo na saída ou na chegada pedia um arco enorme: o voo
///     já avançava enquanto subia, e passar por cima de uma barreira colada
///     no lobo pedia 3,5 m de arco.
/// Agora o voo é testado com a cápsula do corpo ao longo do caminho de
/// verdade (<see cref="SweepArc"/>), e fica o mais baixo que passa. Com algo
/// colado na saída, o lobo primeiro sobe e depois avança (e, colado na
/// chegada, desce na vertical) — <see cref="FlightShape"/> —, em vez de subir
/// alto. Com alvo travado, cai do lado do alvo que dá o voo mais baixo
/// (<see cref="LandAround"/>), e não sempre no da frente.
///
/// Postes, placas e o que mais for fino (<see cref="PounceThinProp"/>) não
/// contam: o voo os atravessa (WolfMoveMotor). Um poste de 12 cm no meio da
/// rua parava o lobo no ar ou mandava ele por cima dele, a 5 m de altura.
/// </summary>
public partial class Weapon
{
	/// <summary>O raio da cápsula que o salto testa no caminho, em metros: a
	/// do corpo (0,3) e a folga com que a física já conta um encosto.</summary>
	[ExportGroup("Lobo: caminho do salto")]
	[Export] public float PounceBodyRadius = 0.36f;

	/// <summary>Sem caminho limpo de lado nenhum do alvo (encostado num canto,
	/// embaixo de uma marquise), o salto aceita raspar: testa de novo com o
	/// corpo mais fino, nesta fração do raio. Raspar desliza o lobo uns
	/// centímetros; bater de frente parava o voo.</summary>
	[Export(PropertyHint.Range, "0.3,1")] public float PounceGrazeShare = 0.6f;

	/// <summary>O que é mais baixo que isto, acima dos pés, não conta no
	/// caminho do salto (a junta de uma calçada): o corpo passa por cima.</summary>
	[Export] public float PounceFootClear = 0.03f;

	/// <summary>Peça de cenário mais fina que isto, de lado a lado (poste,
	/// placa, haste), não barra o salto: o voo a atravessa.</summary>
	[Export] public float PounceThinProp = 0.35f;

	/// <summary>O arco mais rasante que o salto aceita, em metros: por baixo
	/// de uma copa ou de um toldo.</summary>
	[Export] public float PounceLowArc = 0.3f;

	/// <summary>Até quanto acima do arco de sempre o voo fica com a forma de
	/// sempre (sobe e avança junto); passou disso, prova subir antes de
	/// avançar ou descer na vertical no fim.</summary>
	[Export] public float PounceShapeAfter = 0.3f;

	/// <summary>De quanto em quanto o arco sobe ao procurar o que passa, em metros.</summary>
	private const float ArcStep = 0.1f;

	/// <summary>Os lados do alvo em que o especial pode cair, em graus a partir
	/// do lado de onde o lobo vem: o da frente primeiro.</summary>
	private static readonly float[] LandAngles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f };

	/// <summary>A que distância do alvo, além de <see cref="PounceStandOff"/>:
	/// colado numa parede ou num canto, o lobo cai um pouco mais longe, ainda
	/// dentro da área do golpe (<see cref="PounceHitRadius"/>).</summary>
	private static readonly float[] LandFarther = { 0f, 0.3f, 0.6f };

	/// <summary>As formas de voo que se provam quando a de sempre precisa
	/// subir demais: quanto do voo o lobo só sobe antes de avançar, e quanto
	/// do fim ele só desce.</summary>
	private static readonly FlightShape[] Shapes =
		{ new(0.15f, 0f), new(0f, 0.15f), new(0.15f, 0.15f), new(0.3f, 0f), new(0f, 0.3f) };

	/// <summary>Quanto cair mais longe do alvo custa, em metros de arco por metro.</summary>
	private const float FartherCost = 0.5f;

	/// <summary>Quanto um lado do alvo custa a mais que o da frente, em metros
	/// de arco por grau: cair de lado só vale se poupar subida de verdade.</summary>
	private const float SideCost = 0.003f;

	/// <summary>Quanto subir antes de avançar (ou descer na vertical) custa,
	/// em metros de arco por fração do voo: só vale se poupar subida.</summary>
	private const float ShapeCost = 1.5f;

	/// <summary>A forma do voo no chão: a fração do começo em que o lobo só
	/// sobe (<see cref="Lead"/>) e a do fim em que ele só desce
	/// (<see cref="Hold"/>). No meio, o sino de sempre.</summary>
	public readonly record struct FlightShape(float Lead, float Hold)
	{
		/// <summary>O avanço no chão (0 a 1) no instante <paramref name="u"/> do voo.</summary>
		public float At(float u)
		{
			float span = Mathf.Max(1f - Lead - Hold, 0.05f);
			return Bell(Mathf.Clamp((u - Lead) / span, 0f, 1f));
		}

		public float Cost => (Lead + Hold) * ShapeCost;
	}

	private PhysicsShapeQueryParameters3D _sweep;

	/// <summary>O arco de sempre no chão plano: cresce com a distância.</summary>
	private float BaseArc(float distance) => Mathf.Clamp(PounceArc + 0.07f * distance, PounceArc, 1.6f);

	/// <summary>O centro do corpo no instante <paramref name="u"/> (0 a 1) de
	/// um voo de <paramref name="start"/> a <paramref name="end"/> com este
	/// arco e esta forma — o mesmo de <see cref="PounceFlightPoint"/>: no chão
	/// em sino, para cima em parábola.</summary>
	private static Vector3 FlightAt(Vector3 start, Vector3 end, float arc, FlightShape shape, float u)
	{
		Vector3 flat = (end - start) with { Y = 0 };
		float y = (end.Y - start.Y) * u + 4f * arc * u * (1f - u);
		return start + flat * shape.At(u) + Vector3.Up * y;
	}

	/// <summary>O arco mais perto de <paramref name="from"/> (o de sempre),
	/// até <paramref name="limit"/>, em que o corpo vai de
	/// <paramref name="start"/> a <paramref name="end"/> sem bater em nada:
	/// primeiro o de sempre, depois um pouco mais baixo e um pouco mais alto,
	/// alternando. Mais baixo serve quando há algo por cima do caminho (a copa
	/// de uma árvore, um toldo); mais alto, por cima de um carro. Nulo:
	/// nenhum passa.</summary>
	private float? SweepArc(Vector3 start, Vector3 end, float from, float limit, float radius, FlightShape shape)
	{
		if (from <= limit + 0.001f && SweepFree(start, end, from, radius, shape)) return from;
		for (float step = ArcStep; ; step += ArcStep)
		{
			float low = from - step, high = from + step;
			bool tryLow = low >= PounceLowArc, tryHigh = high <= limit + 0.001f;
			if (!tryLow && !tryHigh) return null;
			if (tryLow && SweepFree(start, end, low, radius, shape)) return low;
			if (tryHigh && SweepFree(start, end, high, radius, shape)) return high;
		}
	}

	/// <summary>O voo mais baixo de <paramref name="start"/> a
	/// <paramref name="end"/>: a forma de sempre, se não precisar subir mais
	/// que <see cref="PounceShapeAfter"/> acima do arco de sempre; senão a
	/// forma (<see cref="Shapes"/>) que poupa mais subida. Devolve o arco, a
	/// forma e o custo (o que o arco passa do de sempre, mais a forma).</summary>
	private (float Arc, FlightShape Shape, float Cost)? SweepFlight(Vector3 start, Vector3 end, float limit, float radius, float budget = float.MaxValue)
	{
		float baseArc = BaseArc(((end - start) with { Y = 0 }).Length());
		(float, FlightShape, float)? best = null;
		float bestCost = budget;
		if (SweepArc(start, end, baseArc, Mathf.Min(limit, baseArc + bestCost), radius, default) is float plain)
		{
			best = (plain, default, Mathf.Max(plain - baseArc, 0f));
			bestCost = best.Value.Item3;
			if (plain <= baseArc + PounceShapeAfter) return best;
		}

		// A forma aperta o avanço no chão no resto do voo: no meio do sino o
		// corpo anda π/2 × D / (o que sobra do voo). Mais rápido que o teto do
		// voo, não dá (o corpo ficaria para trás e cairia antes).
		float flat = ((end - start) with { Y = 0 }).Length();
		foreach (FlightShape shape in Shapes)
		{
			if (shape.Cost >= bestCost) continue;
			float span = Mathf.Max(1f - shape.Lead - shape.Hold, 0.05f);
			if (Mathf.Pi * 0.5f * flat / (span * (float)_flyTotal) > WolfMoveMotor.MaxFlightSpeed * 0.9f) continue;
			if (SweepArc(start, end, baseArc, Mathf.Min(limit, baseArc + bestCost - shape.Cost), radius, shape) is not float arc) continue;
			float cost = Mathf.Max(arc - baseArc, 0f) + shape.Cost;
			if (cost < bestCost)
			{
				bestCost = cost;
				best = (arc, shape, cost);
			}
		}
		return best;
	}

	/// <summary>O corpo passa livre por este voo, da saída à chegada? Gente
	/// não conta (o voo trata quem encosta), nem o que é fino.</summary>
	private bool SweepFree(Vector3 start, Vector3 end, float arc, float radius, FlightShape shape)
	{
		float half = -((_body as Player)?.FeetOffset ?? -0.875f);
		radius = Mathf.Max(radius, 0.05f);
		float height = Mathf.Max(2f * (half - PounceFootClear), 2f * radius + 0.01f);
		_sweep ??= new PhysicsShapeQueryParameters3D { CollideWithAreas = false };
		if (_sweep.Shape is not CapsuleShape3D capsule || !Mathf.IsEqualApprox(capsule.Radius, radius) || !Mathf.IsEqualApprox(capsule.Height, height))
			_sweep.Shape = new CapsuleShape3D { Radius = radius, Height = height };
		_sweep.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };

		var space = _body.GetWorld3D().DirectSpaceState;
		float flat = ((end - start) with { Y = 0 }).Length();
		int steps = Mathf.Clamp(Mathf.CeilToInt(flat / 0.25f), 12, 64);
		for (int k = 1; k <= steps; k++)
		{
			_sweep.Transform = new Transform3D(Basis.Identity, FlightAt(start, end, arc, shape, k / (float)steps));
			foreach (var hit in space.IntersectShape(_sweep, 8))
			{
				var other = hit["collider"].Obj as GodotObject;
				if (other is CharacterBody3D || IsThinProp(other, (int)hit["shape"], PounceThinProp)) continue;
				return false;
			}
		}
		return true;
	}

	/// <summary>Uma peça de cenário fina (poste, placa, haste): a forma de
	/// colisão dela, de lado a lado, não passa de <paramref name="thin"/>.</summary>
	public static bool IsThinProp(GodotObject collider, int shape, float thin)
	{
		if (collider is not StaticBody3D body) return false;
		uint owner = body.ShapeFindOwner(shape);
		if ((GodotObject)body.ShapeOwnerGetOwner(owner) is not CollisionShape3D node || node.Shape == null) return false;

		Vector3 scale = node.GlobalBasis.Scale.Abs();
		Vector2 across = node.Shape switch
		{
			BoxShape3D box => new Vector2(box.Size.X * scale.X, box.Size.Z * scale.Z),
			CylinderShape3D cylinder => new Vector2(cylinder.Radius * 2f * scale.X, cylinder.Radius * 2f * scale.Z),
			CapsuleShape3D capsule => new Vector2(capsule.Radius * 2f * scale.X, capsule.Radius * 2f * scale.Z),
			_ => new Vector2(float.MaxValue, float.MaxValue),
		};
		return Mathf.Max(across.X, across.Y) <= thin;
	}

	/// <summary>O voo mais baixo para um ponto de queda já escolhido (sem
	/// alvo travado): limpo, ou raspando se não houver limpo. Nulo: nenhum.</summary>
	private (float Arc, FlightShape Shape)? FlightTo(Vector3 start, Vector3 end, float limit)
	{
		var found = SweepFlight(start, end, limit, PounceBodyRadius)
			?? SweepFlight(start, end, limit, PounceBodyRadius * Mathf.Clamp(PounceGrazeShare, 0.3f, 1f));
		return found is var (arc, shape, _) ? (arc, shape) : null;
	}

	/// <summary>
	/// Onde o especial cai em volta do alvo: a <see cref="PounceStandOff"/>
	/// do centro dele (ou um pouco mais, num canto), do lado que deixa o voo
	/// mais baixo sem bater em nada (o da frente, se não for muito mais alto).
	/// Nulo: nenhum lado passa.
	/// </summary>
	/// <param name="target">O centro do corpo do alvo (onde ele vai estar).</param>
	private (Vector3 End, float Arc, FlightShape Shape)? LandAround(Vector3 start, Vector3 target, float arcLimit)
	{
		var clean = LandAround(start, target, arcLimit, PounceBodyRadius);
		if (clean != null) return clean;
		if (Debug) GD.Print("leap: no clean path around the locked target; grazing");
		return LandAround(start, target, arcLimit, PounceBodyRadius * Mathf.Clamp(PounceGrazeShare, 0.3f, 1f));
	}

	private (Vector3 End, float Arc, FlightShape Shape)? LandAround(Vector3 start, Vector3 target, float arcLimit, float radius)
	{
		var space = _body.GetWorld3D().DirectSpaceState;
		float half = -((_body as Player)?.FeetOffset ?? -0.875f);
		float targetFeet = target.Y - half;

		Vector3 back = (start - target) with { Y = 0 };
		back = back.LengthSquared() > 0.0001f ? back.Normalized() : -_pounceDir;

		(Vector3, float, FlightShape)? best = null;
		float bestScore = float.MaxValue;
		foreach (float farther in LandFarther)
		foreach (float angle in LandAngles)
		{
			float score = Mathf.Abs(angle) * SideCost + farther * FartherCost;
			if (score >= bestScore) continue;

			Vector3 side = back.Rotated(Vector3.Up, Mathf.DegToRad(angle));
			Vector3 spot = target + side * (PounceStandOff + farther);
			if (FloorBelow(space, new Vector3(spot.X, targetFeet + 1.2f, spot.Z), 2.4f) is not Vector3 floor) continue;
			if (Mathf.Abs(floor.Y - targetFeet) > 0.6f) continue;

			Vector3 end = new(spot.X, floor.Y + half, spot.Z);
			if (SweepFlight(start, end, arcLimit, radius, bestScore - score) is not var (arc, shape, cost)) continue;

			score += cost;
			if (score < bestScore)
			{
				bestScore = score;
				best = (end, arc, shape);
			}
		}
		return best;
	}
}
