using Tests.Infrastructure;
using Xunit;

namespace Tests;

/// <summary>
/// Интеграционные тесты компилятора CEvol.
/// Каждый тест компилирует .cev исходники через реальный компилятор (Compiler.Execute)
/// в изолированную временную папку, запускает полученный исполняемый файл в этой папке
/// (чтобы относительные файловые пути разрешались корректно) и проверяет поведение:
/// вывод в stdout, коды возврата и запись в файлы.
/// </summary>
/// <remarks>
/// Для корректного запуска тестов на машине должны быть установлены:
/// .NET SDK 10, а также clang в PATH (используется линковщиком компилятора).
/// Тесты рассчитаны на Windows-окружение разработки (в проекте используются
/// libLLVM.runtime.win-x64 и флаг линковки -llegacy_stdio_definitions).
/// </remarks>
public sealed class CompilerTest : IDisposable
{
	private readonly CompilerRunner _compiler = new();

	[Fact]
	public void Program_CompilesRunsWritesFile_AndPrintsExpectedOutput()
	{
		string exePath = _compiler.Compile(["Program.cev", "File.cev", "String.cev"]);

		// Программа читает строку со стандартного ввода (scanf), выводит её в консоль
		// и пишет её в файл popa.txt (относительный путь -> нужна рабочая папка).
		const string inputLine = "Hello CEvol Test";

		var result = ExecutableRunner.Run(
			exePath,
			workingDirectory: _compiler.WorkDir,
			standardInput: inputLine + "\n");

		Assert.False(result.TimedOut, "Программа превысила таймаут.");
		Assert.Equal(0, result.ExitCode);

		Assert.Contains("aboba111", result.CombinedOutput);
		Assert.Contains("LALALALALALA", result.CombinedOutput);

		// structure.Num = 10 + 15 = 25, выводится через printf("%d\n", ...)
		Assert.Contains("25", result.CombinedOutput);

		// bigArray[1][3] = 7, выводится через printf("%d", ...)
		Assert.Contains("7", result.CombinedOutput);

		// Прочитанная со входа строка выводится обратно в консоль.
		Assert.Contains(inputLine, result.CombinedOutput);

		// И та же строка записывается в файл popa.txt (File.Write + strlen).
		string outputFilePath = Path.Combine(_compiler.WorkDir, "popa.txt");
		Assert.True(File.Exists(outputFilePath), $"Файл {outputFilePath} не был создан.");
		Assert.Equal(inputLine, File.ReadAllText(outputFilePath));
	}

	[Fact]
	public void Test2_CompilesRuns_AndWritesExpectedFile()
	{
		string exePath = _compiler.Compile(["test2.cev", "String.cev", "File.cev"]);

		var result = ExecutableRunner.Run(exePath, workingDirectory: _compiler.WorkDir);

		Assert.False(result.TimedOut, "Программа превысила таймаут.");
		Assert.Equal(0, result.ExitCode);

		// File.Write(ref globalStrTets, 4) записывает первые 4 байта строки "aboba" -> "abob".
		string outputFilePath = Path.Combine(_compiler.WorkDir, "popa.txt");
		Assert.True(File.Exists(outputFilePath), $"Файл {outputFilePath} не был создан.");
		Assert.Equal("abob", File.ReadAllText(outputFilePath));

		// connTest.ConnType = 500, затем SetConnectType(10) -> 510, выводится PrintConnectType().
		Assert.Contains("510", result.CombinedOutput);
	}

	[Fact]
	public void Arithmetic_IntegerOperations_ReturnZero_Runs()
	{
		string exePath = _compiler.Compile(["Arithmetic.cev"]);

		var result = ExecutableRunner.Run(exePath, workingDirectory: _compiler.WorkDir);

		Assert.False(result.TimedOut, "Программа превысила таймаут.");
		Assert.Equal(0, result.ExitCode);
	}

	[Fact]
	public void ReturnValue_TreatedAsProcessExitCode()
	{
		string exePath = _compiler.Compile(["ReturnValue.cev"]);

		var result = ExecutableRunner.Run(exePath, workingDirectory: _compiler.WorkDir);

		Assert.False(result.TimedOut, "Программа превысила таймаут.");
		// main возвращает 5 - 3 = 2; это значение становится кодом возврата процесса.
		Assert.Equal(2, result.ExitCode);
	}

	[Fact]
	public void CompileError_ThrowsAndDoesNotProduceExecutable()
	{
		// Broken.cev ссылается на неопределённую переменную -> ошибка компиляции,
		// исполняемый файл не должен создаваться.
		var ex = Assert.Throws<CompilationFailedException>(() => _compiler.Compile(["Broken.cev"]));

		Assert.False(File.Exists(ex.OutputPath), "При ошибке компиляции не должен создаваться exe.");
	}

	public void Dispose()
	{
		_compiler.Dispose();
		GC.SuppressFinalize(this);
	}
}
