using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.Analysis
{
	public abstract class SemanticTreeVisitor<T>
	{
		public void Visit(ProgramStatement program)
		{
			foreach (var child in program.Childs)
			{
				if (!(child is Statement stm)) throw new NotImplementedException();
				HandleStatement(stm);
			}
		}

		protected void HandleStatement(Statement statement)
		{
			switch (statement)
			{
				case NamespaceStatement nameSpaceStatement:
					HandleNameSpace(nameSpaceStatement);
					break;
				case ClassStatement classStatement:
					HandleClass(classStatement);
					break;
				case FunctionStatement functionStatement:
					HandleFunctionalBlock(functionStatement);
					break;
				case ConstructorStatement contructorStatetment:
					HandleFunctionalBlock(contructorStatetment);
					break;
				case IfStatement ifStatement:
					HandleIfStatement(ifStatement);
					break;
				case ReturnStatement returnStatement:
					HandleReturnStatement(returnStatement);
					break;
				case WhileStatement whileStatement:
					HandleWhileStatement(whileStatement);
					break;
				default:
					throw new NotImplementedException();
			}
		}

		protected virtual void HandleNameSpace(NamespaceStatement statement)
		{
			foreach (var child in statement.Childs)
			{
				if (!(child is Statement stm)) throw new NotImplementedException();
				HandleStatement(stm);
			}
		}

		protected virtual void HandleClass(ClassStatement statement)
		{
			foreach (var child in statement.Childs)
			{
				if (!(child is Statement stm)) throw new NotImplementedException();
				HandleStatement(stm);
			}
		}

		protected virtual void HandleFunctionalBlock<TBlock>(TBlock statement) where TBlock : Statement, IFunctionalBlockStatement
		{
			foreach (var child in statement.Childs)
			{
				if ((child is Statement stm)) HandleStatement(stm);
				else if (child is Expression expr) HandleExpression(expr);
				else throw new NotImplementedException();
			}
		}

		protected virtual void HandleIfStatement(IfStatement statement)
		{
			HandleExpression(statement.Condition);

			foreach (var child in statement.Childs)
			{
				if ((child is Statement stm)) HandleStatement(stm);
				else if (child is Expression expr) HandleExpression(expr);
				else throw new NotImplementedException();
			}
		}

		protected virtual void HandleReturnStatement(ReturnStatement statement)
		{
			Expression resExpr = statement.Value;
			if (resExpr.ResultTypeSpec.Type.Name != "void")
			{
				HandleExpression(resExpr);
			}
		}

		protected virtual void HandleWhileStatement(WhileStatement statement)
		{
			HandleExpression(statement.Condition);

			foreach (var child in statement.Childs)
			{
				if ((child is Statement stm)) HandleStatement(stm);
				else if (child is Expression expr) HandleExpression(expr);
				else throw new NotImplementedException();
			}
		}

		protected T HandleExpression(Expression expression)
		{
			switch (expression)
			{
				case NumConstExpression numConstExpression:
					return CreateNum(numConstExpression);
				case VariableCreatingExpression varCreate:
					return CreateVar(varCreate);
				case VariableAccessExpression varAccess:
					return VarAccess(varAccess);
				case AllocateHeapMemoryToType allocateHeapMemoryToType:
					return AllocateHeapMemory(allocateHeapMemoryToType);
				case AppealToThisExpression appealToThis:
					return AppealToThis(appealToThis);
				case ArrayCellAccessExpression arrayCellAccess:
					return ArrayCellAccess(arrayCellAccess);
				case SimpleBinaryOperationExpression simpleBinaryOperation:
					return SimpleBinaryOperationHandle(simpleBinaryOperation);
				case CallFunctionExpression callFunction:
					return CallFunction(callFunction);
				case CompareOperationExpression compareOperation:
					return CompareOperationHandle(compareOperation);
				case GetPointerToVarExpression getPointerToVar:
					return GetPointerToVar(getPointerToVar);
				case NotExpression notExpression:
					return Not(notExpression);
				case PointerDereferenceExpression pointerDereference:
					return PointerDereference(pointerDereference);
				case StructureFieldAccessExpression structureFieldAccess:
					return StructureFiledAccess(structureFieldAccess);
				case DoNotAutoDereferenceIfPointerExpression doNotAutoDereferenceIfPointerExpression:
					return HandleExpression(doNotAutoDereferenceIfPointerExpression.Expression);
				case CallConstructorExpression callConstructorExpression:
					return CallConstructor(callConstructorExpression);
				case GlobalArrayExpression globalArrayExpression:
					return CreateGlobalArray(globalArrayExpression);
				case CastExpression castExpression:
					return CastHandle(castExpression);
				case IntToIntExtensionExpression intToIntExtensionExpression:
					return IntToIntExtensionHandle(intToIntExtensionExpression);
				case IntToFloatExtensionExpression intToFloatExtensionExpression:
					return IntToFloatExtensionHandle(intToFloatExtensionExpression);
				case IntTruncExpression intTruncExpression:
					return IntTruncHandle(intTruncExpression);
				case IntToPointerExpression intToPointerExpression:
					return IntToPointerHandle(intToPointerExpression);
				case PointerToIntExpression pointerToIntExpression:
					return PointerToIntHandle(pointerToIntExpression);
				case FloatToIntExpression floatToIntExpression:
					return FloatToIntHandle(floatToIntExpression);
				case FloatToFloatExpression floatToFloatExpression:
					return FloatToFloatHandle(floatToFloatExpression);
				case FloatTruncExpression floatTruncExpression:
					return FloatTruncHandle(floatTruncExpression);
				case ReinterpretCastExpression reinterpretCastExpression:
					return ReinterpretCastHandle(reinterpretCastExpression);
				default:
					throw new NotImplementedException();
			}

		}

		protected virtual T CreateNum(NumConstExpression expr)
		{
			return default!;
		}

		protected virtual T CreateVar(VariableCreatingExpression expr)
		{
			return default!;
		}

		protected virtual T VarAccess(VariableAccessExpression expr)
		{
			return default!;
		}

		protected virtual T AppealToThis(AppealToThisExpression expr)
		{
			return default!;
		}

		protected virtual T AllocateHeapMemory(AllocateHeapMemoryToType expr)
		{
			return default!;
		}

		protected virtual T ArrayCellAccess(ArrayCellAccessExpression expr)
		{
			HandleExpression(expr.ArrayGetting);
			HandleExpression(expr.IndexGetting);
			return default!;
		}

		protected virtual T SimpleBinaryOperationHandle(SimpleBinaryOperationExpression expr)
		{
			HandleExpression(expr.LeftExpression);
			HandleExpression(expr.RightExpression);
			return default!;
		}

		protected virtual T CallFunction(CallFunctionExpression expr)
		{
			foreach (var arg in expr.Arguments) HandleExpression(arg);
			return default!;
		}

		protected virtual T CompareOperationHandle(CompareOperationExpression expr)
		{
			HandleExpression(expr.LeftExpression);
			HandleExpression(expr.RightExpression);
			return default!;
		}

		protected virtual T GetPointerToVar(GetPointerToVarExpression expr)
		{
			return HandleExpression(expr.Variable);
		}

		protected virtual T Not(NotExpression expr)
		{
			return HandleExpression(expr.Expression);
		}

		protected virtual T PointerDereference(PointerDereferenceExpression expr)
		{
			return HandleExpression(expr.Target);
		}

		protected virtual T StructureFiledAccess(StructureFieldAccessExpression expr)
		{
			return HandleExpression(expr.StructureGetting);
		}

		protected virtual T CallConstructor(CallConstructorExpression expr)
		{
			HandleExpression(expr.MemoryGetting);
			foreach (var arg in expr.Arguments) HandleExpression(arg);
			return default!;
		}

		protected virtual T CastHandle(CastExpression expr)
		{
			return HandleExpression(expr.Expression);
		}

		protected virtual T IntToIntExtensionHandle(IntToIntExtensionExpression expr)
		{
			return HandleExpression(expr.NumGetting);
		}

		protected virtual T IntToFloatExtensionHandle(IntToFloatExtensionExpression expr)
		{
			return HandleExpression(expr.NumGetting);
		}

		protected virtual T IntTruncHandle(IntTruncExpression expr)
		{
			return HandleExpression(expr.NumGetting);
		}

		protected virtual T IntToPointerHandle(IntToPointerExpression expr)
		{
			return HandleExpression(expr.NumGetting);
		}

		protected virtual T PointerToIntHandle(PointerToIntExpression expr)
		{
			return HandleExpression(expr.PointerGetting);
		}

		protected virtual T FloatToIntHandle(FloatToIntExpression expr)
		{
			return HandleExpression(expr.NumGetting);
		}

		protected virtual T FloatToFloatHandle(FloatToFloatExpression expr)
		{
			return HandleExpression(expr.NumGetting);
		}

		protected virtual T FloatTruncHandle(FloatTruncExpression expr)
		{
			return HandleExpression(expr.NumGetting);
		}

		protected virtual T ReinterpretCastHandle(ReinterpretCastExpression expr)
		{
			return HandleExpression(expr.CastedExpression);
		}

		protected virtual T CreateGlobalArray(GlobalArrayExpression expr)
		{
			return default!;
		}

	}
}
