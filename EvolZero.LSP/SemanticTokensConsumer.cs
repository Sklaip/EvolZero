using EvolZero.Core;
using System.Collections.Concurrent;

namespace EvolZero.LSP
{
    internal class SemanticTokensConsumer : ISemanticTokensConsumer
    {
        private ConcurrentDictionary<string, List<(PositionInSources pos, SemanticTokenType tokenType)>> _positions = new();

        public void Push(PositionInSources pos, SemanticTokenType tokenType)
        {
            if (!_positions.TryGetValue(pos.SourceFile, out var positions))
            {
                positions = new List<(PositionInSources pos, SemanticTokenType tokenType)>();
                _positions[pos.SourceFile] = positions;
            }
            positions.Add((pos, tokenType));
        }

        public IReadOnlyList<(PositionInSources pos, SemanticTokenType tokenType)> GetPositions(string file)
        {
            if (_positions.TryGetValue(file, out var positions))
            {
                // TODO: поставить флаг чтобы постоянно не вызывать сортировку и сделать через Sort
                return positions.OrderBy(x => x.pos.Line).ThenBy(x => x.pos.Symbol).ToList();
            }

            return new List<(PositionInSources pos, SemanticTokenType tokenType)>();
        }

        public void Clear(string file)
        {
            if (_positions.TryGetValue(file, out var positionsList))
            {
                positionsList.Clear();
            }
        }
    }
}
