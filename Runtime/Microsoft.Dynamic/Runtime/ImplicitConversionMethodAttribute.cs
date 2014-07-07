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

namespace Microsoft.Scripting.Runtime
{
	/// <summary>メソッドによって暗黙的なユーザー定義の変換を行うことが可能であることを DLR に通知します。</summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public sealed class ImplicitConversionMethodAttribute : Attribute
	{
		/// <summary><see cref="Microsoft.Scripting.Runtime.ImplicitConversionMethodAttribute"/> クラスの新しいインスタンスを初期化します。</summary>
		public ImplicitConversionMethodAttribute() { }
	}
}
