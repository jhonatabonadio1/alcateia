using Godot;
using System.Threading.Tasks;

/// <summary>
/// A estocada da faca (botão direito; Jhon, 24/11: "ele dará mais dano que o
/// primário, é um ataque mais lento, mas eficaz caso pegue o adversário de
/// costas"): de frente tira 120 e não mata; pelas costas tira 240 e mata um
/// humano de vida cheia. Na primeira pessoa toca o começo do "inspecionar".
/// </summary>
public sealed class KnifeStabTest : RegressionTest
{
	public override string Name => "EstocadaDaFaca";
	public override string Description => "botão direito com a faca: a estocada anima, tira 120 de frente e mata pelas costas";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(keep: 2);
		var bots = b.Bots();
		foreach (var bot in bots)
		{
			RegressionBench.Freeze(bot);
			// humanos (o bot pode ter nascido lobo): o dano de lobo tem a resistência dele
			bot.GetNode<FormController>("FormController").RemoteSet(Form.Human);
		}
		CharacterBody3D away = bots[0], facing = bots[1];

		b.Weapon.SelectSlot(Slot.Knife);
		// Mirando o peito (25° para baixo, o alvo a 1,4 m). Desde 26/11 a câmera fica
		// nos olhos do corpo (decisão do Jhon: agachar idêntico na 1ª e na 3ª pessoa;
		// em pé ela subiu 12 cm): reto, o golpe de frente pega a cabeça e tira o
		// dobro — o que está certo, mas este teste é sobre a estocada no corpo.
		await b.Place(new Vector3(0, 1, 2), 0f, -25f);
		var view = b.Player.GetNode<ViewModel>("Head/Camera3D/ViewModel");

		// De frente primeiro: o abate acende o "em chamas" de quem matou, que
		// aumenta o dano dos golpes seguintes.
		// de frente: olha para +z, para quem bate
		facing.GlobalPosition = new Vector3(0, 1, 0.6f);
		facing.Rotation = new Vector3(0, Mathf.Pi, 0);
		away.GlobalPosition = new Vector3(6, 1, 0.6f);
		await b.Seconds(3.5); // proteção de quem acabou de nascer

		var facingHealth = facing.GetNode<Health>("Health");
		b.Check(!facing.GetNode<FormController>("FormController").IsWolf, "o alvo é humano");
		float before = facingHealth.Hp;
		await b.Tap("special");
		b.Check(view.Stabbing, "a estocada toca na mão (primeira pessoa)");
		await b.Seconds(0.1);
		b.Near(before, facingHealth.Hp, 0.5, "o dano ainda não saiu (sai quando a lâmina chega à frente)");
		await b.Seconds(0.3);
		b.Near(b.Weapon.KnifeStabDamage, before - facingHealth.Hp, 0.5, "de frente, a estocada tira 120");
		b.Check(!facingHealth.IsDead, "e não mata sozinha");

		// mais lenta que o golpe rápido: logo depois, o botão direito não sai de novo
		before = facingHealth.Hp;
		await b.Tap("special");
		await b.Seconds(0.3);
		b.Near(before, facingHealth.Hp, 0.5, "a faca ainda está ocupada");

		// de costas: olha para −z, para onde a estocada vai
		facing.GlobalPosition = new Vector3(-6, 1, 0.6f);
		away.GlobalPosition = new Vector3(0, 1, 0.6f);
		away.Rotation = Vector3.Zero;
		await b.Seconds(0.6); // a faca ocupada (KnifeStabCooldown)
		var awayHealth = away.GetNode<Health>("Health");
		before = awayHealth.Hp;
		await b.Tap("special");
		await b.Seconds(0.4);
		b.Check(awayHealth.IsDead, $"pelas costas, a estocada mata de vida cheia (tirou {before - awayHealth.Hp:0} de {before:0})");
		b.Check(b.Weapon.KnifeStabCooldown > b.Weapon.KnifeCooldown * 1.5f, "a estocada demora mais que o golpe rápido");
	}
}

/// <summary>De fora (o boneco de outro jogador), a estocada toca o clipe
/// "estocada" no corpo, que vem de animacoes/faca.json (Eve e Barut).</summary>
public sealed class PuppetStabTest : RegressionTest
{
	public override string Name => "EstocadaDeFora";
	public override string Description => "o boneco que recebe a estocada pela rede toca o clipe dela na terceira pessoa (Eve e Barut)";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		foreach (int team in new[] { Teams.Blue, Teams.Orange })
		{
			var puppet = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Bot>();
			puppet.Name = "Estoca";
			puppet.Team = team;
			puppet.Remote = true;
			b.GetTree().CurrentScene.AddChild(puppet);
			await b.Frames(3);
			Vector3 at = b.Player.GlobalPosition + new Vector3(team == Teams.Blue ? 2 : -2, 0, -4f);
			void Tell()
			{
				puppet.RemoteUpdate(at, Vector3.Zero, 0f, 0f, true, Form.Human, Slot.Knife, "ar15", "deagle", 200f, 200f, 0f, 0f, false);
				puppet.RemoteMotion(false, false, false, false, false);
				puppet.CollisionLayer = 0;
			}
			for (int i = 0; i < 60; i++) { Tell(); await b.Frames(1); }

			puppet.RemoteAttack(Slot.Knife, 0f, 0f, NetWire.KnifeStabMove);
			HeldRig rig = Nodes.FindFirst<HeldRig>(puppet);
			bool playing = false;
			for (int i = 0; i < 10 && !playing; i++)
			{
				Tell();
				await b.Frames(1);
				playing = rig != null && rig.Playing == "estocada";
			}
			string who = team == Teams.Blue ? "Eve" : "Barut";
			b.Check(playing, $"{who}: o conjunto toca a estocada");
			b.Check(b.Player.GetNode<Health>("Health").Hp >= b.Player.GetNode<Health>("Health").MaxHp - 0.5f, $"{who}: o boneco não fere ninguém (o dano é do dono)");
			puppet.QueueFree();
			await b.Frames(2);
		}
	}
}
