using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Проверка анализа времени жизни ссылок в конструкциях if/else:
/// ссылка, владение которой передано в ветке if, должна оставаться доступной
/// в ветке else (там она ещё жива), но после всей конструкции if/else она уже
/// деинициализированна и использовать её нельзя.
/// </summary>
public sealed class RefsInIfElseTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void RefDestructedInIf_IsUsableInElseBranch()
	{
		// cond==true: берётся ветка if, a забирает объект b; c не тронут.
		string exePath = _compiler.Compile(["RefsInIfElse.cev"]);

		string[] expectedLines =
		{
			"2",  // a.Value после ref a = b
			"3",  // c.Value
		};

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, expectedLines);
	}

	[Fact]
	public void RefDestructedInBothBranches_NotUsableAfterIfElse_ThrowsCompilationError()
	{
		// Использование b после if/else невозможна в любой ветке -> ошибка компиляции.
		var ex = Assert.Throws<CompilationFailedException>(
			() => _compiler.Compile(["RefsInIfElse_UseAfterIf.cev"]));

		Assert.False(File.Exists(ex.OutputPath), "При ошибке компиляции не должен создаваться exe.");
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}