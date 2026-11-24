using Godot;

/// <summary>
/// A mira animada dos braços (pedido do Jhon, 18/11: "crie a animação da
/// AR-15 escopada, ou seja, do personagem mirando com ela; deixe natural e
/// implemente no jogo").
///
/// O conjunto que tem os clipes de mira (o AR-15; feitos por
/// <c>tools/armas/mira.py</c>) leva a arma ao olho quando o jogador mira
/// (botão direito): a alça e a massa ficam em linha no centro da tela, e
/// quem mira é a arma, não um desenho por cima.
///
/// - <c>mirar</c> ao apertar e <c>desmirar</c> ao soltar. Soltou (ou
///   apertou) no meio do caminho: o outro clipe começa de onde a arma
///   está, não do começo;
/// - <c>mira</c> parado e <c>mira_andar</c> andando, em laço;
/// - <c>mira_tiro</c> a cada tiro na mira.
///
/// Recarregar, guardar e sacar tiram da mira sem animação própria: o
/// clipe deles entra por cima.
///
/// Com a arma mudando de lugar, a boca do cano (o clarão, o traçante, a
/// cápsula) segue o osso da arma — a ficha diz qual e onde
/// (<c>cano_osso</c>).
///
/// Conjunto sem esses clipes (as outras armas) mira como antes: a arma
/// fica na cintura e a mira circular aparece (<see cref="Crosshair"/>).
/// </summary>
public partial class ViewModel
{
	private bool _rigAiming;

	/// <summary>A arma está (ou está indo) na mira animada. Para os testes.</summary>
	public bool RigAiming => _rigAiming;

	/// <summary>Esta arma mira pela própria mira (tem os clipes)? É o que
	/// a cruz pergunta para não desenhar a mira circular por cima.</summary>
	public static bool HasSights(string weaponId)
	{
		if (string.IsNullOrEmpty(weaponId)) return false;
		Godot.Collections.Dictionary rig = RigFor(weaponId);
		if (rig == null) return false;
		Godot.Collections.Dictionary clips = rig["animacoes"].AsGodotDictionary();
		return clips.ContainsKey("mira") && clips.ContainsKey("mirar");
	}

	private static bool IsAimClip(string name) => name is "mirar" or "desmirar" or "mira" or "mira_andar" or "mira_tiro";

	/// <summary>Entra na mira ou sai dela quando o jogador pede. Verdadeiro
	/// se começou uma transição agora (o resto do quadro espera).</summary>
	private bool UpdateRigAim()
	{
		if (!_rigClips.ContainsKey("mirar") || !_rigClips.ContainsKey("mira")) return false;
		// Guardando ou sacando: a mira espera.
		if (_holster > 0f || (_rigPlaying == "sacar" && _rigBusy > 0)) return false;

		bool want = _weapon.WantsAim;
		if (want == _rigAiming) return false;
		_rigAiming = want;

		string name = want ? "mirar" : "desmirar";
		if (!_rigClips.TryGetValue(name, out Clip clip)) return false;

		// No meio da outra transição: começa de onde a arma está. As duas
		// andam quase tudo no primeiro terço, daí o quadrado.
		double skip = 0;
		string other = want ? "desmirar" : "mirar";
		if (_rigPlaying == other && _rigPlayer.IsPlaying() && _rigClips.TryGetValue(other, out Clip running))
		{
			double done = Mathf.Clamp((_rigPlayer.CurrentAnimationPosition - running.From) / running.Length, 0, 1);
			skip = (1 - done) * (1 - done) * clip.Length * 0.6;
		}

		PlayRig(name, 0, skip > 0 ? 0.1 : 0.06);
		if (skip > 0) _rigPlayer.Seek(clip.From + skip, true);
		_rigBusy = clip.Length - skip;
		return true;
	}

	// ------------------------------------------------------------ a boca do cano

	private Skeleton3D _muzzleSkeleton;
	private Node3D _muzzleScene;
	private int _muzzleBone = -1;
	private Vector3 _muzzlePoint, _ejectDelta;
	private Node3D _muzzleMarker, _ejectMarker;

	/// <summary>Prende a boca do cano ao osso da arma, se a ficha diz qual.</summary>
	private void AttachMuzzle(Godot.Collections.Dictionary rig, Node3D scene)
	{
		_muzzleBone = -1;
		if (!rig.ContainsKey("cano_osso")) return;
		Godot.Collections.Dictionary where = rig["cano_osso"].AsGodotDictionary();
		var skeletons = scene.FindChildren("*", "Skeleton3D", true, false);
		if (skeletons.Count == 0) return;

		_muzzleSkeleton = (Skeleton3D)skeletons[0];
		_muzzleBone = _muzzleSkeleton.FindBone(where["osso"].AsString());
		if (_muzzleBone < 0) return;

		_muzzleScene = scene;
		_muzzlePoint = ToVector(where["ponto"]);
		_muzzleMarker = _held.GetNodeOrNull<Node3D>(WeaponModel.MuzzleName);
		_ejectMarker = _held.GetNodeOrNull<Node3D>(WeaponModel.EjectName);
		_ejectDelta = _ejectMarker != null && _muzzleMarker != null ? _ejectMarker.Position - _muzzleMarker.Position : Vector3.Zero;
	}

	private void ForgetMuzzle()
	{
		_muzzleBone = -1;
		_muzzleSkeleton = null;
		_muzzleScene = null;
		_muzzleMarker = _ejectMarker = null;
		_rigAiming = false;
	}

	/// <summary>A boca do cano onde a animação pôs a arma agora.</summary>
	private void FollowMuzzle()
	{
		if (_muzzleBone < 0 || _muzzleMarker == null || !IsInstanceValid(_muzzleSkeleton)) return;

		Transform3D bone = _muzzleScene.Transform * RigPieces.Relative(_muzzleScene, _muzzleSkeleton)
			* _muzzleSkeleton.GetBoneGlobalPose(_muzzleBone);
		_muzzleMarker.Position = bone * _muzzlePoint;
		if (_ejectMarker != null) _ejectMarker.Position = _muzzleMarker.Position + _ejectDelta;
	}
}
