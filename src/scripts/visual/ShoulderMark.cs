using Godot;

/// <summary>
/// O brilho vermelho no ombro do inimigo (<see cref="EnemyMarks"/>): um
/// quadrado sempre virado para a câmera, com um ponto forte no meio e um
/// halo que some para fora. Misturado, não somado: somado, sumia contra o
/// céu e a neve; misturado, o vermelho aparece em fundo claro e, acima de
/// 1, acende o bloom dos mapas no escuro.
///
/// O tamanho é em metros do mundo, não do osso: o esqueleto do lobo vem
/// com escala 0,01 do arquivo e o do humano com 1, e o brilho tem de ser
/// o mesmo nos dois. Por isso o shader monta o quadrado no espaço da
/// câmera a partir só da posição do osso.
/// </summary>
public static class ShoulderMark
{
	/// <summary>Lado do quadrado do brilho, em metros (o halo inteiro).</summary>
	public const float Size = 0.30f;

	/// <summary>Quanto o brilho fica acima da junta do ombro, em metros:
	/// a junta está dentro do músculo, o brilho fica em cima dele e passa
	/// da linha do ombro (visto de lado, o ombro de lá ainda aparece).</summary>
	public const float Lift = 0.11f;

	/// <summary>Quanto o brilho vem na direção da câmera, em metros: o
	/// próprio ombro não o tampa, mas uma parede entre você e o inimigo
	/// tampa (teste de profundidade ligado). Com 0,25 m, visto do lado
	/// direito o brilho do ombro de lá passava na frente do rosto e
	/// parecia um olho aceso; com 0,15 a cabeça o esconde.</summary>
	public const float TowardCamera = 0.15f;

	/// <summary>Força do brilho. Acima de 1 passa do branco do bloom e
	/// acende a auréola dos mapas que têm glow.</summary>
	public const float Energy = 3.0f;

	private static ShaderMaterial _material;
	private static QuadMesh _quad;

	public static ShaderMaterial Material => _material ??= new ShaderMaterial
	{
		Shader = new Shader { Code = Code },
		RenderPriority = 6
	};

	/// <summary>O nó do brilho preso ao braço esquerdo de
	/// <paramref name="skeleton"/>, ou null se o esqueleto não tem esse
	/// osso (o da arma, por exemplo).</summary>
	public static Node3D Make(Skeleton3D skeleton)
	{
		string bone = LeftArm(skeleton);
		if (bone == null) return null;

		var attachment = new BoneAttachment3D { Name = EnemyMarks.MarkName, BoneName = bone };
		attachment.AddToGroup(EnemyMarks.MarkGroup);

		var glow = new MeshInstance3D
		{
			Name = "Brilho",
			Mesh = _quad ??= new QuadMesh { Size = Vector2.One },
			MaterialOverride = Material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			// O quadrado é montado no shader, longe da caixa do nó (que
			// no lobo mede 1 cm): sem uma caixa larga a câmera o cortava
			// na beira da tela.
			CustomAabb = new Aabb(new Vector3(-100, -100, -100), new Vector3(200, 200, 200)),
			Layers = LayersOf(skeleton)
		};
		glow.AddToGroup(EnemyMarks.MarkGroup);
		attachment.AddChild(glow);
		return attachment;
	}

	/// <summary>O osso do braço esquerdo (o da junta do ombro), seja qual
	/// for o prefixo do arquivo: "mixamorig_LeftArm", "mixamorig:LeftArm".</summary>
	private static string LeftArm(Skeleton3D skeleton)
	{
		for (int i = 0; i < skeleton.GetBoneCount(); i++)
		{
			string name = skeleton.GetBoneName(i);
			if (name.EndsWith("LeftArm") && !name.EndsWith("ForeArm")) return name;
		}
		return null;
	}

	/// <summary>A camada de desenho do corpo: o brilho aparece para as
	/// mesmas câmeras que o veem.</summary>
	private static uint LayersOf(Node node)
	{
		foreach (Node child in node.GetChildren())
			if (child is VisualInstance3D visual && child.Name != EnemyMarks.MarkName) return visual.Layers;
		return 1;
	}

	private static string F(float value) => value.ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture);

	private static string Code => $$"""
		shader_type spatial;
		render_mode unshaded, skip_vertex_transform, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

		uniform vec4 color : source_color = vec4(1.0, 0.06, 0.03, 1.0);

		void vertex() {
			// Só a posição do osso vale; o tamanho é em metros do mundo.
			vec3 center = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz + vec3(0.0, {{F(Lift)}}, 0.0);
			vec3 view = (VIEW_MATRIX * vec4(center, 1.0)).xyz;
			view += normalize(-view) * {{F(TowardCamera)}};
			VERTEX = view + vec3(VERTEX.xy * {{F(Size)}}, 0.0);
			NORMAL = vec3(0.0, 0.0, 1.0);
		}

		void fragment() {
			float r = length(UV - vec2(0.5)) * 2.0;
			float halo = pow(clamp(1.0 - r, 0.0, 1.0), 2.2);
			float core = smoothstep(0.32, 0.0, r);
			// Pulsa de leve: parado no canto do olho, um ponto que pulsa
			// chama mais atenção que um aceso.
			float pulse = 0.82 + 0.18 * sin(TIME * 5.0);
			ALBEDO = mix(color.rgb, vec3(1.0, 0.75, 0.6), core * 0.5) * {{F(Energy)}};
			ALPHA = clamp(halo * 0.95 + core, 0.0, 1.0) * pulse;
		}
		""";
}
