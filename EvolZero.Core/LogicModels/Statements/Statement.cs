using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public abstract class Statement : ILogicModel
	{
		public readonly IReadOnlyList<ILogicModel> Childs;
		public readonly PositionInSources Pos;

		public virtual bool InevitableTerminating { get; } = false;

		protected Statement(IReadOnlyList<ILogicModel> childs, PositionInSources pos)
		{
			Childs = childs;
			Pos = pos;
		}

		public void AddLogicModel(ILogicModel model)
		{
			((List<ILogicModel>)Childs).Add(model); //  TODO: че-то сделать с приведением к List<ILogicModel>, переосмыслить эту хуету
		}

		public void AddLogicModel(ILogicModel model, int index)
		{
			((List<ILogicModel>)Childs).Insert(index, model);
		}

		protected virtual bool LastStatementIsTerminating()
		{
			var last = Childs.LastOrDefault();
			if (last == null) return false;
			if (last is Statement lastStatement) return lastStatement.InevitableTerminating;

			return false;
		}
	}
}
