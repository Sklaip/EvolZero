using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Тонкая проверка цепочек ссылок и полей: чтение/запись через несколько
/// уровней ref-полей (Outer.Mid.Child.Num), вызов методов через цепочку,
/// возврат ref-поля методом и переуказание ссылки в середине цепочки.
/// </summary>
public sealed class RefChainsTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void RefChains_CompilesRunsAndPrintsExpectedOutput()
	{
		string exePath = _compiler.Compile(["RefChains.cev"]);

		string[] expectedLines =
		{
			"5",   // 1. чтение через цепочку Outer.Mid.Child.Num
			"25",  // 2a. мутация через цепочку: inner.Num
			"25",  // 2b. мутация через цепочку: outer.Mid.Child.Num
			"35",  // 3. вызов метода через цепочку: inner.Num + 10
			"33",  // 4a. запись 33: inner.Num
			"33",  // 4b. mid.Child.Num (тот же объект)
			"33",  // 4c. outer.Mid.Child.Num (тот же объект)
			"40",  // 5. метод вернул ref -> запись viaMethod.Num = 40
			"111", // 6a. после переуказания mid.Child = second
			"40",  // 6b. inner.Num не изменился
		};

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, expectedLines);
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}
