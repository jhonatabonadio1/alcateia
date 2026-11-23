using Godot;
using System.Collections.Generic;

/// <summary>
/// Vitrine da terceira pessoa (descartável): o campo de treino com uma Eve
/// por arma, lado a lado, cada uma repetindo o que a arma dela faz — saque,
/// parado, mira com tiros, recarga; a faca com os quatro golpes e o saque; a
/// granada armando, lançando e sacando a arma de volta. Para ver as
/// animações feitas no Blender (assets/personagens/eve/animacoes) no jogo
/// sem montar partida: abra scenes/debug/vitrine_eve.tscn e rode (F6), ou
/// <c>godot --path . res://scenes/debug/vitrine_eve.tscn</c>.
///
/// As Eves são bonecos de rede (<see cref="Bot.Remote"/>), tocados pelos
/// mesmos recados que chegam numa partida online: não ferem ninguém, e a
/// granada delas é só de enfeite.
/// </summary>
public partial class VitrineEve : Node3D
{
	[Export] public PackedScene Field = GD.Load<PackedScene>("res://scenes/treino.tscn");
	[Export] public PackedScene Puppet = GD.Load<PackedScene>("res://scenes/bot.tscn");

	/// <summary>Distância entre duas Eves e da fila até onde o jogador nasce (m).</summary>
	[Export] public float Spacing = 1.7f;
	[Export] public float Ahead = 4.5f;

	/// <summary>Quanto elas viram de lado (graus): de três quartos pelo lado da arma.</summary>
	[Export] public float TurnAway = 60f;

	/// <summary>1 = Eve, 2 = Barut.</summary>
	[Export] public int Team = 1;

	private enum Kind { Firearm, Knife, Grenade }

	private sealed class Station
	{
		public string Title, Primary, Secondary;
		public Slot Slot;
		public Kind Kind;
		public Bot Body;
		public Label3D Label;
		public Vector3 At;
		public float Yaw, Clock, Offset;
		public int Step;
	}

	private readonly List<Station> _stations = new();

	public override void _Ready()
	{
		Node field = Field.Instantiate();
		// Só a fila: os bots e os alvos do treino ficam de fora, e o menu do nível dos bots
		// também — tirados antes de a cena entrar no jogo: o menu pausa o jogo ao entrar, e
		// tirado depois disso o jogo ficava pausado para sempre (a física parada: os pentes
		// soltos na recarga ficavam no ar). As Eves animam mesmo com o jogo pausado (o menu de
		// conjunto).
		foreach (string name in new[] { "Bots", "Dummies", "Menu" })
			field.GetNodeOrNull(name)?.Free();
		AddChild(field);
		ProcessMode = ProcessModeEnum.Always;
		GetTree().Paused = false;
		CallDeferred(MethodName.Build, field);
	}

	private void Build(Node field)
	{
		var player = field.GetNodeOrNull<Node3D>("Player");
		Vector3 origin = player?.GlobalPosition ?? Vector3.Zero;
		Vector3 forward = player != null ? -player.GlobalBasis.Z : Vector3.Forward;
		forward.Y = 0;
		forward = forward.Normalized();
		Vector3 right = forward.Cross(Vector3.Up).Normalized();

		var list = new (string title, string primary, string secondary, Slot slot, Kind kind)[]
		{
			("AK-74", "ak74", "deagle", Slot.Primary, Kind.Firearm),
			("AR-15", "ar15", "deagle", Slot.Primary, Kind.Firearm),
			("M4A4", "m4a4", "deagle", Slot.Primary, Kind.Firearm),
			("SCAR-H", "scar", "deagle", Slot.Primary, Kind.Firearm),
			("SVD", "svd", "deagle", Slot.Primary, Kind.Firearm),
			("Desert Eagle", "ar15", "deagle", Slot.Secondary, Kind.Firearm),
			("Faca", "ar15", "deagle", Slot.Knife, Kind.Knife),
			("Granada", "ar15", "deagle", Slot.Grenade, Kind.Grenade),
		};

		Vector3 middle = origin + forward * Ahead;
		for (int i = 0; i < list.Length; i++)
		{
			var (title, primary, secondary, slot, kind) = list[i];
			Vector3 at = OnFloor(middle + right * ((i - (list.Length - 1) * 0.5f) * Spacing));
			// Viradas 60° para o lado de fora (de frente para o jogador, os braços esticados para a
			// câmera pareciam curtos e a arma colada no peito); a da granada lança para fora da fila.
			Vector3 look = kind == Kind.Grenade ? right : (-forward).Rotated(Vector3.Up, Mathf.DegToRad(TurnAway));
			float yaw = Mathf.Atan2(-look.X, -look.Z);

			var body = Puppet.Instantiate<Bot>();
			body.Name = "Eve_" + primary + "_" + i;
			body.Team = Team;
			body.Remote = true;
			AddChild(body);
			body.GlobalPosition = at;

			var label = new Label3D
			{
				Text = title,
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				FontSize = 48,
				PixelSize = 0.004f,
				OutlineSize = 10,
				NoDepthTest = true,
				Position = at + Vector3.Up * 2.25f,
			};
			AddChild(label);

			_stations.Add(new Station
			{
				Title = title, Primary = primary, Secondary = secondary, Slot = slot, Kind = kind,
				Body = body, Label = label, At = at, Yaw = yaw,
				// Cada uma num ponto do ciclo, para não andarem todas juntas.
				Offset = i * 0.37f,
			});
		}
	}

	/// <summary>O ponto no chão embaixo de onde o jogador nasce (ele nasce no alto e cai: na
	/// altura dele, as Eves ficavam com os pés a 2 m do chão).</summary>
	private Vector3 OnFloor(Vector3 at)
	{
		var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * 2f, at + Vector3.Down * 50f, 1);
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 ? hit["position"].AsVector3() : new Vector3(at.X, 0f, at.Z);
	}

	public override void _PhysicsProcess(double delta)
	{
		foreach (Station s in _stations)
		{
			if (!IsInstanceValid(s.Body)) continue;
			s.Clock += (float)delta;
			float t = s.Clock - s.Offset;
			switch (s.Kind)
			{
				case Kind.Firearm: Firearm(s, t); break;
				case Kind.Knife: Knife(s, t); break;
				default: Grenade(s, t); break;
			}
		}
	}

	/// <summary>Sacar (vem da faca), parado, mira com três tiros, recarga.</summary>
	private void Firearm(Station s, float t)
	{
		const float Cycle = 9.5f;
		float c = Mathf.PosMod(t, Cycle);
		Slot slot = c < 0.8f ? Slot.Knife : s.Slot;
		bool aiming = c >= 3.2f && c < 5.6f;
		bool reloading = c >= 6.0f && c < 9.0f;
		string what = c < 0.8f ? "faca" : c < 2.0f ? "sacar" : c < 3.2f ? "parado" : aiming ? "mira e tiro" : reloading ? "recarga" : "parado";
		Send(s, slot, aiming, false, reloading, what);
		// três tiros na mira
		int shot = c >= 4.0f && c < 5.2f ? (int)((c - 4.0f) / 0.4f) + 1 : 0;
		if (shot > 0 && shot != s.Step && aiming)
		{
			s.Step = shot;
			s.Body.RemoteAttack(s.Slot, s.Yaw, 0f);
		}
		if (c < 4.0f) s.Step = 0;
	}

	/// <summary>Os quatro golpes, um por um, e o saque da faca (vem do fuzil).</summary>
	private void Knife(Station s, float t)
	{
		const float Cycle = 7.5f;
		float c = Mathf.PosMod(t, Cycle);
		Slot slot = c >= 5.0f && c < 5.8f ? Slot.Primary : Slot.Knife;
		int swing = c >= 1.0f && c < 4.2f ? (int)((c - 1.0f) / 0.8f) + 1 : 0;
		string[] names = { "estocada", "corte", "estocada alta", "corte deitado" };
		string what = swing > 0 ? names[(swing - 1) % 4] : slot == Slot.Primary ? "fuzil" : c >= 5.8f && c < 7.0f ? "sacar" : "guarda";
		Send(s, slot, false, false, false, what);
		if (swing > 0 && swing != s.Step)
		{
			s.Step = swing;
			s.Body.RemoteAttack(Slot.Knife, s.Yaw, 0f);
		}
		if (swing == 0) s.Step = 0;
	}

	/// <summary>Arma (puxa o pino e engatilha), segura armada, lança e saca a arma de volta.</summary>
	private void Grenade(Station s, float t)
	{
		const float Cycle = 7.5f;
		float c = Mathf.PosMod(t, Cycle);
		bool ready = c >= 1.0f && c < 3.0f;
		bool thrown = c >= 3.0f;
		Slot slot = c >= 3.6f && c < 6.2f ? Slot.Primary : Slot.Grenade;
		string what = c < 1.0f ? "granada" : ready ? "armar" : c < 3.6f ? "arremesso" : slot == Slot.Primary ? "saca o fuzil" : "saca a granada";
		Send(s, slot, false, ready, false, what);
		if (thrown && s.Step == 0)
		{
			s.Step = 1;
			s.Body.RemoteAttack(Slot.Grenade, s.Yaw, -0.15f);
		}
		if (!thrown) s.Step = 0;
	}

	private void Send(Station s, Slot slot, bool aiming, bool grenadeReady, bool reloading, string what)
	{
		Bot b = s.Body;
		b.RemoteUpdate(s.At, Vector3.Zero, s.Yaw, 0f, true, Form.Human, slot, s.Primary, s.Secondary, 200f, 200f, 0f, 0f, false);
		b.RemoteMotion(false, false, false, false, grenadeReady, aiming);
		b.RemoteGear(30, reloading, 2, 0f, 0f);
		b.CollisionLayer = 0;
		s.Label.Text = s.Title + "\n" + what;
	}
}
