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
        private static readonly ConcurrentDictionary<int, object> _cachedQueryFunctions = new ConcurrentDictionary<int, object>();

        internal static IEnumerable<TReturn> CallQuery<TReturn, TFunc>(this IDbConnection cnn, string sql, TFunc map, dynamic param = null,
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

        private static Func<IDbConnection, string, TFunc, dynamic, IDbTransaction, bool, string, int?, CommandType?, IEnumerable<TReturn>>
            BuildFunction<TReturn, TFunc>()
        {
            MappingFunctionDescriptor mappingDescriptor = new MappingFunctionDescriptor(typeof (TFunc));
            var queryMethodSignature = GetSqlMapperQueryMethod(mappingDescriptor);

            // variables that are visible in main scope
            var variables = new List<ParameterExpression>();
            //body of main query method
            var methodBodyExpressions = new List<Expression>();
            //query method incoming parameters
            var queryMethodParameters = new List<ParameterExpression>();
            // parameters for fake map function
            var mappingParameters = new List<ParameterExpression>();
            // body for fake map function
            var mappingBodyExpressions = new List<Expression>();

            // declare and assign dictionaries and id evaluators for mapped types
            foreach (var type in mappingDescriptor.GenericTypes.Take(mappingDescriptor.GenericTypes.Length - 1))
            {
                var mapper = LeanMapper.GetMapper(type);
                mappingParameters.Add(Expression.Parameter(type));
                if (mapper == null)
                    continue;
                SetupIdDataChecker(variables, methodBodyExpressions, mappingParameters.Last(), mappingBodyExpressions, mapper);
            }

            // declare parameters for main query method (they will be passed down to inner SqlMapper.Query method call)
            var cnnP = Expression.Parameter(typeof (IDbConnection), "cnn");
            var sqlP = Expression.Parameter(typeof (string), "sql");
            var mapParam = Expression.Parameter(mappingDescriptor.MappingType, "map");
            var paramP = Expression.Parameter(typeof (object), "param");
            var transactionP = Expression.Parameter(typeof (IDbTransaction), "transaction");
            var bufferedP = Expression.Parameter(typeof (bool), "buffered");
            var splitOnP = Expression.Parameter(typeof (string), "splitOn");
            var commandTimeoutP = Expression.Parameter(typeof (int?), "commandTimeout");
            var commandTypeP = Expression.Parameter(typeof (CommandType?), "commandType");
            var newMapper = Expression.Parameter(mappingDescriptor.MappingType, "newMapper");
            methodBodyExpressions.Add(Expression.Assign(newMapper, FinishFakeMappingFunction<TReturn, TFunc>(mapParam, mappingParameters, mappingBodyExpressions)));
            variables.Add(newMapper);
            //call REAL Query Method
            var callRealDapperMethod = Expression.Call(queryMethodSignature, cnnP, sqlP, newMapper, paramP, transactionP, bufferedP, splitOnP, commandTimeoutP, commandTypeP);

            LabelTarget returnFinalTarget = Expression.Label(typeof (IEnumerable<TReturn>));
            var returnFinalValue = Expression.Return(returnFinalTarget, callRealDapperMethod);
            LabelExpression rerturnFinalExpression = Expression.Label(returnFinalTarget, Expression.Default(typeof (IEnumerable<TReturn>)));
            methodBodyExpressions.Add(returnFinalValue);
            methodBodyExpressions.Add(rerturnFinalExpression);

            return Expression.Lambda<Func<IDbConnection, string, TFunc, dynamic, IDbTransaction, bool, string, int?, CommandType?,
                IEnumerable<TReturn>>>(
                    Expression.Block(variables, methodBodyExpressions),
                    cnnP, sqlP, mapParam, paramP, transactionP, bufferedP, splitOnP, commandTimeoutP, commandTypeP).Compile();
        }

        private static MethodCallExpression BuildUniquenessCheckerExpression(IStructureMap mapper, ParameterExpression dictionary, ParameterExpression idPropertyEvaluator, ParameterExpression Data)
        {
            var getUniqueDataFunction = ExpressionHelper.MakeGenericMethod(() => GetUniqueData<string, string>(null, null, null), mapper.IdPropertyType, mapper.SourceType);
            return Expression.Call(getUniqueDataFunction, dictionary, idPropertyEvaluator, Data);
        }

        private static Expression<TFunc> FinishFakeMappingFunction<TReturn, TFunc>(Expression mapParam, List<ParameterExpression> mappingParameters, List<Expression> mappingBodyExpressions)
        {
            var invokeRealMappingFunc = Expression.Invoke(mapParam, mappingParameters);
            LabelTarget returnTarget = Expression.Label(typeof (TReturn));
            var returnValue = Expression.Return(returnTarget, invokeRealMappingFunc);
            LabelExpression rerturnExpression = Expression.Label(returnTarget, Expression.Default(typeof (TReturn)));
            mappingBodyExpressions.Add(returnValue);
            mappingBodyExpressions.Add(rerturnExpression);
            return Expression.Lambda<TFunc>(Expression.Block(mappingBodyExpressions), mappingParameters);
        }

        private static Tuple<ParameterExpression, Type> GetDictionaryVariableForUniqueCheck(Type key, Type value)
        {
            var type = typeof (Dictionary<,>).MakeGenericType(new[] {key, value});
            return new Tuple<ParameterExpression, Type>(Expression.Parameter(type), type);
        }

        private static Tuple<ParameterExpression, ConstantExpression> GetIdEvaluatorExpression(IStructureMap mapper)
        {
            var methodInfo = ExpressionHelper.MakeGenericMethod(() => GetIdEvaluatorFromMapper<string, string>(null), mapper.SourceType, mapper.DestinationType);
            var idPropertyEvaluatorConstant = methodInfo.Invoke(null, new object[] {mapper}) as ConstantExpression;
            var idPropertyEvaluatorParameter = Expression.Parameter(idPropertyEvaluatorConstant.Type);
            return new Tuple<ParameterExpression, ConstantExpression>(idPropertyEvaluatorParameter, idPropertyEvaluatorConstant);
        }

        private static Expression GetIdEvaluatorFromMapper<TSource, TDestination>(IStructureMap mapper) where TSource : class
        {
            var idProperty = (mapper as StructureMap<TSource, TDestination>).GetIdPropertyItem();
            var methodInfo = ExpressionHelper.MakeGenericMethod(() => GetIdEvaluatorFromMappingItem<string, string, string>(null), typeof (TSource), mapper.DestinationType, idProperty.ResultType);
            return methodInfo.Invoke(null, new object[] {idProperty}) as Expression;
        }

        private static Expression GetIdEvaluatorFromMappingItem<TSource, TDestination, TResult>(IStructureMapItem<TSource, TDestination> mappingItem)
        {
            return Expression.Constant((mappingItem as StructureMapItem<TSource, TDestination, TResult>).PropertyGetter);
        }

        private static MethodInfo GetSqlMapperQueryMethod(MappingFunctionDescriptor funcDescriptor)
        {
            var firstOrDefault = typeof (SqlMapper).GetMethods().FirstOrDefault(m => m.Name == "Query" && m.IsGenericMethod && m.GetGenericArguments().Length == funcDescriptor.GenericTypes.Length);
            if (firstOrDefault != null)
                return firstOrDefault.MakeGenericMethod(funcDescriptor.GenericTypes);
            return null;
        }

        private static TValue GetUniqueData<TKey, TValue>(Dictionary<TKey, TValue> dict, Func<TValue, TKey> evaluator, TValue Data)
        {
            if (Data == null)
                return dict.Values.FirstOrDefault();
            if (!dict.ContainsKey(evaluator(Data)))
                dict.Add(evaluator(Data), Data);

            return dict[evaluator(Data)];
        }

        private static void SetupIdDataChecker(List<ParameterExpression> variables,
            List<Expression> bodyExpressions,
            ParameterExpression DataParameter,
            List<Expression> mappingBodyExpressions,
            IStructureMap mapper)
        {
            //get and assign dictionaries
            var dictParamAndVariable = GetDictionaryVariableForUniqueCheck(mapper.IdPropertyType, mapper.SourceType);
            var dictAssignment = Expression.Assign(dictParamAndVariable.Item1, Expression.New(dictParamAndVariable.Item2));
            variables.Add(dictParamAndVariable.Item1);
            bodyExpressions.Add(dictAssignment);
            //get and assign id evaluators
            var propertyEvaluatorParamAndValue = GetIdEvaluatorExpression(mapper);
            var evaulatorAssignment = Expression.Assign(propertyEvaluatorParamAndValue.Item1, propertyEvaluatorParamAndValue.Item2);
            variables.Add(propertyEvaluatorParamAndValue.Item1);
            bodyExpressions.Add(evaulatorAssignment);
            // declare Data parameters and assign them to inner fake mapping function declaration
            var checkerExpression = BuildUniquenessCheckerExpression(mapper, dictParamAndVariable.Item1, propertyEvaluatorParamAndValue.Item1, DataParameter);
            mappingBodyExpressions.Add(Expression.Assign(DataParameter, checkerExpression));
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