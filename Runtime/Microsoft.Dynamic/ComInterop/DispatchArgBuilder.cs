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
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace Microsoft.Scripting.ComInterop
{
	class DispatchArgBuilder : SimpleArgBuilder
	{
		readonly bool _isWrapper;

		internal DispatchArgBuilder(Type parameterType) : base(parameterType) { _isWrapper = parameterType == typeof(DispatchWrapper); }

		internal override Expression Marshal(Expression parameter)
		{
			parameter = base.Marshal(parameter);
			// parameter.WrappedObject
			if (_isWrapper)
				parameter = Expression.Property(Ast.Utils.Convert(parameter, typeof(DispatchWrapper)), typeof(DispatchWrapper).GetProperty("WrappedObject"));
			return Ast.Utils.Convert(parameter, typeof(object));
		}

		internal override Expression MarshalToRef(Expression parameter)
		{
			parameter = Marshal(parameter);
			// parameter == null ? IntPtr.Zero : Marshal.GetIDispatchForObject(parameter);
			return Expression.Condition(Expression.Equal(parameter, Expression.Constant(null)),
				Expression.Constant(IntPtr.Zero),
				Expression.Call(new Func<object, IntPtr>(System.Runtime.InteropServices.Marshal.GetIDispatchForObject).Method, parameter)
			);
		}

		internal override Expression UnmarshalFromRef(Expression value)
		{
			// value == IntPtr.Zero ? null : Marshal.GetObjectForIUnknown(value);
			var unmarshal = Expression.Condition(Expression.Equal(value, Expression.Constant(IntPtr.Zero)),
				Expression.Constant(null),
				Expression.Call(new Func<IntPtr, object>(System.Runtime.InteropServices.Marshal.GetObjectForIUnknown).Method, value)
			);
			return base.UnmarshalFromRef(_isWrapper ? (Expression)Expression.New(typeof(DispatchWrapper).GetConstructor(new[] { typeof(object) }), unmarshal) : unmarshal);
		}
	}
}