using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Тонкая проверка краевых случаев работы со ссылками: псевдонимирование
/// двух ссылок на одну переменную, обмен значениями через ссылки, переуказание
/// ссылки между чтениями, ссылки на поля объектов и арифметика через ссылки.
/// </summary>
public sealed class RefEdgeCasesTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void RefEdgeCases_CompilesRunsAndPrintsExpectedOutput()
	{
		string exePath = _compiler.Compile(["RefEdgeCases.cev"]);

		string[] expectedLines =
		{
			"5",   // 1a. r2 через псевдоним r1 (оба на a)
			"5",   // 1b. a после записи через r1
			"200", // 2a. b после Swap(loc b, loc c)
			"100", // 2b. c после Swap(loc b, loc c)
			"11",  // 3a. p после rp = rp + 10
			"11",  // 3b. p не изменился после переуказания rp на q
			"22",  // 3c. q после rp = rp + 20
			"70",  // 4a. c1.Type после записи через rc
			"70",  // 4b. c1.Type не изменился после переуказания rc на c2
			"90",  // 4c. c2.Type после записи через rc
			"3",   // 5. arr0 = 1 + 2 через ссылки
			"35",  // 6. obj.Type через ссылку + 5
		};

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, expectedLines);
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}
