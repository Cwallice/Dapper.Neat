using System;
using System.Runtime.Serialization;

namespace Dapper.Neat.Exceptions
{
    public class DapperNeatException : Exception
    {
        public DapperNeatException()
        {
        }

        public DapperNeatException(string message) : base(message)
        {
        }

        public DapperNeatException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected DapperNeatException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}