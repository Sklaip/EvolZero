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

	[Theory]
	// Ввод 1: обмен obj.Data <- structureOne только в if-ветке:
	// старый strcRec (57) удалён при переназначении через обмен; obj удалён в конце if-ветки
	// (деинициализация в else-if); obj.Data = structureOne освобождается вместе с obj;
	// strcRec = старый obj.Data (2).
	[InlineData(1,
		"num is 1\n" +
		"Structure destructor. Num: 57\n" +
		"Test function\n" +
		"TestObj destructor. ObjectNum: 2\n" +
		"  Data.Num: 21\n" +
		"  BorrowData.Num: 10\n" +
		"Structure destructor. Num: 21\n" +
		"End function\n" +
		"TestObj destructor. ObjectNum: 1\n" +
		"  Data.Num: 300\n" +
		"  BorrowData.Num: 10\n" +
		"Structure destructor. Num: 300\n" +
		"Structure destructor. Num: 10\n" +
		"Structure destructor. Num: 2")]
	// Ввод 2: PassOwnerRef забирает владение obj в else-if-ветке (объект удалён внутри неё);
	// structureOne деинициализирован в if-ветке, поэтому его деструктор вставлен в else-if-ветку.
	[InlineData(2,
		"num is 2\n" +
		"ownering sum: 23\n" +
		"TestObj destructor. ObjectNum: 2\n" +
		"  Data.Num: 2\n" +
		"  BorrowData.Num: 21\n" +
		"Structure destructor. Num: 2\n" +
		"Test function\n" +
		"Structure destructor. Num: 21\n" +
		"End function\n" +
		"TestObj destructor. ObjectNum: 1\n" +
		"  Data.Num: 300\n" +
		"  BorrowData.Num: 10\n" +
		"Structure destructor. Num: 300\n" +
		"Structure destructor. Num: 10\n" +
		"Structure destructor. Num: 57")]
	// Ввод 3: PassBorrowRef не забирает владение; в else-ветку вставлены деструкторы structureOne и obj.
	[InlineData(3,
		"num is 3\n" +
		"borrow sum: 59\n" +
		"Test function\n" +
		"Structure destructor. Num: 21\n" +
		"TestObj destructor. ObjectNum: 2\n" +
		"  Data.Num: 2\n" +
		"  BorrowData.Num: 57\n" +
		"Structure destructor. Num: 2\n" +
		"End function\n" +
		"TestObj destructor. ObjectNum: 1\n" +
		"  Data.Num: 300\n" +
		"  BorrowData.Num: 10\n" +
		"Structure destructor. Num: 300\n" +
		"Structure destructor. Num: 10\n" +
		"Structure destructor. Num: 57")]
	public void IfElseDestructionOrder_OwnershipAndBorrowAcrossBranches(int input, string expectedOutput)
	{
		// Правило 6 + механика LiftimesConsumer: деинициализация в любой ветке if/else-if/else
		// приводит к вставке деинициализации в остальные ветки. Тест проверяет точный порядок
		// вызова деструкторов по каждой ветке.
		// Пустые строки (printf("\n") в начале/в конце dtor) NormalizeLines отбрасывает.
		string exePath = _compiler.Compile(["LT_Positive_IfElseDestructionOrder.cev"]);

		var result = ExecutableRunner.Run(
			exePath,
			workingDirectory: _compiler.WorkDir,
			standardInput: input + "\n");

		Assert.False(result.TimedOut, "Программа превысила таймаут.");
		Assert.Equal(0, result.ExitCode);

		var expectedLines = TestAssertions.NormalizeLines(expectedOutput);
		var actualLines = TestAssertions.NormalizeLines(result.StandardOutput);
		Assert.Equal(expectedLines, actualLines);
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}