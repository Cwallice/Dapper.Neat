#region Usings

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace Dapper.Neat.Mapper
{
    public static class LeanMapper
    {
        private static readonly Dictionary<Type, Dictionary<Type, IStructureMap>> typeMappers =
            new Dictionary<Type, Dictionary<Type, IStructureMap>>();

        private static bool _userDapperRegistration;

        public static bool UserDapperRegistration
        {
            get { return _userDapperRegistration; }
        }

        public static StructureMap<TSource, TDestination> Map<TSource, TDestination>(
            Action<StructureMap<TSource, TDestination>> funcMapper)
        {
            if (typeMappers.ContainsKey(typeof (TDestination)) &&
                typeMappers[typeof (TDestination)].ContainsKey(typeof (TSource)))
                throw new ArgumentException(String.Format("Mapper already contains record for pair of {0} and {1}",
                    typeof (TSource).Name, typeof (TDestination).Name));

            var classMapper = new StructureMap<TSource, TDestination>();
            if (funcMapper != null)
                funcMapper(classMapper);
            if (!typeMappers.ContainsKey(typeof (TDestination)))
                typeMappers.Add(typeof (TDestination), new Dictionary<Type, IStructureMap>());
            if (!typeMappers[typeof (TDestination)].ContainsKey(typeof (TSource)))
                typeMappers[typeof (TDestination)].Add(typeof (TSource), classMapper);
            return classMapper;
        }

        public static StructureMap<TSource, TDestination> GetMapper<TSource, TDestination>()
        {
            if (!typeMappers.ContainsKey(typeof (TDestination)) &&
                !typeMappers[typeof (TDestination)].ContainsKey(typeof (TSource)))
                return null;
            return typeMappers[typeof (TDestination)][typeof (TSource)] as StructureMap<TSource, TDestination>;
        }

        public static IEnumerable<IStructureMap> GetMappedStructures()
        {
            return (from dest in typeMappers
                select dest.Value
                into descriptorsPairs
                from descriptor in descriptorsPairs
                select descriptor.Value).ToList();
        }

        public static StructureMap<TSource, TSource> Map<TSource>(Action<StructureMap<TSource, TSource>> funcMapper)
        {
            return Map<TSource, TSource>(funcMapper);
        }

        public static IStructureMap<TSource> GetMapper<TSource>()
        {
            return GetMapper(typeof (TSource)) as IStructureMap<TSource>;
        }

        public static IStructureMap GetMapper(Type sourceType)
        {
            return (from t in typeMappers.Values
                select t.Values
                into Values
                from v in Values
                where v.SourceType == sourceType
                select v).FirstOrDefault();
        }

        public static void Compile(bool useDapperRegistration = false)
        {
            _userDapperRegistration = useDapperRegistration;
            foreach (var typeMapper in typeMappers)
            {
                foreach (var structuremap in typeMapper.Value.Values)
                {
                    structuremap.CallCompile();
                    if (_userDapperRegistration)
                    {
                        var defaultType = SqlMapper.GetTypeMap(structuremap.SourceType);

                        if (defaultType is StructureMapTypeMapper)
                            return;

                        var map = new StructureMapTypeMapper(structuremap, defaultType);
                        SqlMapper.SetTypeMap(structuremap.SourceType, map);
                    }
                }
            }
        }
    }
}