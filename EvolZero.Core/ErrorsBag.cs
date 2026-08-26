using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core
{
	public class ErrorsBag
	{
		struct Error
		{
			public string Layer;
			public string ErrorCode;
			public string Comment;
			public PositionInSources Pos;
		}

		public bool HasErrors => _errors.Count > 0;

		private List<Error> _errors = new();

		private Dictionary<string, string> _sources;

		public ErrorsBag(Dictionary<string, string> sources)
		{
			_sources = sources;
		}

		public void AddError(string layer, string errorCode, string comment, PositionInSources pos)
		{
			_errors.Add(new Error()
			{
				Layer = layer,
				ErrorCode = errorCode,
				Comment = comment,
				Pos = pos
			});
		}

		public string BuildErrorsMessage()
		{
			if (_errors.Count == 0)
				return string.Empty;

			var parsedSources = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
			foreach (var (fileName, content) in _sources)
			{
				parsedSources[fileName] = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
			}

			var builder = new StringBuilder();

			for (int i = 0; i < _errors.Count; i++)
			{
				var err = _errors[i];

				builder.AppendLine($"[{err.Pos.SourceFile}]({err.Pos.Line},{err.Pos.Symbol}): error {err.ErrorCode} [{err.Layer}]: {err.Comment}");

				if (parsedSources.TryGetValue(err.Pos.SourceFile, out var lines) &&
					err.Pos.Line > 0 && err.Pos.Line <= lines.Length)
				{
					string sourceLine = lines[err.Pos.Line - 1];

					// Определяем количество начальных пробелов/табуляций
					int leadingWhitespaceCount = 0;
					while (leadingWhitespaceCount < sourceLine.Length && char.IsWhiteSpace(sourceLine[leadingWhitespaceCount]))
					{
						leadingWhitespaceCount++;
					}

					// Обрезаем начальные отступы
					string trimmedSourceLine = sourceLine.Substring(leadingWhitespaceCount);
					string printableLine = trimmedSourceLine.Replace("\t", "    ");

					string lineNumStr = err.Pos.Line.ToString();
					string margin = new string(' ', lineNumStr.Length);

					builder.AppendLine($"{margin} |");
					builder.AppendLine($"{lineNumStr} | {printableLine}");

					// Рассчитываем смещение с автокоррекцией позиции до ближайшего видимого символа
					int pointerOffset = CalculatePointerOffset(sourceLine, leadingWhitespaceCount, err.Pos.Symbol);
					string pointerPadding = new string(' ', pointerOffset);

					builder.AppendLine($"{margin} | {pointerPadding}^");
				}

				if (i < _errors.Count - 1)
				{
					builder.AppendLine();
				}
			}

			return builder.ToString();
		}

		/// <summary>
		/// Рассчитывает смещение указателя с учетом подрезки строки и пропуска пробелов перед токеном.
		/// </summary>
		private static int CalculatePointerOffset(string originalLine, int leadingWhitespaceCount, int symbolPosition)
		{
			if (string.IsNullOrEmpty(originalLine) || leadingWhitespaceCount >= originalLine.Length)
				return 0;

			// Переводим 1-based символ в 0-based индекс
			int targetIndex = Math.Min(Math.Max(0, symbolPosition), originalLine.Length - 1);

			// Коррекция: Если targetIndex попал на пробел/табуляцию, смещаем его вправо до первого печатного символа
			while (targetIndex < originalLine.Length && char.IsWhiteSpace(originalLine[targetIndex]))
			{
				targetIndex++;
			}

			// Если вся оставшаяся часть строки состоит из пробелов — ставим указатель в конец подрезанной строки
			if (targetIndex >= originalLine.Length)
			{
				targetIndex = originalLine.Length - 1;
			}

			// Если токен находится в пределах вырезанного начального отступа — указываем на самый первый символ
			if (targetIndex <= leadingWhitespaceCount)
			{
				return 0;
			}

			// Считаем визуальную ширину от первого видимого символа до исправленной позиции токена
			int offset = 0;
			for (int i = leadingWhitespaceCount; i < targetIndex; i++)
			{
				if (originalLine[i] == '\t')
					offset += 4; // Учитываем ширину табуляции
				else
					offset += 1;
			}

			return offset;
		}
	}
}