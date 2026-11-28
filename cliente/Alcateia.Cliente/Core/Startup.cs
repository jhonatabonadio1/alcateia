using System;
using Microsoft.Win32;

namespace Alcateia.Cliente.Core;

/// <summary>
/// Abrir o cliente junto com o Windows: uma linha na chave Run do usuário
/// (HKCU, não pede administrador), com <c>--minimizado</c> — ele sobe na
/// bandeja, sem janela. No Mac não há (seria um item de login; fica para
/// quando o cliente for assinado).
/// </summary>
public static class Startup
{
	private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run", ValueName = "WolfLeagueLauncher";

	public static bool Enabled
	{
		get
		{
			if (!OperatingSystem.IsWindows()) return false;
			try
			{
				using RegistryKey run = Registry.CurrentUser.OpenSubKey(Key);
				return run?.GetValue(ValueName) is string;
			}
			catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
			{
				return false;
			}
		}
	}

	public static bool Set(bool on)
	{
		if (!OperatingSystem.IsWindows()) return false;
		try
		{
			using RegistryKey run = Registry.CurrentUser.CreateSubKey(Key);
			if (on) run.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --minimizado");
			else run.DeleteValue(ValueName, false);
			return true;
		}
		catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
		{
			return false;
		}
	}
}
