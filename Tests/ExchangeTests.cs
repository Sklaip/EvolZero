using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Проверка оператора обмена '<-': результат выражения — старое значение левого
/// операнда, а сам левый операнд получает значение правого.
/// Проверяются простые значения и владеющие (ref) ссылки.
/// </summary>
public sealed class ExchangeTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void Exchange_CompilesRunsAndPrintsExpectedOutput()
	{
		string exePath = _compiler.Compile(["Exchange.cev"]);

		string[] expectedLines =
		{
			"1",   // 1. z = старое x
			"2",   // 1. x = y
			"2",   // 1. y не изменился
			"10",  // 2. c = старое a
		};

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, expectedLines);
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}