using Godot;
using System.Collections.Generic;

/// <summary>
/// O brilho dos olhos do lobo (Jhon, 25/11: "e o olho também", junto com o
/// neon mais forte do corpo; "quero o neon desse jeito no game"). Dois
/// discos de luz (assets/efeitos/olho.gdshader) presos ao osso da cabeça,
/// por cima da emissão dos olhos do modelo, que sozinha é pequena demais
/// para o glow do mapa fazer halo.
///
/// Criado e alimentado pelo <see cref="WolfFx"/>: a cor é a do personagem
/// e o brilho sobe com o neon do corpo (poder, rugido). Cosmético e local,
/// como o resto dos efeitos do lobo; filho do modelo, some com ele.
/// </summary>
public partial class EyeGlow : BoneAttachment3D
{
	public const string ShaderPath = "res://src/assets/efeitos/olho.gdshader";

	/// <summary>Onde ficam os dois olhos, no espaço do osso da cabeça (na
	/// unidade do esqueleto: centímetros), 2 cm à frente da pele. Medidos na
	/// textura de emissão do modelo (o miolo aceso de cada amêndoa), que é o
	/// mesmo rosto nos dois times (eve/lobo_azul e barut/lobo_vermelho).</summary>
	[Export] public Vector3 LeftEye = new(6.47f, 15.64f, 22.7f);
	[Export] public Vector3 RightEye = new(-1.03f, 15.58f, 24.43f);

	/// <summary>O tamanho de cada disco, em metros (o halo; o miolo é bem menor).</summary>
	[Export] public float Size = 0.11f;

	private readonly List<MeshInstance3D> _discs = new();

	/// <summary>Os olhos no osso da cabeça do esqueleto. Nulo sem o osso ou
	/// sem o shader.</summary>
	public static EyeGlow For(Skeleton3D skeleton)
	{
		if (skeleton == null || !ResourceLoader.Exists(ShaderPath)) return null;
		int head = -1;
		for (int i = 0; i < skeleton.GetBoneCount(); i++)
			if (skeleton.GetBoneName(i).EndsWith("Head")) { head = i; break; }
		if (head < 0) return null;

		var glow = new EyeGlow { Name = "Olhos", BoneName = skeleton.GetBoneName(head) };
		skeleton.AddChild(glow);
		return glow;
	}

	public override void _Ready()
	{
		// O disco é medido em metros; o esqueleto do lobo é em centímetros
		// (e o modelo tem a escala dele): o tamanho vai na unidade do osso.
		float unit = GetParent() is Node3D parent ? Mathf.Max(parent.GlobalBasis.Scale.X, 1e-4f) : 1f;
		var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
		var mesh = new QuadMesh { Size = Vector2.One * (Size / unit), Material = material };
		foreach (Vector3 at in new[] { LeftEye, RightEye })
		{
			var disc = new MeshInstance3D
			{
				Mesh = mesh,
				Position = at,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				// A caixa do disco gira com a câmera: folga para não sumir na borda da tela.
				ExtraCullMargin = Size / unit
			};
			AddChild(disc);
			_discs.Add(disc);
		}
	}

	/// <summary>A cor e o quanto acende (1 = parado; sobe com o poder).</summary>
	public void Show(float level, Color color)
	{
		foreach (MeshInstance3D disc in _discs)
		{
			disc.SetInstanceShaderParameter("level", level);
			disc.SetInstanceShaderParameter("tint", color);
		}
	}

	/// <summary>Os dois discos, para os testes.</summary>
	public IReadOnlyList<MeshInstance3D> Discs => _discs;
}
