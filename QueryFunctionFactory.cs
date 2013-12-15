#region Usings

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Dapper.Neat.Mapper;

#endregion

namespace Dapper.Neat
{
    public static class QueryFunctionFactory
    {
        private static readonly ConcurrentDictionary<int, object> _cachedQueryFunctions =
            new ConcurrentDictionary<int, object>();

        internal static IEnumerable<TReturn> CallQuery<TReturn, TFunc>(this IDbConnection cnn, string sql, TFunc map,
            dynamic param = null,
            IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id",
            int? commandTimeout = null,
            CommandType? commandType = null)
        {
            int hashKey = map.GetHashCode();
            if (!_cachedQueryFunctions.ContainsKey(hashKey))
            {
                _cachedQueryFunctions.AddOrUpdate(hashKey,
                    BuildFunction<TReturn, TFunc>(), (key, value) => value);
            }
            return ((Func<IDbConnection, string, TFunc, dynamic, IDbTransaction, bool, string, int?, CommandType?,
                IEnumerable<TReturn>>) _cachedQueryFunctions[hashKey])(cnn, sql, map, param, transaction, buffered,
                    splitOn, commandTimeout, commandType);
        }

        private static
            Func
                <IDbConnection, string, TFunc, dynamic, IDbTransaction, bool, string, int?, CommandType?,
                    IEnumerable<TReturn>>
            BuildFunction<TReturn, TFunc>()
        {
            var mappingDescriptor = new MappingFunctionDescriptor(typeof (TFunc));
            MethodInfo queryMethodSignature = GetSqlMapperQueryMethod(mappingDescriptor);

            // variables that are visible in main scope
            var variables = new List<ParameterExpression>();
            //body of main query method
            var methodBodyExpressions = new List<Expression>();
            ////query method incoming parameters
            //var queryMethodParameters = new List<ParameterExpression>();
            // parameters for fake map function
            var mappingParameters = new List<ParameterExpression>();
            // body for fake map function
            var mappingBodyExpressions = new List<Expression>();

            // declare and assign dictionaries and id evaluators for mapped types
            foreach (Type type in mappingDescriptor.GenericTypes.Take(mappingDescriptor.GenericTypes.Length - 1))
            {
                IStructureMap mapper = LeanMapper.GetMapper(type);
                mappingParameters.Add(Expression.Parameter(type));
                if (mapper == null)
                    continue;
                SetupIdDataChecker(variables, methodBodyExpressions, mappingParameters.Last(), mappingBodyExpressions,
                    mapper);
            }

            // declare parameters for main query method (they will be passed down to inner SqlMapper.Query method call)
            ParameterExpression cnnP = Expression.Parameter(typeof (IDbConnection), "cnn");
            ParameterExpression sqlP = Expression.Parameter(typeof (string), "sql");
            ParameterExpression mapParam = Expression.Parameter(mappingDescriptor.MappingType, "map");
            ParameterExpression paramP = Expression.Parameter(typeof (object), "param");
            ParameterExpression transactionP = Expression.Parameter(typeof (IDbTransaction), "transaction");
            ParameterExpression bufferedP = Expression.Parameter(typeof (bool), "buffered");
            ParameterExpression splitOnP = Expression.Parameter(typeof (string), "splitOn");
            ParameterExpression commandTimeoutP = Expression.Parameter(typeof (int?), "commandTimeout");
            ParameterExpression commandTypeP = Expression.Parameter(typeof (CommandType?), "commandType");
            ParameterExpression newMapper = Expression.Parameter(mappingDescriptor.MappingType, "newMapper");
            methodBodyExpressions.Add(Expression.Assign(newMapper,
                FinishFakeMappingFunction<TReturn, TFunc>(mapParam, mappingParameters, mappingBodyExpressions)));
            variables.Add(newMapper);
            //call REAL Query Method
            MethodCallExpression callRealDapperMethod = Expression.Call(queryMethodSignature, cnnP, sqlP, newMapper,
                paramP, transactionP, bufferedP, splitOnP, commandTimeoutP, commandTypeP);

            LabelTarget returnFinalTarget = Expression.Label(typeof (IEnumerable<TReturn>));
            GotoExpression returnFinalValue = Expression.Return(returnFinalTarget, callRealDapperMethod);
            LabelExpression rerturnFinalExpression = Expression.Label(returnFinalTarget,
                Expression.Default(typeof (IEnumerable<TReturn>)));
            methodBodyExpressions.Add(returnFinalValue);
            methodBodyExpressions.Add(rerturnFinalExpression);

            return
                Expression
                    .Lambda
                    <Func<IDbConnection, string, TFunc, dynamic, IDbTransaction, bool, string, int?, CommandType?,
                        IEnumerable<TReturn>>>(
                            Expression.Block(variables, methodBodyExpressions),
                            cnnP, sqlP, mapParam, paramP, transactionP, bufferedP, splitOnP, commandTimeoutP,
                            commandTypeP).Compile();
        }

        private static MethodCallExpression BuildUniquenessCheckerExpression(IStructureMap mapper,
            ParameterExpression dictionary, ParameterExpression idPropertyEvaluator, ParameterExpression data)
        {
            MethodInfo getUniqueDataFunction =
                ExpressionHelper.MakeGenericMethod(() => GetUniqueData<string, string>(null, null, null),
                    mapper.IdPropertyType, mapper.SourceType);
            return Expression.Call(getUniqueDataFunction, dictionary, idPropertyEvaluator, data);
        }

        private static Expression<TFunc> FinishFakeMappingFunction<TReturn, TFunc>(Expression mapParam,
            List<ParameterExpression> mappingParameters, List<Expression> mappingBodyExpressions)
        {
            InvocationExpression invokeRealMappingFunc = Expression.Invoke(mapParam, mappingParameters);
            LabelTarget returnTarget = Expression.Label(typeof (TReturn));
            GotoExpression returnValue = Expression.Return(returnTarget, invokeRealMappingFunc);
            LabelExpression rerturnExpression = Expression.Label(returnTarget, Expression.Default(typeof (TReturn)));
            mappingBodyExpressions.Add(returnValue);
            mappingBodyExpressions.Add(rerturnExpression);
            return Expression.Lambda<TFunc>(Expression.Block(mappingBodyExpressions), mappingParameters);
        }

        private static Tuple<ParameterExpression, Type> GetDictionaryVariableForUniqueCheck(Type key, Type value)
        {
            Type type = typeof (Dictionary<,>).MakeGenericType(new[] {key, value});
            return new Tuple<ParameterExpression, Type>(Expression.Parameter(type), type);
        }

        private static Tuple<ParameterExpression, ConstantExpression> GetIdEvaluatorExpression(IStructureMap mapper)
        {
            MethodInfo methodInfo =
                ExpressionHelper.MakeGenericMethod(() => GetIdEvaluatorFromMapper<string, string>(null),
                    mapper.SourceType, mapper.DestinationType);
            var idPropertyEvaluatorConstant = methodInfo.Invoke(null, new object[] {mapper}) as ConstantExpression;
            if(idPropertyEvaluatorConstant==null)
                throw new InvalidCastException("cant't cast idPropertyEvaluatorConstant to ConstantExpression");
            ParameterExpression idPropertyEvaluatorParameter = Expression.Parameter(idPropertyEvaluatorConstant.Type);
            return new Tuple<ParameterExpression, ConstantExpression>(idPropertyEvaluatorParameter,
                idPropertyEvaluatorConstant);
        }

        private static Expression GetIdEvaluatorFromMapper<TSource, TDestination>(IStructureMap mapper)
            where TSource : class
        {
            IStructureMapItem<TSource, TDestination> idProperty =
                (mapper as StructureMap<TSource, TDestination>).GetIdPropertyItem();
            MethodInfo methodInfo =
                ExpressionHelper.MakeGenericMethod(() => GetIdEvaluatorFromMappingItem<string, string, string>(null),
                    typeof (TSource), mapper.DestinationType, idProperty.ResultType);
            return methodInfo.Invoke(null, new object[] {idProperty}) as Expression;
        }

        private static Expression GetIdEvaluatorFromMappingItem<TSource, TDestination, TResult>(
            IStructureMapItem<TSource, TDestination> mappingItem)
        {
            return Expression.Constant((mappingItem as StructureMapItem<TSource, TDestination, TResult>).PropertyGetter);
        }

        private static MethodInfo GetSqlMapperQueryMethod(MappingFunctionDescriptor funcDescriptor)
        {
            MethodInfo firstOrDefault =
                typeof (SqlMapper).GetMethods()
                    .FirstOrDefault(
                        m =>
                            m.Name == "Query" && m.IsGenericMethod &&
                            m.GetGenericArguments().Length == funcDescriptor.GenericTypes.Length);
            if (firstOrDefault != null)
                return firstOrDefault.MakeGenericMethod(funcDescriptor.GenericTypes);
            return null;
        }

        private static TValue GetUniqueData<TKey, TValue>(Dictionary<TKey, TValue> dict, Func<TValue, TKey> evaluator,
            TValue data) 
        {
            if (data == null)
                return dict.Values.FirstOrDefault();
            TKey key = evaluator(data);
            if (!dict.ContainsKey(key))
                dict.Add(key, data);

            return dict[key];
        }

        private static void SetupIdDataChecker(List<ParameterExpression> variables,
            List<Expression> bodyExpressions,
            ParameterExpression dataParameter,
            List<Expression> mappingBodyExpressions,
            IStructureMap mapper)
        {
            //get and assign dictionaries
            Tuple<ParameterExpression, Type> dictParamAndVariable =
                GetDictionaryVariableForUniqueCheck(mapper.IdPropertyType, mapper.SourceType);
            BinaryExpression dictAssignment = Expression.Assign(dictParamAndVariable.Item1,
                Expression.New(dictParamAndVariable.Item2));
            variables.Add(dictParamAndVariable.Item1);
            bodyExpressions.Add(dictAssignment);
            //get and assign id evaluators
            Tuple<ParameterExpression, ConstantExpression> propertyEvaluatorParamAndValue =
                GetIdEvaluatorExpression(mapper);
            BinaryExpression evaulatorAssignment = Expression.Assign(propertyEvaluatorParamAndValue.Item1,
                propertyEvaluatorParamAndValue.Item2);
            variables.Add(propertyEvaluatorParamAndValue.Item1);
            bodyExpressions.Add(evaulatorAssignment);
            // declare data parameters and assign them to inner fake mapping function declaration
            MethodCallExpression checkerExpression = BuildUniquenessCheckerExpression(mapper, dictParamAndVariable.Item1,
                propertyEvaluatorParamAndValue.Item1, dataParameter);
            mappingBodyExpressions.Add(Expression.Assign(dataParameter, checkerExpression));
        }
    }

    internal sealed class MappingFunctionDescriptor
    {
        public MappingFunctionDescriptor(Type type)
        {
            MappingType = type;
            GenericTypes = type.GetGenericArguments();
        }

        public Type[] GenericTypes { get; set; }
        public Type MappingType { get; set; }
        public Type ReturnType { get; set; }
    }
}