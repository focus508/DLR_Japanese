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

using MSAst = System.Linq.Expressions;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {

    public class EmptyStatement : Statement {
        internal static EmptyStatement PreCompiledInstance = new EmptyStatement();

        public EmptyStatement() {
        }

        public override MSAst.Expression Reduce() {
            return GlobalParent.AddDebugInfoAndVoid(AstUtils.Empty(), Span);
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow {
            get {
                return false;
            }
        }
    }
}
