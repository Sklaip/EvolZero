using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Позитивные тесты анализа времён жизни (правила ref/refb).
/// Каждый позитивный тест не только собирает программу, но и запускает её:
/// исполняемый файл должен завершиться кодом 0 и вывести ровно ожидаемые строки.
/// Благодаря этому ошибка компилятора, из-за которой объект удаляется не в тот
/// момент (раньше/позже нужного), проявится как падение программы либо
/// неверный результат (мусор в памяти, перезапись живого объекта следующей аллокацией).
/// </summary>
public sealed class LifetimeAnalyzerPositiveTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void RefIntoBorrow_AliveAfterBorrowUse_PrintsCorrectValues()
	{
		// Правила 3, 5: владеющая ref отдаётся как refb через loc (промоушен ref -> refb
		// в компиляторе не реализован) без деинициализации; loc возвращает refb
		// с временем жизни объекта.
		string exePath = _compiler.Compile(["LT_Positive_RefIntoBorrow.cev"]);

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, new[]
		{
			"10", // печать через refb-аргумент после передачи по loc
			"10", // объект жив после передачи как borrow-аргумент
			"10", // loc на поле владеющей ссылки
		});
	}

	[Fact]
	public void OwnershipTransferNoReuse_PrintsValueInsideFunction()
	{
		// Правило 5: владение передано функции, исходная переменная больше не используется.
		string exePath = _compiler.Compile(["LT_Positive_OwnershipTransferNoReuse.cev"]);

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, new[] { "7" });
	}

	[Fact]
	public void FieldSameLifetime_ObjectSurvivesMoveIntoUninitializedField()
	{
		// Правило 4: лайфтайм поля равен лайфтайму объекта. Поле инициализируется в
		// конструкторе (внешнее присваивание полю трактуется как переназначение — LT008);
		// объект переживает перенос владения и доступен через поле.
		string exePath = _compiler.Compile(["LT_Positive_FieldSameLifetime.cev"]);

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, new[] { "5" });
	}

	[Fact]
	public void LifetimeScopes_BorrowOutlivesOwnerBlock_ProgramProducesCorrectOutput()
	{
		// Правило 1: владеющая ref удаляется при выходе из блока, refb — нет.
		// refb на долгоживущую переменную переживает блок (x == 11 после блока);
		// освобождённый в блоке объект не должен затирать последующие аллокации
		// (heap-reuse проверяет, что tmp удалён вовремя).
		string exePath = _compiler.Compile(["LT_Positive_LifetimeScopes.cev"]);

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, new[]
		{
			"11",
			"100",
			"200",
		});
	}

	[Fact]
	public void StripFieldViaExchange_OldObjectMovesToReceiver_PrintsBoth()
	{
		// Снятие владеющей ссылки с поля только через '<-': старый объект переезжает
		// в r, поле получает новый. Ни один объект не должен быть удалён раньше времени.
		string exePath = _compiler.Compile(["LT_Positive_StripFieldViaExchange.cev"]);

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, new[]
		{
			"1", // r.V = старое значение поля (объект o1 переехал в r, не удалён)
			"2", // holder.F.V = o2
		});
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}