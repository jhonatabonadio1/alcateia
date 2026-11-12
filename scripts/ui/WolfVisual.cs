using Godot;

/// <summary>
/// Braços e garras do lobo, presos ao corpo.
///
/// Ficam no corpo e não na câmera porque a forma lobo é em terceira
/// pessoa: o dono precisa ver os próprios braços, e os outros
/// jogadores precisam reconhecer a silhueta de longe.
///
/// Silhueta é o ponto. Numa luta rápida ninguém lê textura — o que
/// diferencia um lobo de um humano a trinta metros é o contorno.
/// Por isso os braços são largos, abertos e escuros contra o corpo.
/// </summary>
public partial class WolfVisual : Node3D
{
    [Export] public Color FurColor = new Color(0.17f, 0.15f, 0.17f);
    [Export] public Color ClawColor = new Color(0.78f, 0.76f, 0.72f);

    /// <summary>Balanço dos braços ao correr.</summary>
    [Export] public float SwingAmount = 0.35f;
    [Export] public float SwingSpeed = 8f;

    /// <summary>Abertura dos braços ao atacar.</summary>
    [Export] public float StrikeAmount = 1.1f;
    [Export] public float StrikeRecover = 6f;

    private FormController _form;
    private Weapon _weapon;
    private CharacterBody3D _body;

    private Node3D _left;
    private Node3D _right;

    private float _swing;

    /// <summary>Força do golpe de cada braço, separada: é a
    /// alternância que faz o combo parecer uma sequência de patadas em
    /// vez de um corpo pulsando.</summary>
    private float _strikeLeft;
    private float _strikeRight;

    private bool _nextIsLeft;

    public override void _Ready()
    {
        _body = GetParent<CharacterBody3D>();
        _form = _body.GetNode<FormController>("FormController");
        _weapon = _body.GetNode<Weapon>("Weapon");

        _left = BuildArm(-1);
        _right = BuildArm(1);

        _form.FormChanged += _ => Refresh();
        _weapon.Fired += OnFired;

        Refresh();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);

        if (flat.Length() > 0.5f)
            _swing += (float)delta * SwingSpeed;

        _strikeLeft = Mathf.MoveToward(_strikeLeft, 0f, StrikeRecover * (float)delta);
        _strikeRight = Mathf.MoveToward(_strikeRight, 0f, StrikeRecover * (float)delta);

        float swing = Mathf.Sin(_swing) * SwingAmount;

        // Cada braço soma o próprio golpe ao balanço, por isso o combo
        // aparece como patadas alternadas.
        _left.Rotation = new Vector3(-swing - _strikeLeft, 0, _strikeLeft * 0.4f);
        _right.Rotation = new Vector3(swing - _strikeRight, 0, -_strikeRight * 0.4f);
    }

    private void OnFired()
    {
        if (!_form.IsWolf) return;

        // Alterna a pata a cada golpe.
        if (_nextIsLeft) _strikeLeft = StrikeAmount;
        else _strikeRight = StrikeAmount;

        _nextIsLeft = !_nextIsLeft;
    }

    private void Refresh() => Visible = _form.IsWolf;

    /// <summary>Um braço: ombro, antebraço e três garras.</summary>
    private Node3D BuildArm(int side)
    {
        var shoulder = new Node3D
        {
            Position = new Vector3(side * 0.34f, 0.32f, 0)
        };
        AddChild(shoulder);

        Box(shoulder, FurColor, new Vector3(0.17f, 0.17f, 0.46f), new Vector3(0, -0.1f, -0.2f));
        Box(shoulder, FurColor, new Vector3(0.14f, 0.14f, 0.3f), new Vector3(side * 0.03f, -0.26f, -0.42f));

        for (int i = -1; i <= 1; i++)
        {
            var claw = Box(shoulder, ClawColor,
                new Vector3(0.035f, 0.035f, 0.19f),
                new Vector3(side * 0.03f + i * 0.055f, -0.3f, -0.63f));

            claw.RotationDegrees = new Vector3(-14f, i * 9f, 0);
        }

        return shoulder;
    }

    private MeshInstance3D Box(Node parent, Color color, Vector3 size, Vector3 position)
    {
        var node = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.85f },
            Position = position
        };

        parent.AddChild(node);
        return node;
    }
}
