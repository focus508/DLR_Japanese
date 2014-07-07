/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Microsoft Public License, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using MSAst = System.Linq.Expressions;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    
    public class ClassDefinition : ScopeStatement {
        private SourceLocation _header;
        private readonly string _name;
        private Statement _body;
        private readonly Expression[] _bases;
        private IList<Expression> _decorators;

        private PythonVariable _variable;           // Variable corresponding to the class name
        private PythonVariable _modVariable;        // Variable for the the __module__ (module name)
        private PythonVariable _docVariable;        // Variable for the __doc__ attribute
        private PythonVariable _modNameVariable;    // Variable for the module's __name__

        private MSAst.LambdaExpression _dlrBody;       // the transformed body including all of our initialization, etc...

        private static int _classId;

        private static MSAst.ParameterExpression _parentContextParam = Ast.Parameter(typeof(CodeContext), "$parentContext");
        private static MSAst.Expression _tupleExpression = MSAst.Expression.Call(AstMethods.GetClosureTupleFromContext, _parentContextParam);

        public ClassDefinition(string name, Expression[] bases, Statement body) {
            ContractUtils.RequiresNotNull(body, "body");
            ContractUtils.RequiresNotNullItems(bases, "bases");

            _name = name;
            _bases = bases;
            _body = body;
        }

        public SourceLocation Header {
            get { return _header; }
            set { _header = value; }
        }

        public override string Name {
            get { return _name; }
        }

        public IList<Expression> Bases {
            get { return _bases; }
        }

        public Statement Body {
            get { return _body; }
        }

        public IList<Expression> Decorators {
            get {
                return _decorators;
            }
            internal set {
                _decorators = value;
            }
        }

        internal PythonVariable PythonVariable {
            get { return _variable; }
            set { _variable = value; }
        }

        internal PythonVariable ModVariable {
            get { return _modVariable; }
            set { _modVariable = value; }
        }

        internal PythonVariable DocVariable {
            get { return _docVariable; }
            set { _docVariable = value; }
        }

        internal PythonVariable ModuleNameVariable {
            get { return _modNameVariable; }
            set { _modNameVariable = value; }
        }

        internal override bool HasLateBoundVariableSets {
            get {
                return base.HasLateBoundVariableSets || NeedsLocalsDictionary;
            }
            set {
                base.HasLateBoundVariableSets = value;
            }
        }
        
        internal override bool NeedsLocalContext {
            get {
                return true;
            }
        }

        internal override bool ExposesLocalVariable(PythonVariable variable) {
            return true;
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, PythonReference reference) {
            PythonVariable variable;

            // Python semantics: The variables bound local in the class
            // scope are accessed by name - the dictionary behavior of classes
            if (TryGetVariable(reference.Name, out variable)) {
                // TODO: This results in doing a dictionary lookup to get/set the local,
                // when it should probably be an uninitialized check / global lookup for gets
                // and a direct set
                if (variable.Kind == VariableKind.Global) {
                    AddReferencedGlobal(reference.Name);
                } else if (variable.Kind == VariableKind.Local) {
                    return null;
                }

                return variable;
            }

            // Try to bind in outer scopes, if we have an unqualified exec we need to leave the
            // variables as free for the same reason that locals are accessed by name.
            for (ScopeStatement parent = Parent; parent != null; parent = parent.Parent) {
                if (parent.TryBindOuter(this, reference, out variable)) {
                    return variable;
                }
            }

            return null;
        }
        
        public override MSAst.Expression Reduce() {
            var codeObj = GetOrMakeFunctionCode();
            var funcCode = GlobalParent.Constant(codeObj);
            FuncCodeExpr = funcCode;

            MSAst.Expression lambda;
            if (EmitDebugSymbols) {
                lambda = GetLambda();
            } else {
                lambda = Ast.Convert(funcCode, typeof(object));
                ThreadPool.QueueUserWorkItem((x) => {
                    // class defs are almost always run, so start 
                    // compiling the code now so it might be ready
                    // when we actually go and execute it
                    codeObj.UpdateDelegate(PyContext, true);
                });
            }

            MSAst.Expression classDef = Ast.Call(
                AstMethods.MakeClass,
                lambda,
                Parent.LocalContext,
                AstUtils.Constant(_name),
                Ast.NewArrayInit(
                    typeof(object),
                    ToObjectArray(_bases)
                ),
                AstUtils.Constant(FindSelfNames())
            );

            classDef = AddDecorators(classDef, _decorators);

            return GlobalParent.AddDebugInfoAndVoid(AssignValue(Parent.GetVariableExpression(_variable), classDef), new SourceSpan(Start, Header));
        }

        private MSAst.Expression<Func<CodeContext, CodeContext>> MakeClassBody() {
            string className = _name;

            // we always need to create a nested context for class defs            

            var init = new List<MSAst.Expression>();
            var locals = new ReadOnlyCollectionBuilder<MSAst.ParameterExpression>();

            locals.Add(LocalCodeContextVariable);
            locals.Add(PythonAst._globalContext);

            init.Add(Ast.Assign(PythonAst._globalContext, new GetGlobalContextExpression(_parentContextParam)));

            GlobalParent.PrepareScope(locals, init);

            CreateVariables(locals, init);

            var createLocal = CreateLocalContext(_parentContextParam);

            init.Add(Ast.Assign(LocalCodeContextVariable, createLocal));

            List<MSAst.Expression> statements = new List<MSAst.Expression>();
            // Create the body
            MSAst.Expression bodyStmt = _body;

            // __module__ = __name__
            MSAst.Expression modStmt = AssignValue(GetVariableExpression(_modVariable), GetVariableExpression(_modNameVariable));

            string doc = GetDocumentation(_body);
            if (doc != null) {
                statements.Add(
                    AssignValue(
                        GetVariableExpression(_docVariable),
                        AstUtils.Constant(doc)
                    )
                );
            }

            if (_body.CanThrow && GlobalParent.PyContext.PythonOptions.Frames) {
                bodyStmt = AddFrame(LocalContext, FuncCodeExpr, bodyStmt);
                locals.Add(FunctionStackVariable);
            }

            bodyStmt = WrapScopeStatements(
                Ast.Block(
                    Ast.Block(init),
                    statements.Count == 0 ?
                        EmptyBlock :
                        Ast.Block(new ReadOnlyCollection<MSAst.Expression>(statements)),
                    modStmt,
                    bodyStmt,
                    LocalContext
                ),
                _body.CanThrow
            );

            var lambda = Ast.Lambda<Func<CodeContext, CodeContext>>(
                Ast.Block(
                    locals,
                    bodyStmt
                ),
                Name + "$" + Interlocked.Increment(ref _classId),
                new[] { _parentContextParam }
                );

            return lambda;
        }

        internal override MSAst.LambdaExpression GetLambda() {
            if (_dlrBody == null) {
                PerfTrack.NoteEvent(PerfTrack.Category.Compiler, "Creating FunctionBody");
                _dlrBody = MakeClassBody();
            }

            return _dlrBody;
        }

        /// <summary>
        /// Gets the closure tuple from our parent context.
        /// </summary>
        internal override MSAst.Expression GetParentClosureTuple() {
            return _tupleExpression;
        }

        internal override string ScopeDocumentation {
            get {
                return GetDocumentation(_body);
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_decorators != null) {
                    foreach (Expression decorator in _decorators) {
                        decorator.Walk(walker);
                    }
                }
                if (_bases != null) {
                    foreach (Expression b in _bases) {
                        b.Walk(walker);
                    }
                }
                if (_body != null) {
                    _body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        private string FindSelfNames() {
            SuiteStatement stmts = Body as SuiteStatement;
            if (stmts == null) return "";

            foreach (Statement stmt in stmts.Statements) {
                FunctionDefinition def = stmt as FunctionDefinition;
                if (def != null && def.Name == "__init__") {
                    return string.Join(",", SelfNameFinder.FindNames(def));
                }
            }
            return "";
        }

        private class SelfNameFinder : PythonWalker {
            private readonly FunctionDefinition _function;
            private readonly Parameter _self;

            public SelfNameFinder(FunctionDefinition function, Parameter self) {
                _function = function;
                _self = self;
            }

            public static string[] FindNames(FunctionDefinition function) {
                var parameters = function.Parameters;

                if (parameters.Count > 0) {
                    SelfNameFinder finder = new SelfNameFinder(function, parameters[0]);
                    function.Body.Walk(finder);
                    return ArrayUtils.ToArray(finder._names.Keys);
                } else {
                    // no point analyzing function with no parameters
                    return ArrayUtils.EmptyStrings;
                }
            }

            private Dictionary<string, bool> _names = new Dictionary<string, bool>(StringComparer.Ordinal);

            private bool IsSelfReference(Expression expr) {
                NameExpression ne = expr as NameExpression;
                if (ne == null) return false;

                PythonVariable variable;
                if (_function.TryGetVariable(ne.Name, out variable) && variable == _self.PythonVariable) {
                    return true;
                }

                return false;
            }

            // Don't recurse into class or function definitions
            public override bool Walk(ClassDefinition node) {
                return false;
            }
            public override bool Walk(FunctionDefinition node) {
                return false;
            }

            public override bool Walk(AssignmentStatement node) {
                foreach (Expression lhs in node.Left) {
                    MemberExpression me = lhs as MemberExpression;
                    if (me != null) {
                        if (IsSelfReference(me.Target)) {
                            _names[me.Name] = true;
                        }
                    }
                }
                return true;
            }
        }

        internal override void RewriteBody(PythonAst.LookupVisitor visitor) {
            _dlrBody = null;
            _body = new PythonAst.RewrittenBodyStatement(Body, visitor.Visit(Body));
        }
    }
}
