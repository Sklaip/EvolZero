using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Интерактивные проверки вложенных условий. Число n читается из консоли;
/// разные значения n покрывают:
///  r1: n>0 и n>5 (внутренний if/else), else внешнего
///  r2: n==0, then n==1 внутри else, else внутреннего
///  r3: три уровня (n>0, n>5, n==7/else)
///  r4: else if (n>1) с вложенным if (n==3/else), и else
///  r5: логическое И (n>0 && n<5)
///  r6: цепочка else if + конечный else
/// </summary>
public sealed class NestedConditionsTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Theory]
	// n=0: r1=102(else),  r2=200,      r3=0,  r4=400,        r5=501(&& ложь), r6=600
	// n=1: r1=101,        r2=201,      r3=0,  r4=403,        r5=500,          r6=601
	// n=2: r1=101,        r2=202,      r3=0,  r4=402(else),  r5=500,          r6=602
	// n=3: r1=101,        r2=202,      r3=0,  r4=401(n==3),  r5=500,          r6=603
	// n=6: r1=100(n>5),   r2=202,      r3=301, r4=402,       r5=501,          r6=603
	// n=7: r1=100,        r2=202,      r3=300(n==7), r4=402, r5=501,          r6=603
	[InlineData(0, "102\n200\n0\n400\n501\n600")]
	[InlineData(1, "101\n201\n0\n403\n500\n601")]
	[InlineData(2, "101\n202\n0\n402\n500\n602")]
	[InlineData(3, "101\n202\n0\n401\n500\n603")]
	[InlineData(6, "100\n202\n301\n402\n501\n603")]
	[InlineData(7, "100\n202\n300\n402\n501\n603")]
	public void NestedConditions_EachInputCoversBranches(int input, string expectedOutput)
	{
		string exePath = _compiler.Compile(["NestedConditions.cev"]);

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