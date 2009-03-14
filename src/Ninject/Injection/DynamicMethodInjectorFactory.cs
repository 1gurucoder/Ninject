﻿#region License
// Author: Nate Kohari <nate@enkari.com>
// Copyright (c) 2007-2009, Enkari, Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion
#region Using Directives
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Ninject.Components;
using Ninject.Infrastructure;
using Ninject.Infrastructure.Language;
#endregion

namespace Ninject.Injection
{
	/// <summary>
	/// Creates injectors for members via <see cref="DynamicMethod"/>s.
	/// </summary>
	public class DynamicMethodInjectorFactory : NinjectComponent, IInjectorFactory
	{
		/// <summary>
		/// Gets or creates an injector for the specified constructor.
		/// </summary>
		/// <param name="constructor">The constructor.</param>
		/// <returns>The created injector.</returns>
		public ConstructorInjector Create(ConstructorInfo constructor)
		{
			var dynamicMethod = new DynamicMethod(GetAnonymousMethodName(), typeof(object), new[] { typeof(object[]) });
			ILGenerator il = dynamicMethod.GetILGenerator();

			EmitLoadMethodArguments(il, constructor);
			il.Emit(OpCodes.Newobj, constructor);

			if (constructor.ReflectedType.IsValueType)
				il.Emit(OpCodes.Box, constructor.ReflectedType);

			il.Emit(OpCodes.Ret);

			return (ConstructorInjector) dynamicMethod.CreateDelegate(typeof(ConstructorInjector));
		}

		/// <summary>
		/// Gets or creates an injector for the specified property.
		/// </summary>
		/// <param name="property">The property.</param>
		/// <returns>The created injector.</returns>
		public PropertyInjector Create(PropertyInfo property)
		{
			var dynamicMethod = new DynamicMethod(GetAnonymousMethodName(), typeof(void), new[] { typeof(object), typeof(object) });
			ILGenerator il = dynamicMethod.GetILGenerator();

			il.Emit(OpCodes.Ldarg_0);
			EmitUnboxOrCast(il, property.DeclaringType);

			il.Emit(OpCodes.Ldarg_1);
			EmitUnboxOrCast(il, property.PropertyType);

			EmitMethodCall(il, property.GetSetMethod());

			il.Emit(OpCodes.Ret);

			return (PropertyInjector) dynamicMethod.CreateDelegate(typeof(PropertyInjector));
		}

		/// <summary>
		/// Gets or creates an injector for the specified method.
		/// </summary>
		/// <param name="method">The method.</param>
		/// <returns>The created injector.</returns>
		public MethodInjector Create(MethodInfo method)
		{
			var dynamicMethod = new DynamicMethod(GetAnonymousMethodName(), typeof(void), new[] { typeof(object), typeof(object[]) });
			ILGenerator il = dynamicMethod.GetILGenerator();

			il.Emit(OpCodes.Ldarg_0);
			EmitUnboxOrCast(il, method.DeclaringType);

			EmitLoadMethodArguments(il, method);
			EmitMethodCall(il, method);

			if (method.ReturnType != typeof(void))
				il.Emit(OpCodes.Pop);

			il.Emit(OpCodes.Ret);

			return (MethodInjector) dynamicMethod.CreateDelegate(typeof(MethodInjector));
		}

		private static void EmitLoadMethodArguments(ILGenerator il, MethodBase targetMethod)
		{
			ParameterInfo[] parameters = targetMethod.GetParameters();
			OpCode ldargOpcode = targetMethod is ConstructorInfo ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1;
			
			for (int idx = 0; idx < parameters.Length; idx++)
			{
				il.Emit(ldargOpcode);
				il.Emit(OpCodes.Ldc_I4, idx);
				il.Emit(OpCodes.Ldelem_Ref);

				EmitUnboxOrCast(il, parameters[idx].ParameterType);
			}
		}

		private static void EmitMethodCall(ILGenerator il, MethodInfo method)
		{
			OpCode opCode = method.IsFinal ? OpCodes.Call : OpCodes.Callvirt;
			il.Emit(opCode, method);
		}

		private static void EmitUnboxOrCast(ILGenerator il, Type type)
		{
			OpCode opCode = type.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass;
			il.Emit(opCode, type);
		}

		private static string GetAnonymousMethodName()
		{
			return "DynamicInjector" + Guid.NewGuid().ToString("N");
		}
	}
}