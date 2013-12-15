namespace Dapper.Neat.Mapper
{
    public enum ColumnReadWriteState
    {
        Read = 1,
        Write = 1 << 1,
    }
}