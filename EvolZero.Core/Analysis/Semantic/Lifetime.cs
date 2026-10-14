namespace EvolZero.Core.Analysis.Semantic
{
	public struct Lifetime
	{
		private int _lifetimeNum; // Чем меньше LifetimeNum (номер лайфтайма), тем больше время жизни
		private int _connectCode;
		public bool IsUndefined => _lifetimeNum == int.MinValue;

		public static Lifetime ForGlobal() => new Lifetime(int.MinValue + 1);
		public static Lifetime ForThis() => new Lifetime(-1) { _connectCode = 0 };
		public static Lifetime ForReturn(int lifetimeNum) => new Lifetime(lifetimeNum) { _connectCode = 1 };
		public static Lifetime ForArgument(int argumentNum) => new Lifetime(0) { _connectCode = argumentNum + 2 };
		public static Lifetime ForArgument() => new Lifetime(0);
		public static Lifetime Undefined() => new Lifetime();

		public Lifetime()
		{
			_lifetimeNum = int.MinValue;
			_connectCode = -1;
		}

		public Lifetime(int lifetimeNum)
		{
			_lifetimeNum = lifetimeNum;
			_connectCode = -1;
		}

		public void Connect(Lifetime forConnect)
		{
			_connectCode = forConnect._connectCode;
		}

		private static bool Connected(Lifetime a, Lifetime b) => a._connectCode != -1 && a._connectCode == b._connectCode;

		public static bool operator <=(Lifetime a, Lifetime b)
		{
			if (Connected(a, b)) return true;
			if (a.IsUndefined || b.IsUndefined) return false;

			return a._lifetimeNum <= b._lifetimeNum;
		}

		public static bool operator >=(Lifetime a, Lifetime b)
		{
			if (Connected(a, b)) return true;
			if (a.IsUndefined || b.IsUndefined) return false;

			return a._lifetimeNum >= b._lifetimeNum;
		}

		public static bool operator ==(Lifetime a, Lifetime b)
		{
			if (Connected(a, b)) return true;
			if (a.IsUndefined || b.IsUndefined) return false;

			return a._lifetimeNum == b._lifetimeNum;
		}

		public static bool operator !=(Lifetime a, Lifetime b)
		{
			if (Connected(a, b)) return false;
			if (a.IsUndefined || b.IsUndefined) return true;

			return a._lifetimeNum != b._lifetimeNum;
		}
	}
}
