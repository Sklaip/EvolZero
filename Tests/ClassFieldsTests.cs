using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Тонкая проверка работы с полями классов: чтение/запись public-полей
/// у стек- и heap-экземпляров, независимость полей, ref-поля, переуказание
/// ref-полей, вызовы методов через ref-поля и loc на поля.
/// </summary>
public sealed class ClassFieldsTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void ClassFields_CompilesRunsAndPrintsExpectedOutput()
	{
		string exePath = _compiler.Compile(["ClassFields.cev"]);

		string[] expectedLines =
		{
			"10",  // 1. чтение public-поля стек-экземпляра
			"99",  // 2. запись public-поля стек-экземпляра
			"30",  // 3. чтение public-поля heap-экземпляра
			"66",  // 4. запись public-поля heap-экземпляра
			"9",   // 5a. Extra = 5 + 4 (метод AddExtra)
			"99",  // 5b. Type стек-экземпляра не изменился
			"66",  // 6a. holder.First.Type (First = b)
			"15",  // 6b. holder.Second.Type (Second = second)
			"111", // 7a. b.Type после записи через ref-поле
			"111", // 7b. holder.First.Type та же ссылка
			"15",  // 8a. переуказание First = second
			"111", // 8b. b.Type не изменился
			"7",   // 9. Extra через ref-поле (0 + 3 + 4)
			"200", // 10a. запись через loc на поле heap-экземпляра
			"200", // 10b. поле second.Type обновилось
		};

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, expectedLines);
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}
