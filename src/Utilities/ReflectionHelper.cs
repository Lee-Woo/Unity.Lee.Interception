﻿

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Unity.Interception.Utilities
{
    /// <summary>
    /// A collection of utility functions to encapsulate details of
    /// reflection and finding attributes.
    /// </summary>
    public static class ReflectionHelper
    {
        /// <summary>
        /// Given a MethodBase for a property's get or set method,
        /// return the corresponding property info.
        /// </summary>
        /// <param name="method">MethodBase for the property's get or set method.</param>
        /// <returns>PropertyInfo for the property, or null if method is not part of a property.</returns>
        public static PropertyInfo GetPropertyFromMethod(MethodBase method)
        {
            Guard.ArgumentNotNull(method, "method");

            var methodInfo = method as MethodInfo;
            if (methodInfo != null)
            {
                return GetPropertyFromMethod(methodInfo);
            }

            return null;
        }

        /// <summary>
        /// Given a MethodInfo for a property's get or set method,
        /// return the corresponding property info.
        /// </summary>
        /// <param name="method">MethodBase for the property's get or set method.</param>
        /// <returns>PropertyInfo for the property, or null if method is not part of a property.</returns>
        public static PropertyInfo GetPropertyFromMethod(MethodInfo method)
        {
            Guard.ArgumentNotNull(method, "method");

            PropertyInfo property = null;
            if (method.IsSpecialName)
            {
                var containingType = method.DeclaringType;
                if (containingType != null)
                {
                    var isGetter = method.Name.StartsWith("get_", StringComparison.Ordinal);
                    var isSetter = method.Name.StartsWith("set_", StringComparison.Ordinal);
                    if (isSetter || isGetter)
                    {
                        var propertyName = method.Name.Substring(4);
                        Type propertyType;
                        Type[] indexerTypes;

                        GetPropertyTypes(method, isGetter, out propertyType, out indexerTypes);

                        property =
                            containingType.GetProperty(
                                propertyName,
                                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                                null,
                                propertyType,
                                indexerTypes,
                                null);
                    }
                }
            }
            return property;
        }

        private static void GetPropertyTypes(MethodInfo method, bool isGetter, out Type propertyType, out Type[] indexerTypes)
        {
            var parameters = method.GetParameters();
            if (isGetter)
            {
                propertyType = method.ReturnType;
                indexerTypes =
                    parameters.Length == 0
                        ? Type.EmptyTypes
                        : parameters.Select(pi => pi.ParameterType).ToArray();
            }
            else
            {
                propertyType = parameters[parameters.Length - 1].ParameterType;
                indexerTypes =
                    parameters.Length == 1
                        ? Type.EmptyTypes
                        : parameters.Take(parameters.Length - 1).Select(pi => pi.ParameterType).ToArray();
            }
        }

        /// <summary>
        /// Given a particular MemberInfo, return the custom attributes of the
        /// given type on that member.
        /// </summary>
        /// <typeparam name="TAttribute">Type of attribute to retrieve.</typeparam>
        /// <param name="member">The member to look at.</param>
        /// <param name="inherits">True to include attributes inherited from base classes.</param>
        /// <returns>Array of found attributes.</returns>
        public static TAttribute[] GetAttributes<TAttribute>(MemberInfo member, bool inherits) where TAttribute : Attribute
        {
            Guard.ArgumentNotNull(member, "member");

            object[] attributesAsObjects = member.GetCustomAttributes(typeof(TAttribute), inherits);
            TAttribute[] attributes = new TAttribute[attributesAsObjects.Length];
            int index = 0;
            Array.ForEach(attributesAsObjects,
                delegate(object o)
                {
                    attributes[index++] = (TAttribute)o;
                });
            return attributes;
        }

        /// <summary>
        /// Given a particular MemberInfo, find all the attributes that apply to this
        /// member. Specifically, it returns the attributes on the type, then (if it's a
        /// property accessor) on the property, then on the member itself.
        /// </summary>
        /// <typeparam name="TAttribute">Type of attribute to retrieve.</typeparam>
        /// <param name="member">The member to look at.</param>
        /// <param name="inherits">true to include attributes inherited from base classes.</param>
        /// <returns>Array of found attributes.</returns>
        public static TAttribute[] GetAllAttributes<TAttribute>(MemberInfo member, bool inherits)
            where TAttribute : Attribute
        {
            Guard.ArgumentNotNull(member, "member");

            List<TAttribute> attributes = new List<TAttribute>();

            if (member.DeclaringType != null)
            {
                attributes.AddRange(GetAttributes<TAttribute>(member.DeclaringType, inherits));

                MethodInfo methodInfo = member as MethodInfo;
                if (methodInfo != null)
                {
                    PropertyInfo prop = GetPropertyFromMethod(methodInfo);
                    if (prop != null)
                    {
                        attributes.AddRange(GetAttributes<TAttribute>(prop, inherits));
                    }
                }
            }
            attributes.AddRange(GetAttributes<TAttribute>(member, inherits));
            return attributes.ToArray();
        }

        public static readonly MethodInfo ExceptionDispatchInfoCaptureMethod;

        public static readonly MethodInfo ExceptionDispatchInfoThrowMethod;

        static ReflectionHelper()
        {
            Assembly mscorlib = typeof(int).Assembly;
            ExceptionDispatchInfoCaptureMethod = mscorlib
                ?.GetType("System.Runtime.ExceptionServices.ExceptionDispatchInfo")
                ?.GetMethod("Capture", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(Exception) }, null);

            ExceptionDispatchInfoThrowMethod = mscorlib
                ?.GetType("System.Runtime.ExceptionServices.ExceptionDispatchInfo")
                ?.GetMethod("Throw", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { }, null);
        }
    }
}
