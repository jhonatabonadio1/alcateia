using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// O tempo de lobo e a espera (decisão do Jhon, 26/11: "um tempo limite
/// para usar o lobo; ao passar esse tempo volta a ser humano, deve avisar o
/// player. E deve ter cooldown para voltar a ser lobo, para forçar os
/// players a jogarem com armas"): 30 s de lobo, 20 s de espera, aviso nos
/// últimos 5 s. O teste confere os números de fábrica (jogador e bot) e
/// roda o relógio encurtado (4 s, 3 s de espera, aviso a 2 s) para não
/// esperar um minuto: a regra é a mesma.
/// </summary>
public sealed class WolfTimeTest : RegressionTest
{
	public override string Name => "TempoDeLobo";
	public override string Description => "lobo dura 30 s (aviso nos últimos 5 s, texto e som), volta a humano sozinho, espera 20 s para virar de novo (pela tecla também); morrer zera; o bot respeita; o boneco da rede não volta sozinho";

	/// <summary>O começo do texto do HUD, até o número, no idioma do jogo:
	/// o padrão é o inglês desde 27/11 (Jhon), e o português só vale para quem
	/// escolheu. O teste confere o aviso, não a língua.</summary>
	private static string Before(string key)
	{
		string text = Loc.T(key);
		int at = text.IndexOf('{');
		return at < 0 ? text : text[..at];
	}

	public override async Task Run(RegressionBench b)
	{
		FormController form = b.Form;
		b.Near(30, form.WolfTimeLimit, 1e-3, "tempo de lobo de fábrica (s)");
		b.Near(20, form.WolfCooldown, 1e-3, "espera de fábrica (s)");
		b.Near(5, form.WolfWarnAt, 1e-3, "aviso de fábrica (s antes do fim)");
		var botScene = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Node>();
		FormController botForm = botScene.GetNode<FormController>("FormController");
		b.Check(Mathf.IsEqualApprox(botForm.WolfTimeLimit, 30f) && Mathf.IsEqualApprox(botForm.WolfCooldown, 20f), "o bot tem o mesmo tempo e a mesma espera");
		botScene.Free();

		b.RemoveBots(keep: 1);
		Hud hud = b.GetTree().Root.FindChildren("*", "", true, false).OfType<Hud>().FirstOrDefault();
		Label warning = hud == null ? null : (Label)typeof(Hud).GetField("_warning", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hud);
		Label shiftState = hud == null ? null : (Label)typeof(Hud).GetField("_shiftState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hud);
		var slots = hud == null ? null : (Dictionary<Slot, PanelContainer>)typeof(Hud).GetField("_slots", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hud);
		b.Check(hud != null, "o HUD está na cena");

		// O relógio curto.
		form.WolfTimeLimit = 4f;
		form.WolfCooldown = 3f;
		form.WolfWarnAt = 2f;
		float warnedAt = -1f, upAt = -1f, coolingSaid = -1f;
		double start = 0;
		form.WolfTimeWarning += _ => warnedAt = (float)(b.Time - start);
		form.WolfTimeUp += () => upAt = (float)(b.Time - start);
		form.TransformCooling += left => coolingSaid = left;

		await b.BecomeWolf(timed: true);
		start = b.Time;
		b.Near(4f, form.WolfTimeLeft, 0.05, "virou lobo: o relógio começa cheio (s)");
		await b.Seconds(1.0);
		string midText = shiftState?.Text ?? "";
		RegressionBench.Note($"no meio do lobo o HUD diz '{midText}'");
		b.Check(midText.StartsWith(Before("LOBO · {0:0} S")), $"o HUD mostra o tempo de lobo ('{midText}')");

		b.Check(await b.Until(() => warnedAt >= 0, 4.0), "avisou antes do fim");
		await b.Frames(2);
		RegressionBench.Note($"aviso a {warnedAt:0.00} s (esperado 2,0); o HUD diz '{warning?.Text}' e '{shiftState?.Text}'");
		b.Near(2.0, warnedAt, 0.1, "o aviso sai a 2 s do fim");
		b.Check((warning?.Text ?? "").StartsWith(Before("Você volta a humano em {0:0} s")), $"o aviso na tela fala da volta a humano ('{warning?.Text}')");
		b.Check((shiftState?.Text ?? "").StartsWith(Before("HUMANO EM {0:0} S")), $"a barra conta a volta ('{shiftState?.Text}')");

		b.Check(await b.Until(() => form.Current == Form.Human, 4.0), "voltou a humano sozinho");
		RegressionBench.Note($"o tempo acabou a {upAt:0.00} s (esperado 4,0)");
		b.Near(4.0, upAt, 0.1, "o lobo durou o tempo dele (s)");
		b.Check(form.WolfCooldownLeft > 2.5f, $"a espera começou ({form.WolfCooldownLeft:0.00} s)");
		b.Check(!form.WolfReady, "na espera não pode virar lobo");
		if (slots != null)
		{
			await b.Frames(2);
			string chip = slots[Slot.Wolf].GetChild<Label>(0).Text;
			RegressionBench.Note($"espaço 3 na espera: '{chip}'; a barra diz '{shiftState?.Text}'");
			b.Check(chip.EndsWith("s") && int.TryParse(chip.TrimEnd('s'), out int secs) && secs is >= 1 and <= 3, $"o espaço 3 mostra os segundos da espera ('{chip}')");
			b.Check((shiftState?.Text ?? "").StartsWith(Before("ESPERE {0:0} S")), $"a barra mostra a espera ('{shiftState?.Text}')");
		}

		form.ToggleForm();
		await b.Frames(2);
		b.Check(form.Current == Form.Human, "apertar 3 na espera não transforma");
		b.Check(coolingSaid > 0f, $"e o HUD é avisado de quanto falta ({coolingSaid:0.0} s)");
		b.Check((warning?.Text ?? "").StartsWith(Before("Lobo de novo em {0:0} s")), $"o aviso na tela diz quando volta ('{warning?.Text}')");

		b.Check(await b.Until(() => form.WolfReady, 4.0), "a espera acaba");
		await b.Frames(2);
		if (slots != null) b.Check(slots[Slot.Wolf].GetChild<Label>(0).Text == "3", "fora da espera o espaço 3 volta ao 3");
		form.AddWp(5000);
		form.ToggleForm();
		b.Check(await b.Until(() => form.Current == Form.Wolf, 2.0), "depois da espera vira lobo de novo");

		// Voltar pela tecla também começa a espera.
		await b.Tap("slot_1");
		b.Check(await b.Until(() => form.Current == Form.Human, 2.0), "a tecla 1 volta a humano");
		b.Check(form.WolfCooldownLeft > 2.5f, $"voltar pela tecla também começa a espera ({form.WolfCooldownLeft:0.00} s)");

		// Morrer zera os dois.
		form.ResetWolfClock();
		form.ToggleForm();
		await b.Until(() => form.Current == Form.Wolf, 2.0);
		await b.Seconds(0.5);
		b.Health.TakeDamage(99999f);
		await b.Seconds(0.3);
		b.Check(b.Health.IsDead, "morreu de lobo");
		b.Check(form.WolfTimeLeft <= 0f && form.WolfReady, "morrer zera o tempo de lobo e a espera");

		// O bot: o mesmo relógio, e fica na espera.
		var bot = (CharacterBody3D)b.Bots()[0];
		FormController bf = bot.GetNode<FormController>("FormController");
		bf.WolfTimeLimit = 1.5f;
		bf.WolfCooldown = 3f;
		bf.AddWp(5000);
		bf.ToggleForm();
		b.Check(await b.Until(() => bf.Current == Form.Wolf, 2.0), "o bot vira lobo");
		b.Check(await b.Until(() => bf.Current is Form.Human or Form.ShiftingToHuman, 3.0), "o bot volta a humano quando o tempo acaba");
		b.Check(!bf.WolfReady, "e fica na espera");
		bf.ToggleForm();
		await b.Frames(2);
		b.Check(!bf.IsWolf || bf.Current == Form.ShiftingToHuman, "na espera o bot não vira lobo de novo");

		// O boneco da rede (forma vinda de fora): o tempo é o do dono, não volta sozinho aqui.
		await b.Until(() => bf.Current == Form.Human, 2.0);
		bf.ResetWolfClock();
		bf.WolfTimeLimit = 1f;
		bf.RemoteSet(Form.Wolf);
		await b.Seconds(2.0);
		b.Check(bf.Current == Form.Wolf, "o boneco que espelha o dono não volta sozinho");
	}
}

/// <summary>
/// O lobo é um só, no meio do Bruto e do Ligeiro de antes, e é canhão de
/// vidro (decisões do Jhon, 26/11: "remover as classes de lobo, deixar
/// apenas melhorias"; o arquétipo escolhido: "canhão de vidro"): escudo
/// 100 (era 200), o corpo leva o tiro inteiro (o Bruto levava 60%), a
/// garra ainda mata um humano em dois golpes, e o rugido devolve os 100.
/// </summary>
public sealed class GlassWolfTest : RegressionTest
{
	public override string Name => "LoboCanhaoDeVidro";
	public override string Description => "lobo único: escudo 100, leva o dano inteiro no corpo, a garra mata humano em 2 golpes, o rugido devolve o escudo; sem classe na loja";

	public override async Task Run(RegressionBench b)
	{
		b.Check(!Catalog.All.Any(i => i.Id.StartsWith("raca_")), "não há classe de lobo no catálogo (nem Bruto nem Ligeiro)");
		b.Check(!ResourceLoader.Exists("res://src/resources/breeds/bruto.tres") && !ResourceLoader.Exists("res://src/resources/breeds/ligeiro.tres"), "os arquivos das classes saíram");

		b.RemoveBots();
		await b.BecomeWolf();
		await b.Seconds(0.3);
		WolfBreed breed = b.Form.Breed;
		RegressionBench.Note($"lobo: escudo {breed.Shield}, corpo {breed.BodyResistance}, garra {breed.AttackDamage}, velocidade ×{breed.SpeedMultiplier}, pulo ×{breed.JumpMultiplier}");
		b.Near(100, b.Health.MaxShield, 0.01, "escudo máximo do lobo (era 200)");
		b.Near(100, b.Health.Shield, 0.01, "vira lobo com o escudo cheio");
		b.Near(1.0, b.Health.BodyResistance, 1e-4, "o corpo leva o dano inteiro (o Bruto levava 60%)");

		float claw = CombatRules.WolfStrike(breed.AttackDamage, 1f, true, b.Weapon.ClawVsHumanBonus, false, breed.AirAttackBonus);
		RegressionBench.Note($"garra contra humano: {claw:0.#} (2 golpes = {2 * claw:0.#}, vida do humano {FormController.DefaultHumanHp})");
		b.Check(claw < FormController.DefaultHumanHp && 2 * claw >= FormController.DefaultHumanHp, "a garra mata um humano em dois golpes, não em um");

		// O tiro no corpo: o lobo perde o mesmo que um humano perderia (o escudo antes, depois a vida).
		float before = b.Health.Hp + b.Health.Shield;
		b.Health.TakeDamage(40f);
		await b.Frames(2);
		b.Near(40f, before - (b.Health.Hp + b.Health.Shield), 0.01, "40 no corpo do lobo tira 40");

		// O rugido devolve o escudo inteiro: 100.
		b.Health.TakeDamage(80f);
		await b.Frames(2);
		b.Check(b.Health.Shield < 100f, $"o escudo caiu ({b.Health.Shield:0})");
		b.Health.RestoreShield(9999f);
		b.Near(100, b.Health.Shield, 0.01, "o rugido (escudo cheio) não passa de 100");
	}
}
