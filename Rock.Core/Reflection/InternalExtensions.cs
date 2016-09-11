using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Rock.Reflection
{
    internal static class InternalExtensions
    {
        public static Expression EnsureConvertableTo<T>(this Expression expression)
        {
            return expression.EnsureConvertableTo(typeof(T));
        }

        public static Expression EnsureConvertableTo(this Expression expression, Type type)
        {
            if (expression.Type.IsLessSpecificThan(type)
                || expression.Type.RequiresBoxingWhenConvertingTo(type))
            {
                return Expression.Convert(expression, type);
            }

            return expression;
        }

        public static bool RequiresBoxingWhenConvertingTo(this Type fromType, Type toType)
        {
            return fromType.GetTypeInfo().IsValueType && !toType.GetTypeInfo().IsValueType;
        }
    }
}