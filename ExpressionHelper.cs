#region Usings
using System;
using System.Linq.Expressions;
using System.Reflection;
using Dapper.Neat.Mapper;
#endregion

namespace Dapper.Neat
{
    public class ExpressionHelper
    {
        public static ParameterExpression CreateGenericFuncParameter(string name, params Type[] types)
        {
            return Expression.Parameter(typeof (Expression<>).MakeGenericType(typeof (Func<,>).MakeGenericType(types)), name);
        }

        public static Expression GetPropertyExpression(PropertyInfo property)
        {
            var parameter = Expression.Parameter(property.ReflectedType, "source");
            var propertyExpression = Expression.Property(parameter, property);
            var delegateType = typeof (Func<,>).MakeGenericType(property.ReflectedType, property.PropertyType);
            return Expression.Lambda(delegateType, propertyExpression, parameter);
        }

        public static Expression<Func<TSource, TResult>> GetPropertyExpression<TSource, TResult>(PropertyInfo property)
        {
            var parameter = Expression.Parameter(typeof (TSource), "source");
            var propertyExpression = Expression.Property(parameter, property);
            return Expression.Lambda<Func<TSource, TResult>>(propertyExpression, parameter);
        }

        public static MemberInfo GetMemberInfo(Expression memberExpression)
        {
            if (memberExpression is UnaryExpression)
                return GetMemberInfo((memberExpression as UnaryExpression).Operand);
            if (memberExpression.NodeType != ExpressionType.MemberAccess)
                return null;
            return (memberExpression as MemberExpression).Member;
        }

        public static string GetPropertyName(Expression propertyExpression)
        {
            var memberinfo = GetMemberInfo(propertyExpression);
            return memberinfo != null ? memberinfo.Name : null;
        }

        public static MethodInfo MakeGenericMethod<T1>(Expression<Func<T1>> action, params Type[] genericTypes)
        {
            return MakeGenericMethod(action as LambdaExpression, genericTypes);
        }

        public static MethodInfo MakeGenericMethod<T1, T2>(Expression<Func<T1, T2>> action, params Type[] genericTypes)
        {
            return MakeGenericMethod(action as LambdaExpression, genericTypes);
        }

        public static MethodInfo MakeGenericMethod(LambdaExpression action, params Type[] genericTypes)
        {
            if (action.Body.NodeType != ExpressionType.Call && action.Body as MethodCallExpression == null)
                return null;
            var method = (action.Body as MethodCallExpression).Method;
            if ((genericTypes == null || genericTypes.Length == 0) && !method.IsGenericMethodDefinition)
                return method;
            return method.GetGenericMethodDefinition().MakeGenericMethod(genericTypes);
        }
    }

    public static class ExpressionParser
    {
        public static Tuple<string, string, object, Type> ParseExpression<TSource>(Expression<Func<TSource, bool>> predicate) //Experiments
        {
            //1 checkout if expression is unary
            var body = predicate.Body;
            string left = String.Empty;
            object right = null;
            Type propertyType = null;
            string condition = "=";
            if (body is UnaryExpression && body.NodeType == ExpressionType.Not)
            {
                var member = ((body as UnaryExpression).Operand as MemberExpression).Member;
                left = member.Name;
                propertyType = member.DeclaringType;
                right = 0;
            }

            if (body.NodeType == ExpressionType.MemberAccess)
            {
                var member = (body as MemberExpression).Member;
                left = member.Name;
                propertyType = member.DeclaringType;
                right = 1;
            }


            if (body is BinaryExpression)
            {
                var propertyItem =
                    LeanMapper.GetMapper<TSource>()
                        .GetPropertyItem(BinaryPartExpression((body as BinaryExpression).Left).ToString());
                left = propertyItem.DestinationName;
                propertyType = propertyItem.ResultType;
                right = BinaryPartExpression((body as BinaryExpression).Right);
                switch (body.NodeType)
                {
                    case ExpressionType.Equal:
                        condition = "=";
                        break;
                    case ExpressionType.NotEqual:
                        condition = "!=";
                        break;
                    case ExpressionType.LessThan:
                        condition = "<";
                        break;
                    case ExpressionType.LessThanOrEqual:
                        condition = "<=";
                        break;
                    case ExpressionType.GreaterThan:
                        condition = ">";
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        condition = ">=";
                        break;
                }
            }
            //dirty fix
            return new Tuple<string, string, object, Type>(left, condition, right, propertyType);
        }

        private static object BinaryPartExpression(Expression exp)
        {
            object evaluated;
            if (TryEvaluate(exp, out evaluated) && (exp.NodeType == ExpressionType.MemberAccess || exp.NodeType == ExpressionType.Constant))
                return evaluated;
            if (exp is MethodCallExpression)
                return Expression.Lambda(exp as MethodCallExpression).Compile().DynamicInvoke();
            if (exp is UnaryExpression)
                return BinaryPartExpression((exp as UnaryExpression).Operand);
            return (exp as MemberExpression).Member.Name;
        }

        private static bool TryEvaluate(Expression operation, out object value)
        {
            if (operation == null)
            {
                // used for static fields, etc
                value = null;
                return true;
            }
            switch (operation.NodeType)
            {
                case ExpressionType.Constant:
                    value = ((ConstantExpression) operation).Value;
                    return true;
                case ExpressionType.MemberAccess:
                    MemberExpression me = (MemberExpression) operation;
                    object target;
                    if (TryEvaluate(me.Expression, out target))
                    {
                        // instance target
                        switch (me.Member.MemberType)
                        {
                            case MemberTypes.Field:
                                value = ((FieldInfo) me.Member).GetValue(target);
                                return true;
                            case MemberTypes.Property:
                                value = ((PropertyInfo) me.Member).GetValue(target, null);
                                return true;
                        }
                    }
                    break;
            }
            value = null;
            return false;
        }
    }
}