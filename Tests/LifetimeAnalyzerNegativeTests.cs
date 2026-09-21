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

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}