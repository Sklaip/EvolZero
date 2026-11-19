namespace EvolZero.Core.MemebersModels
{
	public readonly struct TypeSpec : IEquatable<TypeSpec>
	{
		public readonly TypeDesc Type;
		public readonly Qualifier[] Qualifiers;
		public readonly TypeSpec[] ConfiguredGenericParams;

		public bool QualifiersExists => Qualifiers != null && Qualifiers.Length > 0;
		public bool IsRef => QualifiersExists && Qualifiers[0] is ReferenceQualifier or BorrowReferenceQualifier;
		public bool IsBorrowRef => QualifiersExists && Qualifiers[0] is BorrowReferenceQualifier;
		public bool IsOwnerRef => QualifiersExists && Qualifiers[0] is ReferenceQualifier;
		public bool IsValueOwner => QualifiersExists && Qualifiers[0] is OwnQualifier;
		public bool IsArray => QualifiersExists && Qualifiers[0] is ArrayQualifier;
		public bool ArrayExists => Qualifiers.Any(x => x is ArrayQualifier);

		public TypeSpec()
		{
			throw new NotImplementedException();
		}

		public TypeSpec(TypeDesc type)
		{
			Type = type;
			Qualifiers = Array.Empty<Qualifier>();
			ConfiguredGenericParams = []; 
		}

		public TypeSpec(TypeDesc type, Qualifier[] qualifiers)
		{
			Type = type;
			Qualifiers = qualifiers;
			ConfiguredGenericParams = [];
		}

		public bool QualifiersEquals(Qualifier[] qualifiers)
		{
			if (qualifiers == null || Qualifiers == null) return false;


			Qualifier[] first;
			Qualifier[] second;

			if (Qualifiers.Length >= qualifiers.Length)
			{
				first = Qualifiers;
				second = qualifiers;
			}
			else
			{
				first = qualifiers;
				second = Qualifiers;
			}

			for (int i = 0; i < first.Length; i++)
			{
				Qualifier qualifier = first[i];
				if (!qualifier.Equals(i < second.Length ? second[i] : null)) return false;
			}

			return true;
		}

		public bool QualifiersEquals(TypeSpec other)
		{
			return QualifiersEquals(other.Qualifiers);
		}

		public bool GenericEquals(TypeSpec other)
		{
			if(Type.IsGeneric != other.Type.IsGeneric || ConfiguredGenericParams.Length != other.ConfiguredGenericParams.Length) 
				return false;

			for (int i = 0; i < other.ConfiguredGenericParams.Length; i++)
			{
				if (!other.ConfiguredGenericParams[i].Equals(ConfiguredGenericParams[i])) return false;
			}

			return true;
		}

		public bool Equals(TypeSpec other)
		{
			return Type == other.Type && QualifiersEquals(other);
		}

		public override bool Equals(object? obj)
		{
			return obj is TypeSpec && Equals((TypeSpec)obj);
		}

		public override int GetHashCode()
		{
			var hashCode = new HashCode();
			hashCode.Add(Type);

			if (Qualifiers != null)
			{
				foreach (var t in Qualifiers)
				{
					hashCode.Add(t);
				}
			}

			return hashCode.ToHashCode();
		}

		public ITypeRef GetRealTypeRef(IPlatformTypesProvider typesProvider)
		{
			var baseType = Type.TypeRef;
			if (!QualifiersExists) return baseType;

			if (Qualifiers.Length > 1)
				return Qualifiers[0].GetTypeRef(typesProvider, Qualifiers.Skip(1), baseType);

			return Qualifiers[0].GetTypeRef(typesProvider, [], baseType);
		}

		public TypeSpec RemoveFirtsQualifier()
		{
			return new TypeSpec(Type, Qualifiers.Skip(1).ToArray());
		}

		public override string ToString()
		{
			if (!QualifiersExists) return Type.Name;

			// Создаем список элементов с сохранением исходного индекса квалификатора
			var items = new List<(string Text, int OrigIndex)>();

			for (int i = 0; i < Qualifiers.Length; i++)
			{
				items.Add((Qualifiers[i].ToString()!, i));
			}

			// Добавляем тип в конец
			items.Add((Type.Name, -1));

			// Применяем правило перемещения для ArrayQualifier
			for (int i = 0; i < Qualifiers.Length; i++)
			{
				if (Qualifiers[i] is ArrayQualifier)
				{
					int currIndex = items.FindIndex(x => x.OrigIndex == i);

					// Если элемент не последний в списке (то есть перед ним/за ним есть другой квалификатор или тип)
					if (currIndex < items.Count - 1)
					{
						var item = items[currIndex];
						items.RemoveAt(currIndex);
						items.Insert(currIndex + 1, item); // перемещаем за следующий элемент
					}
				}
			}

			return string.Join(" ", items.Select(x => x.Text));
		}
	}
}
