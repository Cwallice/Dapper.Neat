#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#endregion

namespace Dapper.Neat.Mapper
{
    public class StructureMapTypeMapper : SqlMapper.ITypeMap
    {
        private readonly SqlMapper.ITypeMap _defaultTypeMap;
        private readonly List<IPropertyDescriptor> _mapItems;
        private readonly IStructureMap _structureMap;

        public StructureMapTypeMapper(IStructureMap structureMap, SqlMapper.ITypeMap defaultTypeMap)
        {
            _structureMap = structureMap;
            _defaultTypeMap = defaultTypeMap;

            _mapItems = structureMap.GetPropertyItems();
        }

        public ConstructorInfo FindConstructor(string[] names, Type[] types)
        {
            return _defaultTypeMap.FindConstructor(names, types);
        }

        public ConstructorInfo FindExplicitConstructor()
        {
            return _defaultTypeMap.FindExplicitConstructor();
        }

        public SqlMapper.IMemberMap GetConstructorParameter(ConstructorInfo constructor, string columnName)
        {
            return _defaultTypeMap.GetConstructorParameter(constructor, columnName);
        }

        public SqlMapper.IMemberMap GetMember(string columnName)
        {
            var mapItem =
                _mapItems.FirstOrDefault(
                    mi => mi.DestinationName.Equals(columnName, StringComparison.InvariantCultureIgnoreCase));
            if (mapItem != null)
                return new StructureMapMemberMap(mapItem.PropertyInfo, columnName);

            if (_structureMap.MapExtraColumns)
                return _defaultTypeMap.GetMember(columnName);

            return null;
        }
    }

    public class StructureMapMemberMap : SqlMapper.IMemberMap
    {
        private readonly string _column;
        private readonly MemberInfo _member;

        public StructureMapMemberMap(MemberInfo member, string column)
        {
            _member = member;
            _column = column;
        }

        public string ColumnName
        {
            get { return _column; }
        }

        public Type MemberType
        {
            get
            {
                switch (_member.MemberType)
                {
                    case MemberTypes.Field:
                        return ((FieldInfo) _member).FieldType;
                    case MemberTypes.Property:
                        return ((PropertyInfo) _member).PropertyType;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        public PropertyInfo Property
        {
            get { return _member as PropertyInfo; }
        }

        public FieldInfo Field
        {
            get { return _member as FieldInfo; }
        }

        /// <summary>
        /// We don't support parameter info at the moment
        /// </summary>
        public ParameterInfo Parameter
        {
            get { return null; }
        }
    }
}