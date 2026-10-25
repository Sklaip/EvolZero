namespace EvolZero.Core.Analysis.Semantic
{
	public class VarMeta(Lifetime lifetime, bool isDestructed, bool isInitialized, VarMeta? parent = null)
	{
		/// <summary>
		/// Если это локлаьна переменная то тут будет имя, если поле класса, this или какая-то другая хуйня то null
		/// </summary>
		public string? Name { get; set; }
		public string? DebugName { get; set; } // TODO: для дебага
		public Lifetime Lifetime { get; set; } = lifetime;
		public bool IsDestructed { get; set; } = isDestructed;
		public bool IsInitialized { get; set; } = isInitialized;
		public List<VarMeta> Aliases { get; set; } = new();
		public List<VarMeta> IsAliaseTo { get; set; } = new();
		public List<VarMeta> LinkedVars { get; set; } = new();
		private VarMeta? Parent { get; set; } = parent;

		public bool LinkedVarsExists => LinkedVars != null && LinkedVars.Count > 0;
		public bool AliasesExists => (Aliases != null && Aliases.Count > 0) || Parent != null;
		public bool IsAliaseToSomething => (IsAliaseTo != null && IsAliaseTo.Count > 0);

		public void SetParent(VarMeta parent)
		{
			Parent = parent;
		}

		public void RemoveParent()
		{
			Parent = null;
		}

		public void SetAliase(VarMeta aliase)
		{
			if (Aliases == null)
				Aliases = new();

			Aliases.Add(aliase);
			Parent?.SetAliase(aliase);
			aliase.SetIsAliaseTo(this);
		}

		private void SetIsAliaseTo(VarMeta toAliase)
		{
			IsAliaseTo.Add(toAliase);
			Parent?.SetIsAliaseTo(toAliase);
		}

		public void LinkVars(VarMeta varToLink)
		{
			if (this == varToLink) return;

			LinkedVars.Add(varToLink);
			varToLink.LinkedVars.Add(this);

			Parent?.LinkVars(varToLink);
			varToLink.Parent?.LinkVars(this);

			foreach (var linkedVar in Aliases)
			{
				varToLink.LinkVars(linkedVar);
			}

			foreach (var linkedVar in varToLink.Aliases)
			{
				LinkVars(linkedVar);
			}
		}
	}
}
