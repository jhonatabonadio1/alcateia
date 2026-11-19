using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Os calls de rádio: Z abre a lista, um número fala e fecha, os números
/// não trocam de arma com a lista aberta, só o time ouve, e cada time tem
/// a sua voz (azul feminina, vermelho masculina), com os dez arquivos.
/// </summary>
public sealed class RadioTest : RegressionTest
{
	public override string Name => "RadioDoTime";
	public override string Description => "Z abre a lista do rádio, o número fala para o time e fecha; inimigo não ouve; cada time com a sua voz";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(keep: 1);
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.5);
		Hud hud = b.GetTree().Root.FindChildren("*", "", true, false).OfType<Hud>().First();
		Radio.Reset();

		for (int i = 0; i < Radio.Calls.Length; i++)
			b.Check(Sfx.Has(Radio.SoundOf(Teams.Blue, i)) && Sfx.Has(Radio.SoundOf(Teams.Orange, i)), $"fala {i + 1} ({Radio.Calls[i]}): as duas vozes existem");
		b.Check(Radio.SoundOf(Teams.Blue, 0) == "radio_f_1" && Radio.SoundOf(Teams.Orange, 9) == "radio_m_10", "azul com a voz feminina, vermelho com a masculina");

		Slot before = b.Weapon.CurrentSlot;
		await b.Tap("radio");
		await b.Frames(2);
		b.Check(hud.RadioOpen && Radio.MenuOpen, "Z abre a lista");

		string heard = null;
		void Listen(string who, int team, int call) => heard = Radio.Calls[call];
		Radio.Heard += Listen;

		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Key2, Keycode = Key.Key2, Pressed = true });
		Input.ActionPress("slot_2");
		await b.Frames(2);
		Input.ActionRelease("slot_2");
		await b.Frames(3);
		b.Check(heard == "Roger That!", $"o 2 fala \"Roger That!\" (ouviu: {heard})");
		b.Check(!hud.RadioOpen && !Radio.MenuOpen, "falou, a lista fecha");
		b.Check(hud.RadioSaid.EndsWith("Roger That!"), $"a fala aparece no canto ({hud.RadioSaid})");
		b.Check(b.Weapon.CurrentSlot == before, "com a lista aberta o número não troca de arma");

		// Só o time ouve.
		CharacterBody3D bot = b.Bots()[0];
		Health botHealth = bot.GetNode<Health>("Health");
		heard = null;
		botHealth.Team = b.Health.Team == Teams.Blue ? Teams.Orange : Teams.Blue;
		Radio.Receive(bot, 4);
		b.Check(heard == null, "call do inimigo não toca");
		botHealth.Team = b.Health.Team;
		Radio.Receive(bot, 4);
		b.Check(heard == "Go, Go, Go!", "call do aliado toca");

		// Fecha sozinha.
		await b.Seconds(Radio.Cooldown);
		await b.Tap("radio");
		await b.Frames(2);
		b.Check(hud.RadioOpen, "Z abre de novo");
		await b.Seconds(hud.RadioListTime + 0.2);
		b.Check(!hud.RadioOpen, "sem escolher, fecha sozinha");
		Radio.Heard -= Listen;
	}
}
