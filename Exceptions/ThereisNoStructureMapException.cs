#region Usings
using System;
#endregion

namespace Dapper.Neat.Exceptions
{
    public class ThereisNoStructureMapException : DapperNeatException
    {
        public ThereisNoStructureMapException(Type sourceType) :
            base(string.Format("There is no structure map for '{0}'", sourceType))
        {
        }
    }
}