using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Тонкая проверка базовых операций со ссылками (refo/loc):
/// чтение через ссылку, запись через ссылку с изменением исходной переменной,
/// переуказание ссылки, ссылки на поля классов, метод, возвращающий ссылку,
/// и передача ссылки в качестве параметра функции.
/// </summary>
public sealed class RefBasicsTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void RefBasics_CompilesRunsAndPrintsExpectedOutput()
	{
		string exePath = _compiler.Compile("RefBasics", ["RefBasics.cev"]);

		string[] expectedLines =
		{
			"42",  // 1. чтение стек-переменной через refo
			"7",   // 2a. запись через refo изменила a
			"7",   // 2b. та же ссылка теперь показывает новое значение
			"7",   // 3a. a не изменился после переуказания ra на b
			"5",   // 3b. запись через ra изменила b
			"10",  // 4. чтение поля стек-экземпляра через refo
			"50",  // 5. изменение поля через refo
			"20",  // 6. чтение поля heap-экземпляра через refo
			"50",  // 7a. метод вернул refo, прочитали
			"77",  // 7b. поле изменилось
			"7",   // 8. два Increment(loc c) -> c = 5 + 1 + 1
		};

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, expectedLines);
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}
