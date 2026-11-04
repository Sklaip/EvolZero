using EvolZero.Core.LogicModels.Expressions;

namespace EvolZero.Core.Analysis.Semantic
{
	public class ValueMeta
	{
		//VarData и MayReferTo не должны одновременно иметь значение. Либо, либо другое

		public Expression Expr { get; set; }
		public VarMeta? VarData { get; set; }
		public bool IsLocal { get; set; }
		public bool IsAnonymous => VarData == null;
		public bool IsStrippedViaExchange { get; set; }
	}
}
