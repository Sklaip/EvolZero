using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class VariableDesc
	{
		public readonly TypeSpec Declaring;
		public readonly string Name;
		public readonly uint Order;
		public readonly AccessModifier Access;

		public VariableDesc(TypeSpec type, string name, uint order, AccessModifier access)
		{
			Declaring = type;
			Name = name;
			Order = order;
			Access = access;
		}
	}
}
