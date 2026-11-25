using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O neon forte e os olhos acesos do lobo (Jhon, 25/11: "coloque os leds do
/// equipamento brilhando bem forte", "e o olho também", "quero o neon desse
/// jeito no game"). O neon do corpo passa do branco (o glow do mapa faz o
/// halo) e cada olho tem um disco de luz preso ao osso da cabeça
/// (<see cref="EyeGlow"/>), na frente do rosto: aparece no lobo e some com o
/// humano.
/// </summary>
public sealed class WolfEyeGlowTest : RegressionTest
{
	public override string Name => "OlhosDoLobo";
	public override string Description => "o neon do lobo é forte (passa do branco) e os dois olhos acendem na frente do rosto, presos à cabeça; somem com o humano";

	public override async Task Run(RegressionBench b)
	{
		// O valor de fábrica do neon, lido no código do shader (o material do lobo não o muda).
		string code = GD.Load<Shader>(WolfSkin.ShaderPath).Code;
		var found = System.Text.RegularExpressions.Regex.Match(code, @"uniform float neon_energy\s*=\s*([0-9.]+)");
		float energy = found.Success ? float.Parse(found.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0f;
		RegressionBench.Note($"neon do corpo: {energy}");
		b.Check(energy >= 10f, $"o neon do corpo passa do branco e acende o glow ({energy}; era 2,6)");
		// Todo LED acende igual (25/11: "os leds do peitoral e das costas devem ter os mesmos brilhos").
		var even = System.Text.RegularExpressions.Regex.Match(code, @"uniform float neon_even\s*=\s*([0-9.]+)");
		b.Check(even.Success && float.Parse(even.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) >= 0.99f,
			"peitoral e costas acendem como o bracelete (neon_even 1)");

		b.RemoveBots();
		await b.BecomeWolf();
		await b.Seconds(0.6);
		EyeGlow eyes = b.Player.FindChildren("*", "", true, false).OfType<EyeGlow>().FirstOrDefault();
		b.Check(eyes != null && eyes.Discs.Count == 2, "o lobo tem os dois olhos acesos");
		if (eyes == null || eyes.Discs.Count < 2) return;

		var skeleton = eyes.GetParent<Skeleton3D>();
		int head = skeleton.FindBone(eyes.BoneName);
		Vector3 headAt = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(head).Origin;
		Vector3 forward = -b.Player.GlobalBasis.Z;
		forward.Y = 0;
		forward = forward.Normalized();
		Vector3 a = eyes.Discs[0].GlobalPosition, c = eyes.Discs[1].GlobalPosition;
		float apart = a.DistanceTo(c);
		float ahead = Mathf.Min((a - headAt).Dot(forward), (c - headAt).Dot(forward));
		float fromHead = Mathf.Max(a.DistanceTo(headAt), c.DistanceTo(headAt));
		RegressionBench.Note($"olhos: {RegressionBench.F(apart * 100)} cm um do outro, {RegressionBench.F(ahead * 100)} cm à frente do osso da cabeça, no máximo {RegressionBench.F(fromHead * 100)} cm dele");
		b.Check(apart > 0.05f && apart < 0.12f, $"os dois olhos ficam a um palmo um do outro ({RegressionBench.F(apart * 100)} cm)");
		b.Check(ahead > 0.15f, $"na frente do rosto ({RegressionBench.F(ahead * 100)} cm à frente do osso da cabeça)");
		// O osso da cabeça nasce na nuca: o olho fica a uns 30 cm dele.
		b.Check(fromHead < 0.45f, $"presos à cabeça ({RegressionBench.F(fromHead * 100)} cm do osso)");
		b.Check(eyes.Discs.All(d => d.IsVisibleInTree()), "aparecem no lobo");
		float level = eyes.Discs[0].GetInstanceShaderParameter("level").AsSingle();
		b.Check(level >= 0.99f, $"acesos parado ({level:0.00})");

		await b.Tap("slot_1");
		await b.Until(() => b.Form.Current == Form.Human, 4.0);
		await b.Seconds(0.5);
		b.Check(eyes.Discs.All(d => !d.IsVisibleInTree()), "somem com o humano");
	}
}
