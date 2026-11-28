using Godot;
using System.Collections.Generic;

/// <summary>
/// Um personagem: o corpo humano, o lobo que vem junto, a vida e o que a
/// loja e o inventário mostram. Cada personagem é uma pasta em
/// assets/personagens/ com os dois modelos e este arquivo
/// (personagem.tres); o catálogo acha as pastas sozinho. O passo a passo
/// para fazer um novo está em docs/PERSONAGENS.md.
///
/// Humano e lobo andam juntos de propósito (decisão do Jhon): quem
/// escolhe a Eve vira o lobo da Eve. Escolher os dois em separado
/// deixaria o inimigo sem saber que o lobo na frente dele é o mesmo
/// jogador que estava ali de fuzil.
///
/// Os modelos precisam ter a altura dos de sempre (humano 1,79 m, lobo
/// 1,66 m): a cabeça que leva tiro é a do modelo que se vê (HeadHitbox),
/// e um personagem mais baixo seria mais difícil de acertar na cabeça. O
/// teste "Personagens" reprova quem fugir disso.
/// </summary>
[GlobalClass]
public partial class CharacterSkin : Resource
{
	/// <summary>Onde ficam as pastas dos personagens.</summary>
	public const string Folder = "res://src/assets/personagens";

	/// <summary>O arquivo de cada pasta.</summary>
	public const string FileName = "personagem.tres";

	/// <summary>Altura de pé dos modelos, em metros, que o jogo assume
	/// (cápsula, cabeça, câmera da morte).</summary>
	public const float HumanHeight = 1.79f;
	public const float WolfHeight = 1.66f;

	[ExportGroup("Vitrine")]
	[Export] public string DisplayName = "";

	/// <summary>O texto do cartão no inventário.</summary>
	[Export(PropertyHint.MultilineText)] public string Description = "";

	/// <summary>Time em que pode ser usado: 1 azul, 2 laranja, 0 os dois.</summary>
	[Export(PropertyHint.Enum, "Os dois:0,Azul:1,Laranja:2")] public int Team;

	/// <summary>0 comum … 4 lendário (a cor da moldura).</summary>
	[Export(PropertyHint.Range, "0,4")] public int Rarity = 1;

	[ExportGroup("Modelos")]
	/// <summary>O corpo humano. Precisa do esqueleto do Mixamo (os
	/// clipes do X Bot valem sem conversão).</summary>
	[Export(PropertyHint.File, "*.fbx,*.glb,*.tscn")] public string HumanModel = "";

	/// <summary>O lobo. Precisa do esqueleto de 24 ossos do lobo do Meshy
	/// (as animações do lobo_anim.glb valem sem conversão).</summary>
	[Export(PropertyHint.File, "*.fbx,*.glb,*.tscn")] public string WolfModel = "";

	[ExportGroup("Efeitos")]
	/// <summary>A cor dos efeitos do lobo deste personagem: o rastro das
	/// garras, a aura e o brilho da fumaça (scripts/visual/fx/WolfFx.cs).
	/// Transparente (alfa 0) = a cor do time.</summary>
	[Export] public Color EffectColor = new(0, 0, 0, 0);

	[ExportGroup("Atributos")]
	/// <summary>Multiplica a vida (a mesma nas duas formas,
	/// FormController.HumanHp, 200). 1 = a vida de todo mundo.</summary>
	[Export] public float HpMultiplier = 1f;

	[ExportGroup("Corpo")]
	/// <summary>O meio dos dois olhos do humano, no espaço do osso da cabeça
	/// (<c>mixamorig_Head</c>), em metros do modelo. É onde fica a câmera da 1ª
	/// pessoa: o olho de quem joga é o olho do corpo que os outros veem, em pé e
	/// agachado (Jhon, 26/11: "o nível de agachamento deve ser idêntico ao de
	/// 3ª pessoa, extremamente preciso"). Medido na malha da cabeça (o branco
	/// dos olhos na textura, levado ao osso pela ligação da pele):
	/// src/tools/personagens/olhos.py. Modelo novo: meça de novo.</summary>
	[Export] public Vector3 EyeInHead = new(0f, 0.075f, 0.09f);

	/// <summary>O tom da voz do humano (o "uh" da coronhada): multiplica a
	/// altura do som. A voz gravada é masculina (src/tools/sons/coronhada.py);
	/// na Eve ela sobe (Jhon, 27/11: "o barulho de coronhada da Eve deve ter
	/// pitch"). 1 = como foi gravada.</summary>
	[Export] public float VoicePitch = 1f;

	/// <summary>O personagem cujo corpo humano é <paramref name="model"/> (nulo se nenhum).</summary>
	public static CharacterSkin OfHumanModel(string model) =>
		string.IsNullOrEmpty(model) ? null : All().Find(s => s.HumanModel == model);

	/// <summary>A imagem do cartão na vitrine: thumb.jpg na pasta do
	/// personagem (16:9). Sem ela, o cartão mostra o desenho genérico.</summary>
	public string ThumbnailPath => $"{ResourcePath.GetBaseDir()}/thumb.jpg";

	/// <summary>A imagem do cartão, ou nulo.</summary>
	public Texture2D Thumbnail => ResourceLoader.Exists(ThumbnailPath) ? GD.Load<Texture2D>(ThumbnailPath) : null;

	/// <summary>O retrato (retrato.png na pasta, quadrado): a foto de quem
	/// joga com este personagem na partida, no placar (Tab) e no HUD
	/// (Jhon, 21/11: "se eu tiver de eve, exibir a foto da eve").</summary>
	public string PortraitPath => $"{ResourcePath.GetBaseDir()}/retrato.png";

	/// <summary>O de sempre do time, quando o jogador não escolheu outro (e
	/// todo bot e todo jogador de outra máquina): o personagem daquele time,
	/// Eve no azul e Barut no laranja, os mesmos modelos do CharacterAnimator.
	/// Sem time, o do azul, como o CharacterAnimator.</summary>
	public static CharacterSkin DefaultFor(int team)
	{
		List<CharacterSkin> all = All();
		return all.Find(s => s.Team == team) ?? all.Find(s => s.Team == Teams.Blue);
	}

	/// <summary>O id no catálogo e no perfil: "pers_" + o nome da pasta.</summary>
	public string Id => $"pers_{ResourcePath.GetBaseDir().GetFile()}";

	/// <summary>Todos os personagens, um por pasta, na ordem do nome da
	/// pasta. Pasta sem personagem.tres é ignorada.</summary>
	public static List<CharacterSkin> All()
	{
		var list = new List<CharacterSkin>();
		if (!DirAccess.DirExistsAbsolute(Folder)) return list;

		var names = new List<string>(DirAccess.GetDirectoriesAt(Folder));
		names.Sort(System.StringComparer.Ordinal);

		foreach (string name in names)
		{
			string path = $"{Folder}/{name}/{FileName}";
			if (!ResourceLoader.Exists(path)) continue;
			if (GD.Load<CharacterSkin>(path) is CharacterSkin skin) list.Add(skin);
		}

		return list;
	}
}
