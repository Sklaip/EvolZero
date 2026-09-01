using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Тонкая проверка различий между стек- и heap-аллокацией экземпляров классов:
/// независимость полей у разных экземпляров, аллокация в циклах, одинаковая
/// работа методов/полей для обоих вариантов размещения.
/// </summary>
public sealed class ClassAllocationTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void ClassAllocation_CompilesRunsAndPrintsExpectedOutput()
	{
		string exePath = _compiler.Compile(["ClassAllocation.cev"]);

		string[] expectedLines =
		{
			"10",  // 1a. стек-экземпляр: начальное поле
			"15",  // 1b. стек-экземпляр: после AddType(5)
			"20",  // 2a. heap-экземпляр: начальное поле
			"25",  // 2b. heap-экземпляр: после AddType(5)
			"11",  // 3a. независимость: x.Type = 1 + 10
			"2",   // 3b. независимость: y.Type не изменился
			"5",   // 4a. независимость heap: m.Type = 5
			"200", // 4b. независимость heap: n.Type не изменился
			"8",   // 5a. стек s.Type = 7 + 1
			"9",   // 5b. heap h.Type = 8 + 1
			"100", // 6a. стек в цикле i=0
			"101", // 6b. стек в цикле i=1
			"102", // 6c. стек в цикле i=2
			"0",   // 7a. heap в цикле j=0
			"1",   // 7b. heap в цикле j=1
			"2",   // 7c. heap в цикле j=2
		};

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, expectedLines);
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}
