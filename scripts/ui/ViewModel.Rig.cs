using Godot;
using System.Collections.Generic;

/// <summary>
/// Os braços animados: para as armas de fogo, o que o jogador vê é um
/// conjunto pronto — dois braços, a arma e as animações de um animador
/// (parado, sacar, tiro, recarga) —, convertido por
/// <c>tools/armas/blender/bracos.py</c> para <c>assets/armas/fp/</c>.
///
/// A ficha <c>bracos.json</c> diz que arma usa que conjunto e que trecho
/// de que clipe é cada animação. O conjunto já vem com o olho na origem:
/// entra na câmera sem ajuste.
///
/// Aqui só se escolhe o que tocar, seguindo a arma:
/// - **tiro**: o clipe do disparo, do começo, a cada tiro;
/// - **recarga**: o clipe tocado na velocidade que o faz terminar junto
///   com a recarga de verdade (<see cref="Weapon.ReloadDuration"/>) — o
///   de pente vazio quando o pente esvaziou; na escopeta, um ciclo por
///   cartucho;
/// - **sacar** ao trocar de arma, **andar** ao andar, **parado** no resto.
///
/// Faca e granada não têm conjunto: continuam com as mãos de
/// <see cref="WeaponModel"/> (ViewModel.Hands).
/// </summary>
public partial class ViewModel
{
	public const string RigSheetPath = "res://assets/armas/fp/bracos.json";

	/// <summary>Um trecho de um clipe: a animação "recarga" do M16 é do
	/// segundo 2,3 ao 4,5 da linha do tempo única dele.</summary>
	private readonly record struct Clip(string Name, double From, double To)
	{
		public double Length => To - From;
	}

	/// <summary>Com os braços animados, o coice de código soma com o da
	/// animação: fica só uma fração.</summary>
	private const float RigKick = 0.3f;

	private static Godot.Collections.Dictionary _rigSheet;

	private AnimationPlayer _rigPlayer;
	private readonly Dictionary<string, Clip> _rigClips = new();
	private string _rigPlaying = "";
	private double _rigBusy;
	private float _rigLastReload;

	/// <summary>Os braços animados estão em uso (arma de fogo com conjunto).</summary>
	public bool HasRig => _rigPlayer != null;

	/// <summary>A animação dos braços que está tocando ("parado", "tiro"...).</summary>
	public string RigAnimation => _rigPlaying;

	private static Godot.Collections.Dictionary RigSheet()
	{
		if (_rigSheet != null) return _rigSheet;
		_rigSheet = ResourceLoader.Exists(RigSheetPath)
			? GD.Load<Json>(RigSheetPath).Data.AsGodotDictionary()
			: new Godot.Collections.Dictionary();
		return _rigSheet;
	}

	/// <summary>A entrada da ficha para esta arma, ou null se ela não tem conjunto.</summary>
	private static Godot.Collections.Dictionary RigFor(string weaponId)
	{
		foreach (Variant entry in RigSheet().Values)
		{
			Godot.Collections.Dictionary rig = entry.AsGodotDictionary();
			if (rig["armas"].AsGodotArray().Contains(weaponId) && ResourceLoader.Exists(rig["arquivo"].AsString())) return rig;
		}
		return null;
	}

	private void ForgetRig()
	{
		_rigPlayer = null;
		_rigClips.Clear();
		_rigPlaying = "";
		_rigBusy = 0;
	}

	/// <summary>Monta os braços animados desta arma. Falso se ela não tem
	/// conjunto — aí valem as mãos de sempre.</summary>
	private bool BuildRig(string id)
	{
		Godot.Collections.Dictionary rig = RigFor(id);
		if (rig == null) return false;

		var scene = GD.Load<PackedScene>(rig["arquivo"].AsString()).Instantiate<Node3D>();
		_held = new Node3D { Name = id };
		_held.AddChild(scene);
		AddChild(_held);

		foreach (Node node in scene.FindChildren("*", "MeshInstance3D", true, false))
			((MeshInstance3D)node).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

		// A boca do cano e, um pouco atrás, a janela por onde sai a cápsula.
		Vector3 muzzle = ToVector(rig["cano"]);
		_held.AddChild(new Marker3D { Name = WeaponModel.MuzzleName, Position = muzzle });
		_held.AddChild(new Marker3D
		{
			Name = WeaponModel.EjectName,
			Position = new Vector3(muzzle.X + 0.02f, muzzle.Y + 0.03f, muzzle.Z + Mathf.Min(0.55f, -muzzle.Z * 0.5f))
		});

		_rigPlayer = scene.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		foreach (var (name, value) in rig["animacoes"].AsGodotDictionary())
		{
			Godot.Collections.Dictionary a = value.AsGodotDictionary();
			string clip = a["clipe"].AsString();
			if (_rigPlayer != null && _rigPlayer.HasAnimation(clip))
				_rigClips[name.AsString()] = new Clip(clip, a["de"].AsDouble(), a["ate"].AsDouble());
		}

		if (_rigPlayer == null || !_rigClips.ContainsKey("parado"))
		{
			GD.PushWarning($"ViewModel: o conjunto de {id} não tem animação de parado.");
			RemoveChild(_held);
			_held.QueueFree();
			_held = null;
			ForgetRig();
			return false;
		}

		PlayRig(_rigClips.ContainsKey("sacar") ? "sacar" : "parado", 0);
		if (_rigPlaying == "sacar") _rigBusy = _rigClips["sacar"].Length;
		return true;
	}

	private static Vector3 ToVector(Variant value)
	{
		var list = value.AsGodotArray();
		return new Vector3(list[0].AsSingle(), list[1].AsSingle(), list[2].AsSingle());
	}

	/// <summary>Toca uma animação do conjunto, do começo, em <paramref name="seconds"/>
	/// segundos (0 = no tempo dela).</summary>
	private void PlayRig(string name, double seconds, double blend = 0.08)
	{
		if (!_rigClips.TryGetValue(name, out Clip clip)) return;

		float speed = seconds > 0.01 ? (float)(clip.Length / seconds) : 1f;
		// Trecho do mesmo clipe não se mistura consigo mesmo: troca seca.
		bool sameClip = _rigPlayer.CurrentAnimation == clip.Name;
		_rigPlayer.PlaySection(clip.Name, clip.From, clip.To, sameClip ? 0 : blend, speed);
		_rigPlaying = name;
	}

	private void RigFired()
	{
		if (!HasRig || !_rigClips.TryGetValue("tiro", out Clip shot)) return;

		PlayRig("tiro", 0, 0.02);
		_rigBusy = shot.Length;
	}

	private void UpdateRig(float dt)
	{
		_rigBusy -= dt;
		bool reloading = _weapon.IsReloading && _weapon.Held != null;

		if (reloading)
		{
			float p = _weapon.ReloadProgress;
			bool shells = _weapon.Held.ShellByShell;
			string name = shells && _rigClips.ContainsKey("recarga_um") ? "recarga_um"
				: _emptyReload && _rigClips.ContainsKey("recarga_vazia") ? "recarga_vazia"
				: "recarga";

			// Começou a recarga — ou, na escopeta, o cartucho seguinte.
			if (_rigPlaying != name || p < _rigLastReload - 0.2f)
				PlayRig(name, _weapon.ReloadDuration * (1f - p));
			_rigLastReload = p;
			_rigBusy = 0;
			return;
		}

		_rigLastReload = 0f;
		if (_rigBusy > 0 && _rigPlayer.IsPlaying()) return;

		var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		string rest = _body.IsOnFloor() && flat.Length() > 0.5f && _rigClips.ContainsKey("andar") ? "andar" : "parado";
		if (_rigPlaying != rest || !_rigPlayer.IsPlaying()) PlayRig(rest, 0, 0.15);
	}
}
