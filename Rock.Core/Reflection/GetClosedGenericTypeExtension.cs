using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Rock.Reflection
{
    public static class GetClosedGenericTypeExtension
    {
        public static Type GetClosedGenericType(this Type targetType, Type openGenericType, IEnumerable<Type> typeArguments = null)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException("targetType");
            }

            if (targetType.GetTypeInfo().IsGenericTypeDefinition)
            {
                throw new ArgumentException("targetType type must not be an open generic type.", "targetType");
            }

            if (openGenericType == null)
            {
                throw new ArgumentNullException("openGenericType");
            }

            if (!openGenericType.GetTypeInfo().IsGenericTypeDefinition)
            {
                throw new ArgumentException("openGenericType type must be an open generic type.", "targetType");
            }

            Func<Type, bool> isSpecifiedOpenGenericType;

            var typeArgumentsToMatch = typeArguments != null ? typeArguments.ToList() : new List<Type>();
            if (typeArgumentsToMatch.Count > 0)
            {
                if (typeArgumentsToMatch.Count != openGenericType.GetGenericArguments().Length)
                {
                    throw new ArgumentException("typeArguments must be the same length as the generic arguments of openGenericType.", "typeArguments");
                }

                isSpecifiedOpenGenericType =
                    t =>
                        t.GetTypeInfo().IsGenericType
                        && t.GetGenericTypeDefinition() == openGenericType
                        && t.GetGenericArguments().Select((genericArgument, index) => new { genericArgument, index }).Count(x => typeArgumentsToMatch[x.index] == null || x.genericArgument == typeArgumentsToMatch[x.index]) == typeArgumentsToMatch.Count;
            }
            else
            {
                isSpecifiedOpenGenericType =
                    t =>
                        t.GetTypeInfo().IsGenericType
                        && t.GetGenericTypeDefinition() == openGenericType;
            }

            if (openGenericType.GetTypeInfo().IsInterface)
            {
                if (targetType.GetTypeInfo().IsInterface && isSpecifiedOpenGenericType(targetType))
                {
                    return targetType;
                }

                return targetType.GetInterfaces().FirstOrDefault(isSpecifiedOpenGenericType);
            }

            var type = targetType;

            do
            {
                if (isSpecifiedOpenGenericType(type))
                {
                    return type;
                }

                type = type.GetTypeInfo().BaseType;
            } while (type != null);

            return type;
        }
    }
}