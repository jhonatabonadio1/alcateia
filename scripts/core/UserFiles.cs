using Godot;
using System.IO;

/// <summary>
/// Os arquivos que o jogo grava na pasta do jogador (%APPDATA%\WolfLeague,
/// ~/Library/Application Support/WolfLeague), com nome em inglês (pedido do
/// Jhon, 21/11: "arquivos do jogo devem ser em inglês"). Até 21/11 eles
/// tinham nome em português: <see cref="Resolve"/> renomeia o antigo na
/// primeira vez, e ninguém perde perfil, opções nem login.
/// </summary>
public static class UserFiles
{
	public const string Settings = "user://settings.json";      // era opcoes.json
	public const string Profile = "user://profile.json";        // era perfil.json
	public const string Avatar = "user://avatar.cfg";           // era foto.cfg
	public const string Online = "user://online.cfg";

	/// <summary>O caminho do arquivo; se só existe o de nome antigo, ele
	/// passa a ter o nome novo antes.</summary>
	public static string Resolve(string current, string legacy)
	{
		try
		{
			string now = ProjectSettings.GlobalizePath(current);
			string old = ProjectSettings.GlobalizePath(legacy);
			if (!File.Exists(now) && File.Exists(old)) File.Move(old, now);
		}
		catch (IOException e) { GD.PushWarning($"User files: could not rename {legacy} to {current}: {e.Message}"); }
		catch (System.UnauthorizedAccessException e) { GD.PushWarning($"User files: could not rename {legacy} to {current}: {e.Message}"); }
		return current;
	}
}
