using Antlr4.Runtime;
using EvolZero.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Parsing.Parsing
{
    public class SyntaxErrorsConsumer : BaseErrorListener
    {
        private readonly ErrorsBag _errorsBag;
        private readonly string _file;

        public SyntaxErrorsConsumer(ErrorsBag errorsBag, string file)
        {
            _errorsBag = errorsBag;
            _file = file;
        }

        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, 
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var pos = new PositionInSources(_file, line, charPositionInLine, charPositionInLine);
            _errorsBag.AddError("Parser", "SyntaxError", $"{msg}", pos);
        }
    }
}
