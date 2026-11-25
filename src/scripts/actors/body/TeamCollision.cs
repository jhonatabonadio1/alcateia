using Godot;

/// <summary>
/// Aliado não é parede: corpos do mesmo time passam um pelo outro; o
/// inimigo continua sólido. O mesmo para o jogador e para o bot.
///
/// Por quê (Jhon, 25/11: "ao apertar W W com o lobo e encostar num aliado,
/// o lobo vai pra frente e pra trás piscando e às vezes atravessa o aliado";
/// "pode afetar o ataque especial e os secundários"): o corpo do aliado
/// barrava o lobo como uma parede, e isso dava três defeitos:
///   - o aliado da rede é um boneco posto onde a máquina dele diz (com
///     atraso, e sem física): ele aparecia por cima do lobo, a física
///     tirava o lobo de dentro dele empurrando para trás, o lobo voltava a
///     correr, e assim a cada quadro — o vai e volta; quando o boneco
///     pulava para a frente, passava através;
///   - o bote para no que encosta (WolfMoveMotor.Dive), e o aliado contava;
///   - o salto e o passo da garrada batiam no aliado do caminho.
/// Encostar em aliado não tem uso no jogo (só travar corredor do próprio
/// time). Por isso os jogos de tiro em equipe costumam deixar o time
/// atravessável; o tiro já passava pelo aliado (TiroAtravessaAliado).
///
/// É uma exceção de colisão entre os dois corpos: vale para o mover
/// (MoveAndSlide, TestMove, o degrau), não para os raios e as consultas —
/// tiro, garra e visão continuam vendo o aliado e decidem por time.
/// </summary>
public static class TeamCollision
{
	/// <summary>Sem time aplicado ainda.</summary>
	public const int None = int.MinValue;

	/// <summary>
	/// Chame a cada passo de física: na primeira vez (o corpo chegou) e
	/// quando o time dele muda, refaz o par com cada lutador do mesmo mundo.
	/// Cada par fica certo sem ninguém varrer a cena a todo quadro: quem chega
	/// depois acerta o par no primeiro passo dele, e quem troca de time
	/// acerta os dele. (Um contador global de mudanças faria cada corpo de
	/// cada partida do servidor refazer tudo a cada entrada.)
	/// </summary>
	public static void Keep(CharacterBody3D body, Health health, ref int applied)
	{
		if (health == null || health.Team == applied || !body.IsInsideTree()) return;
		applied = health.Team;
		Apply(body, health.Team);
	}

	/// <summary>Do mesmo time: atravessam. De times diferentes: batem.</summary>
	public static void Apply(CharacterBody3D body, int team)
	{
		World3D world = body.GetWorld3D();
		// Os corpos do servidor de partida também (Groups.ServerBodies): sem
		// eles, no servidor o aliado era parede e no jogo não, e a correção
		// puxava o lobo que atravessava um aliado.
		foreach (string group in Groups.Bodies)
		{
			foreach (Node node in body.GetTree().GetNodesInGroup(group))
			{
				if (node == body || node is not CharacterBody3D other || other.GetWorld3D() != world) continue;
				if (other.GetNodeOrNull<Health>("Health") is not Health health) continue;
				if (health.Team == team)
				{
					body.AddCollisionExceptionWith(other);
					other.AddCollisionExceptionWith(body);
				}
				else
				{
					body.RemoveCollisionExceptionWith(other);
					other.RemoveCollisionExceptionWith(body);
				}

				// A câmera do lobo não recua por gente, de time nenhum
				// (Player.IgnoreForCamera). Aqui porque é onde cada par se
				// encontra: o jogador fica sabendo de quem chega depois dele.
				(body as Player)?.IgnoreForCamera(other);
				(other as Player)?.IgnoreForCamera(body);
			}
		}
	}
}
