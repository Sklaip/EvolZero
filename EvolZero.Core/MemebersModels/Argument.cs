using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class Argument
	{
		public readonly TypeSpec Declaring;
		public readonly string Name;

		public Argument(TypeSpec declaring, string name)
		{
			Declaring = declaring;
			Name = name;
		}
	}

}
