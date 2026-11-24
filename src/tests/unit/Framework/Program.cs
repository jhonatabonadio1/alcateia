using System;
using System.Linq;
using System.Reflection;

namespace Alcateia.UnitTests;

/// <summary>
/// Acha e roda todo método marcado com <see cref="TestAttribute"/>.
///
///   dotnet run --project tests/unit            todos
///   dotnet run --project tests/unit -- salto   só os que têm "salto" no nome
///
/// Sai com código 1 se algum falhar — é o que o CI e o tools/testar.sh
/// leem.
/// </summary>
public static class Program
{
	public static int Main(string[] args)
	{
		string filter = args.FirstOrDefault()?.ToLowerInvariant();

		var tests = Assembly.GetExecutingAssembly().GetTypes()
			.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
			.Select(method => (Method: method, Info: method.GetCustomAttribute<TestAttribute>()))
			.Where(t => t.Info != null)
			.Where(t => filter == null
				|| t.Method.Name.ToLowerInvariant().Contains(filter)
				|| t.Info.Description.ToLowerInvariant().Contains(filter)
				|| t.Method.DeclaringType!.Name.ToLowerInvariant().Contains(filter))
			.OrderBy(t => t.Method.DeclaringType!.Name).ThenBy(t => t.Method.Name)
			.ToList();

		int failed = 0;
		string group = null;

		foreach (var (method, info) in tests)
		{
			if (method.DeclaringType!.Name != group)
			{
				group = method.DeclaringType.Name;
				Console.WriteLine($"\n{group}");
			}

			try
			{
				method.Invoke(null, null);
				Console.WriteLine($"  ok      {info.Description}");
			}
			catch (TargetInvocationException error)
			{
				failed++;
				Exception cause = error.InnerException ?? error;
				string why = cause is CheckFailed ? cause.Message : cause.ToString();
				Console.WriteLine($"  FALHOU  {info.Description}\n          {why}");
			}
		}

		Console.WriteLine($"\n{tests.Count - failed} de {tests.Count} testes de unidade passaram.");
		return failed == 0 ? 0 : 1;
	}
}
