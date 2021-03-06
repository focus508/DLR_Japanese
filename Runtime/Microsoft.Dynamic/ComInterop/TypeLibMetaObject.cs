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

using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace Microsoft.Scripting.ComInterop
{
	class TypeLibMetaObject : DynamicMetaObject
	{
		readonly ComTypeLibDesc _lib;

		internal TypeLibMetaObject(Expression expression, ComTypeLibDesc lib) : base(expression, BindingRestrictions.Empty, lib) { _lib = lib; }

		DynamicMetaObject TryBindGetMember(string name)
		{
			if (_lib.HasMember(name))
				return new DynamicMetaObject(
					AstUtils.Constant(((ComTypeLibDesc)Value).GetTypeLibObjectDesc(name)),
					BindingRestrictions.GetTypeRestriction(Expression, typeof(ComTypeLibDesc)).Merge(
						BindingRestrictions.GetExpressionRestriction(
							Expression.Equal(
								Expression.Property(AstUtils.Convert(Expression, typeof(ComTypeLibDesc)), typeof(ComTypeLibDesc).GetProperty("Guid")),
								AstUtils.Constant(_lib.Guid)
							)
						)
					)
				);
			return null;
		}

		public override DynamicMetaObject BindGetMember(GetMemberBinder binder) { return TryBindGetMember(binder.Name) ?? base.BindGetMember(binder); }

		public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
		{
			var result = TryBindGetMember(binder.Name);
			return result != null ? binder.FallbackInvoke(result, args, null) : base.BindInvokeMember(binder, args);
		}

		public override IEnumerable<string> GetDynamicMemberNames() { return _lib.GetMemberNames(); }
	}
}