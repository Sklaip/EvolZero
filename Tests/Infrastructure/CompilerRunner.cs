using EvolZero;

namespace Tests.Infrastructure;

/// <summary>
/// Оркестрирует запуск компилятора CEvol в изолированной временной рабочей папке,
/// чтобы артефакты компиляции (output.o/output.obj, итоговый exe) не пересекались между тестами.
/// </summary>
public sealed class CompilerRunner : IDisposable
{
	private readonly string _workDir;
	private readonly string _sourceDir;
	private readonly string _originalWorkingDirectory;
	private bool _disposed;

	/// <summary>Рабочая папка, в которую кладутся объектный файл и исполняемый файл.</summary>
	public string WorkDir => _workDir;

	/// <summary>Папка с исходными .cev файлами (копия cvol_temp из вывода сборки).</summary>
	public string SourceDir => _sourceDir;

	public CompilerRunner(string? workDir = null)
	{
		_workDir = workDir ?? Path.Combine(Path.GetTempPath(), "cevol_tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_workDir);

		_sourceDir = Path.Combine(AppContext.BaseDirectory, "cevol_temp");
		if (!Directory.Exists(_sourceDir))
		{
			_sourceDir = FindProjectSourceDir();
		}

		_originalWorkingDirectory = Directory.GetCurrentDirectory();
	}

	/// <summary>Полный путь к исходному файлу по имени (например "Program.cev").</summary>
	public string PathToSource(string sourceFileName)
	{
		var path = Path.Combine(_sourceDir, sourceFileName);
		if (!File.Exists(path))
		{
			throw new FileNotFoundException($"Исходный файл CEvol не найден: {path}", path);
		}

		return path;
	}

	/// <summary>
	/// Компилирует переданные исходные файлы в исполняемый файл внутри рабочей папки.
	/// Возвращает полный путь к собранному исполняемому файлу.
	/// Бросает <see cref="CompilationFailedException"/>, если компиляция не дала исполняемый файл.
	/// </summary>
	public string Compile(IEnumerable<string> sourceFileNames, string exeName = "program")
	{
		var sourcePaths = sourceFileNames.Select(PathToSource).ToList();
		var exePath = Path.Combine(_workDir, exeName + TestEnvironment.ExecutableExtension);

		string compileOutput = RunCompiler(sourcePaths, exePath);

		if (!File.Exists(exePath))
		{
			var message = string.IsNullOrWhiteSpace(compileOutput)
				? "Компилятор не создал исполняемый файл, но не вывел сообщений об ошибках."
				: compileOutput;
			throw new CompilationFailedException(message, exePath);
		}

		return exePath;
	}

	private string RunCompiler(List<string> sourcePaths, string exePath)
	{
		var originalOut = Console.Out;
		var originalError = Console.Error;
		var writer = new StringWriter();

		try
		{
			Directory.SetCurrentDirectory(_workDir);

			Console.SetOut(writer);
			Console.SetError(writer);

			var compiler = new Compiler();
			compiler.Execute(sourcePaths, exePath);

			writer.Flush();
			return writer.ToString();
		}
		finally
		{
			Console.SetOut(originalOut);
			Console.SetError(originalError);
			Directory.SetCurrentDirectory(_originalWorkingDirectory);
		}
	}

	private static string FindProjectSourceDir()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null)
		{
			var candidate = Path.Combine(dir.FullName, "cevol_temp");
			if (Directory.Exists(candidate))
			{
				return candidate;
			}

			dir = dir.Parent;
		}

		throw new DirectoryNotFoundException(
			"Не удалось найти папку cevol_temp с исходными файлами для тестов.");
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		Directory.SetCurrentDirectory(_originalWorkingDirectory);

		if (Directory.Exists(_workDir))
		{
			Directory.Delete(_workDir, recursive: true);
		}

		GC.SuppressFinalize(this);
	}
}

public sealed class CompilationFailedException : Exception
{
	public string OutputPath { get; }

	public CompilationFailedException(string message, string outputPath) : base(message)
	{
		OutputPath = outputPath;
	}
}
