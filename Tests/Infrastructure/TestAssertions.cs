using System.Text.RegularExpressions;

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
	/// а список ошибок компилятора чётко совпадает с ожидаемым: те же коды, то же количество
	/// и тот же порядок (например "error LT002"). Любая лишняя, неверная или иначе
	/// упорядоченная ошибка приводит к падению теста.
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

		var actualCodes = ExtractErrorCodes(ex.Message);
		Assert.True(expectedErrorCodes.SequenceEqual(actualCodes),
			"Набор ошибок компиляции не совпал с ожидаемым.\n" +
			$"Ожидалось: [{string.Join(", ", expectedErrorCodes)}]\n" +
			$"Получено:  [{string.Join(", ", actualCodes)}]");

		return ex;
	}

	/// <summary>
	/// Извлекает коды всех ошибок из сообщения компилятора в порядке их появления.
	/// Матч идёт только по заголовочным строкам вида "[файл](строка,кол): error КОД [Слой]:",
	/// поэтому комментарии в коде вида "// LT001" и сниппеты исходников не учитываются.
	/// </summary>
	private static string[] ExtractErrorCodes(string compilerOutput)
	{
		const string pattern = @"^\[[^\]]*\]\(\d+,\d+\): error (?<code>[A-Za-z0-9_]+) \[[^\]]+\]:";

		return Regex.Matches(compilerOutput, pattern, RegexOptions.Multiline)
			.Select(x => x.Groups["code"].Value)
			.ToArray();
	}
}
