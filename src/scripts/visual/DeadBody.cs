using Godot;
using System.Collections.Generic;

/// <summary>
/// O corpo de quem morreu, como se vê: assenta no chão e some por igual
/// (Jhon, 26/11: "corpo do morto afundando/ficando transparente, dá para ver o
/// interior dos personagens. Ele deve ficar transparente por igual, sem poder
/// ver o interior do corpo. E não deve afundar, deve ficar no limite do chão").
/// O mesmo para o humano (<see cref="CharacterAnimator"/>) e o lobo
/// (<see cref="WolfVisual"/>), o seu, o do bot e o boneco da rede.
///
/// **No chão.** O clipe de morte foi animado sobre um chão que não é o do mapa:
/// o lobo (o "mutant dying" do Mixamo) entrava 28 cm no chão no meio da queda e
/// terminava 23 cm enterrado; a Eve ficava 5 cm pairando (o 12 cm fixo que
/// descia todo humano, <c>CorpseDrop</c>, servia ao X Bot). Agora, a cada quadro,
/// o ponto mais baixo da pele (a pele deformada pelo esqueleto, numa amostra
/// dos vértices) é medido contra o chão logo abaixo dele: caindo, o corpo só
/// sobe o que entraria no chão; depois de cair, encosta nele.
///
/// **Some por igual.** Cada superfície da pele vira duas passadas: primeiro a
/// profundidade do corpo inteiro, sem cor (<c>corpo_profundidade.gdshader</c>,
/// ou a do lobo, <c>lobo_profundidade.gdshader</c>, com o pelo balançando
/// igual); depois a cor, transparente, que só passa na pele de fora. O
/// "AlphaDepthPrePass" do Godot que se usava só escreve a profundidade onde o
/// alfa passa de 0,99: no primeiro quadro do sumiço ela sumia, e os olhos, os
/// dentes, o cabelo de baixo e o forro apareciam por cima da pele. O que não é
/// pele (a arma na mão, os discos dos olhos acesos) some pela transparência
/// da própria malha (<see cref="GeometryInstance3D.Transparency"/>).
/// </summary>
public sealed class DeadBody
{
	public const string DepthPath = "res://src/assets/efeitos/corpo_profundidade.gdshader";
	public const string WolfDepthPath = "res://src/assets/efeitos/lobo_profundidade.gdshader";
	public const string WolfFadePath = "res://src/assets/efeitos/lobo_sumindo.gdshader";

	/// <summary>A marca na passada de profundidade do sumiço (o teste confere).</summary>
	public const string Mark = "corpo_sumindo";

	/// <summary>O material é a passada de profundidade do sumiço.</summary>
	public static bool IsFading(Material material) => material != null && material.HasMeta(Mark);

	/// <summary>Tempo à mostra no chão antes de sumir, e o sumiço, em segundos.</summary>
	public float Hold = 1.2f, Fade = 1.2f;

	/// <summary>O quanto o corpo pode subir (m) para sair do chão, e descer
	/// para encostar nele: mais que isso é a medida errada (o chão de baixo de
	/// uma escada, uma borda), e o corpo fica onde o clipe o põe.</summary>
	public float MaxLift = 0.6f, MaxDrop = 0.4f;

	private readonly Node3D _model;
	private readonly CollisionObject3D _body;
	private float _baseY, _offset, _delta;
	private bool _begun, _fallen;

	/// <summary>O esqueleto que avisa quando a pose do quadro ficou pronta: a
	/// medida é feita ali, e não no _Process do dono, que corre antes da
	/// animação (um quadro atrasado, o lobo caindo rápido entrava 5 cm).</summary>
	private Skeleton3D _listening;

	private readonly List<Sampled> _skin = new();
	private readonly List<(MeshInstance3D Mesh, int Surface, Material Before)> _surfaces = new();
	private readonly List<(BaseMaterial3D Material, float Alpha, float Emission)> _colors = new();
	private readonly List<ShaderMaterial> _wolfColors = new();
	private readonly List<(GeometryInstance3D Node, float Before)> _instances = new();

	/// <summary>Uma malha da pele e a amostra dos vértices dela (vértice, os
	/// ossos e os pesos), tirada uma vez por morte.</summary>
	private sealed class Sampled
	{
		public MeshInstance3D Mesh;
		public Skeleton3D Skeleton;
		public int[] BindBone;
		public Transform3D[] BindPose;
		public Transform3D[] Bind;
		public readonly List<Vector3> Vertex = new();
		public readonly List<int> Bones = new();
		public readonly List<float> Weights = new();
	}

	/// <summary>Ossos por vértice guardados (o Godot usa 4 ou 8).</summary>
	private const int Slots = 8;

	public DeadBody(Node3D model, CollisionObject3D body)
	{
		_model = model;
		_body = body;
	}

	/// <summary>O modelo deste corpo (o personagem pode trocar de modelo).</summary>
	public Node3D Model => _model;

	/// <summary>Algum material do sumiço está no corpo agora.</summary>
	public bool HasFadeMaterials => _surfaces.Count > 0 || _instances.Count > 0;

	/// <summary>Quanto o corpo foi levado para encostar no chão, em metros
	/// (positivo: para cima).</summary>
	public float Offset => _offset;

	/// <summary>Morreu: guarda a altura do modelo e a amostra da pele.</summary>
	public void Begin()
	{
		if (_model == null) return;
		if (!_begun) _baseY = _model.Position.Y;
		_begun = true;
		_offset = 0f;
		_fallen = false;
		SampleSkin();
		Listen(_skin.Count > 0 ? _skin[0].Skeleton : null);
	}

	/// <summary>
	/// Um quadro do corpo morto. <paramref name="timer"/> é o tempo desde a
	/// morte; <paramref name="clip"/>, a duração do clipe de morte. Durante o
	/// clipe o corpo só sobe o que entraria no chão; depois, encosta nele, fica
	/// <see cref="Hold"/> à mostra e some em <see cref="Fade"/>.
	/// </summary>
	public void Step(double delta, double timer, double clip)
	{
		if (_model == null || !_begun || !_model.Visible) return;

		_fallen = timer >= clip;
		_delta = (float)delta;
		if (_listening == null) OnPose();

		double t = (timer - clip - Hold) / Mathf.Max(Fade, 0.01f);
		float progress = t <= 0 ? 0f : Mathf.Clamp((float)t, 0f, 1f);
		if (progress <= 0f) return;

		SetOpacity(1f - progress);
		if (progress >= 1f)
		{
			_model.Visible = false;
			Listen(null);
		}
	}

	/// <summary>A pose do quadro está pronta: encosta o corpo no chão.</summary>
	private void OnPose()
	{
		if (_model == null || !_begun || !_model.Visible) return;
		FitFloor(_delta, _fallen);
		_model.Position = new Vector3(_model.Position.X, _baseY + _offset, _model.Position.Z);
	}

	private void Listen(Skeleton3D skeleton)
	{
		if (_listening == skeleton) return;
		if (_listening != null && GodotObject.IsInstanceValid(_listening)) _listening.SkeletonUpdated -= OnPose;
		_listening = skeleton;
		if (_listening != null) _listening.SkeletonUpdated += OnPose;
	}

	/// <summary>Renasceu: a altura, os materiais e a transparência de antes.</summary>
	public void Reset()
	{
		Listen(null);
		Restore();
		if (_model != null && _begun)
		{
			_model.Position = new Vector3(_model.Position.X, _baseY, _model.Position.Z);
			_model.Visible = true;
		}
		_offset = 0f;
		_skin.Clear();
	}

	// ---------------------------------------------------------------- no chão

	private void SampleSkin()
	{
		_skin.Clear();
		foreach (Node node in _model.FindChildren("*", nameof(MeshInstance3D), true, false))
		{
			if (node is not MeshInstance3D { Mesh: Mesh mesh, Skin: Skin skin } instance) continue;
			Skeleton3D skeleton = instance.GetNodeOrNull<Skeleton3D>(instance.Skeleton);
			if (skeleton == null) continue;

			var sampled = new Sampled { Mesh = instance, Skeleton = skeleton };
			int binds = skin.GetBindCount();
			sampled.BindBone = new int[binds];
			sampled.BindPose = new Transform3D[binds];
			sampled.Bind = new Transform3D[binds];
			for (int k = 0; k < binds; k++)
			{
				int bone = skin.GetBindBone(k);
				sampled.BindBone[k] = bone >= 0 ? bone : skeleton.FindBone(skin.GetBindName(k));
				sampled.BindPose[k] = skin.GetBindPose(k);
			}

			for (int s = 0; s < mesh.GetSurfaceCount(); s++)
			{
				var arrays = mesh.SurfaceGetArrays(s);
				if (arrays[(int)Mesh.ArrayType.Bones].VariantType == Variant.Type.Nil) continue;
				Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
				int[] bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
				float[] weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
				if (vertices.Length == 0) continue;
				int per = Mathf.Min(bones.Length / vertices.Length, Slots);
				// Uns 6000 por superfície: o bastante para achar o pé, a garra e o
				// rabo (o lobo é uma malha só), e barato para fazer a cada quadro.
				int stride = Mathf.Max(1, vertices.Length / 6000);
				for (int v = 0; v < vertices.Length; v += stride)
				{
					sampled.Vertex.Add(vertices[v]);
					int stored = bones.Length / vertices.Length;
					for (int j = 0; j < Slots; j++)
					{
						sampled.Bones.Add(j < per ? bones[v * stored + j] : 0);
						sampled.Weights.Add(j < per ? weights[v * stored + j] : 0f);
					}
				}
			}
			if (sampled.Vertex.Count > 0) _skin.Add(sampled);
		}
	}

	/// <summary>O ponto mais baixo da pele agora, no mundo. Falso sem pele.</summary>
	private bool Lowest(out Vector3 lowest)
	{
		lowest = Vector3.Zero;
		float low = float.MaxValue;
		foreach (Sampled s in _skin)
		{
			if (!GodotObject.IsInstanceValid(s.Mesh) || !GodotObject.IsInstanceValid(s.Skeleton)) continue;
			Transform3D skeleton = s.Skeleton.GlobalTransform;
			for (int k = 0; k < s.Bind.Length; k++)
				s.Bind[k] = s.BindBone[k] >= 0 ? skeleton * s.Skeleton.GetBoneGlobalPose(s.BindBone[k]) * s.BindPose[k] : skeleton;
			for (int v = 0; v < s.Vertex.Count; v++)
			{
				Vector3 p = Vector3.Zero;
				for (int j = 0; j < Slots; j++)
				{
					float w = s.Weights[v * Slots + j];
					if (w > 0f) p += s.Bind[s.Bones[v * Slots + j]] * s.Vertex[v] * w;
				}
				if (p.Y < low)
				{
					low = p.Y;
					lowest = p;
				}
			}
		}
		return low < float.MaxValue;
	}

	private void FitFloor(float delta, bool fallen)
	{
		if (_skin.Count == 0 || !Lowest(out Vector3 low)) return;
		// O ponto mais baixo sem a correção de agora: a pose do clipe.
		float pose = low.Y - _offset;
		if (!Floor(new Vector3(low.X, pose, low.Z), out float floor)) return;

		float target = Mathf.Clamp(floor - pose, -MaxDrop, MaxLift);
		if (!fallen)
		{
			// Caindo, o clipe manda: só sobe o que entraria no chão.
			_offset = Mathf.Max(target, 0f);
			return;
		}
		// Caído: encosta. Subir é na hora (nada entra no chão); descer, em
		// um quarto de segundo, para não dar um tranco.
		_offset = target > _offset ? target : Mathf.Lerp(_offset, target, 1f - Mathf.Exp(-delta / 0.08f));
	}

	private bool Floor(Vector3 at, out float floor) => FloorUnder(_model, _body, at, out floor);

	/// <summary>A altura do chão logo abaixo do ponto (o cenário, não os
	/// corpos), no mundo de <paramref name="node"/>.</summary>
	public static bool FloorUnder(Node3D node, CollisionObject3D self, Vector3 at, out float floor)
	{
		floor = 0f;
		World3D world = node?.GetWorld3D();
		if (world == null) return false;
		var exclude = new Godot.Collections.Array<Rid>();
		if (self != null) exclude.Add(self.GetRid());
		for (int tries = 0; tries < 4; tries++)
		{
			var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * 0.8f, at + Vector3.Down * 2.5f, 1, exclude);
			var hit = world.DirectSpaceState.IntersectRay(query);
			if (hit.Count == 0) return false;
			if (hit["collider"].AsGodotObject() is CharacterBody3D other)
			{
				exclude.Add(other.GetRid());
				continue;
			}
			floor = ((Vector3)hit["position"]).Y;
			return true;
		}
		return false;
	}

	// ---------------------------------------------------------------- o sumiço

	private void SetOpacity(float opacity)
	{
		if (!HasFadeMaterials) Prepare();

		foreach (var (material, alpha, emission) in _colors)
		{
			Color color = material.AlbedoColor;
			color.A = alpha * opacity;
			material.AlbedoColor = color;
			// O brilho (as faixas acesas da roupa) apaga junto, um pouco antes.
			material.EmissionEnergyMultiplier = emission * opacity * opacity;
		}
		foreach (ShaderMaterial material in _wolfColors)
			material.SetShaderParameter("sumindo", opacity);
		foreach (var (node, before) in _instances)
			if (GodotObject.IsInstanceValid(node)) node.Transparency = 1f - (1f - before) * opacity;
	}

	private void Prepare()
	{
		Shader depth = GD.Load<Shader>(DepthPath);
		foreach (Node node in _model.FindChildren("*", nameof(GeometryInstance3D), true, false))
		{
			var geometry = (GeometryInstance3D)node;
			if (geometry is MeshInstance3D { Mesh: Mesh mesh, Skin: not null } skinned && geometry.MaterialOverride == null)
			{
				bool all = true;
				for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
					all &= PrepareSurface(skinned, surface, depth);
				if (all) continue;
			}
			// O que não é pele (a arma na mão, os olhos acesos): a malha inteira.
			_instances.Add((geometry, geometry.Transparency));
		}
	}

	/// <summary>A superfície vira profundidade antes, cor transparente depois.
	/// Falso se o material não é dos que se sabe dividir.</summary>
	private bool PrepareSurface(MeshInstance3D mesh, int surface, Shader depthShader)
	{
		Material active = mesh.GetActiveMaterial(surface);
		ShaderMaterial depth;
		Material color;

		if (active is ShaderMaterial wolf && wolf.Shader?.ResourcePath == WolfSkin.ShaderPath)
		{
			depth = Copy(wolf, GD.Load<Shader>(WolfDepthPath));
			var fade = Copy(wolf, GD.Load<Shader>(WolfFadePath));
			fade.SetShaderParameter("sumindo", 1f);
			_wolfColors.Add(fade);
			color = fade;
		}
		else if (active is BaseMaterial3D source)
		{
			var fade = (BaseMaterial3D)source.Duplicate();
			fade.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			_colors.Add((fade, fade.AlbedoColor.A, fade.EmissionEnergyMultiplier));
			color = fade;

			depth = new ShaderMaterial { Shader = depthShader };
			depth.SetShaderParameter("albedo", source.AlbedoColor);
			depth.SetShaderParameter("uv1_scale", source.Uv1Scale);
			depth.SetShaderParameter("uv1_offset", source.Uv1Offset);
			bool cut = source.Transparency != BaseMaterial3D.TransparencyEnum.Disabled && source.AlbedoTexture != null;
			depth.SetShaderParameter("use_cutout", cut);
			if (cut) depth.SetShaderParameter("albedo_tex", source.AlbedoTexture);
			depth.SetShaderParameter("cutout", source.Transparency == BaseMaterial3D.TransparencyEnum.AlphaScissor ? source.AlphaScissorThreshold : 0.5f);
		}
		else return false;

		// A profundidade antes da cor: os dois são transparentes, da mesma
		// malha e à mesma distância, e o Godot só os põe em ordem pela prioridade.
		color.RenderPriority = Mathf.Clamp(active.RenderPriority, (int)Material.RenderPriorityMin + 1, (int)Material.RenderPriorityMax);
		depth.RenderPriority = color.RenderPriority - 1;
		depth.NextPass = color;
		depth.SetMeta(Mark, true);

		_surfaces.Add((mesh, surface, mesh.GetSurfaceOverrideMaterial(surface)));
		mesh.SetSurfaceOverrideMaterial(surface, depth);
		return true;
	}

	/// <summary>Um material novo com o shader dado e os parâmetros do lobo.</summary>
	private static ShaderMaterial Copy(ShaderMaterial source, Shader shader)
	{
		var copy = new ShaderMaterial { Shader = shader };
		foreach (Godot.Collections.Dictionary uniform in source.Shader.GetShaderUniformList())
		{
			string name = uniform["name"].AsString();
			Variant value = source.GetShaderParameter(name);
			if (value.VariantType != Variant.Type.Nil) copy.SetShaderParameter(name, value);
		}
		return copy;
	}

	private void Restore()
	{
		foreach (var (mesh, surface, before) in _surfaces)
			if (GodotObject.IsInstanceValid(mesh)) mesh.SetSurfaceOverrideMaterial(surface, before);
		foreach (var (node, before) in _instances)
			if (GodotObject.IsInstanceValid(node)) node.Transparency = before;
		_surfaces.Clear();
		_colors.Clear();
		_wolfColors.Clear();
		_instances.Clear();
	}
}
