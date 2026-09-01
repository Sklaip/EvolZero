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
}
