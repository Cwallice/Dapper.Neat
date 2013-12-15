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

        public static string GetPropertyName(Expression propertyExpression)
        {
            if (propertyExpression is UnaryExpression)
                return GetPropertyName((propertyExpression as UnaryExpression).Operand);
            if (propertyExpression.NodeType != ExpressionType.MemberAccess)
                return null;
            return (propertyExpression as MemberExpression).Member.Name;
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
        public static Tuple<string, string, object> ParseExpression<TSource>(Expression<Func<TSource, bool>> predicate) //Experiments
        {
            //1 checkout if expression is unary
            var body = predicate.Body;
            string left = String.Empty;
            object right = null;
            string condition = "=";
            if (body is UnaryExpression && body.NodeType == ExpressionType.Not)
            {
                left = ((body as UnaryExpression).Operand as MemberExpression).Member.Name;
                right = 0;
            }

            if (body.NodeType == ExpressionType.MemberAccess)
            {
                left = (body as MemberExpression).Member.Name;
                right = 1;
            }


            if (body is BinaryExpression)
            {
                left = LeanMapper.GetMapper<TSource>().GetPropertyName(BinaryPartExpression((body as BinaryExpression).Left));
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


            //if (body is MethodCallExpression && (((body as MethodCallExpression).Object as MemberExpression).Member as PropertyInfo).PropertyType == typeof(string))
            //{
            //    if ((body as MethodCallExpression).Method.Name == "StartsWith")
            //        condition = String.Format("{0} like '{1}%'", LeanMapper.GetMapper<TSource>().GetPropertyName(((body as MethodCallExpression).Object as MemberExpression).Member.Name),
            //                                  BinaryPartExpression((body as MethodCallExpression).Arguments[0]));
            //    if ((body as MethodCallExpression).Method.Name == "EndsWith")
            //        condition = String.Format("{0} like '%{1}'", LeanMapper.GetMapper<TSource>().GetPropertyName(((body as MethodCallExpression).Object as MemberExpression).Member.Name),
            //                                  BinaryPartExpression((body as MethodCallExpression).Arguments[0]));
            //}
            //dirty fix
            return new Tuple<string, string, object>(left, condition, right);
        }

        private static string BinaryPartExpression(Expression exp)
        {
            object evaluated;
            if (TryEvaluate(exp, out evaluated) && (exp.NodeType == ExpressionType.MemberAccess || exp.NodeType == ExpressionType.Constant))
                return evaluated.ToString();
            if (exp is MethodCallExpression)
                return Expression.Lambda(exp as MethodCallExpression).Compile().DynamicInvoke().ToString();
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