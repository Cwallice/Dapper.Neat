using System;
using System.Collections.Generic;
using System.Data;
using Dapper.Neat.Mapper;

namespace Dapper.Neat
{
    public static class Extensions
    {
        public static void Delete<TSource>(this IDbConnection connection, TSource Data)
        {
            var structureMap = LeanMapper.GetMapper<TSource>();
            if (structureMap == null)
                return;
            var dynamicParameters = structureMap.GetIdParameters(Data);
            connection.Execute(structureMap.DeleteSqlTemplate, dynamicParameters);
        }

        public static IEnumerable<TSource> GetAll<TSource>(this IDbConnection connection)
        {
            var structureMap = LeanMapper.GetMapper<TSource>();
            if (structureMap == null)
                return new List<TSource>();
            return
                connection.Query<TSource>(string.Format("Select {0} from {1} {2};", structureMap.GetColumns("it"),
                    structureMap.TableName, "it"));
        }

        public static TSource Insert<TSource>(this IDbConnection connection, TSource Data)
        {
            var structureMap = LeanMapper.GetMapper<TSource>();
            if (structureMap == null)
                return default(TSource);
            var dynamicParameters = structureMap.GetParameters(Data);
            connection.Execute(structureMap.InsertSqlTemplate, dynamicParameters);
            structureMap.UpdateIdFromParameters(Data, dynamicParameters);
            return Data;
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
            //return finalMethod.Compile()(cnn, sql,map,param,transaction,buffered,splitOn,commandTimeout,commandType);
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

        public static void Update<TSource>(this IDbConnection connection, TSource Data)
        {
            var structureMap = LeanMapper.GetMapper<TSource>();
            if (structureMap == null)
                return;
            var dynamicParameters = structureMap.GetParameters(Data, false);
            connection.Execute(structureMap.UpdateSqlTemplate, dynamicParameters);
        }

    }
}