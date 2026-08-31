using EvolZero.Core;
using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace EvolZero.Core.Analysis.Semantic
{
	public class TypeAnalyzer
	{
		private readonly MembersFinder _membersFinder;

		private class ArgumentsComparer : IEqualityComparer<TypeSpec>
		{
			private readonly TypeAnalyzer _typeAnalyzer;
			public readonly TypeSpec?[] Downcasts;
			private int _index = 0;

			public ArgumentsComparer(TypeAnalyzer typeAnalyzer, int size)
			{
				_typeAnalyzer = typeAnalyzer;
				Downcasts = new TypeSpec?[size];
			}

			public bool Equals(TypeSpec x, TypeSpec y)
			{
				var result = _typeAnalyzer.CheckTypeMatching(x, y, out bool notDirectMatch);

				if (notDirectMatch)
				{
					Downcasts[_index] = x;
				}

				_index++;
				return result;
			}

			public int GetHashCode([DisallowNull] TypeSpec obj)
			{
				return obj.GetHashCode();
			}
		}

		public TypeAnalyzer(MembersFinder membersFinder)
		{
			_membersFinder = membersFinder;
		}

		public bool CheckTypeMatching(TypeSpec to, TypeSpec from, out bool notDirectMatch)
		{
			return Is(to, from, out notDirectMatch);
		}

		public bool SoftCheckTypeMatching(TypeSpec first, TypeSpec second)
		{
			if (Is(first, second, out _)) return true;
			return Is(second, first, out _);
		}

		public FuncDesc? FindSuitableFunction(FuncDesc[] functions, IEnumerable<TypeSpec> arguments, out TypeSpec?[] downcasts)
		{
			// TODO: проверять что подходит только одна функция, если подходит несколько то выдавать ошибку
			foreach (var func in functions)
			{
				var agrsCount = func.Arguments.Length;
				var comparer = new ArgumentsComparer(this, agrsCount);
				var funcArgs = func.Arguments.Select(x => x.Declaring);

				if (funcArgs.SequenceEqual(arguments, comparer))
				{
					downcasts = comparer.Downcasts;
					return func;
				}

				if (func.IsInfArgs)
				{
					var funcArgsArr = funcArgs.ToArray();
					var argsArray = arguments.Take(agrsCount).ToArray();
					comparer = new ArgumentsComparer(this, agrsCount);

					if (funcArgsArr.SequenceEqual(argsArray, comparer))
					{
						downcasts = comparer.Downcasts;
						return func;
					}
				}
			}

			downcasts = [];
			return null;
		}

		public ConstructorDesc? FindSuitableConstructor(IEnumerable<ConstructorDesc> constructors, IEnumerable<TypeSpec> arguments)
		{
			return FindSuitableConstructor(constructors, arguments, out _);
		}

		public ConstructorDesc? FindSuitableConstructor(IEnumerable<ConstructorDesc> constructors, IEnumerable<TypeSpec> arguments, out TypeSpec?[] downcasts)
		{
			foreach (var ctor in constructors)
			{
				var comparer = new ArgumentsComparer(this, ctor.Arguments.Length);
				var funcArgs = ctor.Arguments.Select(x => x.Declaring);
				if (funcArgs.SequenceEqual(arguments, comparer))
				{
					downcasts = comparer.Downcasts;
					return ctor;
				}
			}

			downcasts = [];
			return null;
		}

		private bool TryCastArrayRefToTypeRef(TypeSpec to, TypeSpec from)
		{
			if (!to.QualifiersExists || !from.QualifiersExists) return false;
			if (!to.Type.IsBaseType || !from.Type.IsBaseType) return false;
			if (!to.IsRef || !from.IsRef) return false;
			if (to.Type != from.Type) return false;

			if (from.Qualifiers.Length > 1)
			{
				// TODO: оптимизирвоать эту поеботу. Наеврное переписать на урчной перебор
				int offset = 0;
				foreach (var qualifier in from.Qualifiers)
				{
					if (from.Qualifiers.Length - offset == to.Qualifiers.Length)
					{
						return to.QualifiersEquals([Qualifier.Reference, .. from.Qualifiers.Skip(offset + 1).ToArray()]);
					}

					if (qualifier is not ArrayQualifier && offset > 0) return false;

					offset++;
				}
			}

			return false;
		}

		private bool Is(TypeSpec to, TypeSpec from, out bool notDirectMatch)
		{
			notDirectMatch = false;

			if (!to.QualifiersEquals(from) && !TryCastArrayRefToTypeRef(to, from)) return false;

			if (to.Type == from.Type) return true;

			notDirectMatch = true;
			foreach (var inheritedType in from.Type.InheritedTypes)
			{
				if (Is(to.Type, inheritedType, out _)) return true;
			}

			foreach (var expandedType in from.Type.CanExpandedTo)
			{
				if (Is(to.Type, expandedType, out _)) return true;
			}

			return false;
		}

		private bool Is(TypeDesc to, TypeDesc from, out bool notDirectMatch)
		{
			notDirectMatch = false;

			if (to == from) return true;

			notDirectMatch = true;
			foreach (var inheritedType in from.InheritedTypes)
			{
				if (Is(to, inheritedType, out _)) return true;
			}

			foreach (var expandedType in from.CanExpandedTo)
			{
				if (Is(to, expandedType, out _)) return true;
			}

			return false;
		}


	}
}
