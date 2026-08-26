using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class ProgramStatement : Statement
	{
		private readonly List<ILogicModel> _childs;

		public ProgramStatement(PositionInSources pos) : base(new List<ILogicModel>(), pos)
		{
			_childs = (Childs as List<ILogicModel>)!; // ну и хуета, но да поебать
		}

		public void AddStatement(Statement statement)
		{
			_childs.Add(statement);
		}
	}
}
