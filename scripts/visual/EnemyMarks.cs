using Godot;
using System.Collections.Generic;

/// <summary>
/// Inimigo com um brilho vermelho no ombro esquerdo (decisão do Jhon,
/// 15/11: troca a borda vermelha no corpo inteiro, que deixava o boneco
/// com cara de recortado). Só no seu cliente, só no time contrário ao seu,
/// e sem atravessar parede: através de parede seria ver quem está
/// escondido.
///
/// O brilho vai num <see cref="BoneAttachment3D"/> no osso do braço
/// esquerdo de cada esqueleto do corpo (humano e lobo): segue o ombro na
/// animação, e o esqueleto escondido (o da forma que não está em uso)
/// esconde o brilho junto.
///
/// Varre os corpos de tempo em tempo: o lobo é um esqueleto que aparece
/// depois do corpo, e o time pode ser trocado entre partidas. Morto, o
/// brilho apaga na hora (a varredura só cria e tira).
/// </summary>
public partial class EnemyMarks : Node
{
	/// <summary>Nome do nó do brilho, filho do esqueleto.</summary>
	public const string MarkName = "MarcaInimigo";

	/// <summary>Grupo dos brilhos: a câmera de morte não põe a silhueta
	/// do matador neles.</summary>
	public const string MarkGroup = "enemy_mark";

	private Health _local;
	private double _timer;
	private readonly List<(Health Health, Node3D Mark)> _marks = new();

	public override void _Process(double delta)
	{
		// Morto, apaga no quadro em que morreu: meio segundo de ombro
		// brilhando no corpo caído parecia que ele ainda estava no jogo.
		for (int i = _marks.Count - 1; i >= 0; i--)
		{
			var (health, mark) = _marks[i];
			if (!IsInstanceValid(mark) || !IsInstanceValid(health)) { _marks.RemoveAt(i); continue; }
			mark.Visible = !health.IsDead;
		}

		_timer -= delta;
		if (_timer > 0) return;
		_timer = 0.5;

		if (_local == null || !IsInstanceValid(_local))
			_local = Groups.LocalPlayer(GetTree())?.GetNodeOrNull<Health>("Health");
		if (_local == null) return;

		foreach (string group in Groups.Fighters)
		{
			foreach (Node body in GetTree().GetNodesInGroup(group))
			{
				if (body.GetNodeOrNull<Health>("Health") is not Health health || health == _local) continue;
				bool enemy = health.Team != _local.Team;

				foreach (Node node in body.FindChildren("*", nameof(Skeleton3D), true, false))
				{
					if (node is not Skeleton3D skeleton) continue;
					Node3D existing = skeleton.GetNodeOrNull<Node3D>(MarkName);

					if (enemy && existing == null && ShoulderMark.Make(skeleton) is Node3D mark)
					{
						skeleton.AddChild(mark);
						_marks.Add((health, mark));
					}
					else if (!enemy && existing != null)
					{
						existing.QueueFree();
					}
				}
			}
		}
	}
}
