using EvolZero.Core;
using System;
namespace EvolZero.LSP
{
    internal class CodeGeneratorStub : ICodeGenerator
    {
        public class TypeRefStub : ITypeRef { }
        public class FuncRefDataStub : IFuncRefData { }

        private static readonly TypeRefStub TypeStub = new TypeRefStub();
        private static readonly FuncRefDataStub FuncStub = new FuncRefDataStub();

        public ITypeRef PointerType => TypeStub;

        public ITypeRef VoidType => TypeStub;

        public void CreateElseBlock()
        {
            
        }

        public IFuncRefData CreateFunctionSiganture(string funcName, ITypeRef resultType, IEnumerable<ITypeRef> argumentsTypes, bool infArgs = false)
        {
            return FuncStub;
        }

        public ITypeRef CreateStructure(string name)
        {
            return TypeStub;
        }

        public void EndIfBlock()
        {
            
        }

        public void EndWhileBlock()
        {
            
        }

        public void FillStructureBody(ITypeRef structure, IEnumerable<ITypeRef> types)
        {
            
        }

        public ITypeRef GetType(BaseTypes type)
        {
            return TypeStub;
        }

        public void StopFunctionBodyFill()
        {
           
        }

        public void VerifyModule()
        {
            
        }
    }
}
