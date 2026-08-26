using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class SimpleTypeExpression(TypeSpec resultTypeSpec, PositionInSources pos) : Expression(resultTypeSpec, pos);
}
