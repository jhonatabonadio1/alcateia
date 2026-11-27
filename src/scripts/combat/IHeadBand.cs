/// <summary>
/// Um corpo sem modelo de ossos (o robô de treino) que diz onde a cabeça
/// dele começa. Sem isto, a cabeça de quem leva tiro na caixa de colisão é
/// a faixa de cima dela (<c>Weapon.HeadZone</c>, pensada para a cápsula de
/// 1,75 m do humano) — no robô, só o alto das aletas.
/// </summary>
public interface IHeadBand
{
	/// <summary>Metros acima do meio do corpo a partir dos quais o acerto é na cabeça.</summary>
	float HeadFrom { get; }
}
