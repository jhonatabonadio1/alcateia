using Godot;
using System.Collections.Generic;

/// <summary>
/// Câmera de morte: quem te matou fica com a **silhueta vermelha** — só
/// para você, vista através das paredes — e a câmera vira para ele, com
/// um cartão sobre ele com a patente e o nome (decisão do Jhon). Dura até
/// você renascer.
///
/// A silhueta é um <see cref="GeometryInstance3D.MaterialOverlay"/> nas
/// malhas do matador: uma segunda passada vermelha, sem teste de
/// profundidade, mais forte na borda. Fica no seu cliente e mais nenhum,
/// porque é você quem põe e tira. A câmera é própria, fora do corpo: o
/// corpo morto não gira, e o morto não tem mouse.
/// </summary>
public partial class KillCam : Node3D
{
    /// <summary>Quanto tempo a câmera fica no próprio corpo antes de virar
    /// para o matador: o bastante para ver que morreu.</summary>
    [Export] public float Hold = 0.35f;

    /// <summary>Rapidez da virada (quanto do caminho por segundo).</summary>
    [Export] public float TurnRate = 5f;

    private Node3D _killer;
    private Node3D _victim;
    private Camera3D _camera;
    private Transform3D _from;
    private float _fov = 75f;
    private double _time;
    private double _remark;
    private readonly Dictionary<GeometryInstance3D, Material> _marked = new();

    private CanvasLayer _tagLayer;
    private Control _tag;

    public static KillCam Begin(Node parent, Node3D victim, Node3D killer, Camera3D current)
    {
        var cam = new KillCam
        {
            Name = "KillCam",
            _killer = killer,
            _victim = victim,
            _from = current?.GlobalTransform ?? killer.GlobalTransform,
            _fov = current?.Fov ?? 75f
        };
        parent.AddChild(cam);
        return cam;
    }

    public override void _Ready()
    {
        TopLevel = true;
        _camera = new Camera3D { Fov = _fov };
        AddChild(_camera);
        _camera.GlobalTransform = _from;
        _camera.MakeCurrent();
        Mark();
        BuildTag();
    }

    /// <summary>O cartão sobre o matador: patente e nome.</summary>
    private void BuildTag()
    {
        if (_killer?.GetNodeOrNull<Health>("Health") is not Health health) return;

        _tagLayer = new CanvasLayer { Layer = 8 };
        AddChild(_tagLayer);

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTokens.Bg, 0.85f), new Color(UiTokens.Danger, 0.8f), 1, 0, UiTokens.S2, UiTokens.S1));
        HBoxContainer row = UiKit.Row(UiTokens.S2);
        panel.AddChild(row);
        row.AddChild(RankBadge.Make(DuelCard.RankOf(health.PublicName, false), 26));
        VBoxContainer text = UiKit.Column(0);
        text.AddChild(UiKit.Text(health.PublicName, null, UiTokens.Danger));
        text.AddChild(UiKit.Caption(Ranks.NameOf(DuelCard.RankOf(health.PublicName, false))));
        row.AddChild(text);

        _tag = UiTheme.Apply(panel);
        _tag.Visible = false;
        _tagLayer.AddChild(_tag);
    }

    private void PlaceTag()
    {
        if (_tag == null) return;
        Vector3 head = _killer.GlobalPosition + Vector3.Up * 1.4f;
        bool shown = _time >= Hold && !_camera.IsPositionBehind(head);
        _tag.Visible = shown;
        if (!shown) return;

        Vector2 at = _camera.UnprojectPosition(head);
        _tag.Position = at - new Vector2(_tag.Size.X * 0.5f, _tag.Size.Y + 8f);
    }

    public override void _Process(double delta)
    {
        _time += delta;

        if (_killer == null || !IsInstanceValid(_killer)) return;

        // O matador pode trocar de forma enquanto você espera: o corpo
        // novo ganha a silhueta também.
        _remark -= delta;
        if (_remark <= 0) { _remark = 0.5; Mark(); }

        PlaceTag();
        if (_time < Hold) return;

        float weight = Mathf.Min(1f, (float)delta * TurnRate);
        Vector3 target = _killer.GlobalPosition + Vector3.Up * 0.6f;

        // Sobe e recua por cima do próprio corpo, do lado oposto ao
        // matador: de trás do ombro o corpo caído tapava justamente ele.
        if (_victim != null && IsInstanceValid(_victim))
        {
            Vector3 toKiller = target - _victim.GlobalPosition;
            toKiller.Y = 0;
            Vector3 away = toKiller.LengthSquared() > 0.01f ? -toKiller.Normalized() : Vector3.Zero;
            Vector3 perch = _victim.GlobalPosition + Vector3.Up * 2.2f + away * 1.4f;
            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(perch, weight);
        }

        Vector3 eye = _camera.GlobalPosition;
        if (eye.DistanceSquaredTo(target) < 0.01f) return;

        Basis look = Basis.LookingAt(target - eye, Vector3.Up);
        Quaternion turned = _camera.GlobalBasis.GetRotationQuaternion().Slerp(look.GetRotationQuaternion(), weight);
        _camera.GlobalBasis = new Basis(turned);

        // Longe, aproxima: a 70 m o matador era um ponto na tela. O quadro
        // fecha em uns 10 m em volta dele, sem passar de 15°.
        float distance = eye.DistanceTo(target);
        float fov = Mathf.Clamp(Mathf.RadToDeg(2f * Mathf.Atan(5f / Mathf.Max(distance, 0.1f))), 15f, _fov);
        _camera.Fov = Mathf.Lerp(_camera.Fov, fov, weight);
    }

    /// <summary>Volta a ser a câmera da tela, se alguém a tirou (o corpo
    /// troca de câmera ao mudar de forma).</summary>
    public void Reclaim() => _camera?.MakeCurrent();

    /// <summary>Tira a silhueta e devolve a câmera ao corpo.</summary>
    public void Finish()
    {
        foreach (var (mesh, previous) in _marked)
            if (IsInstanceValid(mesh)) mesh.MaterialOverlay = previous;
        _marked.Clear();
        QueueFree();
    }

    public override void _ExitTree()
    {
        foreach (var (mesh, previous) in _marked)
            if (IsInstanceValid(mesh)) mesh.MaterialOverlay = previous;
        _marked.Clear();
    }

    private void Mark()
    {
        if (_killer == null || !IsInstanceValid(_killer)) return;

        foreach (Node node in _killer.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            if (node is not MeshInstance3D mesh || _marked.ContainsKey(mesh)) continue;
            _marked[mesh] = mesh.MaterialOverlay;
            mesh.MaterialOverlay = Silhouette.Killer;
        }
    }
}
