using Godot;
using System.Collections.Generic;

/// <summary>
/// Escada de subir (de marinheiro, de degraus presos na parede). É uma peça
/// do jogo, não de um mapa: toda escada, em qualquer mapa, sobe e desce do
/// mesmo jeito (decisão do Jhon, 17/11). Nos mapas por código entra com
/// <c>m.escada(...)</c> (tools/mapas/kit.py), que põe este nó e uma chapa
/// lisa de colisão no lugar dos degraus. Num mapa montado no editor, basta
/// pôr um nó destes (e uma caixa de colisão atrás).
///
/// O nó é o volume da escada: X ao longo dos degraus, Y para cima, Z para
/// fora da parede. Quem sobe fica do lado +Z, de frente para −Z
/// (<see cref="Facing"/>). O volume vai do chão até um corpo acima do piso
/// de cima: o centro do corpo passa do topo, a escada acabou.
///
/// É um Area3D só para o servidor guardar o nó (<see cref="MatchWorld"/>
/// tira do mapa o que não colide). Ele não detecta nada: quem pergunta é
/// o corpo (<see cref="At"/>), pela posição, igual no jogo e no servidor.
/// O que o corpo faz na escada está em Player.Ladder.cs.
/// </summary>
public partial class Ladder : Area3D
{
	/// <summary>Tamanho do volume, em metros (largura, altura, fundura).</summary>
	[Export] public Vector3 Size = new(1.1f, 4.66f, 1.05f);

	private static readonly List<Ladder> _all = new();

	public override void _Ready()
	{
		Monitoring = false;
		Monitorable = false;
		CollisionLayer = 0;
		CollisionMask = 0;
		if (!_all.Contains(this)) _all.Add(this);
	}

	public override void _ExitTree() => _all.Remove(this);

	/// <summary>Para onde olha quem está na escada: para a parede.</summary>
	public Vector3 Facing => -GlobalBasis.Z.Normalized();

	/// <summary>Ao longo dos degraus, para a direita de quem sobe.</summary>
	public Vector3 Right => GlobalBasis.X.Normalized();

	public float Bottom => GlobalPosition.Y - Size.Y / 2f;
	public float Top => GlobalPosition.Y + Size.Y / 2f;

	public bool Contains(Vector3 point)
	{
		Vector3 local = GlobalTransform.AffineInverse() * point;
		return Mathf.Abs(local.X) <= Size.X / 2f && Mathf.Abs(local.Y) <= Size.Y / 2f && Mathf.Abs(local.Z) <= Size.Z / 2f;
	}

	/// <summary>A escada em que um ponto (o centro do corpo) está, ou nenhuma.</summary>
	public static Ladder At(Vector3 point)
	{
		foreach (Ladder ladder in _all)
			if (ladder.IsInsideTree() && ladder.Contains(point))
				return ladder;
		return null;
	}
}
