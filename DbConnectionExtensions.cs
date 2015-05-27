#region Usings

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using Dapper.Neat.Exceptions;
using Dapper.Neat.Mapper;

#endregion

namespace Dapper.Neat
{
    public static class Extensions
    {
        #region Static Methods (public)

        public static void Delete<TSource>(this IDbConnection connection, TSource data, IDbTransaction transaction = null)
        {
            var structureMap = LeanMapper.GetMapper<TSource>();
            if (structureMap == null)
                return;
            var dynamicParameters = structureMap.GetIdParameters(data);
            connection.Execute(structureMap.DeleteSqlTemplate, dynamicParameters, transaction:transaction);
        }

        public static TSource Insert<TSource>(this IDbConnection connection, TSource data, IDbTransaction transaction = null)
        {
            IStructureMap<TSource> structureMap = LeanMapper.GetMapper<TSource>();

            if (structureMap == null)
            {
                throw new ThereisNoStructureMapException(typeof (TSource));
            }

            DynamicParameters dynamicParameters = structureMap.GetParameters(data);
            connection.Execute(structureMap.InsertSqlTemplate, dynamicParameters,transaction:transaction);
            structureMap.UpdateIdFromParameters(data, dynamicParameters);

            return data;
        }

        public static IEnumerable<TReturn> NeatQuery<TFirst, TSecond, TReturn>(
            this IDbConnection cnn,
            string sql,
            Func<TFirst, TSecond, TReturn> map,
            dynamic param = null,
            IDbTransaction transaction = null,
            bool buffered = true,
            string splitOn = "Id",
            int? commandTimeout = null,
            CommandType? commandType = null)
        {
            return QueryFunctionFactory.CallQuery<TReturn, Func<TFirst, TSecond, TReturn>>
                (cnn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }

        public static IEnumerable<TReturn> NeatQuery<TFirst, TSecond, TThird, TReturn>(this IDbConnection cnn,
            string sql, Func<TFirst, TSecond, TThird, TReturn> map, dynamic param = null,
            IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null,
            CommandType? commandType = null)
        {
            return QueryFunctionFactory.CallQuery<TReturn, Func<TFirst, TSecond, TThird, TReturn>>
                (cnn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }

        public static IEnumerable<TReturn> NeatQuery<TFirst, TSecond, TThird, TFourth, TReturn>(this IDbConnection cnn,
            string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map,
            dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id",
            int? commandTimeout = null, CommandType? commandType = null)
        {
            return QueryFunctionFactory.CallQuery<TReturn, Func<TFirst, TSecond, TThird, TFourth, TReturn>>
                (cnn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }

        public static IEnumerable<TReturn> NeatQuery<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(
            this IDbConnection cnn, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map,
            dynamic param = null, IDbTransaction transaction = null, bool buffered = true,
            string splitOn = "Id",
            int? commandTimeout = null, CommandType? commandType = null)
        {
            return QueryFunctionFactory.CallQuery<TReturn, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>>
                (cnn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }

        public static void Update<TSource>(this IDbConnection connection, TSource data, IDbTransaction transaction = null)
        {
            var structureMap = LeanMapper.GetMapper<TSource>();
            if (structureMap == null)
                return;
            var dynamicParameters = structureMap.GetParameters(data, false);
            connection.Execute(structureMap.UpdateSqlTemplate, dynamicParameters, transaction:transaction);
        }

        #endregion
    }
}