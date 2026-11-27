using Godot;
using System.IO;
using System.Text;

/// <summary>
/// O que passa entre o jogo e o servidor de partida, byte a byte.
///
/// Cada pacote começa com um byte dizendo o que é. O estado (onde estou,
/// para onde olho) vai 20 vezes por segundo, sem garantia de entrega —
/// perdeu um, o seguinte já vale. O resto (entrei, ataquei, acertei,
/// morri, o abate, o relógio) vai com garantia.
///
/// | Tipo | Quem manda | Conteúdo |
/// |---|---|---|
/// | Hello | jogo | chave da partida, conta, validade e ingresso |
/// | Welcome / Refused | servidor | o relógio, ou por que não entrou |
/// | State | jogo → todos | <see cref="NetState"/> |
/// | Attack | jogo → todos | o que tinha na mão e para onde mirava |
/// | Hit | jogo → vítima | dano, cabeça, causa, arma, de onde veio |
/// | Died | vítima | quem a matou e quem ajudou |
/// | Kill | servidor | o abate aceito e o placar |
/// | Clock | servidor | fase, contagem, tempo e placar |
/// | Gone | servidor | alguém saiu |
/// | Fire | servidor, 2 por segundo | o rage de cada um: conta, calor, se está em chamas e os segundos em chamas |
///
/// **Bots.** Quem move os bots é a máquina do dono da sala; ela fala por
/// eles com os mesmos pacotes, mais o número do bot na frente. O
/// servidor confere que o bot é mesmo de quem fala e passa adiante como
/// se fosse um jogador — para os outros, bot e gente chegam iguais.
///
/// | Tipo | Quem manda | Conteúdo |
/// |---|---|---|
/// | BotState / BotAttack | dono → todos | bot + o mesmo de State / Attack |
/// | BotHit | dono → vítima | bot que atirou + o mesmo de Hit |
/// | BotDied | dono | bot que morreu + o mesmo de Died |
/// | HitBot | servidor → dono | quem atirou, o bot atingido e o acerto |
/// | Touch | dono | autor, vítima e dano de um acerto entre dois corpos da mesma máquina (o jogador e os bots dele): o dano já foi aplicado lá; o juiz só anota a autoria |
///
/// O que o servidor repassa de um jogador aos outros leva a conta do
/// autor logo depois do tipo.
///
/// **Golpes do lobo** (21/11): o <c>Attack</c> do lobo sai quando o golpe
/// começa, não quando ele acerta, e leva no fim qual golpe é
/// (<see cref="WolfMove"/> + 1) e dois números: no salto do especial, quanto
/// do clipe ele pula e o ritmo do voo; no bote, em quanto tempo ele encosta.
/// Antes o boneco recebia só "atacou" na hora do impacto e tocava uma
/// garrada: o especial e o bote não apareciam, e a garrada chegava atrasada.
///
/// **Estocada da faca** (24/11): o <c>Attack</c> com a faca e, no fim,
/// <see cref="KnifeStabMove"/> (sem os dois números): o boneco toca a
/// estocada em vez do golpe rápido.
///
/// **Coronhada** (26/11): o <c>Attack</c> com a arma de fogo e
/// <see cref="ButtMove"/> no fim: o boneco toca a coronhada.
///
/// **Estado sem ordem trocada** (21/11): o estado vai sem garantia, mas em
/// ordem (<c>UnreliableOrdered</c>): um estado velho que chega depois de um
/// novo é jogado fora, em vez de voltar o boneco no tempo.
///
/// **Modo autoritativo** (decisão 0011, <c>ALCATEIA_NETCODE=autoritativo</c>
/// no servidor de partida): o servidor move os corpos. O <c>Welcome</c>
/// traz, depois do relógio, o modo e o número de cada corpo na escalação;
/// o jogo para de mandar <c>State</c> e passa a mandar:
///
/// | Tipo | Quem manda | Conteúdo |
/// |---|---|---|
/// | Input | jogo, a cada passo | os últimos <see cref="InputCommand.Redundancy"/> comandos (<see cref="InputCommand"/>) |
/// | Snapshot | servidor, 30 por segundo | o passo, o último comando seu simulado e cada corpo (<see cref="SnapshotWire"/>) |
/// | Status | jogo → todos, 10 por segundo | vida, escudo, morto, armas (a vida ainda é do jogo nesta leva) |
/// | Loadout | jogo, ao entrar | a classe de lobo e as melhorias (mudam o movimento do lobo) |
/// | Radio | jogo → todos, repassado | o call de rádio (1 byte: qual fala); cada máquina toca se for do time |
///
/// **Vida e acerto no servidor** (decisão 0016, na autoritativa): o
/// <c>Hit</c> e o <c>BotHit</c> levam, no fim, o número do comando do tiro
/// e o passo em que o jogo via os outros; o servidor confere e não repassa
/// o acerto num jogador — aplica e conta:
///
/// | Tipo | Quem manda | Conteúdo |
/// |---|---|---|
/// | Damage | servidor → vítima | quem acertou, o dano que valeu, cabeça, causa, arma, de onde, a vida e o escudo que ficaram |
/// | Vitals | servidor → cada um, 2 por segundo | a vida, o escudo e se está morto, como o servidor tem |
///
/// O <c>Status</c> do jogo ganha, no fim, a guarda do especial do lobo; o
/// que o servidor repassa dele aos outros leva a vida que o servidor tem.
///
/// **Arma no chão** (26/11, nos dois modos): é da partida, não de uma
/// máquina. Quem solta avisa; o servidor guarda as que estão no chão (até
/// 30 s, no máximo 20), passa a todos e decide quem pega — o primeiro
/// pedido leva, ninguém leva duas vezes. Quem entra no meio recebe as que
/// estão no chão.
///
/// | Tipo | Quem manda | Conteúdo |
/// |---|---|---|
/// | Drop | quem soltou → servidor → todos | número da arma, de quem caiu (conta ou bot), a arma, pente, reserva, de onde saiu, a força; do servidor, mais há quanto tempo está no chão |
/// | Take | jogo → servidor | número da arma e quem pede (a conta ou um bot da máquina) |
/// | Hurt | servidor → todos menos a vítima | autoritativa: um acerto valeu — vítima, autor, dano, vida e escudo como ficaram, caiu?, de onde veio |
/// | Taken | servidor → todos | número da arma, quem levou (0: sumiu no chão), a arma, pente e reserva |
/// </summary>
public static class NetWire
{
	/// <summary>O golpe no fim do <c>Attack</c> que quer dizer "estocada da faca" (os do lobo são <see cref="WolfMove"/> + 1).</summary>
	public const byte KnifeStabMove = 200;

	/// <summary>O golpe no fim do <c>Attack</c> que quer dizer "coronhada" (V com arma de fogo, 26/11): o boneco toca a
	/// coronhada da arma que tem na mão. Antes ela não ia pela rede: quem via de fora não via nada.</summary>
	public const byte ButtMove = 201;

	public const byte Hello = 1, Welcome = 2, Refused = 3, State = 4, Attack = 5,
		Hit = 6, Died = 7, Kill = 8, Clock = 9, Gone = 10,
		BotState = 11, BotAttack = 12, BotHit = 13, BotDied = 14, HitBot = 15, Touch = 16,
		Input = 17, Snapshot = 18, Status = 19, Loadout = 20, Radio = 21,
		Damage = 22, Vitals = 23, Fire = 24, Seat = 25, BotOwner = 26,
		Drop = 27, Take = 28, Taken = 29, Hurt = 30;

	/// <summary>O número de uma arma no chão é a conta de quem soltou vezes
	/// isto, mais um contador da máquina dele: nunca se repete entre duas
	/// máquinas, e o servidor confere que o número é de quem fala.</summary>
	public const long DropIdBase = 1_000_000;

	/// <summary>O modo da partida, no <c>Welcome</c>.</summary>
	public const byte ModeRelay = 0, ModeAuthoritative = 1;

	/// <summary>Estados por segundo que cada jogo manda. Era 20: com 30 o
	/// boneco dos outros anda menos atrasado (Jhon, 21/11: "sinto um leve
	/// delay no que eu faço para o que o player recebe").</summary>
	public const int StateRate = 30;

	public static BinaryWriter Begin(byte type, out MemoryStream stream)
	{
		stream = new MemoryStream(64);
		var writer = new BinaryWriter(stream, Encoding.UTF8);
		writer.Write(type);
		return writer;
	}

	public static BinaryReader Open(byte[] packet) => new(new MemoryStream(packet), Encoding.UTF8);

	public static void Write(this BinaryWriter w, Vector3 v)
	{
		w.Write(v.X);
		w.Write(v.Y);
		w.Write(v.Z);
	}

	public static Vector3 ReadVector(this BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

	/// <summary>O pacote que o dono mandou em nome de um bot, como o de um
	/// jogador: troca o tipo e deixa o número do bot onde iria a conta.</summary>
	public static byte[] AsPlayer(byte[] packet, byte type)
	{
		var copy = (byte[])packet.Clone();
		copy[0] = type;
		return copy;
	}

	/// <summary>O pacote de um jogador, repassado aos outros: o tipo, a
	/// conta do autor e o resto como veio.</summary>
	public static byte[] Relay(byte[] packet, long from)
	{
		var relayed = new byte[packet.Length + 8];
		relayed[0] = packet[0];
		System.BitConverter.TryWriteBytes(new System.Span<byte>(relayed, 1, 8), from);
		System.Array.Copy(packet, 1, relayed, 9, packet.Length - 1);
		return relayed;
	}
}

/// <summary>Um instante de um jogador, como ele mesmo conta.</summary>
public struct NetState
{
	public Vector3 Position, Velocity;
	public float Yaw, Pitch;
	public byte Form, Slot;
	public bool Grounded, Dead;
	/// <summary>Correndo na parede (desde 21/11: sem isto quem via o lobo
	/// de outra máquina na parede via ele pulando/caindo). Um bit no byte
	/// de Grounded e Dead: jogo antigo ignora.</summary>
	public bool WallRunning;

	/// <summary>O que o boneco adivinhava e errava (21/11), mais bits no
	/// mesmo byte: correndo de quatro (o boneco achava pela velocidade, e o
	/// lobo andando em pé aparecia de quatro), agachado (não aparecia),
	/// rugindo (o rugido não aparecia) e andando quieto (agachado ou
	/// devagar: o passo não faz barulho nas outras máquinas).</summary>
	public bool Sprinting, Crouching, Howling, Quiet;

	/// <summary>Segurando a granada com o pino puxado: o boneco arma o
	/// braço. Um byte depois do HUD (desde 21/11); jogo antigo não lê.</summary>
	public bool GrenadeReady;

	/// <summary>Na mira (22/11): o boneco sobe a arma ao olho como a do dono
	/// (HeldRig toca mirar/mira/desmirar). Um bit no byte da granada: jogo
	/// antigo ignora.</summary>
	public bool Aiming;
	public float Hp, MaxHp, Shield, MaxShield;
	public string Primary, Secondary;

	/// <summary>O que o HUD mostra (desde 19/11, no fim da mensagem: quem
	/// assiste esse corpo no espectador vê o mesmo): pente, recarga,
	/// granadas, WP e a carga do especial.</summary>
	public bool HasGear, Reloading;
	public short Ammo;
	public byte Grenades;
	public float Wp, Charge;

	/// <summary>Quando, no relógio de quem mandou (segundos), o corpo estava
	/// aqui (26/11). Com isto o boneco é desenhado entre dois estados, um
	/// pouco no passado, no ritmo de quem mandou — como na partida
	/// autoritativa — e não perseguindo a última notícia (que dava tranco e
	/// voltava no tempo com a rede irregular). No fim da mensagem: jogo antigo
	/// não manda nem lê.</summary>
	public bool HasTime;
	public double Time;

	public void Write(BinaryWriter w)
	{
		w.Write(Position);
		w.Write(Velocity);
		w.Write(Yaw);
		w.Write(Pitch);
		w.Write(Form);
		w.Write(Slot);
		w.Write((byte)((Grounded ? 1 : 0) | (Dead ? 2 : 0) | (WallRunning ? 4 : 0)
			| (Sprinting ? 8 : 0) | (Crouching ? 16 : 0) | (Howling ? 32 : 0) | (Quiet ? 64 : 0)));
		w.Write(Hp);
		w.Write(MaxHp);
		w.Write(Shield);
		w.Write(MaxShield);
		w.Write(Primary ?? "");
		w.Write(Secondary ?? "");
		if (!HasGear) return;
		w.Write(Ammo);
		w.Write(Reloading);
		w.Write(Grenades);
		w.Write(Wp);
		w.Write(Charge);
		w.Write((byte)((GrenadeReady ? 1 : 0) | (Aiming ? 2 : 0)));
		if (HasTime) w.Write(Time);
	}

	public static NetState Read(BinaryReader r)
	{
		var s = new NetState
		{
			Position = r.ReadVector(),
			Velocity = r.ReadVector(),
			Yaw = r.ReadSingle(),
			Pitch = r.ReadSingle(),
			Form = r.ReadByte(),
			Slot = r.ReadByte()
		};
		byte flags = r.ReadByte();
		s.Grounded = (flags & 1) != 0;
		s.Dead = (flags & 2) != 0;
		s.WallRunning = (flags & 4) != 0;
		s.Sprinting = (flags & 8) != 0;
		s.Crouching = (flags & 16) != 0;
		s.Howling = (flags & 32) != 0;
		s.Quiet = (flags & 64) != 0;
		s.Hp = r.ReadSingle();
		s.MaxHp = r.ReadSingle();
		s.Shield = r.ReadSingle();
		s.MaxShield = r.ReadSingle();
		s.Primary = r.ReadString();
		s.Secondary = r.ReadString();
		if (r.BaseStream.Length - r.BaseStream.Position >= 2 + 1 + 1 + 4 + 4)
		{
			s.HasGear = true;
			s.Ammo = r.ReadInt16();
			s.Reloading = r.ReadBoolean();
			s.Grenades = r.ReadByte();
			s.Wp = r.ReadSingle();
			s.Charge = r.ReadSingle();
			if (r.BaseStream.Length - r.BaseStream.Position >= 1)
			{
				byte extra = r.ReadByte();
				s.GrenadeReady = (extra & 1) != 0;
				s.Aiming = (extra & 2) != 0;
				if (r.BaseStream.Length - r.BaseStream.Position >= 8)
				{
					s.HasTime = true;
					s.Time = r.ReadDouble();
				}
			}
		}
		return s;
	}
}
