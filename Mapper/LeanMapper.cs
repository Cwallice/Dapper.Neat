using System;
using System.Collections.Generic;
using System.Linq;

namespace Dapper.Neat.Mapper
{
    public static class LeanMapper
    {
        static Dictionary<Type, Dictionary<Type, IStructureMap>> typeMappers = new Dictionary<Type, Dictionary<Type, IStructureMap>>();
        public static StructureMap<TSource, TDestination> Map<TSource, TDestination>(Action<StructureMap<TSource, TDestination>> funcMapper)
        {
            if (typeMappers.ContainsKey(typeof(TDestination)) && typeMappers[typeof(TDestination)].ContainsKey(typeof(TSource)))
                throw new ArgumentException(String.Format("Mapper already contains record for pair of {0} and {1}", typeof(TSource).Name, typeof(TDestination).Name));

            StructureMap<TSource, TDestination> classMapper = new StructureMap<TSource, TDestination>();
            if (funcMapper != null)
                funcMapper(classMapper);
            if (!typeMappers.ContainsKey(typeof(TDestination)))
                typeMappers.Add(typeof(TDestination), new Dictionary<Type, IStructureMap>());
            if (!typeMappers[typeof(TDestination)].ContainsKey(typeof(TSource)))
                typeMappers[typeof(TDestination)].Add(typeof(TSource), classMapper);
            return classMapper;
        }

        public static StructureMap<TSource, TDestination> GetMapper<TSource, TDestination>()
        {
            if (!typeMappers.ContainsKey(typeof(TDestination)) && !typeMappers[typeof(TDestination)].ContainsKey(typeof(TSource)))
                return null;
            return typeMappers[typeof(TDestination)][typeof(TSource)] as StructureMap<TSource, TDestination>;
        }

        public static StructureMap<TSource, TSource> Map<TSource>(Action<StructureMap<TSource, TSource>> funcMapper)
        {
            return Map<TSource, TSource>(funcMapper);
        }

        public static IStructureMap<TSource> GetMapper<TSource>()
        {
            return GetMapper(typeof(TSource)) as IStructureMap<TSource>;
        }

        public static IStructureMap GetMapper(Type sourceType)
        {
            return (from t in typeMappers.Values
                    select t.Values into Values
                    from v in Values
                    where v.SourceType == sourceType
                    select v).FirstOrDefault();
        }

        public static void Compile()
        {
            foreach (var typeMapper in typeMappers)
            {
                foreach (var structuremap in typeMapper.Value.Values)
                {
                    structuremap.CallCompile();
                }
            }
        }
    }
}
