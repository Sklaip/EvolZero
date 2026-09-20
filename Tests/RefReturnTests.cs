using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Проверка анализа времени жизни возвращаемых ссылок: при возврате локальной
/// владеющей ссылки (return ref a) её деструкт перед return вызываться не должен,
/// владение передаётся вызывающему коду.
/// </summary>
public sealed class RefReturnTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void ReturnedRef_IsNotDestructedBeforeReturn()
	{
		string exePath = _compiler.Compile(["ReturnRef.cev"]);

		string[] expectedLines =
		{
			"42",  // r.X из объекта, созданного в CreateTestObj
			"43",  // r.X после инкремента через возвращённую ссылку
		};

		TestAssertions.AssertOutputLines(exePath, _compiler.WorkDir, expectedLines);
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}