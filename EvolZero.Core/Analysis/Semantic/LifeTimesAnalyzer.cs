using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using EvolZero.Core.Tools;

namespace EvolZero.Core.Analysis.Semantic
{
    public class LifeTimesAnalyzer : SemanticTreeVisitor<ValueMeta?>
    {
        public const string LIFETIMES_LAYER = "LifeTimesAnalyzer";

        private int _currentBlockNum = -1; // -1 чтобы был 0, потому что при первом входе в HandleStatemetChilds будет инкремент
        private Dictionary<string, VarMeta> _vars = new();
        private Stack<CurrentBlock> _currentBlocks = new();
        private readonly ErrorsBag _errorsBag;

        /// <summary>
        /// Стек анализа конструкций if/else. Позволяет изолировать состояние переменных
        /// между параллельными ветками (if/else-if/else): каждая ветка анализируется от
        /// общего состояния на входе, а после всей конструкции средства совмещаются.
        /// </summary>
        private Stack<IfAnalysis> _ifAnalyses = new();

        class IfAnalysis
        {
            public Dictionary<string, VarMetaState> Snapshot { get; } = new();
            public HashSet<string> DestructedInAnyBranch { get; } = new();
        }

        class VarMetaState
        {
            public readonly VarMeta Var;
            public readonly Lifetime Lifetime;
            public readonly bool IsDestructed;
            public readonly bool IsInitialized;
            public readonly List<VarMeta> Aliases;
            public readonly List<VarMeta> IsAliaseTo;
            public readonly List<VarMeta> LinkedVars;
            public readonly int AliasesCount;
            public readonly int IsAliaseToCount;
            public readonly int LinkedVarsCount;

            public VarMetaState(VarMeta meta)
            {
                Var = meta;
                Lifetime = meta.Lifetime;
                IsDestructed = meta.IsDestructed;
                IsInitialized = meta.IsInitialized;
                Aliases = meta.Aliases;
                AliasesCount = meta.Aliases.Count;
                IsAliaseTo = meta.IsAliaseTo;
                IsAliaseToCount = meta.IsAliaseTo.Count;
                LinkedVars = meta.LinkedVars;
                LinkedVarsCount = meta.LinkedVars.Count;
            }

            public void Restore()
            {
                Var.Lifetime = Lifetime;
                Var.IsDestructed = IsDestructed;
                Var.IsInitialized = IsInitialized;

                Truncate(Aliases, AliasesCount);
                Truncate(IsAliaseTo, IsAliaseToCount);
                Truncate(LinkedVars, LinkedVarsCount);
            }

            private static void Truncate(List<VarMeta> list, int count)
            {
                if (list.Count > count) list.RemoveRange(count, list.Count - count);
            }
        }

        private record ArgHandlerRes(Lifetime Lifetime, bool LinkWithThis, bool LinkWithReturn);

        private VarMeta? _currentClass;
        private Dictionary<string, VarMeta>? _currentClassFields;
        private TypeDesc? _currentDesc;
        private DestructorStatement? _currentDestructor;

        /// <summary>
        /// последовательность передач значений (например присвоение в переменную, передача в качестве аргумента функции)
        /// </summary>
        private ValueTransmissionsSequence _transmissionsSequence = new();

        private IFunctionalBlockStatement? _currentFunc = null;

        private ILifitemesBypassConsumer _lifetimesConsumer;

        class CurrentBlock(Statement codeBlock)
        {
            public Statement CodeBlock { get; } = codeBlock;
            public List<ValueMeta> Vars { get; } = new();
            public bool IsDestructed { get; set; }
        }

        public LifeTimesAnalyzer(ErrorsBag errorsBag, ILifitemesBypassConsumer lifetimesConsumer)
        {
            _errorsBag = errorsBag;
            _lifetimesConsumer = lifetimesConsumer;
        }

        private void AddLifetimeError(string errorCode, string message, PositionInSources pos)
        {
            _errorsBag.AddError(LIFETIMES_LAYER, errorCode, message, pos);
        }

        protected override void HandleClass(ClassStatement statement)
        {
            _currentDesc = statement.TypeDesc;
            _currentClass = new VarMeta(Lifetime.ForThis(), false, true);
            _currentClass.DebugName = "this";

            _currentClassFields = statement.TypeDesc.Variables.Values
                .Select(x => (x.Name, new VarMeta(Lifetime.ForThis(), false, false, _currentClass)
                {
                    DebugName = x.Name
                }
                )).ToDictionary();

            base.HandleClass(statement);

            _currentClass = null;
            _currentClassFields = null;
            _currentDesc = null;
        }

        protected override void HandleFunctionalBlock<TBlock>(TBlock statement)
        {
            base.HandleFunctionalBlock(statement);

            _currentBlocks = new();
            _vars = new();
            _currentBlockNum = -1;
        }

        protected override void HandleStatemetChilds(Statement statement)
        {
            if (statement is BlockStatement block && block.IsUnsafe) return;

            _currentBlocks.Push(new(statement));
            _currentBlockNum++;

            if (statement is IFunctionalBlockStatement fst)
            {
                _currentFunc = fst;
                foreach (var argument in fst.Arguments)
                {
                    var liftime = DefineArgumentLifetime(argument, fst.Lifetimes);

                    var variable = new VarMeta(liftime, false, true)
                    {
                        DebugName = argument.Name,
                        Name = argument.Name
                    };

                    _vars.Add(argument.Name, variable);

                    var value = new ValueMeta()
                    {
                        Expr = new VariableAccessExpression(argument.Name, argument.Declaring, true, statement.Pos),
                        VarData = variable
                    };

                    _currentBlocks.Peek().Vars.Add(value);
                }
            }

            if (statement is DestructorStatement destructor)
            {
                _currentDestructor = destructor;
                base.HandleStatemetChilds(statement);
                _currentDestructor = null;
            }
            else
            {
                base.HandleStatemetChilds(statement);
            }

            _currentBlockNum--;

            DestructLifetimes();
            _currentBlocks.Pop();
        }

        protected override void HandleWhileStatement(WhileStatement statement)
        {
            //все текущие недеинициализированные переменные
            var sourceVars = _vars.Values.Where(x => !x.IsDestructed).ToList();

            base.HandleWhileStatement(statement);

            //недеинициализированные перменные после выполнения while
            var afterExecuteVars = _vars.Values.Where(x => !x.IsDestructed).ToHashSet();

            //переменные которые были деинициализированы внутри while
            var destructedInWhileVars = sourceVars.Where(x => !afterExecuteVars.Contains(x)).ToList();

            if (destructedInWhileVars.Count > 0)
            {
                AddLifetimeError("LT014", "Переменные созданные за блоком while не должны быть " +
                    "деинициализированны внутри тела цикла.", statement.Pos);
            }
        }

        protected override void HandleIfStatement(IfStatement statement)
        {
            _lifetimesConsumer.EnterToIfStatement(statement);

            var analysis = new IfAnalysis();
            foreach (var (name, meta) in _vars)
            {
                analysis.Snapshot[name] = new VarMetaState(meta);
            }

            _ifAnalyses.Push(analysis);

            try
            {
                base.HandleIfStatement(statement);
                MergeDestructedState(analysis);
            }
            finally
            {
                _ifAnalyses.Pop();
                _lifetimesConsumer.ExitFromIfStatement(statement);
            }
        }

        /// <summary>Возвращает переменные к состоянию на входе в конструкцию if/else, чтобы ветки не видели изменения соседних.</summary>
        private void RestoreVariablesToSnapshot()
        {
            var snapshot = _ifAnalyses.Peek().Snapshot;
            foreach (var (name, state) in snapshot)
            {
                state.Restore();
                _vars[name] = state.Var;
            }
        }

        /// <summary>Запоминает, какие переменные были деинициализированы в только что обработанной ветке.</summary>
        private void CollectDestructedVariables()
        {
            var analysis = _ifAnalyses.Peek();
            foreach (var name in analysis.Snapshot.Keys)
            {
                if (_vars.TryGetValue(name, out var meta) && meta.IsDestructed)
                    analysis.DestructedInAnyBranch.Add(name);
            }
        }

        /// <summary>
        /// После обработки всех веток любая переменная, деинициализированная хотя бы в одной
        /// ветке, считается деинициализированной и после всей конструкции if/else.
        /// </summary>
        private void MergeDestructedState(IfAnalysis analysis)
        {
            foreach (var name in analysis.DestructedInAnyBranch)
            {
                if (_vars.TryGetValue(name, out var meta))
                    meta.IsDestructed = true;
            }
        }

        protected override void HandleIfChilds(IfStatement statement)
        {
            RestoreVariablesToSnapshot();
            _lifetimesConsumer.HandleConditionSubStatement(statement);
            base.HandleIfChilds(statement);
            CollectDestructedVariables();
        }

        protected override void HandleElseIfChilds(IfStatement statement)
        {
            RestoreVariablesToSnapshot();
            _lifetimesConsumer.HandleConditionSubStatement(statement);
            base.HandleElseIfChilds(statement);
            CollectDestructedVariables();
        }

        protected override void HandleElseChilds(Statement statement)
        {
            RestoreVariablesToSnapshot();
            _lifetimesConsumer.HandleConditionSubStatement(statement);
            base.HandleElseChilds(statement);
            CollectDestructedVariables();
        }

        protected override void SubTreeEnd(ValueMeta? value)
        {
            if (value == null) return;

            if (value.IsAnonymous && value.Expr.ResultTypeSpec.IsOwnerRef)
            {
                //ToDestructPointer(value);
            }
        }

        protected override ValueMeta AllocateHeapMemory(AllocateHeapMemoryToType expr)
        {
            return new ValueMeta()
            {
                Expr = expr
            };
        }

        protected override ValueMeta? PointerDereference(PointerDereferenceExpression expr)
        {
            var res = base.PointerDereference(expr);
            if (res == null) return null;

            res.Expr = expr;
            return res;
        }

        protected override ValueMeta CallConstructor(CallConstructorExpression expr)
        {
            base.CallConstructor(expr);

            // обнуляем текущий лайфтайм в который сейчас происходит присвоение
            var transmission = _transmissionsSequence.GetCurrent();
            _transmissionsSequence.AddNull();

            var acceptedArguments = expr.Constructor.Arguments;
            var passedArguments = expr.Arguments;

            var thisGetting = HandleExpression(expr.MemoryGetting);

            var result = new ValueMeta()
            {
                Expr = expr
            };

            // возвращаем лайфтайм обратно
            _transmissionsSequence.RemoveCurrent();

            for (int i = 0; i < acceptedArguments.Length; i++)
            {
                // TODO: и DefineArgumentLifetime, и PassToArgumentHandler под капотом вызывают CheckLinkWithReturn и CheckLinkWithThis. Как-то это оптимизировать, чтобы вызовы не дублировались
                var lifetimes = expr.Constructor.Lifetimes;
                var lifetime = transmission.GetRealLifetime();
                var res = DefinePassedArgumentLifetime(acceptedArguments[i], lifetimes, lifetime, lifetime);

                if (!res.LinkWithReturn && !res.LinkWithThis)
                {
                    // если нету связи с this или return в _transmissionsSequence пихаем новый лайфтайм,
                    // чтобы следующий код не подцепил лайфтайм в которы сейчас происходит присвоение
                    _transmissionsSequence.AddLifetime(new Lifetime(_currentBlockNum + 1));

                    var arg = HandleExpression(passedArguments[i]);
                    if (arg == null) continue;

                    PassToArgumentHandler(acceptedArguments[i], arg, res);
                    _transmissionsSequence.RemoveCurrent();
                }
                else
                {
                    // если связь с this или return есть, то в  _transmissionsSequence не пихаем ничего,
                    // чтобы следующий код смог получить лайфтайм в который сейчас происходит просвоение
                    var arg = HandleExpression(passedArguments[i]);
                    if (arg == null) continue;

                    PassToArgumentHandler(acceptedArguments[i], arg, res);

                    if (transmission.Value != null && arg?.VarData != null)
                    {
                        arg.VarData.SetAliase(transmission.Value);
                    }

                }
            }

            return result;
        }

        protected override ValueMeta GetPointerToVar(GetPointerToVarExpression expr)
        {
            var lifteime = base.GetPointerToVar(expr);
            if (lifteime?.VarData == null)
                throw new NotImplementedException(); // такой хуйни быть не должно

            return new ValueMeta()
            {
                Expr = expr,
                VarData = lifteime.VarData
            };
        }

        protected override ValueMeta CreateGlobalArray(GlobalArrayExpression expr)
        {
            // TODO: вроде array называется global, а вроде мы его потом удалять будем. Хуета какая-то
            return new ValueMeta()
            {
                Expr = expr,
                VarData = new VarMeta(Lifetime.ForGlobal(), false, true)
            };
        }

        protected override ValueMeta AppealToThis(AppealToThisExpression expr)
        {
            return new ValueMeta()
            {
                Expr = expr,
                VarData = _currentClass
            };
        }

        protected override ValueMeta? ArrayCellAccess(ArrayCellAccessExpression expr)
        {
            var arrayGetting = HandleExpression(expr.ArrayGetting);

            if (arrayGetting == null)
                return null;

            if (arrayGetting.VarData == null)
                throw new NotImplementedException(); // такой хуйни быть не должно

            var meta = new VarMeta(arrayGetting.VarData.Lifetime, false, arrayGetting.VarData.IsInitialized, arrayGetting.VarData);

            return new ValueMeta()
            {
                Expr = expr,
                VarData = meta
            };
        }


        protected override ValueMeta? StructureFiledAccess(StructureFieldAccessExpression expr)
        {
            var structureGetting = HandleExpression(expr.StructureGetting);

            if (structureGetting == null)
                return null; // ошибка уже зарегистрирована (например, обращение к деинициализированной ссылке)

            if (structureGetting.VarData == null)
                throw new NotImplementedException(); // такой хуйни быть не должно

            VarMeta meta;
            if (structureGetting.Expr is AppealToThisExpression)
            {
                if (_currentClassFields == null)
                    throw new NotImplementedException(); // такой хуйни быть не должно
                meta = _currentClassFields[expr.Field.Name];
                meta.IsInitialized = expr.IsInitialized;
            }
            else
            {
                meta = new VarMeta(structureGetting.VarData.Lifetime, false, expr.IsInitialized);
                meta.DebugName = expr.Field.Name;
            }

            meta.SetParent(structureGetting.VarData);

            return new ValueMeta()
            {
                Expr = expr,
                VarData = meta
            };
        }

        protected override ValueMeta? VarAccess(VariableAccessExpression expr)
        {
            _vars.TryGetValue(expr.Name, out var varMeta);

            if (varMeta == null) throw new NotImplementedException();

            if (varMeta.IsDestructed)
            {
                _errorsBag.AddError(LIFETIMES_LAYER, "LT001", $"Нет доступа к деинициализированной ссылке '{expr.Name}'", expr.Pos);
                return null;
            }

            varMeta.IsInitialized = expr.IsInitialized;

            return new ValueMeta()
            {
                Expr = expr,
                VarData = varMeta,
                IsLocal = true
            };
        }

        protected override ValueMeta? CallFunction(CallFunctionExpression expr)
        {
            int i = 0;
            int j = 0;

            var acceptedArguments = expr.Function.Arguments;
            var passedArguments = expr.Arguments;

            var transmission = _transmissionsSequence.GetCurrent();
            var lastAssignLifetime = transmission.GetRealLifetime();

            VarMeta? varMeta = null;
            if (lastAssignLifetime != null)
            {
                varMeta = new VarMeta(lastAssignLifetime.Value, false, true);
            }

            var result = new ValueMeta()
            {
                Expr = expr,
                VarData = varMeta
            };

            // обнуляем текущий лайфтайм в который сейчас происходит присвоение
            _transmissionsSequence.AddNull();

            ValueMeta? thisGetting = null; // если это метод класса, здесь будет лайфтайм this, если обычная функция, то просто null
            if (expr.Function.DeclaringType != null)
            {
                thisGetting = HandleExpression(passedArguments[0]);
                j++;
            }

            // возвращаем лайфтайм обратно
            _transmissionsSequence.RemoveCurrent();

            bool linkWithThisExists = false;
            bool linkWithReturnExists = false;

            // TODO: для функций с бесконечным количеством аргументов (printf например) не будут проверяться переданные аргументы, которые выъодят за acceptedArguments.Length
            for (; i < acceptedArguments.Length; i++, j++)
            {
                // TODO: и PassToArgumentHandler под капотом тоже вызывает DefinePassedArgumentLifetime, как то это оптимизировать
                var lifetimes = expr.Function.Lifetimes;
                var res = DefinePassedArgumentLifetime(acceptedArguments[i], lifetimes, thisGetting?.VarData?.Lifetime, lastAssignLifetime);

                if (res.LinkWithThis)
                {
                    if (thisGetting == null) throw new NotImplementedException();
                    if (!thisGetting.IsAnonymous)
                        _transmissionsSequence.AddValue(thisGetting.VarData);
                }
                else if (!res.LinkWithReturn)
                {
                    _transmissionsSequence.AddLifetime(new Lifetime(_currentBlockNum + 1));
                }

                linkWithThisExists = linkWithThisExists || res.LinkWithThis;
                linkWithReturnExists = linkWithReturnExists || res.LinkWithReturn;

                var arg = HandleExpression(passedArguments[j]);
                if (arg == null) continue;
                PassToArgumentHandler(acceptedArguments[i], arg, res);

                if (res.LinkWithReturn && !arg.IsAnonymous)
                {
                    if (transmission.Value == null) throw new NotImplementedException();
                    arg.VarData!.SetAliase(transmission.Value);
                }

                if (res.LinkWithThis && !arg.IsAnonymous)
                {
                    if (thisGetting == null) throw new NotImplementedException();
                    if (!thisGetting.IsAnonymous)
                        arg.VarData!.SetAliase(thisGetting?.VarData!);
                }

                _transmissionsSequence.RemoveCurrent();
            }

            return result;
        }

        protected override ValueMeta? CreateVar(VariableCreatingExpression expr)
        {
            var currentVar = new VarMeta(new Lifetime(_currentBlockNum), false, false)
            {
                Name = expr.Name,
                DebugName = expr.Name
            };

            _vars[expr.Name] = currentVar;

            var lifetime = new ValueMeta()
            {
                Expr = new VariableAccessExpression(expr.Name, expr.ResultTypeSpec, expr.IsInitialized, expr.Pos),
                VarData = currentVar
            };

            _currentBlocks.Peek().Vars.Add(lifetime);

            return lifetime;
        }

        protected override ValueMeta? SimpleBinaryOperationHandle(SimpleBinaryOperationExpression expr)
        {
            switch (expr.OperationType)
            {
                case BinaryOperation.Assing:
                    {
                        ValueMeta? left = HandleExpression(expr.LeftExpression);

                        _transmissionsSequence.AddValue(left?.VarData);
                        ValueMeta? right = HandleExpression(expr.RightExpression);
                        _transmissionsSequence.RemoveCurrent();

                        if (right == null || left == null) return null;

                        AssignHandler(left, right);

                        return left;
                    }
                default:
                    {
                        _transmissionsSequence.AddNull();

                        HandleExpression(expr.LeftExpression);
                        HandleExpression(expr.RightExpression);

                        _transmissionsSequence.RemoveCurrent();

                        return null;
                    }
            }
        }

        protected override ValueMeta? Exchange(ExchangeExpression expr)
        {
            ValueMeta? target = HandleExpression(expr.Target);
            ValueMeta? value = HandleExpression(expr.Value);

            if (target == null || value == null) return null;

            if (!NeedCheck(target.Expr) || !NeedCheck(value.Expr)) return target;

            if (target.VarData == null)
            {
                throw new NotImplementedException(); // такой хуйни быть не должно
            }

            if (!value.IsAnonymous)
            {
                if (value.VarData!.IsDestructed || !value.VarData.IsInitialized)
                    throw new NotImplementedException(); // ссылка была деинициализированна. Вообще сюда оно поподать не должно, оно должно отбрасываться на других проверках

                if (!CheckLifetimes(value.VarData, target.VarData))
                {
                    ErrorLT002(target.Expr.Pos);
                    return null;
                }
            }

            var varIsOwner = IsOwning(target.Expr);

            if (target.VarData == null)
                throw new NotImplementedException(); // такой хуйни быть не должно

            if (varIsOwner)
            {
                if (!CheckOwnerRef(value)) return null;
                GiveAwayOwnership(target, value);
            }
            else
            {
                AssignBorrowRef(target, value);
            }

            target.VarData.IsInitialized = true;
            target.IsStrippedViaExchange = true;

            return target;
        }

        protected override ValueMeta? HandleReturnStatement(ReturnStatement statement)
        {
            var returnedLifetime = base.HandleReturnStatement(statement);

            var returnLifetime = DefineReturnLifetime(_currentFunc!.Lifetimes);
            var returnedExpr = returnedLifetime?.Expr;
            if (returnedExpr != null && NeedCheck(_currentFunc.ReturnType))
            {
                if (IsOwning(_currentFunc.ReturnType) && !IsOwning(returnedExpr))
                {
                    AddLifetimeError("LT005", "Возвращаемая ссылка должна быть владеющей (ref)", returnedExpr.Pos);
                    return null;
                }

                if (!IsOwning(_currentFunc.ReturnType) && returnedLifetime!.VarData != null
                    && !CheckLifetimes(returnedLifetime.VarData.Lifetime, returnLifetime))
                {
                    // TODO: тут нормальный текст ошибки написать
                    AddLifetimeError("LT005", "Недопустимое время жизни возвращаемого значения.", returnedExpr.Pos);
                    return null;
                }

                if (IsOwning(_currentFunc.ReturnType) && (returnedExpr is StructureFieldAccessExpression or ArrayCellAccessExpression))
                {
                    AddLifetimeError("LT004", "Нельзя снимать владеющую (ref) ссылку с поля класса", returnedExpr.Pos);
                    return null;
                }
            }

            if (returnedLifetime?.VarData != null)
            {
                if (returnedLifetime.VarData.LinkedVarsExists)
                {
                    AddLifetimeError("LT012", "Возвращаемая ссылка не должна иметь связанных ссылок!", returnedLifetime.Expr.Pos);
                    return null;
                }

                if (returnedLifetime.VarData.IsAliaseToSomething
                    && !CheckByAliaseLifetimes(returnedLifetime.VarData, returnLifetime))
                {
                    AddLifetimeError("LT013", "Возвращаемая ссылка не должна быть алисом на " +
                        "ссылки с недостаточным для инструкции return временем жизни!", returnedLifetime.Expr.Pos);
                    return null;
                }
            }

            DestructLifetimes(returnedLifetime?.VarData);

            if (_currentDestructor != null)
            {
                foreach (var newStatement in _lifetimesConsumer.HandleDestructor(_currentDesc!, _currentDestructor))
                {
                    AddToCurrentStatement(newStatement);
                }
            }

            return returnedLifetime;
        }

        protected override ValueMeta ObjectInitializerHandle(ObjectInitializerExpression expr)
        {
            var arrayGetting = HandleExpression(expr.ObjectToInit);

            if (arrayGetting == null)
                throw new NotImplementedException(); // такой хуйни быть не должно

            var currentLifetime = _transmissionsSequence.GetCurrent().Lifetime ?? new Lifetime(_currentBlockNum + 1);

            int i = 0;
            foreach (var childExpr in expr.Expressions)
            {
                var right = HandleExpression(childExpr);
                if (right == null) continue; // null здесь возможно если при HandleExpression найдена ошибка

                var exprObjectToInit = expr.ByRef ? new PointerDereferenceExpression(expr.ObjectToInit, expr.Pos) : expr.ObjectToInit;
                var left = new ValueMeta()
                {
                    //в indexGetting пихаем null потому что в AssignHandler не должен провоцировать анализ дочерних Expression.
                    //А если каким-то образом произойдет вызов проверки indexGetting, то лучше пусть отхебнет нуллпоинтером, чем непредсказуемое поведение
                    Expr = new ArrayCellAccessExpression(exprObjectToInit, null!, expr.ObjectToInit.Pos),
                    IsStrippedViaExchange = true,
                    VarData = new VarMeta(currentLifetime, false, false, arrayGetting.VarData)
                };

                AssignHandler(left, right);
            }
            i++;

            return arrayGetting;
        }

        private void AssignHandler(ValueMeta target, ValueMeta value)
        {
            if (!NeedCheck(target.Expr) || !NeedCheck(value.Expr)) return;

            if (target.VarData == null)
            {
                throw new NotImplementedException(); // такой хуйни быть не должно
            }

            if (!value.IsAnonymous)
            {
                if (value.VarData!.IsDestructed || !value.VarData.IsInitialized)
                    throw new NotImplementedException(); // ссылка была деинициализированна. Вообще сюда оно попадть не должно, оно должно отбрасываться на других проверках

                if (!CheckLifetimes(value.VarData, target.VarData))
                {
                    ErrorLT002(target.Expr.Pos);
                    return;
                }
            }

            var varIsOwner = IsOwning(target.Expr);

            if (varIsOwner)
            {
                if (!CheckOwnerRef(value)) return;

                if (target.VarData.IsInitialized)
                {
                    if (!target.IsLocal)
                    {
                        AddLifetimeError("LT008", "Нельзя переназначать уже инициализированную ссылку, не являющуюся локальной (например, поле класса)", target.Expr.Pos);
                        return;
                    }

                    ToDestructPointer(target, true); // удалям старую ссылку
                }

                GiveAwayOwnership(target, value);
            }
            else
            {
                AssignBorrowRef(target, value);
            }

            target.VarData.IsInitialized = true;
        }

        private void PassToArgumentHandler(Argument arg, ValueMeta value, ArgHandlerRes argsInfo)
        {
            var argument = arg.Declaring;
            if (value.VarData?.IsDestructed == true)
            {
                _errorsBag.AddError(LIFETIMES_LAYER, "LT001",
                    $"Нет доступа к деинициализированной ссылке переданной в аргумент", value.Expr.Pos); // TODO: выводить чо за именно аргумент
                return;
            }

            if (!NeedCheck(argument)) return;

            if (!NeedCheck(value.Expr))
                throw new NotImplementedException(); // рассмотреть эти ситуации. Вроде на уровне семантического древа такого быть не может

            if (argument.IsBorrowRef && value.IsAnonymous || (value.IsStrippedViaExchange && IsOwning(value.Expr)))
            {
                AddLifetimeError("LT009", "Анонимные ссылки (например, результат 'new') можно присваивать только во владеющие (ref) ссылки", value.Expr.Pos);
                return;
            }

            var argumentLifetime = argsInfo.Lifetime;

            if (value.VarData != null && !CheckLifetimes(value.VarData.Lifetime, argumentLifetime))
            {
                ErrorLT002(value.Expr.Pos);
                return;
            }

            if (IsOwning(argument))
            {
                if (!CheckOwnerRef(value)) return;

                if (value.IsStrippedViaExchange)
                    throw new NotImplementedException(); // TODO: реализовать это

                if (!value.IsAnonymous)
                    _lifetimesConsumer.GiveAwayOwnership(value.Expr, value.VarData, value.VarData?.Lifetime == new Lifetime(_currentBlockNum));

                DeinitializeGivenAwayPointer(value, argumentLifetime, true);
            }
        }

        private void AssignBorrowRef(ValueMeta target, ValueMeta value)
        {
            if (value.IsAnonymous || (value.IsStrippedViaExchange && IsOwning(value.Expr)))
            {
                AddLifetimeError("LT009", "Анонимные ссылки (например, результат 'new') можно присваивать только во владеющие (ref) ссылки", value.Expr.Pos);
                return;
            }

            if (target.VarData == null)
                throw new NotImplementedException(); // такой хуйни быть не должно

            value.VarData!.SetAliase(target.VarData);
            target.VarData.Lifetime = value.VarData!.Lifetime;
        }

        private bool CheckOwnerRef(ValueMeta value)
        {
            if (!IsOwning(value.Expr))
            {
                AddLifetimeError("LT003", "Нельзя присвоить заимствованную (refb) ссылку во владеющую (ref) ссылку", value.Expr.Pos);
                return false;
            }

            if (value.Expr is StructureFieldAccessExpression or ArrayCellAccessExpression)
            {
                // снять владеющую ссылку с поля класса или с массива можно только через оператор <-,
                // чтобы поле не осталось деинициализированным, а старая ссылка ушла в приемник
                if (!value.IsStrippedViaExchange)
                {
                    AddLifetimeError("LT006", "Снять владеющую ссылку с поля класса или ячейкимассива можно только через левую часть оператора '<-'", value.Expr.Pos);
                    return false;
                }
            }

            return true;
        }

        private void ToDestructPointer(ValueMeta pointer, bool checkAliases)
        {
            if (pointer.VarData == null) throw new NotImplementedException(); // такой хуйни быть не должно

            if (checkAliases && pointer.VarData.AliasesExists)
            {
                AddLifetimeError("LT010", "Нельзя передавать владение ссылкой, у которой есть алиасы", pointer.Expr.Pos);
                return;
            }

            if (!pointer.VarData.IsInitialized || pointer.VarData.IsDestructed || !IsOwning(pointer.Expr))
                return;

            var expr = _lifetimesConsumer.PointerLifetimeEnd(pointer.Expr);
            if (expr != null)
            {
                AddToCurrentStatement(expr);
            }
        }

        private void GiveAwayOwnership(ValueMeta target, ValueMeta value)
        {
            if (!value.IsAnonymous && !value.IsStrippedViaExchange)
                _lifetimesConsumer.GiveAwayOwnership(value.Expr, value.VarData, value.VarData?.Lifetime == new Lifetime(_currentBlockNum));

            if (value.IsStrippedViaExchange)
            {
                if (value.VarData == null || target.VarData == null) throw new NotImplementedException(); //такой хуйни быть не должно
                if (!CheckLifetimes(target.VarData, value.VarData))
                {
                    AddLifetimeError("LT007", "Время жизни ссылки-получателя меньше времени жизни объекта, с которого снимается ссылка", target.Expr.Pos);
                    return;
                }

                value.VarData.LinkVars(target.VarData);
                value.VarData.RemoveParent();
            }

            DeinitializeGivenAwayPointer(value, target.VarData!.Lifetime, true);
        }

        private void DeinitializeGivenAwayPointer(ValueMeta pointer, Lifetime lifetimeToGiveAway, bool checkAliases)
        {
            if (pointer.VarData == null) return;

            if (checkAliases && pointer.VarData.AliasesExists)
            {
                AddLifetimeError("LT010", "Нельзя передавать владение ссылкой, у которой есть алиасы", pointer.Expr.Pos);
                return;
            }

            if (CheckLinkedVarsLifetimesError(lifetimeToGiveAway, pointer.VarData))
            {
                AddLifetimeError("LT011", "Попытка передать владение ссылкой, у которой есть связанные ссылки, в область с другим временем жизни", pointer.Expr.Pos);
                return;
            }

            if (!pointer.IsStrippedViaExchange) pointer.VarData.IsDestructed = true;
        }

        private void DestructLifetimes(VarMeta? excludedVar = null)
        {
            var block = _currentBlocks.Peek();

            if (block.IsDestructed) return;
            block.IsDestructed = true;

            foreach (var variable in block.Vars)
            {
                if (excludedVar != null && variable.VarData == excludedVar) continue;
                ToDestructPointer(variable, false);

                if (variable.VarData?.Name != null)
                {
                    _vars.Remove(variable.VarData.Name);
                }
            }
        }

        private bool CheckLifetimes(Lifetime value, Lifetime target)
        {
            return value >= target;
        }

        private bool CheckLifetimes(VarMeta value, VarMeta target) => CheckLifetimes(value.Lifetime, target.Lifetime);

        private bool CheckByAliaseLifetimes(VarMeta varMeta, Lifetime toCompare, HashSet<VarMeta>? alreadyChecked = null!)
        {
            // TODO: а если тут каким то образом окажется varMeta, у которого Parent имеет другой Lifetime? Проверить че будет и возможно ли это
            alreadyChecked ??= new();
            alreadyChecked.Add(varMeta);

            foreach (var alise in varMeta.IsAliaseTo)
            {
                if (alise.Lifetime < toCompare) return false;
                if (!CheckByAliaseLifetimes(alise, toCompare, alreadyChecked))
                    return false;
            }

            return true;
        }

        private bool CheckLinkedVarsLifetimesError(Lifetime lifetime, VarMeta varMeta)
        {
            if (!varMeta.LinkedVarsExists) return false;
            return varMeta.Lifetime != lifetime;
        }

        /// <summary>
        /// Определяет какой должен быть лайфтайм выражения, переданного в аргумент функции. Не самого аргумента, а именно выражения.
        /// То есть этот метод нужен для обработки кода, который вызывает функцию.
        /// </summary>
        /// <param name="arg">Сам аргумент</param>
        /// <param name="lifetimes">lifetimes() текущей функции</param>
        /// <param name="instanceObjecteLifetime">Если функция метод класса, то здесь должен быть лайфтайм экземпляра объекта</param>
        /// <param name="returnValueLifetime">Лайфтайм выражения (перменная, поле класса), куда будет присвоено возвращаемое функцией значение</param>
        /// <returns></returns>
        private ArgHandlerRes DefinePassedArgumentLifetime(Argument arg, LifetimeDecl[] lifetimes,
            Lifetime? instanceObjecteLifetime, Lifetime? returnValueLifetime)
        {
            var lifetime = lifetimes.FirstOrDefault(x => (x.LeftKey.KeyType == LifetimeDecl.KeyType.Var && x.LeftKey.VarName == arg.Name));
            if (lifetime == null) return new(new Lifetime(_currentBlockNum + 1), false, false);

            if (instanceObjecteLifetime != null && lifetime.RightKey.KeyType == LifetimeDecl.KeyType.This)
                return new(instanceObjecteLifetime.Value, true, false);

            if (returnValueLifetime != null && lifetime.RightKey.KeyType == LifetimeDecl.KeyType.Return)
                return new(returnValueLifetime.Value, false, true);

            return new(new Lifetime(_currentBlockNum + 1), false, false);
        }

        private Lifetime DefineArgumentLifetime(Argument argument, LifetimeDecl[] lifetimes)
        {
            var baseLifetime = argument.Declaring.IsBorrowRef ? Lifetime.ForRefbArgument() : Lifetime.ForArgument();
            var lifetime = lifetimes.FirstOrDefault(x => x.LeftKey.KeyType == LifetimeDecl.KeyType.Var && x.LeftKey.VarName == argument.Name);
            if (argument.Declaring.IsRef && lifetime != null)
            {
                if (lifetime.RightKey.KeyType == LifetimeDecl.KeyType.This) baseLifetime.Connect(Lifetime.ForThis());
                else if (lifetime.RightKey.KeyType == LifetimeDecl.KeyType.Return) baseLifetime.Connect(Lifetime.ForReturn());
            }

            return baseLifetime;
        }

        private Lifetime DefineReturnLifetime(LifetimeDecl[] lifetimes)
        {
            if (lifetimes.Any(x => x.LeftKey.KeyType == LifetimeDecl.KeyType.Return && x.RightKey.KeyType == LifetimeDecl.KeyType.This))
                return Lifetime.ForThis();

            return Lifetime.ForReturn();
        }

        private bool NeedCheck(Expression expr) => NeedCheck(expr.ResultTypeSpec);

        private bool NeedCheck(TypeSpec typeSpec)
        {
            if (typeSpec.IsRef) return true;
            if (!typeSpec.Type.IsBaseType)
                return true; // Значимые типы, которые не являются базовыми тоже проверяем

            return false;
        }

        private bool IsOwning(Expression expr) => IsOwning(expr.ResultTypeSpec);

        private bool IsOwning(TypeSpec typeSpec)
        {
            if (typeSpec.IsOwnerRef) return true;
            if (!typeSpec.IsRef)
                return true; // Если это не ссылка, то относимся к значению так же, как к владеющей ссылке

            return false;
        }

        private bool IsBorrow(Expression expr)
        {
            return expr.ResultTypeSpec.IsBorrowRef;
        }

        public void ErrorLT002(PositionInSources pos)
        {
            AddLifetimeError("LT002", "Время жизни присваиваемой ссылки меньше времени жизни ссылки-получателя", pos);
        }
    }
}
