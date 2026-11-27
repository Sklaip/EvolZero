namespace EvolZero.Core.MemebersModels
{
	public struct TypeApplication
	{
		public readonly TypeDesc Type;
		public readonly TypeSpec[] ConfiguredGenericParams;

		public TypeApplication(TypeDesc type)
		{
			Type = type;
			ConfiguredGenericParams = [];
		}

		public TypeApplication(TypeDesc type, TypeSpec[] configuredGenericParams)
		{
			Type = type;
			ConfiguredGenericParams = configuredGenericParams;
		}

		public bool GenericEquals(TypeApplication other)
		{
			if (Type.HasGenericParams != other.Type.HasGenericParams || ConfiguredGenericParams.Length != other.ConfiguredGenericParams.Length)
				return false;

			for (int i = 0; i < other.ConfiguredGenericParams.Length; i++)
			{
				if (!other.ConfiguredGenericParams[i].Equals(ConfiguredGenericParams[i])) return false;
			}

			return true;
		}

		public bool Equals(TypeApplication other)
		{
			return Type == other.Type && GenericEquals(other);
		}

		public override bool Equals(object? obj)
		{
			return obj is TypeApplication && Equals((TypeApplication)obj);
		}

		public override int GetHashCode()
		{
			var hashCode = new HashCode();
			hashCode.Add(Type);

			if (ConfiguredGenericParams != null)
			{
				foreach (var p in ConfiguredGenericParams)
				{
					hashCode.Add(p);
				}
			}

			return hashCode.ToHashCode();
		}
	}
}
