using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Parsing.Models
{
	internal record TypeDeclaring(string TypeName, string[] Qualifiers, string[] Modifiers);
	internal record FuncSignature(string Name, TypeDeclaring ReturnType, List<(TypeDeclaring Type, string Name)>? Arguments, string[] modifiers, AccessModifier Access);
	internal record ConstructorSignature(List<(TypeDeclaring Type, string Name)>? Arguments, string[] modifiers, AccessModifier Access);
	internal record VariableSignature(string Name, TypeDeclaring Type, AccessModifier Access);
	internal record ClassSignature(string Name, List<ConstructorSignature> Ctors, Dictionary<string, List<FuncSignature>> Functions, Dictionary<string, VariableSignature> Fields);
}
