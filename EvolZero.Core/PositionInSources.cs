using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core
{
	public struct PositionInSources
	{
		public readonly string SourceFile;
		public readonly int Line;
		public readonly int Symbol;

		public PositionInSources(string sourceFile, int line, int symbol) : this()
		{
			SourceFile = sourceFile;
			Line = line;
			Symbol = symbol;
		}
	}
}
