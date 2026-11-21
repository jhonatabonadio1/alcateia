using Godot;
using System;
using System.IO;

/// <summary>
/// O modo de vídeo no Windows: Vulkan, DirectX 12 ou compatibilidade
/// (OpenGL traduzido para DirectX 11 pelo ANGLE). Quem usa é o iniciador
/// (instalador/iniciador.nsi), o WolfLeague.exe que o jogador abre: ele lê
/// o <c>video.ini</c> na pasta do jogo (%APPDATA%\WolfLeague) e abre o jogo
/// no modo escolhido. "auto" deixa o iniciador achar o que funciona, e se o
/// escolhido não abrir ele volta para "auto" sozinho.
///
/// Vale da próxima vez que o jogo abrir: o Godot escolhe o vídeo antes de
/// qualquer código do jogo rodar.
/// </summary>
public static class VideoMode
{
	/// <summary>O que vai no video.ini, na ordem do seletor das Opções.</summary>
	public static readonly string[] Ids = { "auto", "vulkan", "d3d12", "opengl3_angle" };
	public static readonly string[] Names = { "Automático", "Vulkan", "DirectX 12", "Compatibilidade (DirectX 11)" };

	/// <summary>Só no Windows existe o iniciador.</summary>
	public static bool Available => OS.GetName() == "Windows";

	private static string FilePath => Path.Combine(OS.GetUserDataDir(), "video.ini");

	/// <summary>O índice do escolhido (0, Automático, se não tem arquivo).</summary>
	public static int Chosen()
	{
		try
		{
			if (!File.Exists(FilePath)) return 0;
			foreach (string line in File.ReadAllLines(FilePath))
			{
				string l = line.Trim();
				if (!l.StartsWith("modo=", StringComparison.OrdinalIgnoreCase)) continue;
				int i = Array.IndexOf(Ids, l[5..].Trim().ToLowerInvariant());
				return i < 0 ? 0 : i;
			}
		}
		catch (IOException) { }
		catch (UnauthorizedAccessException) { }
		return 0;
	}

	/// <summary>Grava a escolha no formato que o iniciador lê (INI do Windows).</summary>
	public static void Choose(int index)
	{
		string id = Ids[Math.Clamp(index, 0, Ids.Length - 1)];
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			File.WriteAllText(FilePath, $"[video]\r\nmodo={id}\r\n");
		}
		catch (IOException e) { GD.PushWarning($"Modo de vídeo: não consegui gravar {FilePath}: {e.Message}"); }
		catch (UnauthorizedAccessException e) { GD.PushWarning($"Modo de vídeo: não consegui gravar {FilePath}: {e.Message}"); }
	}

	/// <summary>O modo em que o jogo está rodando agora, para mostrar.</summary>
	public static string Current => RenderingServer.GetCurrentRenderingDriverName() switch
	{
		"vulkan" => "Vulkan",
		"d3d12" => "DirectX 12",
		"opengl3_angle" => "compatibilidade (DirectX 11)",
		"opengl3" => "compatibilidade (OpenGL)",
		"metal" => "Metal",
		var other => other
	};
}
