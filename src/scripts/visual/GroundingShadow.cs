using Godot;

/// <summary>
/// A sombra de apoio dos personagens (pedido do Jhon, 26/11: "os personagens
/// precisam ter sombras mais precisas e definidas, mesmo em lugares escuros,
/// e sem mexer na saturação; precisa realmente de uma lógica por trás dessa
/// sombra" — "tanto o homem quanto o lobo").
///
/// **A lógica.** A sombra de um corpo só existe onde há luz direta para ele
/// tapar: dentro da sombra de um prédio, num beco, num mapa de noite, o
/// sol não chega e o personagem fica sem sombra nenhuma — parece flutuar.
/// E ao sol a sombra é macia de propósito (o sol do mapa tem tamanho, e a
/// sombra dele cobre 120 a 200 m): certa para um prédio, frouxa para um
/// corpo de 1,8 m.
///
/// Aqui entra um **par de luzes direcionais que se anulam**: uma soma
/// <see cref="Strength"/> de luz, a outra subtrai exatamente o mesmo
/// (<c>light_negative</c>), na mesma direção. Em todo o mapa o resultado é
/// zero — **o brilho, a cor e a saturação de nada mudam**. Só a primeira
/// tem sombra, e **só os personagens fazem sombra para ela**
/// (<c>shadow_caster_mask</c>): onde um corpo tapa essa luz, sobra a que
/// subtrai, e o chão escurece ali. É uma sombra pura, sem luz junto.
///
/// - **Definida**: o mapa de sombra dela cobre só <see cref="Reach"/> m em
///   volta da câmera e só desenha os corpos — cada ponto dele vale poucos
///   centímetros —, sem o tamanho do sol (a borda não abre com a distância).
/// - **No escuro também**: não depende de o sol chegar. Onde a luz do
///   lugar é pouca, tirar <see cref="Strength"/> dela pesa mais: a sombra
///   aparece mais justamente onde o corpo não tinha nenhuma.
/// - **Coerente**: cai para o mesmo lado da sombra do sol do mapa (o mesmo
///   rumo), mais a pino (nunca abaixo de <see cref="MinElevation"/>°), para
///   ficar curta e ao pé do corpo. Ao sol ela cai por dentro da sombra do
///   sol e vira o miolo nítido dela, junto dos pés; na sombra, é a única.
/// - O corpo também a recebe: o que fica por baixo dele (a barriga e as
///   patas do lobo, as pernas sob o tronco) escurece um pouco, como
///   escurece de verdade o que o céu não vê.
///
/// Quem faz sombra: todo corpo marcado como personagem
/// (<see cref="ViewModel.MarkCharacter"/>: humanos, lobos, o robô de
/// treino), o corpo do dono (que a câmera dele não desenha, mas tem
/// sombra) e o de quem se assiste. O mapa, as armas no chão e os efeitos,
/// não. Quem a põe é o <see cref="MapSlot"/>, ao carregar o mapa. Na
/// qualidade Baixa não entra (é uma sombra direcional a mais).
/// </summary>
public partial class GroundingShadow : Node3D
{
	public const string NodeName = "SombraDeApoio";
	public const string Group = "sombra_de_apoio";

	/// <summary>A camada escondida do corpo do dono (<c>CharacterAnimator.BodyLayer</c> 2).</summary>
	public const uint OwnBodyLayer = 1u << 1;

	/// <summary>Quem faz esta sombra: os personagens, o corpo do dono e o de quem se assiste.</summary>
	public const uint Casters = ViewModel.CharacterLayer | OwnBodyLayer | KillCam.WatchedLayer;

	/// <summary>Quanta luz a sombra tira do chão (a energia das duas luzes do par). Ao sol (luz ~1,3) é um miolo
	/// discreto; num beco (luz ~0,3) é a sombra inteira.</summary>
	[Export] public float Strength = 0.42f;

	/// <summary>O menor ângulo da sombra com o chão, em graus: mais a pino que o sol do mapa, para a sombra ficar
	/// curta, ao pé do corpo (a 64°, um corpo de 1,8 m faz 0,9 m de sombra).</summary>
	[Export] public float MinElevation = 64f;

	/// <summary>Até que distância da câmera a sombra é desenhada, em metros. Menos = mais nítida.</summary>
	[Export] public float Reach = 34f;

	/// <summary>A maciez da borda (1 = a do Godot; menos = mais recortada).</summary>
	[Export] public float Blur = 0.4f;

	[Export] public float Bias = 0.035f;
	[Export] public float NormalBias = 1.2f;

	private DirectionalLight3D _casts, _takes;

	/// <summary>A luz que soma (e tem a sombra) e a que subtrai.</summary>
	public DirectionalLight3D Casting => _casts;
	public DirectionalLight3D Taking => _takes;

	/// <summary>Põe a sombra de apoio em <paramref name="map"/>, seguindo o sol <paramref name="sun"/> (o do mapa,
	/// ou o da cena da partida). Uma vez só.</summary>
	public static GroundingShadow AddTo(Node map, DirectionalLight3D sun)
	{
		if (map == null || OS.HasFeature("dedicated_server")) return null;
		if (map.GetNodeOrNull<GroundingShadow>(NodeName) is GroundingShadow already) return already;

		var shadow = new GroundingShadow { Name = NodeName };
		shadow.Aim(sun);
		map.AddChild(shadow);
		return shadow;
	}

	/// <summary>Para onde a luz do par aponta: o rumo do sol, nunca mais deitada que <see cref="MinElevation"/>.
	/// Sem sol, a pino.</summary>
	public static Vector3 DirectionFor(DirectionalLight3D sun, float minElevation) =>
		sun == null ? Vector3.Down : DirectionFor(-sun.GlobalBasis.Z, minElevation);

	/// <summary>O mesmo, a partir de para onde a luz do sol vai.</summary>
	public static Vector3 DirectionFor(Vector3 ray, float minElevation)
	{
		Vector3 down = Vector3.Down;
		if (ray.LengthSquared() < 1e-6f) return down;
		ray = ray.Normalized();
		if (ray.Y > -0.05f) return down;                         // sol no horizonte ou de baixo: a pino
		float elevation = Mathf.RadToDeg(Mathf.Asin(-ray.Y));
		if (elevation >= minElevation) return ray;

		var flat = new Vector3(ray.X, 0, ray.Z);
		if (flat.LengthSquared() < 1e-6f) return down;
		float e = Mathf.DegToRad(minElevation);
		return (flat.Normalized() * Mathf.Cos(e) + Vector3.Down * Mathf.Sin(e)).Normalized();
	}

	private Vector3 _direction = Vector3.Down;

	private void Aim(DirectionalLight3D sun) => _direction = DirectionFor(sun, MinElevation);

	/// <summary>Para quem monta a cena à mão (a ferramenta de fotos dos mapas): segue o sol cuja luz vai para
	/// <paramref name="ray"/> (zero: sem sol, a pino). Antes de entrar na árvore.</summary>
	public void FollowRay(Vector3 ray) => _direction = DirectionFor(ray, MinElevation);

	public override void _Ready()
	{
		AddToGroup(Group);
		TopLevel = true;
		// A luz olha para −Z: aponta o −Z para a direção da sombra.
		Vector3 up = Mathf.Abs(_direction.Y) > 0.98f ? Vector3.Forward : Vector3.Up;
		GlobalTransform = new Transform3D(Basis.LookingAt(_direction, up), new Vector3(0, 60, 0));

		_casts = Light("Soma");
		_casts.ShadowEnabled = true;
		_casts.ShadowCasterMask = Casters;
		_casts.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal;
		_casts.DirectionalShadowMaxDistance = Reach;
		_casts.DirectionalShadowFadeStart = 0.85f;
		_casts.ShadowBlur = Blur;
		_casts.ShadowBias = Bias;
		_casts.ShadowNormalBias = NormalBias;
		_casts.LightAngularDistance = 0f;

		_takes = Light("Subtrai");
		_takes.LightNegative = true;
		_takes.ShadowEnabled = false;

		AddChild(_casts);
		AddChild(_takes);
		Apply();
	}

	/// <summary>Uma luz do par: branca, sem brilho especular, sem entrar no céu, na névoa nem na luz indireta — as
	/// duas têm que ser iguais em tudo, menos no sinal, para se anularem.</summary>
	private DirectionalLight3D Light(string name) => new()
	{
		Name = name,
		LightColor = Colors.White,
		LightEnergy = Strength,
		LightSpecular = 0f,
		LightIndirectEnergy = 0f,
		LightVolumetricFogEnergy = 0f,
		LightBakeMode = Light3D.BakeMode.Disabled,
		SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly,
	};

	/// <summary>Liga ou desliga pela qualidade escolhida (na Baixa não entra).</summary>
	public void Apply()
	{
		bool on = Settings.Values.Quality > 0;
		if (_casts != null) _casts.Visible = on;
		if (_takes != null) _takes.Visible = on;
	}
}
