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

using System.Linq.Expressions;

namespace Microsoft.Scripting.ComInterop
{
	/// <summary>常に null を生成する <see cref="ArgBuilder"/> を表します。</summary>
	sealed class NullArgBuilder : ArgBuilder
	{
		internal NullArgBuilder() { }

		internal override Expression Marshal(Expression parameter) { return Expression.Constant(null); }
	}
}