using Antlr4.Runtime.Misc;
using EvolZero.Core.MemebersModels;
using EvolZero.Parsing.Models;
using static CEvolParser;

namespace EvolZero.Parsing;

internal static class ParsingCommon
{
	public static QualifierWorkpiece? ParseQualifier(QualifierContext? context)
	{
		if (context == null) return null;

		var arr = context.arraySpec();
		if (arr == null)
			return new QualifierWorkpiece(context.GetText());

		return ParseArraySpec(arr);
	}

	public static QualifierWorkpiece ParseArraySpec([NotNull] ArraySpecContext context)
	{
		var expr = context.expression();
		var numExpr = expr as NumberExprContext;

		if (numExpr == null) throw new NotImplementedException();

		var value = numExpr.NUMBER().GetText();
		var num = ulong.Parse(value);

		return new QualifierWorkpiece("array", num);
	}

	public static List<QualifierWorkpiece> ParseQualifiers([NotNull] TypeSpecContext context)
	{
		var qualifiers = new List<QualifierWorkpiece>();
		int i = 0;
		foreach (var qualifier in context.qualifier())
		{
			qualifiers.Add(ParseQualifier(qualifier)!);
			if (qualifiers[i].Kind == "array")
			{
				if (i == 0) throw new NotImplementedException();

				var temp = qualifiers[i - 1];
				qualifiers[i - 1] = qualifiers[i];
				qualifiers[i] = temp;
			}

			i++;
		}

		foreach (var arr in context.arraySpec())
		{
			qualifiers.Add(ParseArraySpec(arr));
		}

		return qualifiers;
	}

	public static AccessModifier ParseAccessModifier(AccessModifierContext? context, bool isClassMember)
	{
		if (context == null)
		{
			return isClassMember ? AccessModifier.Private : AccessModifier.Public;
		}

		return context.GetText() switch
		{
			"public" => AccessModifier.Public,
			"private" => AccessModifier.Private,
			_ => throw new NotImplementedException()
		};
	}
}
