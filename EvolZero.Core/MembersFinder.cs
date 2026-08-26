using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core
{
	public class MembersFinder
	{
		private readonly MembersTable _membersTable;
		private HashSet<string> _namespaces = [];
		private HashSet<string> _usings = [];

		public MembersFinder(MembersTable membersTable)
		{
			_membersTable = membersTable;
		}

		public void AddNamespace(string nameSpace)
		{
			_namespaces.Add(nameSpace);
		}

		public bool AddUsing(string nameSpace)
		{
			if (!_namespaces.Contains(nameSpace)) return false;
			_usings.Add(nameSpace);

			return true;
		}

		public void ClearUsings()
		{
			_usings.Clear();
		}

		public TypeDesc FindType(string name)
		{
			return TryFindType(name) ?? throw new NotImplementedException();
		}

		public TypeDesc? TryFindType(string name)
		{
			if (_membersTable.Types.TryGetValue(name, out TypeDesc? typeDesc))
				return typeDesc;

			foreach (string nameSpace in _usings)
			{
				if (_membersTable.Types.TryGetValue($"{nameSpace}.{name}", out typeDesc))
					return typeDesc;
			}

			return null;
		}

		public FuncDesc[]? FindFunction(string name)
		{
			return FindFunction(_membersTable.Functions, name);
		}

		public FuncDesc[]? FindFunction(TypeDesc parentType, string name)
		{
			return FindFunction(parentType.Functions, name);
		}

		private FuncDesc[]? FindFunction(Dictionary<string, FuncDesc[]> functionsList, string name)
		{
			if (!functionsList.TryGetValue(name, out FuncDesc[] functions))
			{
				return null;
			}

			return functions;
		}

		public IReadOnlyCollection<ConstructorDesc> FindConstructors(TypeDesc parentType)
		{
			return parentType.Constructors;
		}
	}
}
