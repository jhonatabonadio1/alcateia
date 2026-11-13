using System;
using System.Globalization;

namespace Alcateia.UnitTests;

/// <summary>Marca um método como teste. O texto é a regra que ele
/// prova, em português — é o que aparece no relatório.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute
{
	public string Description { get; }

	public TestAttribute(string description) => Description = description;
}

/// <summary>Uma verificação que não passou.</summary>
public sealed class CheckFailed : Exception
{
	public CheckFailed(string message) : base(message) { }
}

/// <summary>
/// As verificações dos testes. Poucas e explícitas: um projeto sem
/// pacote nenhum não pode depender do xUnit, e o que os testes do jogo
/// precisam é isto — igualdade, número perto de outro e condição.
/// </summary>
public static class Check
{
	public static void True(bool condition, string what)
	{
		if (!condition) throw new CheckFailed(what);
	}

	public static void False(bool condition, string what) => True(!condition, what);

	public static void Equal<T>(T expected, T actual, string what)
	{
		if (!Equals(expected, actual))
			throw new CheckFailed($"{what}: esperado {expected}, veio {actual}");
	}

	public static void Near(double expected, double actual, double tolerance, string what)
	{
		if (double.IsNaN(actual) || Math.Abs(expected - actual) > tolerance)
			throw new CheckFailed(
				$"{what}: esperado {F(expected)} ± {F(tolerance)}, veio {F(actual)}");
	}

	public static void Between(double min, double max, double actual, string what)
	{
		if (double.IsNaN(actual) || actual < min || actual > max)
			throw new CheckFailed($"{what}: esperado entre {F(min)} e {F(max)}, veio {F(actual)}");
	}

	public static void Throws<TException>(Action action, string what) where TException : Exception
	{
		try { action(); }
		catch (TException) { return; }
		catch (Exception other)
		{
			throw new CheckFailed($"{what}: esperado {typeof(TException).Name}, veio {other.GetType().Name}");
		}

		throw new CheckFailed($"{what}: esperado {typeof(TException).Name}, não lançou nada");
	}

	private static string F(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
