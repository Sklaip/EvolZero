using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Интерактивные проверки вложенных циклов while. Число n читается из консоли
/// и задаёт границы всех циклов:
///   n=0 -> ни один из вложенных циклов не выполняется (ветка "не выполняется"),
///   n=3 -> все вложенные циклы выполняются (ветка "выполняется").
/// </summary>
public sealed class NestedLoopsTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Theory]
	// n=0: все суммы/счётчики равны 0
	// n=3: total=(1+2+3)^2=36, runs=3*3=9, triple=3^3=27,
	//       acc= (1)+(1+2)+(1+2+3)=10, pairs=1+2+3=6
	[InlineData(0, "0\n0\n0\n0\n0")]
	[InlineData(3, "36\n9\n27\n10\n6")]
	public void NestedLoops_EachInputCoversBranches(int input, string expectedOutput)
	{
		string exePath = _compiler.Compile(["NestedLoops.cev"]);

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