namespace Tests.Infrastructure;

/// <summary>
/// Хелпер для интеграционных тестов: запускает скомпилированный exe
/// и сравнивает stdout с ожидаемым построчным выводом (с нормализацией CRLF/LF).
/// </summary>
public static class TestAssertions
{
	/// <summary>
	/// Запускает exe в рабочей папке и сравнивает строки стандартного вывода
	/// с ожидаемым списком строк (без учёта переводов строк CRLF/LF).
	/// </summary>
	public static ProgramRunResult AssertOutputLines(
		string exePath,
		string workingDirectory,
		string[] expectedLines,
		string? standardInput = null)
	{
		var result = ExecutableRunner.Run(
			exePath,
			workingDirectory: workingDirectory,
			standardInput: standardInput);

		Assert.False(result.TimedOut, "Программа превысила таймаут.");
		Assert.Equal(0, result.ExitCode);

		var actualLines = NormalizeLines(result.StandardOutput);
		Assert.Equal(expectedLines, actualLines);

		return result;
	}

	/// <summary>Разбивает вывод на строки, игнорируя CRLF/LF и пустые строки.</summary>
	public static string[] NormalizeLines(string output)
	{
		return output
			.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
			.ToArray();
	}

	/// <summary>
	/// Проверяет, что компиляция исходника завершилась ошибкой: исполняемый файл не создан,
	/// а сообщения компилятора содержат ожидаемые коды ошибок (например "error LT002").
	/// Если компилятор упал в служебное исключение (NotImplementedException), сообщение
	/// окажется пустым и проверка кода провалится.
	/// </summary>
	public static CompilationFailedException AssertCompilationFails(
		CompilerRunner compiler,
		string sourceFile,
		params string[] expectedErrorCodes)
	{
		var ex = Assert.Throws<CompilationFailedException>(
			() => compiler.Compile(new[] { sourceFile }));

		Assert.False(File.Exists(ex.OutputPath), "При ошибке компиляции не должен создаваться exe.");

		foreach (var code in expectedErrorCodes)
		{
			Assert.Contains($"error {code}", ex.Message);
		}

		return ex;
	}
}
