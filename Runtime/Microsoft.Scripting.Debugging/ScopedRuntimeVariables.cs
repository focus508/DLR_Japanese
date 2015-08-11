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
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.Scripting.Debugging
{
	/// <summary>ラムダ内のスコープ化を保存する方法で <see cref="IRuntimeVariables"/> を実装します。</summary>
	class ScopedRuntimeVariables : IRuntimeVariables
	{
		readonly IList<VariableInfo> _variableInfos;
		readonly IRuntimeVariables _variables;

		internal ScopedRuntimeVariables(IList<VariableInfo> variableInfos, IRuntimeVariables variables)
		{
			_variableInfos = variableInfos;
			_variables = variables;
		}

		public int Count { get { return _variableInfos.Count; } }

		public object this[int index]
		{
			get
			{
				Debug.Assert(index < _variableInfos.Count);
				return _variables[_variableInfos[index].GlobalIndex];
			}
			set
			{
				Debug.Assert(index < _variableInfos.Count);
				_variables[_variableInfos[index].GlobalIndex] = value;
			}
		}
	}
}
