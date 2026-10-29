using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class AnonymousObjectTypeDesc : TypeDesc
	{
		public const string TYPE_NAME = $"${nameof(AnonymousObjectTypeDesc)}";
		public AnonymousObjectTypeDesc() : base(TYPE_NAME, null!)
		{
		}
	}
}
