using Godot;
using System.Threading.Tasks;

/// <summary>O salto cai onde a mira aponta (decisão do Jhon, 17/11): o
/// ponto mirado dá a distância, até o alcance do salto, com qualquer
/// carga. Sem ponto mirado (céu), vale a distância da barra.</summary>
public sealed class ChargedPounceTest : RegressionTest
{
	public override string Name => "SaltoCarregado";
	public override string Description => "o salto cai no ponto mirado, em linha reta; sem ponto, anda o que a barra manda";

	private static readonly Vector3 Start = new(4, 1, -3.5f);
	private const float FacingMinusX = 90f;

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(keep: 1);
		var enemy = b.Bots()[0];
		RegressionBench.Freeze(enemy);
		await b.BecomeWolf();
		// Aqui se mede a distância de um salto atrás do outro: sem esperar a
		// carga do especial encher entre eles.
		b.Weapon.SpecialFree = true;

		await Case(b, enemy, "cheio, olhando reto", 1f, 0f, null);
		await Case(b, enemy, "cheio, olhando para o chão", 1f, -35f, null);
		await Case(b, enemy, "cheio, olhando para cima", 1f, 30f, null);
		await Case(b, enemy, "cheio, inimigo 6 m à frente e 2 m ao lado", 1f, 0f, new Vector3(-2, 1, -1.5f));
		await Case(b, enemy, "cheio, inimigo a 14 m", 1f, 0f, new Vector3(-10, 1, -3.5f));
		await Case(b, enemy, "metade", 0.5f, 0f, null);
		await Case(b, enemy, "metade, olhando para o chão", 0.5f, -20f, null);
		await Case(b, enemy, "sem carga", 0f, 0f, null);

		// Corpo no caminho: o lobo bate nele e para ali — é onde o golpe sai.
		float blocked = await Jump(b, enemy, 1f, 0f, new Vector3(-1, 1, -3.5f), straight: false);
		b.Check(blocked > 3.5f && blocked < 5.5f,
			$"com inimigo no caminho a 5 m o lobo para nele (andou {RegressionBench.F(blocked)} m)");
	}

	private static async Task Case(RegressionBench b, CharacterBody3D enemy, string what, float power, float pitch, Vector3? enemyAt)
	{
		float reach = Mathf.Lerp(b.Weapon.PounceMinDistance, b.Weapon.PounceMaxDistance, power);
		float expected = reach;
		float travelled = await Jump(b, enemy, power, pitch, enemyAt, aim: start =>
		{
			// Com ponto mirado, é a distância dele, até o alcance do salto.
			if (b.Weapon.PounceAimPoint is not Vector3 point) return;
			Vector3 flat = point - start;
			flat.Y = 0;
			expected = Mathf.Clamp(flat.Length(), 0.5f,
				b.Weapon.PounceChargeLimitsReach ? reach : b.Weapon.PounceMaxDistance);
		});
		RegressionBench.Note($"{what}: barra {RegressionBench.F(reach)} m, esperado {RegressionBench.F(expected)} m, andou {RegressionBench.F(travelled)} m");
		b.Near(expected, travelled, 0.12, what);
	}

	private static async Task<float> Jump(RegressionBench b, CharacterBody3D enemy, float power, float pitch,
		Vector3? enemyAt, bool straight = true, System.Action<Vector3> aim = null)
	{
		enemy.GlobalPosition = enemyAt ?? new Vector3(0, 1, 15);
		await b.Place(Start, FacingMinusX, pitch);

		Vector3 from = b.Player.GlobalPosition;
		aim?.Invoke(from);
		bool started = b.Weapon.BotPounce(null, power);
		b.Check(started, "o salto saiu");
		await b.Seconds(3.2);

		Vector3 moved = b.Player.GlobalPosition - from;
		if (straight)
			b.Check(Mathf.Abs(moved.Z) < 0.15f, $"saiu em linha reta (desvio de {RegressionBench.F(moved.Z)} m)");
		return -moved.X;
	}
}
