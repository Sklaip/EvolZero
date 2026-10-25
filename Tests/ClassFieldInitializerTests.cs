using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Проверка field-initializer'ов и обязательной инициализации ref/refb-полей
/// к выходу из конструктора.
///
/// Инициализатор поля компилируется в неявные присваивания в начале каждого
/// конструктора класса (а для класса без явного конструктора — в синтезированный
/// конструктор по умолчанию), после чего тело конструктора может переопределить
/// значение. Любое ref/refb-поле, не инициализированное к выходу из конструктора,
/// отвергается на слое BasicSemanticsValidator с кодом DOLBAEB.
/// </summary>
public sealed class ClassFieldInitializerTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void FieldInitializers_CompileRunsAndPrintExpectedOutput()
	{
		string exePath = _compiler.Compile("ClassFieldInitializer", ["ClassFieldInitializers.cev"]);

		string[] expectedLines =
		{
			"10",  // Config.A — из инициализатора
			"30",  // Config.B — 25 + 5
			"40",  // Config.D — A + B (ссылается на другие поля)
			"87",  // Config.Sum() — 10 + 30 + 7 + 40
			"100", // Point.X — конструктор переопределяет инициализатор X = 1
			"2",   // Point.Y — инициализатор, не тронутый конструктором
			"10",  // Holder.Cfg.A — ref-поле, инициализированное new
			"42",  // Implicit.V — класс без явного конструктора
		};

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, expectedLines);
	}

	[Fact]
	public void MissingRefField_ScanFailsWithDolbaeb()
	{
		// ref-поле (владеющая ссылка) обязано быть инициализировано в конструкторе.
		TestAssertions.AssertCompilationFails(
			_compiler, "ClassFieldInitializer", "ClassFieldInit_MissingRefField.cev", "DOLBAEB");
	}

	[Fact]
	public void MissingRefbField_FailsWithDolbaeb()
	{
		// refb-поле (заимствованная ссылка) тоже обязано быть инициализировано.
		TestAssertions.AssertCompilationFails(
			_compiler, "ClassFieldInitializer", "ClassFieldInit_MissingRefbField.cev", "DOLBAEB");
	}

	[Fact]
	public void MissingRefField_WithoutExplicitCtor_FailsWithDolbaeb()
	{
		// Класс без явного конструктора: проверка идёт по синтезированному конструктору.
		TestAssertions.AssertCompilationFails(
			_compiler, "ClassFieldInitializer", "ClassFieldInit_MissingRefFieldImplicit.cev", "DOLBAEB");
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}
