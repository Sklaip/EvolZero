using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Интерактивные проверки циклов while. Число n читается из консоли:
///   n=0 -> во всех циклах условие сразу ложно (0 итераций),
///   n=4 -> все циклы выполняют итерации.
/// Оба входа покрывают обе ветки каждого цикла "тело выполняется / не выполняется".
/// </summary>
public sealed class WhileLoopsTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Theory]
	// n=0: все циклы дают 0 итераций
	// n=4: count=4, sum=1+2+3+4=10, ran(всегда 0), k=4, evens(0,2 считаются)=2
	[InlineData(0, "0\n0\n0\n0\n0")]
	[InlineData(4, "4\n10\n0\n4\n2")]
	public void WhileLoops_EachInputCoversBranches(int input, string expectedOutput)
	{
		string exePath = _compiler.Compile(["WhileLoops.cev"]);

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