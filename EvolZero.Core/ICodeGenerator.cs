
namespace EvolZero.Core
{
    public interface ICodeGenerator
    {
        void CreateElseBlock();
        IFuncRefData CreateFunctionSiganture(string funcName, ITypeRef resultType, IEnumerable<ITypeRef> argumentsTypes, bool infArgs = false);
        ITypeRef CreateStructure(string name);
        void EndIfBlock();
        void EndWhileBlock();
        void FillStructureBody(ITypeRef structure, IEnumerable<ITypeRef> types);
        ITypeRef GetType(BaseTypes type);
        void StopFunctionBodyFill();
        void VerifyModule();
        public ITypeRef PointerType { get; }
        public ITypeRef VoidType { get; }
    }
}