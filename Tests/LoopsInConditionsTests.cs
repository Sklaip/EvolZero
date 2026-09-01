using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Интерактивные проверки циклов while внутри веток if/else/else if.
/// Число n читается из консоли; разные значения покрывают все ветки:
///   n=0 -> тест1: if не взят; тест2: if-ветка (цикл 0 итераций);
///          тест3: else; тест4: if не взят.
///   n=3,5 -> тест1: if взят (цикл выполняется); тест2: else-ветка;
///            тест3: n=5 -> else if-ветка с циклом; n=3 -> else;
///            тест4: if взят (цикл копит значение).
///   n=99 -> тест3: if-ветка (без цикла).
/// </summary>
public sealed class LoopsInConditionsTests : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Theory]
	// n=0:  acc1=0,  acc2=100, v=30,  base=7
	// n=3:  acc1=30, acc2=102, v=30,  base=13
	// n=5:  acc1=50, acc2=102, v=5,   base=17
	// n=99: acc1=990,acc2=102, v=10,  base=205
	[InlineData(0, "0\n100\n30\n7")]
	[InlineData(3, "30\n102\n30\n13")]
	[InlineData(5, "50\n102\n5\n17")]
	[InlineData(99, "990\n102\n10\n205")]
	public void LoopsInConditions_EachInputCoversBranches(int input, string expectedOutput)
	{
		string exePath = _compiler.Compile(["LoopsInConditions.cev"]);

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