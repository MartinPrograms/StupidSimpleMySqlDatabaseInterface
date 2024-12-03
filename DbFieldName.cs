namespace StupidSimpleMySqlDatabaseInterface;

public class DbFieldName : Attribute
{
    public string Name { get; }

    public DbFieldName(string name)
    {
        Name = name;
    }
}