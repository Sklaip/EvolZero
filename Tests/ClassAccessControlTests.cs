using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Тонкая проверка модификаторов доступа (public/private) в классах:
/// обращение к private-полю/методу/конструктору извне должно давать ошибку
/// компиляции, а доступ к public-членам извне и к private-членам внутри класса
/// должен успешно компилироваться и работать.
/// </summary>
public sealed class ClassAccessControlTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void PrivateField_AccessedFromOutside_ThrowsCompilationError()
	{
		// ACC001: private-поле доступно только внутри своего класса.
		var ex = Assert.Throws<CompilationFailedException>(
			() => _compiler.Compile(["ClassAccessControl_Acc001_PrivateField.cev"]));

		Assert.False(File.Exists(ex.OutputPath), "При ошибке компиляции не должен создаваться exe.");
	}

	[Fact]
	public void PrivateMethod_CalledFromOutside_ThrowsCompilationError()
	{
		// ACC002: private-метод доступен только внутри своего класса.
		var ex = Assert.Throws<CompilationFailedException>(
			() => _compiler.Compile(["ClassAccessControl_Acc002_PrivateMethod.cev"]));

		Assert.False(File.Exists(ex.OutputPath), "При ошибке компиляции не должен создаваться exe.");
	}

	[Fact]
	public void PrivateConstructor_CalledFromOutside_ThrowsCompilationError()
	{
		// ACC003: private-конструктор нельзя вызывать извне класса.
		var ex = Assert.Throws<CompilationFailedException>(
			() => _compiler.Compile(["ClassAccessControl_Acc003_PrivateCtor.cev"]));

		Assert.False(File.Exists(ex.OutputPath), "При ошибке компиляции не должен создаваться exe.");
	}

	[Fact]
	public void PublicAccessAndInClassPrivateAccess_RunSuccessfully()
	{
		// Контрольный тест: public-поле/метод доступны извне, private-члены
		// доступны внутри класса. Программа должна компилироваться и работать.
		string exePath = _compiler.Compile(["ClassAccess.cev"]);

		string[] expectedLines =
		{
			"10",  // public Open = 10
			"15",  // Combine() = Hidden(5) + Open(10)
			"10",  // другой класс читает public Open
			"13",  // item.Open = item2.Open(3) + item.Open(10)
		};

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, expectedLines);
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}
