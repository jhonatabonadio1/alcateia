using Godot;
using System.IO;
using System.Linq;

/// <summary>
/// Os arquivos que o jogo grava na pasta do jogador (%APPDATA%\WolfLeague,
/// ~/Library/Application Support/WolfLeague), com nome em inglês (pedido do
/// Jhon, 21/11: "arquivos do jogo devem ser em inglês"). Até 21/11 eles
/// tinham nome em português: <see cref="Resolve"/> renomeia o antigo na
/// primeira vez, e ninguém perde perfil, opções nem login.
///
/// <b>Mais de um jogo na mesma máquina</b> (Jhon, 22/11: "como eu jogo 2
/// jogos ao mesmo tempo no mac? para testar multijogador"): com
/// <c>--perfil=b</c> depois do <c>--</c>, o login, o perfil e a foto ficam em
/// user://perfis/b/, e cada janela entra com a sua conta. As opções (vídeo,
/// som, mira) continuam as da máquina, de todos. Sem o argumento nada muda.
/// </summary>
public static class UserFiles
{
	public const string Settings = "user://settings.json";      // era opcoes.json
	public const string Profile = "user://profile.json";        // era perfil.json
	public const string Avatar = "user://avatar.cfg";           // era foto.cfg
	public const string Online = "user://online.cfg";

	/// <summary>O perfil desta janela (<c>--perfil=nome</c>), ou nulo para
	/// o de sempre. Só letras, números, "-" e "_", até 24.</summary>
	public static string Slot { get; } = ReadSlot();

	private static string ReadSlot()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (!arg.StartsWith("--perfil=")) continue;
			string name = new string(arg["--perfil=".Length..].Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').Take(24).ToArray());
			return name.Length > 0 ? name.ToLowerInvariant() : null;
		}
		return null;
	}

	/// <summary>O arquivo da conta nesta janela: com <c>--perfil=</c>, dentro
	/// de user://perfis/&lt;nome&gt;/ (a pasta é criada).</summary>
	public static string OfAccount(string path)
	{
		if (Slot == null) return path;
		string folder = $"user://perfis/{Slot}";
		DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
		return path.Replace("user://", folder + "/");
	}

	/// <summary>O caminho do arquivo; se só existe o de nome antigo, ele
	/// passa a ter o nome novo antes. <paramref name="perAccount"/>: é da
	/// conta (login, perfil, foto), e com <c>--perfil=</c> fica na pasta dele.</summary>
	public static string Resolve(string current, string legacy, bool perAccount = true)
	{
		if (perAccount && Slot != null) return OfAccount(current);
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
