using Godot;

/// <summary>
/// A mira do salto no chão (pedido do Jhon, 15/11): carregando o especial,
/// um anel marca onde o lobo cai se soltar agora: no ponto que a mira
/// toca (decisão do Jhon, 17/11), até o alcance do salto. O anel anda com
/// a mira; a barra só muda a cor dele (a força). Só na tela do dono.
///
/// O ponto vem da própria arma (<see cref="Weapon.PounceLanding"/>, o
/// mesmo plano do salto de verdade): em cima do caixote ou do teto que o
/// arco alcança, e antes do muro que ele não passa. O anel aparece onde
/// o lobo vai pousar, não só no chão.
/// </summary>
public partial class PounceAim : Node3D
{
	/// <summary>Diâmetro do anel, em metros: perto do alcance da garra na
	/// queda, para dar ideia de quem o golpe pega.</summary>
	[Export] public float Size = 2.2f;

	private Weapon _weapon;
	private MeshInstance3D _ring;

	public override void _Ready()
	{
		TopLevel = true;
		_ring = new MeshInstance3D
		{
			Mesh = new QuadMesh { Size = Vector2.One, Orientation = PlaneMesh.OrientationEnum.Y },
			MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = Code } },
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Visible = false
		};
		AddChild(_ring);
	}

	public override void _Process(double delta)
	{
		_weapon ??= Groups.LocalPlayer(GetTree())?.GetNodeOrNull<Weapon>("Weapon");
		if (_weapon == null || !IsInstanceValid(_weapon)) { _weapon = null; _ring.Visible = false; return; }

		if (_weapon.PounceLanding is not Vector3 landing || _weapon.GetParent() is not CharacterBody3D body)
		{
			_ring.Visible = false;
			return;
		}

		// O ponto já é o do plano do salto, em cima do que houver ali. Um
		// raio curto só deita o anel na inclinação da superfície.
		var space = body.GetWorld3D().DirectSpaceState;
		var down = PhysicsRayQueryParameters3D.Create(landing + Vector3.Up * 0.6f, landing + Vector3.Down * 0.8f);
		down.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };
		var floor = space.IntersectRay(down);
		Vector3 normal = floor.Count > 0 ? (Vector3)floor["normal"] : Vector3.Up;
		Vector3 at = floor.Count > 0 ? (Vector3)floor["position"] : landing;
		if (normal.Y < 0.5f) normal = Vector3.Up;

		Vector3 side = normal.Cross(Vector3.Forward).LengthSquared() > 0.01f ? normal.Cross(Vector3.Forward).Normalized() : Vector3.Right;
		_ring.GlobalTransform = new Transform3D(
			new Basis(side, normal, side.Cross(normal)).Scaled(Vector3.One * Size),
			at + normal * 0.04f);
		((ShaderMaterial)_ring.MaterialOverride).SetShaderParameter("charge", _weapon.PounceCharge);
		_ring.Visible = true;
	}

	private const string Code = """
		shader_type spatial;
		render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

		uniform float charge = 0.0;

		void fragment() {
			float r = length(UV - vec2(0.5)) * 2.0;
			// Anel fino na borda, um ponto no meio e um preenchimento leve.
			float ring = smoothstep(0.80, 0.86, r) * (1.0 - smoothstep(0.94, 1.0, r));
			float dot_ = 1.0 - smoothstep(0.05, 0.09, r);
			float fill = (1.0 - smoothstep(0.0, 0.86, r)) * 0.10;
			// Da cor da barra de carga: laranja, vermelho com a carga cheia.
			vec3 color = mix(vec3(1.0, 0.62, 0.15), vec3(1.0, 0.12, 0.08), charge);
			float pulse = 0.85 + 0.15 * sin(TIME * 6.0);
			ALBEDO = color * 1.6;
			ALPHA = clamp(ring * pulse + dot_ + fill, 0.0, 1.0);
		}
		""";
}
