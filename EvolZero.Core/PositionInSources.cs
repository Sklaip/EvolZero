using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace EvolZero.Core
{
	public struct PositionInSources
	{
		public readonly string SourceFile;
		public readonly int Line;
		public readonly int Symbol;
		public readonly int EndSymbol;

        public PositionInSources(string sourceFile, int line, int symbol, int endSymbol) : this()
		{
			SourceFile = sourceFile;
			Line = line;
			Symbol = symbol;
            EndSymbol = endSymbol;
        }
    }
}
