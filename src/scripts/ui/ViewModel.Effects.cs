using Godot;
using System.Collections.Generic;

/// <summary>
/// O que o tiro solta na tela: o clarão no cano (com a luz que ele joga
/// em volta), o ferrolho que recua e a cápsula que salta. Tudo enfeite,
/// com sorteio próprio — nada aqui mexe no sorteio do jogo.
/// </summary>
public partial class ViewModel
{
	/// <summary>Quanto tempo a cápsula voa antes de sumir, e quantas no ar.</summary>
	private const float CasingLife = 0.7f;
	private const int MaxCasings = 10;

	private Node3D _flash;
	private OmniLight3D _flashLight;
	private double _flashTimer;
	private GameRandom _fx;

	private sealed class Casing
	{
		public MeshInstance3D Node;
		public Vector3 Velocity;
		public Vector3 Spin;
		public float Age;
	}

	private readonly List<Casing> _casings = new();
	private static Mesh _casingMesh;
	private static Material _brass;
	private static Material _shell;

	private void BuildEffects()
	{
		_fx = MatchRandom.Stream("arma-na-mao");

		// Clarão: três planos em estrela, de luz somada, virados para quem atira.
		var material = new StandardMaterial3D
		{
			AlbedoColor = FlashColor,
			AlbedoTexture = new GradientTexture2D
			{
				Gradient = new Gradient
				{
					Offsets = new[] { 0f, 0.35f, 1f },
					Colors = new[] { new Color(1, 1, 1, 1), new Color(1, 0.8f, 0.5f, 0.55f), new Color(1, 0.5f, 0.2f, 0) }
				},
				Fill = GradientTexture2D.FillEnum.Radial,
				FillFrom = new Vector2(0.5f, 0.5f),
				FillTo = new Vector2(1f, 0.5f),
				Width = 64,
				Height = 64
			},
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};

		_flash = new Node3D { Visible = false };
		for (int i = 0; i < 3; i++)
		{
			_flash.AddChild(new MeshInstance3D
			{
				Mesh = new QuadMesh { Size = new Vector2(i == 0 ? 0.16f : 0.26f, i == 0 ? 0.16f : 0.07f) },
				MaterialOverride = material,
				RotationDegrees = new Vector3(0, 0, i == 0 ? 0 : i == 1 ? 35 : -35),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			});
		}
		AddChild(_flash);

		_flashLight = new OmniLight3D
		{
			LightColor = FlashColor,
			LightEnergy = 2.6f,
			OmniRange = 7f,
			ShadowEnabled = false,
			Visible = false
		};
		AddChild(_flashLight);
	}

	private void FireEffects()
	{
		_boltKick = 1f;

		if (_muzzle != null)
		{
			_flash.Position = ToLocal(_muzzle.GlobalPosition) + new Vector3(0, 0, -0.03f);
			_flash.RotationDegrees = new Vector3(0, 0, _fx.Range(0f, 180f));
			_flash.Scale = Vector3.One * _fx.Range(0.8f, 1.25f);
			_flash.Visible = true;
			_flashLight.Position = _flash.Position;
			_flashLight.Visible = true;
			_flashTimer = FlashLife;
		}

		// A escopeta só cospe o cartucho na bombeada.
		if (_pump != null) _pumpTime = 0f;
		else EjectCasing();
	}

	private void UpdateEffects(float dt)
	{
		if (_flashTimer > 0)
		{
			_flashTimer -= dt;
			if (_flashTimer <= 0)
			{
				_flash.Visible = false;
				_flashLight.Visible = false;
			}
		}

		for (int i = _casings.Count - 1; i >= 0; i--)
		{
			Casing c = _casings[i];
			c.Age += dt;
			if (c.Age > CasingLife || !IsInstanceValid(c.Node))
			{
				if (IsInstanceValid(c.Node)) c.Node.QueueFree();
				_casings.RemoveAt(i);
				continue;
			}

			c.Velocity += Vector3.Down * 9.8f * dt;
			c.Node.GlobalPosition += c.Velocity * dt;
			c.Node.Rotation += c.Spin * dt;
		}
	}

	/// <summary>Uma cápsula saltando da janela de ejeção, para a direita
	/// e para cima. Voa solta do corpo (não acompanha a câmera).</summary>
	private void EjectCasing()
	{
		Node3D port = _held?.GetNodeOrNull<Node3D>(WeaponModel.EjectName);
		if (port == null || !Visible || !IsInsideTree()) return;

		_casingMesh ??= new CylinderMesh { TopRadius = 0.0045f, BottomRadius = 0.0045f, Height = 0.024f, RadialSegments = 6, Rings = 0 };
		_brass ??= new StandardMaterial3D { AlbedoColor = new Color(0.78f, 0.6f, 0.22f), Metallic = 0.9f, Roughness = 0.3f };
		_shell ??= new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.1f, 0.08f), Roughness = 0.6f };
		bool shotgun = _pump != null;

		if (_casings.Count >= MaxCasings)
		{
			if (IsInstanceValid(_casings[0].Node)) _casings[0].Node.QueueFree();
			_casings.RemoveAt(0);
		}

		var node = new MeshInstance3D
		{
			Mesh = _casingMesh,
			MaterialOverride = shotgun ? _shell : _brass,
			TopLevel = true,
			Scale = shotgun ? new Vector3(2f, 2.2f, 2f) : Vector3.One,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		AddChild(node);
		node.GlobalPosition = port.GlobalPosition;

		Basis view = _camera.GlobalTransform.Basis;
		_casings.Add(new Casing
		{
			Node = node,
			Velocity = _body.Velocity + view.X * _fx.Range(1.5f, 2.3f) + view.Y * _fx.Range(1.1f, 1.8f) + view.Z * _fx.Range(0.1f, 0.5f),
			Spin = new Vector3(_fx.Range(-18f, 18f), _fx.Range(-6f, 6f), _fx.Range(-18f, 18f))
		});
	}
}
