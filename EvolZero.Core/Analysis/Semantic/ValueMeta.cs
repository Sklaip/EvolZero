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

		private List<VarMeta>? _mayReferTo;
		public IReadOnlyList<VarMeta>? MayReferTo => _mayReferTo;

		public void FillRefs(VarMeta varData)
		{
			if (_mayReferTo == null) return;
			_mayReferTo.ForEach(x => x.SetAliase(varData));
		}

		public void AddReferTo(ValueMeta valueMeta)
		{
			if (VarData != null)
			{
				if (valueMeta.VarData != null)
				{
					valueMeta.VarData.SetAliase(VarData);					
				}
				else if (valueMeta._mayReferTo != null)
				{
					valueMeta._mayReferTo.ForEach(VarData.SetAliase);
				}
				else
				{
					throw new NotImplementedException();
				}

				return;
			}

			if (valueMeta.MayReferTo != null && valueMeta.MayReferTo.Count > 0)
			{
				InitMayReferTo();
				_mayReferTo!.AddRange(valueMeta.MayReferTo);
				return;
			}

			if (valueMeta.VarData != null)
			{
				InitMayReferTo();
				_mayReferTo!.Add(valueMeta.VarData);
				return;
			}
		}

		private void InitMayReferTo()
		{
			if (_mayReferTo == null) _mayReferTo = new();
		}
	}
}
