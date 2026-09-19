using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public interface IInitializableExpresion
	{
		public bool IsInitialized { get; set; }
	}
}
