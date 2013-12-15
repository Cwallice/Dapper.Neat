using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Dapper.Neat.Mapper;

namespace Dapper.Neat
{
    public static class SqlExtensions
    {
        public static string SqlForSelect<T>(this T source, string alias = null)
        {
            return typeof (T).SqlForSelect();
        }

        public static string SqlForSelect(this Type mappedType, string alias = null)
        {
            IStructureMap structureMap = LeanMapper.GetMapper(mappedType);
            return String.Format("{0}", structureMap.GetColumns(alias ?? structureMap.TableName));
        }

        public static string Columns<T>(this SqlBuilder source, string alias = null)
        {
            return SqlForSelect(typeof (T), alias);
        }

        public static string Table<T>(this SqlBuilder source)
        {
            return LeanMapper.GetMapper<T>().TableName;
        }

        public static SqlBuilder Where<TSource>(this SqlBuilder source, Expression<Func<TSource, bool>> predicate,
            string alias = null)
        {
            Tuple<string, string, object> tuple = ExpressionParser.ParseExpression(predicate); // very dirty fix!!!
            alias = alias ?? source.Table<TSource>();
            source.Where(String.Format("[{0}].[{1}] {2} @{1}", alias, tuple.Item1, tuple.Item2),
                new Dictionary<string, object> {{tuple.Item1, tuple.Item3}});
            return source;
        }

        public static SqlBuilder Select<TSource>(this SqlBuilder source,string alias = null)
        {
            return source.Select(source.Columns<TSource>(alias));
        }
    }
}