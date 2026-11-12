using Godot;

/// <summary>
/// A arma na mão do corpo em terceira pessoa — a que os outros veem.
///
/// O modelo vem de <see cref="WeaponModel"/> e troca junto com o slot:
/// quem está de AK aparece de AK, quem puxou a faca aparece de faca. O
/// lobo não segura nada (o corpo humano inteiro some).
///
/// **Como a arma fica na mão sem calibrar osso por osso.** As animações
/// são de fuzil: a mão direita no punho, a esquerda embaixo do cano. A
/// arma vai na mão direita apontando para a esquerda — a linha entre as
/// duas mãos é o eixo do cano. Isso funciona com qualquer clipe e
/// qualquer esqueleto, sem um número de rotação mágico por osso.
///
/// Na recarga a mão esquerda sai para buscar o carregador, e apontar
/// para ela viraria a arma para baixo. Então, enquanto a arma está bem
/// segura, guarda-se onde ela fica *em relação à mão direita*; na
/// recarga ela só acompanha a mão direita com essa relação congelada.
/// </summary>
public partial class HeldWeapon : Node3D
{
    /// <summary>Do pulso ao centro da palma, ao longo do cano.</summary>
    [Export] public float GripForward = 0.06f;

    /// <summary>Quanto o punho fica abaixo do pulso.</summary>
    [Export] public float GripDown = 0.03f;

    private Skeleton3D _skeleton;
    private int _rightHand = -1;
    private int _leftHand = -1;
    private Weapon _weapon;
    private FormController _form;
    private Health _health;
    private CharacterBody3D _body;
    private uint _layers = 1;

    private Node3D _model;
    private Node3D _magazine;
    private string _id = "";
    private Transform3D _inHand = Transform3D.Identity;
    private bool _calibrated;

    /// <summary>Liga a arma ao esqueleto do modelo. Chamado pelo
    /// <see cref="CharacterAnimator"/> depois que o modelo carrega.</summary>
    public void Attach(Node3D model, CharacterBody3D body, uint layers)
    {
        _skeleton = FindSkeleton(model);
        _body = body;
        _layers = layers;
        _weapon = body.GetNodeOrNull<Weapon>("Weapon");
        _form = body.GetNodeOrNull<FormController>("FormController");
        _health = body.GetNodeOrNull<Health>("Health");

        if (_skeleton == null || _weapon == null) return;

        for (int i = 0; i < _skeleton.GetBoneCount(); i++)
        {
            string bone = _skeleton.GetBoneName(i);
            if (bone.EndsWith("RightHand")) _rightHand = i;
            else if (bone.EndsWith("LeftHand")) _leftHand = i;
        }

        if (_rightHand < 0)
        {
            GD.PrintErr("HeldWeapon: esqueleto sem mão direita.");
            return;
        }

        _weapon.SlotChanged += _ => Rebuild();
        _weapon.PickedUp += _ => Rebuild();
        _weapon.Dropped += _ => Rebuild();
        if (_form != null) _form.FormChanged += _ => Rebuild();
        if (_health != null)
        {
            _health.Died += _ => Rebuild();
            _health.Respawned += Rebuild;
        }

        // Depois da árvore de animação, para pegar a pose deste quadro.
        ProcessPriority = 100;
        Rebuild();
    }

    private string WantedId()
    {
        if (_weapon == null || (_form != null && _form.IsWolf) || (_health != null && _health.IsDead))
            return "";

        return _weapon.HumanSlot switch
        {
            Slot.Primary => _weapon.PrimaryDef?.Id ?? "",
            Slot.Secondary => _weapon.SecondaryDef?.Id ?? "",
            Slot.Knife => "faca",
            Slot.Grenade => "choque",
            _ => ""
        };
    }

    private void Rebuild()
    {
        string id = WantedId();
        if (id == _id && (_model != null || id == "")) return;

        _model?.QueueFree();
        _model = null;
        _magazine = null;
        _id = id;
        _calibrated = false;

        if (id == "") return;

        _model = WeaponModel.Build(id);
        _model.TopLevel = true;
        AddChild(_model);
        WeaponModel.SetLayers(_model, _layers);
        _magazine = _model.GetNodeOrNull<Node3D>(WeaponModel.MagazineName);
    }

    public override void _Process(double delta)
    {
        if (_model == null || _skeleton == null || !IsVisibleInTree()) return;

        Transform3D skeleton = _skeleton.GlobalTransform;
        Transform3D hand = skeleton * _skeleton.GetBoneGlobalPose(_rightHand);
        Vector3 right = hand.Origin;

        bool reloading = _weapon.IsReloading;
        bool aimed = !reloading && _leftHand >= 0 && _id != "faca" && _id != "choque";

        Transform3D placed;

        if (aimed)
        {
            Vector3 left = (skeleton * _skeleton.GetBoneGlobalPose(_leftHand)).Origin;
            Vector3 axis = left - right;

            // Mãos juntas demais não dão direção; fica a frente do corpo.
            Vector3 forward = axis.Length() > 0.1f ? axis.Normalized() : -_body.GlobalTransform.Basis.Z;
            placed = Grip(right, forward);

            _inHand = hand.AffineInverse() * placed;
            _calibrated = true;
        }
        else if (_calibrated)
        {
            placed = hand * _inHand;
            placed.Basis = placed.Basis.Orthonormalized();
        }
        else
        {
            placed = Grip(right, -_body.GlobalTransform.Basis.Z);
        }

        _model.GlobalTransform = placed;

        // O carregador some no meio da recarga, como na primeira pessoa.
        if (_magazine != null)
        {
            float p = _weapon.ReloadProgress;
            _magazine.Visible = !(reloading && p > 0.3f && p < 0.65f);
        }
    }

    /// <summary>Arma com o punho na mão e o cano em
    /// <paramref name="forward"/>.</summary>
    private Transform3D Grip(Vector3 wrist, Vector3 forward)
    {
        Vector3 up = Vector3.Up;
        if (Mathf.Abs(forward.Dot(up)) > 0.95f) up = _body.GlobalTransform.Basis.Z;

        Basis basis = Basis.LookingAt(forward, up);
        Vector3 origin = wrist + forward * GripForward - basis.Y * GripDown;
        return new Transform3D(basis, origin);
    }

    private static Skeleton3D FindSkeleton(Node node)
    {
        if (node is Skeleton3D skeleton) return skeleton;
        foreach (Node child in node.GetChildren())
        {
            Skeleton3D found = FindSkeleton(child);
            if (found != null) return found;
        }
        return null;
    }
}
