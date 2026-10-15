using EvolZero.Core.LogicModels.Expressions;

namespace EvolZero.Core.Analysis.Semantic
{
	public class ValueMeta
	{
		public Expression Expr { get; set; }
		public VarMeta? VarData { get; set; }
		public bool IsLocal { get; set; }
		public bool IsAnonymous => VarData == null;
		public bool IsStrippedViaExchange { get; set; }
	}
}
