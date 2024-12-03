namespace StupidSimpleMySqlDatabaseInterface;

public interface ITable
{
    public int Id { get; set; }
    string TableName { get; }
}

public abstract class CommonTable : ITable
{
    [DbFieldName("id")] public int Id { get; set; }
    public abstract string TableName { get; }
}