using EvolZero.Core.MemebersModels;

namespace EvolZero.Core
{
	public class MembersRefsManager
	{
		private readonly IPlatformTypesProvider _typesProvider;
		private Dictionary<TypeSpec, ITypeRef> _typesList = [];

		private class TypeFace
		{
			public readonly TypeDesc Type;
			public readonly TypeSpec[] GenericParams;

			public bool GenericEquals(TypeFace other)
			{
				if (Type.IsGeneric != other.Type.IsGeneric || GenericParams.Length != other.GenericParams.Length)
					return false;

				for (int i = 0; i < other.GenericParams.Length; i++)
				{
					if (!other.GenericParams[i].Equals(GenericParams[i])) return false;
				}

				return true;
			}

			public bool Equals(TypeFace other)
			{
				return Type == other.Type && GenericEquals(other);
			}

			public override bool Equals(object? obj)
			{
				return obj is TypeFace && Equals((TypeFace)obj);
			}

			public override int GetHashCode()
			{
				var hashCode = new HashCode();
				hashCode.Add(Type);

				if (GenericParams != null)
				{
					foreach (var p in GenericParams)
					{
						hashCode.Add(p);
					}
				}

				return hashCode.ToHashCode();
			}
		}


		public MembersRefsManager(IPlatformTypesProvider typesProvider)
		{
			_typesProvider = typesProvider;
		}

		public ITypeRef GetTypeRef(TypeSpec typeSpec)
		{
			return typeSpec.Type.TypeRef;
		}

		public ITypeRef GetRealTypeRef(TypeSpec typeSpec)
		{
			var baseType = GetTypeRef(typeSpec);
			if (!typeSpec.QualifiersExists) return baseType;

			if (typeSpec.Qualifiers.Length > 1)
				return typeSpec.Qualifiers[0].GetTypeRef(_typesProvider, typeSpec.Qualifiers.Skip(1), baseType);

			return typeSpec.Qualifiers[0].GetTypeRef(_typesProvider, [], baseType);
		}
	}
}
