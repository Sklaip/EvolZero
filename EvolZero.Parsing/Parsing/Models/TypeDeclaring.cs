using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Parsing.Models
{
	public record TypeDeclaring(string TypeName, QualifierWorkpiece[] Qualifiers, string[] Modifiers);

    public record FuncSignature(string Name, TypeDeclaring ReturnType, List<(TypeDeclaring Type, string Name)>? Arguments, 
		string[] modifiers, AccessModifier Access, List<LifetimeDecl>? Lifetimes);

    public record ConstructorSignature(List<(TypeDeclaring Type, string Name)>? Arguments,
		string[] modifiers, AccessModifier Access, List<LifetimeDecl>? Lifetimes);

    public record DestructorSignature(string[] modifiers, AccessModifier Access);

    public record VariableSignature(string Name, TypeDeclaring Type, AccessModifier Access);

    public record ClassSignature(string Name, List<ConstructorSignature> Ctors, List<DestructorSignature> Dtors, 
		Dictionary<string, List<FuncSignature>> Functions, Dictionary<string, VariableSignature> Fields);
}
