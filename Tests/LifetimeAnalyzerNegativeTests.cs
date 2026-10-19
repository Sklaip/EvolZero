using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Негативные тесты анализа времён жизни. Проверяют, что компилятор отвергает
/// программу с ожидаемым кодом ошибки LT00X и НЕ создаёт исполняемый файл.
///
/// Если компилятор падает в служебное исключение (NotImplementedException),
/// сообщения об ошибках компилятора не будет — проверка кода LT00X провалится,
/// и тест явно укажет на сломанный путь в анализаторе.
///
/// Тесты, помеченные FIXME, фиксируют правила, которые анализатор может ещё
/// не доезжать; они станут зелёными вместе с доработкой LifeTimesAnalyzer.
/// </summary>
public sealed class LifetimeAnalyzerNegativeTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void UseAfterFunctionMove_FailsWithLT001()
	{
		// Правило 5: переменная деинициализирована после передачи владения в функцию.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT001_UseAfterFunctionMove.cev", "LT001");
	}

	[Fact]
	public void UseAfterRefMove_FailsWithLT001()
	{
		// Правило 5: после переноса владения в другую ссылку источник использовать нельзя.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT001_UseAfterRefMove.cev", "LT001");
	}

	[Fact]
	public void DeinitInIfThenUseAfterIf_FailsWithLT001()
	{
		// Правило 6: деинициализация в любой ветке => переменная деинициализирована после if/else.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT001_DeinitInIfThenUse.cev", "LT001");
	}

	[Fact]
	public void InnerBlockIntoOuterOwner_FailsWithLT002()
	{
		// Правило 4: нельзя присваивать в более долгоживущую ссылку объект из внутреннего блока.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT002_InnerToOuter.cev", "LT002");
	}

	[Fact]
	public void ShorterLivedIntoField_FailsWithLT002()
	{
		// Правило 4: объект внутреннего блока нельзя класть в поле (лайфтайм поля = лайфтайм объекта).
		TestAssertions.AssertCompilationFails(
			_compiler, "LT002_ShorterIntoField.cev", "LT002");
	}

	[Fact]
	public void BorrowArgIntoClassField_FailsWithLT002()
	{
		// Правило 8: refb-параметр (и this) живут «бесконечно долго», назначать их в поле нельзя.
		// FIXME: может быть зелёным не сразу — зависит от текущей зрелости LifeTimesAnalyzer.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT002_BorrowArgIntoClassField.cev", "LT002");
	}

	[Fact]
	public void BorrowIntoOwnerReference_FailsWithLT003()
	{
		// Правило 2: заимствованную (refb) ссылку нельзя присваивать во владеющую (ref).
		TestAssertions.AssertCompilationFails(
			_compiler, "LT003_BorrowIntoOwner.cev", "LT003");
	}

	[Fact]
	public void ReturnBorrowRef_FailsWithLT005()
	{
		// Правило: функция возвращает только владеющие ссылки; возврат refb запрещён.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT005_ReturnBorrowRef.cev", "LT005");
	}

	[Fact]
	public void StripFieldOwnerWithoutExchange_FailsWithLT006()
	{
		// Правило: снять владеющую ссылку с поля можно только через '<-'.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT006_StripFieldOwnerPlainAssign.cev", "LT006");
	}

	[Fact]
	public void ReassignInitializedClassField_FailsWithLT008()
	{
		// Правило: нельзя переназначать уже инициализированную нелокальную ссылку.
		// FIXME: может быть зелёным не сразу — зависит от текущей зрелости LifeTimesAnalyzer.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT008_ReassignInitializedClassField.cev", "LT008");
	}

	[Fact]
	public void AliasedRefPassedAsOwner_FailsWithLT010()
	{
		// Правило 7: владеющую ссылку с локальным алиасом нельзя передавать как владение.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT010_AliasedRefPassedAsOwner.cev", "LT010");
	}

	[Fact]
	public void AliasedFieldRefPassedAsOwner_FailsWithLT010()
	{
		// Правило 7: владеющую ссылку с алиасом в refb-поле класса нельзя передавать как владение.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT010_AliasedFieldRefPassedAsOwner.cev", "LT010");
	}

	[Fact]
	public void UseAfterExchangeAcrossBranches_FailsWithLT001()
	{
		// Правило 6: деинициализация ('<-') в if-ветке распространяется на всё условие.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT001_UseAfterExchangeAcrossBranches.cev", "LT001");
	}

	[Fact]
	public void DoubleMoveAfterBranchMerge_FailsWithLT001()
	{
		// Правило 6: переназначение в else-ветке не «оживляет» переменную,
		// деинициализированную в if-ветке (двойной перенос владения запрещён).
		TestAssertions.AssertCompilationFails(
			_compiler, "LT001_DoubleMoveAfterBranchMerge.cev", "LT001");
	}

	[Fact]
	public void OwnerIntoRefbParamField_FailsWithLT002()
	{
		// Правило 8: refb-параметр живёт «бесконечно долго», как и его поля;
		// локальный владелец живёт короче и не может туда попасть.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT002_OwnerIntoRefbParamField.cev", "LT002");
	}

	[Fact]
	public void InnerOwnerIntoBorrowField_FailsWithLT002()
	{
		// Правило 4: лайфтайм поля = лайфтайм объекта; объект внутреннего блока
		// не может попасть в refb-поле объекта из внешнего блока.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT002_InnerOwnerIntoBorrowField.cev", "LT002");
	}

	[Fact]
	public void RefbParamIntoOwnerParam_FailsWithLT003()
	{
		// Правило 2: заимствованную (refb) ссылку нельзя передавать во владеющий (ref) параметр.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT003_RefbParamIntoOwnerParam.cev", "LT003");
	}

	[Fact]
	public void OwnerFieldIntoOwnerParam_FailsWithLT004()
	{
		// Правило: владеющую (ref) ссылку с поля класса можно снять только через '<-',
		// но не передачей поля во владеющий параметр.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT004_OwnerFieldIntoOwnerParam.cev", "LT004");
	}

	[Fact]
	public void ExchangeStripShorterReceiver_FailsWithLT007()
	{
		// Правило: получатель, рождённый во внутреннем блоке, живёт короче объекта,
		// с поля которого снимается ссылка через '<-' => LT007.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT007_ExchangeStripShorterReceiver.cev", "LT007");
	}

	[Fact]
	public void AnonymousNewIntoBorrow_FailsWithLT009()
	{
		// Правило: анонимный 'new' можно присваивать только во владеющую (ref) ссылку.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT009_AnonymousNewIntoBorrow.cev", "LT009");
	}

	[Fact]
	public void MoveAfterRefbFieldAlias_FailsWithLT010()
	{
		// Правило 7: владеющую ссылку с алиасом (задаваемым через refb-поле класса)
		// нельзя передавать как владение.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT010_MoveAfterRefbFieldAlias.cev", "LT010");
	}

	[Fact]
	public void LifetimesThisArgShorterThanReceiver_FailsWithLT002()
	{
		// Связь 'b ~ this': receiver живёт во внешнем блоке, значение — во внутреннем.
		// Значение живёт короче объекта, в чьё поле оно попадёт => LT002 на стороне вызова.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT002_LifetimesThisArgShorterThanReceiver.cev", "LT002");
	}

	[Fact]
	public void LifetimesReturnArgShorterThanReceiver_FailsWithLT002()
	{
		// Связь 'source ~ return': получатель результата живёт дольше источника
		// из внутреннего блока => LT002 на стороне вызова.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT002_LifetimesReturnArgShorterThanReceiver.cev", "LT002");
	}

	[Fact]
	public void LifetimesReturnOwnerArgShorterThanReceiver_FailsWithLT002()
	{
		// Та же проверка '~ return', но для owner-ref (ref) аргумента:
		// результат нельзя сохранять в более долгоживущую ссылку => LT002.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT002_LifetimesReturnOwnerArgShorterThanReceiver.cev", "LT002");
	}

	[Fact]
	public void CtorEmptyLifetimesOwnerArgInField_FailsWithLT002()
	{
		// Пустая секция lifetimes () отключает автогенерацию 'data ~ this' для
		// конструктора: без связи owner-аргумент нельзя положить в поле this (LT002).
		TestAssertions.AssertCompilationFails(
			_compiler, "LT002_CtorEmptyLifetimesOwnerArgInField.cev", "LT002");
	}

	[Fact]
	public void CtorThisArgShorterThanReceiver_FailsWithLT002()
	{
		// Связь 'data ~ this' в конструкторе: this принадлежит переменной внешнего
		// блока, значение создаётся во внутреннем и живёт короче => LT002.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT002_CtorThisArgShorterThanReceiver.cev", "LT002");
	}

	[Fact]
	public void ReturnFieldBorrowWithoutReturnThis_FailsWithLT005()
	{
		// Правило 7.6: возврат refb-поля (лайфтайм this) без связи 'return ~ this'
		// дал бы результат, переживающий объект => LT005.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT005_ReturnFieldBorrowWithoutReturnThis.cev", "LT005");
	}

	[Fact]
	public void PassLinkedOwnerAfterExchange_FailsWithLT011()
	{
		// Обмен '<-' через поле связывает владельца с получателем через LinkVars;
		// передавать такой владелец как владение в область с другим временем жизни
		// нельзя (CheckLinkedVarsLifetimesError) => LT011.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT011_PassLinkedOwnerAfterExchange.cev", "LT011");
	}

	[Fact]
	public void ReturnLinkedObjAfterExchange_FailsWithLT012()
	{
		// Возвращаемый владелец связан через LinkVars с получателем '<-' => LT012.
		TestAssertions.AssertCompilationFails(
			_compiler, "LT012_ReturnLinkedObjAfterExchange.cev", "LT012");
	}

	[Fact]
	public void ReturnObjWithUnlinkedBorrowFieldAlias_FailsWithLT013()
	{
		// refb-аргумент, записанный в refb-поле возвращаемого объекта, остаётся
		// алиасом obj (цепочка SetAliase поднимается по Parent); лайфтайм параметра
		// короче return => возвращать obj нельзя (LT013).
		TestAssertions.AssertCompilationFails(
			_compiler, "LT013_ReturnObjWithUnlinkedBorrowFieldAlias.cev", "LT013");
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}