using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class LifetimeDecl
	{
		public enum KeyType
		{
			Var,
			Return,
			This
		}

		public record Key(KeyType KeyType, string? VarName);

		public readonly Key LeftKey;
		public readonly Key RightKey;

		public LifetimeDecl(Key leftKey, Key rightKey)
		{
			LeftKey = leftKey;
			RightKey = rightKey;
		}
	}
}
