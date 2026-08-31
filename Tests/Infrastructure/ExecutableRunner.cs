using System.Diagnostics;
using System.Text;

namespace Tests.Infrastructure;

/// <summary>
/// Результат запуска скомпилированного исполняемого файла.
/// </summary>
public sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut)
{
	public string CombinedOutput =>
		StandardError.Length == 0 ? StandardOutput : StandardOutput + "\n" + StandardError;
}

/// <summary>
/// Запускает скомпилированный исполняемый файл с заданной рабочей папкой и входными данными
/// на стандартный ввод, собирает stdout/stderr и код возврата.
/// </summary>
public static class ExecutableRunner
{
	public static ProgramRunResult Run(
		string executablePath,
		string workingDirectory,
		string? standardInput = null,
		IEnumerable<string>? arguments = null,
		TimeSpan? timeout = null)
	{
		if (!File.Exists(executablePath))
		{
			throw new FileNotFoundException($"Исполняемый файл не найден: {executablePath}", executablePath);
		}

		timeout ??= TimeSpan.FromSeconds(60);

		var psi = new ProcessStartInfo
		{
			FileName = executablePath,
			WorkingDirectory = workingDirectory,
			UseShellExecute = false,
			RedirectStandardInput = standardInput != null,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
		};

		if (arguments != null)
		{
			foreach (var arg in arguments)
			{
				psi.ArgumentList.Add(arg);
			}
		}

		using var process = new Process { StartInfo = psi };

		var stdout = new StringBuilder();
		var stderr = new StringBuilder();

		process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
		process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

		process.Start();

		if (standardInput != null)
		{
			process.StandardInput.Write(standardInput);
			process.StandardInput.Close();
		}

		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		bool timedOut = !process.WaitForExit((int)timeout.Value.TotalMilliseconds);
		if (timedOut)
		{
			process.Kill(entireProcessTree: true);
			process.WaitForExit();
		}

		// Дожидаемся полного сброса асинхронного чтения вывода.
		process.WaitForExit();

		return new ProgramRunResult(process.ExitCode, stdout.ToString(), stderr.ToString(), timedOut);
	}
}
