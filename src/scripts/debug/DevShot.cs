using Godot;
using System.Globalization;
using System.Linq;

/// <summary>
/// Ferramenta de desenvolvimento: fotografa o jogo de verdade pela linha de
/// comando e fecha. Serve para conferir o que só se vê na tela (a sombra em
/// primeira pessoa, o header do lobby) sem ninguém jogar.
///
///   -- --foto=a.png            salva a tela neste arquivo (vários: a.png|b.png)
///      --foto-pos=x,y,z        antes da foto, põe o jogador aqui (mundo; um por foto, com |)
///      --foto-olhar=yaw,pitch  e vira-o para cá, em graus (0 = norte, −90 = leste); "sombra": de costas para o sol
///                              do mapa, olhando o chão à frente — onde cai a sombra do próprio corpo
///      --foto-espera=2.5       segundos antes da primeira foto (o mapa e as sombras assentam)
///      --foto-teclas=slot_1    ações apertadas um segundo depois de abrir (escolher o conjunto 1, por exemplo)
///
/// Nunca prende o mouse (a janela abre atrás, com `open -g`: quem está usando
/// o computador não perde o cursor). Não entra no jogo exportado sem o
/// argumento. Descartável.
/// </summary>
public partial class DevShot : Node
{
	private string[] _files, _places, _looks;
	private double _wait = 2.5;
	private int _next;
	private double _left;
	private bool _placed;
	private string[] _taps = System.Array.Empty<string>();
	private double _age;
	private int _tapped;

	/// <summary>Liga a ferramenta se a linha de comando pedir.</summary>
	public static void Start(Node host, string[] args)
	{
		string files = Value(args, "--foto=");
		if (files == null) return;

		var shot = new DevShot
		{
			Name = "DevShot",
			ProcessMode = ProcessModeEnum.Always,
			_files = files.Split('|'),
			_places = Value(args, "--foto-pos=")?.Split('|') ?? System.Array.Empty<string>(),
			_looks = Value(args, "--foto-olhar=")?.Split('|') ?? System.Array.Empty<string>(),
		};
		shot._taps = Value(args, "--foto-teclas=")?.Split(',') ?? System.Array.Empty<string>();
		if (double.TryParse(Value(args, "--foto-espera="), NumberStyles.Float, CultureInfo.InvariantCulture, out double wait)) shot._wait = wait;
		shot._left = shot._wait;
		host.AddChild(shot);
	}

	private static string Value(string[] args, string key) => args.FirstOrDefault(a => a.StartsWith(key))?[key.Length..];

	public override void _Process(double delta)
	{
		// A janela é de fotografia: o cursor fica com quem está no computador.
		if (Input.MouseMode != Input.MouseModeEnum.Visible) Input.MouseMode = Input.MouseModeEnum.Visible;

		// As teclas pedidas: uma a cada 0,4 s, a partir de 1 s.
		_age += delta;
		if (_tapped < _taps.Length * 2 && _age > 1.0 + 0.2 * _tapped)
		{
			Input.ParseInputEvent(new InputEventAction { Action = _taps[_tapped / 2], Pressed = _tapped % 2 == 0 });
			_tapped++;
		}

		_left -= delta;
		if (!_placed && _left <= 1.2)
		{
			_placed = true;
			Place();
		}
		if (_left > 0) return;

		Image image = GetViewport().GetTexture().GetImage();
		string file = _files[_next];
		string path = file.StartsWith("res://") || file.StartsWith("user://") ? ProjectSettings.GlobalizePath(file) : file;
		DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir());
		image.SavePng(path);
		GD.Print($"foto: {path}");

		_next++;
		if (_next >= _files.Length)
		{
			GetTree().Quit();
			SetProcess(false);
			return;
		}
		_placed = false;
		_left = 1.6;
	}

	/// <summary>Põe o jogador no lugar e no olhar da foto da vez, se pedidos.</summary>
	private void Place()
	{
		if (Groups.LocalPlayer(GetTree()) is not CharacterBody3D player) return;

		if (_next < _places.Length && Numbers(_places[_next]) is { Length: 3 } at)
		{
			player.GlobalPosition = new Vector3(at[0], at[1], at[2]);
			player.Velocity = Vector3.Zero;
		}
		if (_next >= _looks.Length) return;

		float yaw, pitch;
		if (_looks[_next] == "sombra")
		{
			// De costas para o sol: a sombra do corpo cai à frente; o olhar, no chão.
			Vector3 ray = Vector3.Forward;
			foreach (Node node in GetTree().Root.FindChildren("Sol", nameof(DirectionalLight3D), true, false))
				ray = -((DirectionalLight3D)node).GlobalBasis.Z;
			yaw = Mathf.RadToDeg(Mathf.Atan2(-ray.X, -ray.Z));
			pitch = -52f;
		}
		else if (Numbers(_looks[_next]) is { Length: 2 } look) (yaw, pitch) = (look[0], look[1]);
		else return;

		player.Rotation = new Vector3(0, Mathf.DegToRad(yaw), 0);
		if (player.GetNodeOrNull<Node3D>("Head") is Node3D head) head.Rotation = new Vector3(Mathf.DegToRad(pitch), 0, 0);
	}

	private static float[] Numbers(string text)
	{
		string[] parts = text.Split(',');
		var values = new float[parts.Length];
		for (int i = 0; i < parts.Length; i++)
			if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return null;
		return values;
	}
}
