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

	[Fact]
	public void LifetimesOwnerArgInCtor_AutoAndExplicitThisLink_PrintsValues()
	{
		// Автогенерация 'data ~ this' для конструктора без секции lifetimes (правило для
		// ctor) и явная связь 'data ~ this': владелец/заимствование из внешнего блока
		// может лечь в поле this; алиас-поле пишет обратно в исходный объект.
		string exePath = _compiler.Compile(["LT_Positive_LifetimesOwnerArgInCtor.cev"]);

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, new[]
		{
			"11", // holder.Data.Num после автогенерированной связи
			"22", // bh.Num() после явной связи data ~ this
			"33", // запись через алиас-поле BorrowData видна источнику
		});
	}

	[Fact]
	public void LifetimesBorrowArgThisInField_BorrowArgWithThisLink_PrintsValues()
	{
		// Связь 'b ~ this' приравнивает лайфтайм refb-параметра к this, иначе
		// refb-аргумент жил бы «бесконечно долго» и поле его принять не могло (LT002).
		// Поле — алиас источника, деструктор источника срабатывает в конце main.
		string exePath = _compiler.Compile(["LT_Positive_LifetimesBorrowArgThisInField.cev"]);

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, new[]
		{
			"7", // box.Num() после вызова Put с 'b ~ this'
			"8", // запись через алиас-поле Data видна источнику
			"Structure dtor: 8", // владелец source удалён в конце main с финальным значением
		});
	}

	[Fact]
	public void LifetimesReturnThisField_ReturnThisLink_PrintsValues()
	{
		// Связь 'return ~ this' приравнивает лайфтайм возвращаемого значения к this:
		// refb-поле можно вернуть (правило 7.6), результат — алиас поля.
		string exePath = _compiler.Compile(["LT_Positive_LifetimesReturnThisField.cev"]);

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, new[]
		{
			"10", // чтение Value через GetRef до записи
			"42", // запись через возвращённый refb видна полю Value
		});
	}

	[Fact]
	public void LifetimesReturnArgSameScope_ReturnLink_PrintsValues()
	{
		// Связь 'source ~ return' (правило 7.5) разрешает возвращать заимствованную
		// ссылку: лайфтайм источника приравнен к лайфтайму возвращаемого значения.
		string exePath = _compiler.Compile(["LT_Positive_LifetimesReturnArgSameScope.cev"]);

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, new[]
		{
			"5", // result.Num — алиас structureOne
			"6", // запись через result видна structureOne
		});
	}

	[Fact]
	public void LifetimesReturnArgWithBorrowFieldAlias_ReturnLink_PrintsValues()
	{
		// 'structureOne ~ return' приравнивает лайфтайм refb-аргумента к return:
		// аргумент можно положить в refb-поле возвращаемого объекта (алиас-цепочка
		// поля не короче лайфтайма возвращаемого значения, LT013 не срабатывает).
		string exePath = _compiler.Compile(["LT_Positive_LifetimesReturnArgWithBorrowFieldAlias.cev"]);

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, new[]
		{
			"311", // obj.Sum() = Data.Num (300) + BorrowData.Num (11)
			"12", // запись через алиас BorrowData видна исходному объекту
		});
	}

	[Fact]
	public void LifetimesMultiLinks_MixedThisAndReturnLinks_PrintsValues()
	{
		// Несколько связей в одной секции: 'b ~ this' класть в поле, 'a ~ return'
		// возвращать как refb — оба аргумента проходят свои проверки независимо.
		string exePath = _compiler.Compile(["LT_Positive_LifetimesMultiLinks.cev"]);

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, new[]
		{
			"1", // box.Pinned.Num — алиас one ('b ~ this')
			"2", // returned.Num — алиас two ('a ~ return')
		});
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}