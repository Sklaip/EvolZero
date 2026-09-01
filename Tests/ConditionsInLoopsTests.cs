using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Интерактивные проверки условий внутри циклов while. Число n читается из
/// консоли и управляет как границами циклов, так и условиями внутри:
///   n=0,3 -> ветка тест1 "val > 0" и ложная ветка, тест2 и if и else,
///            тест3 "n == 5" ложно;
///   n=5   -> тест3 "n == 5" истинно (ранний выход из цикла).
/// </summary>
public sealed class ConditionsInLoopsTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Theory]
	// n=0: sum=0 (цикл не идёт), evens=0, odds=0, acc=15, j=6
	// n=3: sum=(1..2 из i-n)=3, evens=2, odds=1, acc=36, j=9
	// n=5: sum=(1..4 из i-n)=10, evens=3, odds=2, acc=1 (ранний выход), j=2
	[InlineData(0, "0\n0\n0\n15\n6")]
	[InlineData(3, "3\n2\n1\n36\n9")]
	[InlineData(5, "10\n3\n2\n1\n2")]
	public void ConditionsInLoops_EachInputCoversBranches(int input, string expectedOutput)
	{
		string exePath = _compiler.Compile(["ConditionsInLoops.cev"]);

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