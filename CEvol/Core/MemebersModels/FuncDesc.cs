using EvolZero.Generation;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class FuncDesc
	{
		public readonly TypeSpec ReturnType;
		public readonly string Name;
		public readonly Argument[] Arguments;
		public readonly FuncRefData RefData;
		public readonly bool IsInfArgs;
		public readonly AccessModifier Access;

		/// <summary>
		/// Класс-владелец метода. <c>null</c> для функций, объявленных вне класса.
		/// </summary>
		public readonly TypeDesc? DeclaringType;

		public FuncDesc(TypeSpec returnType, string name, Argument[] arguments, FuncRefData refData, bool isInfArgs, AccessModifier access, TypeDesc? declaringType)
		{
			ReturnType = returnType;
			Name = name;
			Arguments = arguments;
			RefData = refData;
			IsInfArgs = isInfArgs;
			Access = access;
			DeclaringType = declaringType;
		}

	}
}
