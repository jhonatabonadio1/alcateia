using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum ItemKind
{
	Weapon,
	Utility,
	Tattoo,
	Character,
	Service,
	/// <summary>Foto de perfil (<see cref="Avatars"/>): só a aparência, vale para sempre.</summary>
	Photo
}

/// <summary>As durações da loja. O preço por dia cai com a duração — o
/// desconto por volume precisa ser visível.</summary>
public enum Duration
{
	Day1,
	Day7,
	Day30,
	Day90,
	Permanent
}

/// <summary>Efeito de uma melhoria: multiplicadores sobre a classe de
/// lobo. Pequenos de propósito — ajuste fino, não poder.</summary>
public sealed class TattooEffect
{
	public float Speed = 1f;
	public float Jump = 1f;
	public float Shield = 1f;
	public float ClawDamage = 1f;
	public float ShieldDecay = 1f;
}

public sealed class CatalogItem
{
	public string Id;

	// O nome, o tipo e a descrição saem no idioma do jogo (assets/i18n,
	// pedido do Jhon, 21/11): o que está aqui é a chave, em português.
	private string _name, _type, _description;

	public string Name { get => Loc.T(_name); set => _name = value; }
	/// <summary>Tipo por extenso no cartão: "Fuzil de assalto".</summary>
	public string Type { get => Loc.T(_type); set => _type = value; }
	public string Description { get => Loc.T(_description); set => _description = value; }
	public ItemKind Kind;
	/// <summary>0 comum … 4 lendário.</summary>
	public int Rarity;
	/// <summary>Patente mínima para comprar ou usar.</summary>
	public int MinRank;
	/// <summary>Preço em Presas por duração, na ordem de <see cref="Duration"/>.
	/// −1 = não vendido nessa duração. Nulo = não está à venda.</summary>
	public int[] Prices;
	/// <summary>Vem com todo perfil, permanente e grátis.</summary>
	public bool Starter;
	/// <summary>Pode ir no conjunto padrão mesmo custando WP (decisão do
	/// Jhon, 18/11: "algumas armas podem ser equipadas no padrão, outras
	/// não; isso deve ser configurável por arma"). No padrão ela não
	/// custa WP. Item gratuito sempre pode.</summary>
	public bool DefaultOk;
	/// <summary>Vai para a aba DESTAQUE da loja.</summary>
	public bool Hot;
	/// <summary>Consumível: some ao usar (troca de nome).</summary>
	public bool Consumable;
	public TattooEffect Tattoo;
	/// <summary>Personagem: a pasta dele em assets/personagens.</summary>
	public CharacterSkin Character;
	/// <summary>Time em que o personagem pode ser usado (0 = os dois).</summary>
	public int Team;

	public bool ForSale => Prices != null && Prices.Any(p => p >= 0);

	public WeaponDefinition Weapon => Kind == ItemKind.Weapon ? Arsenal.Get(Id) : null;

	/// <summary>Menor preço à venda, para "a partir de" no cartão.</summary>
	public int FromPrice => ForSale ? Prices.Where(p => p >= 0).Min() : 0;
}

/// <summary>
/// Tudo o que existe para possuir: armas, granada, melhorias, classes de
/// lobo, personagens e serviços. Os personagens não estão escritos aqui:
/// cada pasta de assets/personagens vira um item (CharacterSkin). É a
/// fonte única de nome, raridade, patente mínima e preço — loja,
/// inventário e editor de conjuntos leem daqui.
///
/// Regra do guia que vale ouro: tudo que afeta o combate se compra com
/// Presas, a moeda ganha jogando. Sangue (a moeda paga) nunca compra
/// poder. Por isso nenhum item daqui tem preço em Sangue.
///
/// Os números de combate das armas continuam em resources/weapons/*.tres;
/// aqui fica só o que é de economia e de vitrine.
/// </summary>
public static class Catalog
{
	public static readonly string[] DurationLabels = { "1 dia", "7 dias", "30 dias", "90 dias", "Permanente" };
	public static readonly string[] DurationShort = { "1d", "7d", "30d", "90d", "Perm" };
	private static readonly int[] DurationDays = { 1, 7, 30, 90, 0 };

	public static int Days(Duration d) => DurationDays[(int)d];

	private static readonly List<CatalogItem> _items = WithCharacters(new()
	{
		// ---------------------------------------------------- armas
		// Desde 19/11 só a família AR-15 (com as skins), a pistola, a faca e a
		// granada: as outras armas saíram a pedido do Jhon.
		// Incomum desde 19/11: o conjunto padrão só aceita arma comum, e o AR-15
		// não pode ir nele (pedido do Jhon).
		new() { Id = "ar15", Name = "AR-15", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 1, Starter = true,
			Description = "Preciso no tiro a tiro, previsível na rajada. Não é o melhor em nada e não é ruim em nada." },
		new() { Id = "deagle", Name = "Desert Eagle", Type = "Pistola", Kind = ItemKind.Weapon,
			Rarity = 0, Starter = true,
			Description = "A secundária de todo conjunto. Sete tiros que pesam: dois no corpo resolvem, mas o coice cobra cada um." },
		// As armas de 19/11 (os modelos que o Jhon trouxe: M4A4, AK-74, SCAR-H,
		// SVD; tools/armas/reais.py). A M4A4 vem com o perfil; a AK-74 e a SVD
		// são incomuns e a SCAR-H é épica — essas três se compram (Jhon, 19/11).
		new() { Id = "m4a4", Name = "M4A4", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 0, Starter = true,
			Description = "O fuzil padrão. Carabina rápida e fácil de segurar: um pouco mais de cadência que o AR-15, um pouco menos de alcance." },
		new() { Id = "ak74", Name = "AK-74", Type = "Fuzil de impacto", Kind = ItemKind.Weapon,
			Rarity = 1, MinRank = 1, Prices = new[] { 200, 1000, 3000, 6600, 15500 },
			Description = "Três tiros no corpo. Feita para rajadas de três a cinco: depois disso o cano vai para onde quer." },
		new() { Id = "scar", Name = "SCAR-H", Type = "Arma pesada", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 350, 1700, 5200, 11500, 28000 },
			Description = "Fuzil de batalha 7,62: pente de 20, tiro lento e pesado. Segura um corredor de longe; de perto, perde para qualquer fuzil." },
		new() { Id = "svd", Name = "SVD Dragunov", Type = "Fuzil de precisão", Kind = ItemKind.Weapon,
			Rarity = 1, MinRank = 1, Prices = new[] { 200, 1000, 3000, 6600, 15500 },
			Description = "Semiautomático com luneta. Dois no corpo ou um na cabeça; fora da mira, a bala vai para qualquer lado." },
		new() { Id = Arsenal.KnifeId, Name = "Faca", Type = "Corpo a corpo", Kind = ItemKind.Weapon,
			Rarity = 0, Starter = true,
			Description = "Silenciosa: não faz ruído para os bots. A arma de quem quer o abate sem entregar a posição." },
		// O AR-15 um degrau acima (18/11): mesmo fuzil, mesmas animações, um pouco
		// mais forte em tudo. Pele roxa com flames (tools/armas/blender/ar15_ultimate.py).
		new() { Id = "ar15_ultimate", Name = "AR-15 Ultimate", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 350, 1700, 5200, 11500, 28000 }, Hot = true,
			Description = "O AR-15 no limite: um pouco mais de dano, cadência e pente, e o recuo mais domado. Pele roxa com flames que brilham." },
		// As skins de 19/11 ("crie as outras armas, épicas e lendárias"; à
		// tarde: "uma toda dourada, outra diamante, outra 8 bit"): o mesmo
		// fuzil e as mesmas animações, cada uma com um jeito (dano, cadência,
		// precisão, pente) e uma pintura (tools/armas/blender/ar15_ultimate.py --tema).
		new() { Id = "ar15_8bit", Name = "AR-15 8 Bit", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 350, 1700, 5200, 11500, 28000 },
			Description = "Cadência mais alta e tiro mais junto, com um pouco menos de dano. Pintado em pixel, como nos jogos de antigamente." },
		new() { Id = "ar15_lava", Name = "AR-15 Lava", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 350, 1700, 5200, 11500, 28000 },
			Description = "Um pouco mais de dano por tiro, um pouco menos de cadência. Rocha escura com lava acesa nas rachaduras." },
		new() { Id = "ar15_ouro", Name = "AR-15 Ouro", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 4, MinRank = 5, Prices = new[] { 450, 2200, 6600, 14500, 35000 }, Hot = true,
			Description = "Lendária. O mais forte por tiro do arsenal, pente de 40, recarga rápida e o recuo mais domado. Ouro inteiro, polido e gravado." },
		new() { Id = "ar15_diamante", Name = "AR-15 Diamante", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 4, MinRank = 5, Prices = new[] { 450, 2200, 6600, 14500, 35000 }, Hot = true,
			Description = "Lendária. O tiro mais preciso e a maior cadência do arsenal, pente de 42 e recarga rápida. Facetas de cristal que brilham." },

		// As versões épicas e lendárias das armas novas (19/11: "crie as armas lendárias e épicas (com variação de
		// texturas)"): uma épica e uma lendária de cada, com pintura própria (tools/armas/peles.py) e um pouco mais forte.
		new() { Id = "m4a4_neon", Name = "M4A4 Neon", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 350, 1700, 5200, 11500, 28000 },
			Description = "Épica. A M4A4 mais rápida e mais junta no tiro, com pente de 32. Grafite com trilhas de circuito que acendem em ciano." },
		new() { Id = "m4a4_dragao", Name = "M4A4 Dragão", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 4, MinRank = 5, Prices = new[] { 450, 2200, 6600, 14500, 35000 }, Hot = true,
			Description = "Lendária. Mais dano por tiro, pente de 36, recarga rápida e recuo domado. Escamas vermelhas de borda dourada, com brasa entre elas." },
		new() { Id = "ak74_tigre", Name = "AK-74 Tigre", Type = "Fuzil de impacto", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 350, 1700, 5200, 11500, 28000 },
			Description = "Épica. Um pouco mais de dano e o coice mais fácil de segurar na rajada. Laranja com listras de tigre." },
		new() { Id = "ak74_imperial", Name = "AK-74 Imperial", Type = "Fuzil de impacto", Kind = ItemKind.Weapon,
			Rarity = 4, MinRank = 5, Prices = new[] { 450, 2200, 6600, 14500, 35000 }, Hot = true,
			Description = "Lendária. A AK que bate mais forte e mais rápido, pente de 35 e o coice mais domado. Laca preta com filigrana de ouro." },
		new() { Id = "scar_artico", Name = "SCAR-H Ártico", Type = "Arma pesada", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 350, 1700, 5200, 11500, 28000 },
			Description = "Épica. Recuo menor, tiro mais junto e pente de 22. Camuflagem digital de neve." },
		new() { Id = "scar_galaxia", Name = "SCAR-H Galáxia", Type = "Arma pesada", Kind = ItemKind.Weapon,
			Rarity = 4, MinRank = 5, Prices = new[] { 450, 2200, 6600, 14500, 35000 }, Hot = true,
			Description = "Lendária. Mais dano, mais cadência, pente de 25 e recarga rápida. Uma nebulosa roxa e ciano, com estrelas que brilham." },
		new() { Id = "svd_carbono", Name = "SVD Carbono", Type = "Fuzil de precisão", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 350, 1700, 5200, 11500, 28000 },
			Description = "Épica. Segundo tiro mais cedo, recarga e saque mais rápidos. Fibra de carbono com detalhes vermelhos." },
		new() { Id = "svd_tempestade", Name = "SVD Tempestade", Type = "Fuzil de precisão", Kind = ItemKind.Weapon,
			Rarity = 4, MinRank = 5, Prices = new[] { 450, 2200, 6600, 14500, 35000 }, Hot = true,
			Description = "Lendária. Mais dano, segundo tiro mais cedo, pente de 12 e recarga rápida. Nuvens de tempestade cortadas por raios acesos." },
		new() { Id = "deagle_cobra", Name = "Desert Eagle Cobra", Type = "Pistola", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 220, 1100, 3300, 7300, 17000 },
			Description = "Épica. O coice cobra menos e o tiro seguinte sai mais cedo. Escamas de cobra verde-esmeralda." },
		new() { Id = "deagle_fenix", Name = "Desert Eagle Fênix", Type = "Pistola", Kind = ItemKind.Weapon,
			Rarity = 4, MinRank = 5, Prices = new[] { 280, 1400, 4200, 9300, 22000 }, Hot = true,
			Description = "Lendária. Mais dano, oito tiros no pente e recarga rápida. Cromo preto com chamas acesas que lambem o cano." },
		new() { Id = "faca_damasco", Name = "Faca Damasco", Type = "Corpo a corpo", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 180, 900, 2700, 6000, 14000 },
			Description = "Épica. Um pouco mais de dano e golpe um pouco mais rápido. Lâmina de aço damasco em ondas, cabo de madeira e cobre." },
		new() { Id = "faca_obsidiana", Name = "Faca Obsidiana", Type = "Corpo a corpo", Kind = ItemKind.Weapon,
			Rarity = 4, MinRank = 5, Prices = new[] { 240, 1200, 3600, 8000, 19000 }, Hot = true,
			Description = "Lendária. A faca que mais fere, a mais rápida e com um palmo a mais de alcance. Vidro vulcânico preto com veios roxos acesos." },

		// ---------------------------------------------------- utilitário
		new() { Id = Arsenal.GrenadeId, Name = "Granada", Type = "Granada", Kind = ItemKind.Utility,
			Rarity = 0, Starter = true, DefaultOk = true,
			Description = "Explosiva: mata de perto e fere até a borda do raio. Cai onde a mira aponta. Todo mundo tem, e já vem no conjunto padrão." },
		// As versões da granada (20/11): a mesma granada, com outra pintura — nada muda no dano nem no raio. Qualquer uma
		// pode ir no conjunto padrão (pedido do Jhon: "granada pode ser equipada qualquer uma no padrão"): DefaultOk.
		new() { Id = "choque_carbono", Name = "Granada Carbono", Type = "Granada", Kind = ItemKind.Utility,
			Rarity = 2, MinRank = 2, Prices = new[] { 60, 300, 900, 2000, 4800 }, DefaultOk = true,
			Description = "Rara. A granada explosiva em fibra de carbono sob verniz, com faixa e alavanca vermelhas. Só a aparência muda." },
		new() { Id = "choque_neon", Name = "Granada Neon", Type = "Granada", Kind = ItemKind.Utility,
			Rarity = 3, MinRank = 3, Prices = new[] { 90, 450, 1350, 3000, 7000 }, DefaultOk = true,
			Description = "Épica. Placas pretas com as juntas e a faixa do meio acesas em ciano. Só a aparência muda." },
		new() { Id = "choque_magma", Name = "Granada Magma", Type = "Granada", Kind = ItemKind.Utility,
			Rarity = 3, MinRank = 3, Prices = new[] { 90, 450, 1350, 3000, 7000 }, DefaultOk = true,
			Description = "Épica. Basalto com a lava acesa nas rachaduras e ferragem de ferro queimado. Só a aparência muda." },
		new() { Id = "choque_ouro", Name = "Granada Ouro", Type = "Granada", Kind = ItemKind.Utility,
			Rarity = 4, MinRank = 5, Prices = new[] { 120, 600, 1800, 4000, 9500 }, Hot = true, DefaultOk = true,
			Description = "Lendária. Ouro polido com treliça gravada, espoleta de ônix e alavanca de ouro branco. Só a aparência muda." },

		// ---------------------------------------------------- melhorias
		new() { Id = "tat_pata", Name = "Pata Leve", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 1, MinRank = 1, Prices = new[] { 120, 600, 1800, 4000, 9500 },
			Tattoo = new TattooEffect { Speed = 1.05f },
			Description = "+5% de velocidade na forma de lobo." },
		new() { Id = "tat_salto", Name = "Salto Longo", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 1, MinRank = 2, Prices = new[] { 120, 600, 1800, 4000, 9500 },
			Tattoo = new TattooEffect { Jump = 1.06f },
			Description = "+6% de força no pulo do lobo." },
		new() { Id = "tat_couro", Name = "Couro Grosso", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 2, MinRank = 3, Prices = new[] { 150, 750, 2200, 5000, 12000 }, Hot = true,
			Tattoo = new TattooEffect { Shield = 1.10f },
			Description = "+10% de escudo máximo do lobo." },
		new() { Id = "tat_garra", Name = "Garra Afiada", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 2, MinRank = 4, Prices = new[] { 150, 750, 2200, 5000, 12000 },
			Tattoo = new TattooEffect { ClawDamage = 1.06f },
			Description = "+6% de dano da garra." },
		new() { Id = "tat_pelo", Name = "Pelo Denso", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 3, MinRank = 6, Prices = new[] { 180, 900, 2700, 6000, 14000 },
			Tattoo = new TattooEffect { ShieldDecay = 0.8f },
			Description = "O escudo do lobo se desfaz 20% mais devagar." },
		// Épicas e lendárias (19/11): duas coisas de uma vez, ainda ajuste
		// fino — a maior soma fica perto de duas comuns.
		new() { Id = "tat_cacador", Name = "Caçador Noturno", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 3, MinRank = 5, Prices = new[] { 220, 1100, 3300, 7300, 17000 },
			Tattoo = new TattooEffect { Speed = 1.06f, Jump = 1.05f },
			Description = "+6% de velocidade e +5% de força no pulo, na forma de lobo." },
		new() { Id = "tat_presas", Name = "Presas de Aço", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 3, MinRank = 5, Prices = new[] { 220, 1100, 3300, 7300, 17000 },
			Tattoo = new TattooEffect { ClawDamage = 1.09f },
			Description = "+9% de dano da garra." },
		new() { Id = "tat_alfa", Name = "Alfa", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 4, MinRank = 7, Prices = new[] { 280, 1400, 4200, 9300, 22000 }, Hot = true,
			Tattoo = new TattooEffect { Shield = 1.12f, ShieldDecay = 0.8f },
			Description = "Lendária. +12% de escudo máximo do lobo, e ele se desfaz 20% mais devagar." },
		new() { Id = "tat_lua", Name = "Fúria da Lua", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 4, MinRank = 7, Prices = new[] { 280, 1400, 4200, 9300, 22000 }, Hot = true,
			Tattoo = new TattooEffect { ClawDamage = 1.07f, Speed = 1.05f },
			Description = "Lendária. +7% de dano da garra e +5% de velocidade na forma de lobo." },

		// ---------------------------------------------------- fotos de perfil
		// As da loja (as de graça não são item: Avatars.cs). Só permanente.
		new() { Id = "foto_garras", Name = "Foto Garras", Type = "Foto de perfil", Kind = ItemKind.Photo,
			Rarity = 2, Prices = new[] { -1, -1, -1, -1, 800 }, Description = "O ícone Garras no seu retrato." },
		new() { Id = "foto_capacete", Name = "Foto Capacete", Type = "Foto de perfil", Kind = ItemKind.Photo,
			Rarity = 2, Prices = new[] { -1, -1, -1, -1, 800 }, Description = "O ícone Capacete no seu retrato." },
		new() { Id = "foto_escorpiao", Name = "Foto Escorpião", Type = "Foto de perfil", Kind = ItemKind.Photo,
			Rarity = 2, Prices = new[] { -1, -1, -1, -1, 800 }, Description = "O ícone Escorpião no seu retrato." },
		new() { Id = "foto_bau", Name = "Foto Baú", Type = "Foto de perfil", Kind = ItemKind.Photo,
			Rarity = 2, Prices = new[] { -1, -1, -1, -1, 800 }, Description = "O ícone Baú no seu retrato." },
		new() { Id = "foto_urso", Name = "Foto Urso de Guerra", Type = "Foto de perfil", Kind = ItemKind.Photo,
			Rarity = 3, Prices = new[] { -1, -1, -1, -1, 1200 }, Description = "O ícone Urso de Guerra no seu retrato." },
		new() { Id = "foto_cristal_lua", Name = "Foto Cristal da Lua", Type = "Foto de perfil", Kind = ItemKind.Photo,
			Rarity = 3, Prices = new[] { -1, -1, -1, -1, 1200 }, Description = "O ícone Cristal da Lua no seu retrato." },
		new() { Id = "foto_coruja_neve", Name = "Foto Coruja da Neve", Type = "Foto de perfil", Kind = ItemKind.Photo,
			Rarity = 3, Prices = new[] { -1, -1, -1, -1, 1500 }, Description = "O ícone Coruja da Neve no seu retrato." },
		new() { Id = "foto_raposa_espirito", Name = "Foto Raposa Espírito", Type = "Foto de perfil", Kind = ItemKind.Photo,
			Rarity = 3, Prices = new[] { -1, -1, -1, -1, 1500 }, Description = "O ícone Raposa Espírito no seu retrato." },
		new() { Id = "foto_lobo_neon", Name = "Foto Lobo Neon", Type = "Foto de perfil", Kind = ItemKind.Photo,
			Rarity = 4, Prices = new[] { -1, -1, -1, -1, 2000 }, Description = "O ícone Lobo Neon no seu retrato." },
		new() { Id = "foto_granada_ouro", Name = "Foto Granada de Ouro", Type = "Foto de perfil", Kind = ItemKind.Photo,
			Rarity = 4, Prices = new[] { -1, -1, -1, -1, 2000 }, Description = "O ícone Granada de Ouro no seu retrato." },
		new() { Id = "foto_lobo_gelo", Name = "Foto Lobo de Gelo", Type = "Foto de perfil", Kind = ItemKind.Photo,
			Rarity = 4, Prices = new[] { -1, -1, -1, -1, 2500 }, Description = "O ícone Lobo de Gelo no seu retrato." },
		new() { Id = "foto_lobo_fogo", Name = "Foto Lobo de Fogo", Type = "Foto de perfil", Kind = ItemKind.Photo,
			Rarity = 4, Prices = new[] { -1, -1, -1, -1, 2500 }, Description = "O ícone Lobo de Fogo no seu retrato." },

		// ---------------------------------------------------- serviços
		new() { Id = "troca_nome", Name = "Troca de nome", Type = "Serviço", Kind = ItemKind.Service,
			Rarity = 1, Consumable = true, Prices = new[] { -1, -1, -1, -1, 1500 },
			Description = "Troca o seu nickname uma vez. O nome é fixo sem este item." }
	});

	/// <summary>
	/// Um item por pasta de personagem, antes dos serviços. Por enquanto
	/// todo personagem vem com o perfil (Starter): preço de personagem é
	/// decisão de economia ainda não tomada.
	/// </summary>
	private static List<CatalogItem> WithCharacters(List<CatalogItem> items)
	{
		int at = items.FindIndex(i => i.Kind == ItemKind.Service);
		if (at < 0) at = items.Count;

		foreach (CharacterSkin skin in CharacterSkin.All())
		{
			string team = skin.Team == Teams.Blue ? "Vanguarda" : skin.Team == Teams.Orange ? "Sindicato" : "os dois times";
			items.Insert(at++, new CatalogItem
			{
				Id = skin.Id, Name = skin.DisplayName, Type = Loc.F("Personagem · {0}", team), Kind = ItemKind.Character,
				Rarity = skin.Rarity, Starter = true, Character = skin, Team = skin.Team,
				Description = skin.Description
			});
		}

		return items;
	}

	private static readonly Dictionary<string, CatalogItem> _byId = _items.ToDictionary(i => i.Id);

	public static IReadOnlyList<CatalogItem> All => _items;

	public static CatalogItem Get(string id) =>
		id != null && _byId.TryGetValue(id, out CatalogItem item) ? item : null;

	public static IEnumerable<CatalogItem> OfKind(ItemKind kind) => _items.Where(i => i.Kind == kind);

	public static string NameOf(string id) => Get(id)?.Name ?? Arsenal.NameOf(id);

	/// <summary>Preço numa duração, ou −1 se não vendido nela. Já com o
	/// desconto que estiver valendo no servidor (promoção do painel ou
	/// item do dia) — é o mesmo preço que o servidor vai cobrar.</summary>
	public static int Price(CatalogItem item, Duration d)
	{
		if (item?.Prices == null || (int)d >= item.Prices.Length) return -1;
		int price = item.Prices[(int)d];
		if (price < 0) return -1;

		int percent = ServerData.Discount(item.Id);
		return percent <= 0 ? price : Math.Max(1, (int)Math.Round(price * (100 - percent) / 100.0));
	}

	// ------------------------------------------------------------------ o que o servidor muda

	private sealed record BuiltIn(int[] Prices, int Rarity, int MinRank, bool Hot, bool Starter);
	private static readonly Dictionary<string, BuiltIn> _builtIn = new();

	/// <summary>
	/// A economia de um item como o servidor a tem hoje (o painel muda
	/// preço, raridade, patente, destaque e "vem com a conta" sem versão
	/// nova do jogo). O que veio com o jogo fica guardado para
	/// <see cref="RestoreBuiltIn"/>.
	/// </summary>
	public static void ApplyServer(string id, int[] prices, int rarity, int minRank, bool hot, bool starter)
	{
		if (Get(id) is not CatalogItem item) return;
		_builtIn.TryAdd(id, new BuiltIn(item.Prices, item.Rarity, item.MinRank, item.Hot, item.Starter));
		item.Prices = prices is { Length: > 0 } ? prices : null;
		item.Rarity = Math.Clamp(rarity, 0, 4);
		item.MinRank = Ranks.Clamp(minRank);
		item.Hot = hot;
		item.Starter = starter;
	}

	/// <summary>Volta ao que veio com o jogo (sem servidor, e nos testes).</summary>
	public static void RestoreBuiltIn()
	{
		foreach (var (id, was) in _builtIn)
		{
			if (Get(id) is not CatalogItem item) continue;
			item.Prices = was.Prices;
			item.Rarity = was.Rarity;
			item.MinRank = was.MinRank;
			item.Hot = was.Hot;
			item.Starter = was.Starter;
		}
		_builtIn.Clear();
	}

	/// <summary>Preço por dia, ou −1 para permanente e não vendido.</summary>
	public static float PerDay(CatalogItem item, Duration d)
	{
		int price = Price(item, d);
		int days = Days(d);
		return price < 0 || days == 0 ? -1f : price / (float)days;
	}

	/// <summary>A duração limitada de menor preço por dia — ganha o selo
	/// "melhor valor". Permanente fica de fora: não tem preço por dia.</summary>
	public static Duration? BestValue(CatalogItem item)
	{
		Duration? best = null;
		float bestPerDay = float.MaxValue;

		foreach (Duration d in new[] { Duration.Day1, Duration.Day7, Duration.Day30, Duration.Day90 })
		{
			float perDay = PerDay(item, d);
			if (perDay < 0 || perDay >= bestPerDay) continue;
			bestPerDay = perDay;
			best = d;
		}

		return best;
	}

	// ------------------------------------------------------------------ do dia

	/// <summary>Os itens do dia: a vitrine que o servidor sorteia pela data
	/// (mesmo dia, mesma vitrine, para todo mundo), com desconto até a
	/// meia-noite de Brasília. Sem servidor não há vitrine.</summary>
	public static List<CatalogItem> Daily() => ServerData.DailyItems.Select(Get).Where(i => i != null).ToList();

	public static bool IsDaily(CatalogItem item) => item != null && ServerData.DailyItems.Contains(item.Id);
}
