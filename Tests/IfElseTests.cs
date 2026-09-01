using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Интерактивные проверки базовых условных конструкций: if, if/else,
/// цепочки if/else if/else. Число n читается из консоли; разные входы
/// покрывают все ветки ветвлений.
/// </summary>
public sealed class IfElseTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Theory]
	// n=0: a: if(n==0) взята;     b: else;          c: первый else if (10)
	// n=1: a: if не взята (0);    b: if;            c: второй else if (20)
	// n=2: a: if не взята (0);    b: else;          c: третий else if (30)
	// n=3: a: if не взята (0);    b: else;          c: конечный else (40)
	[InlineData(0, "1\n2\n10")]
	[InlineData(1, "0\n1\n20")]
	[InlineData(2, "0\n2\n30")]
	[InlineData(3, "0\n2\n40")]
	public void IfElse_EachInputCoversBranches(int input, string expectedOutput)
	{
		string exePath = _compiler.Compile(["IfElse.cev"]);

		string[] expectedLines = expectedOutput.Split('\n');

		TestAssertions.AssertOutputLines(
			exePath,
			_compiler.WorkDir,
			expectedLines,
			standardInput: input + "\n");
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}