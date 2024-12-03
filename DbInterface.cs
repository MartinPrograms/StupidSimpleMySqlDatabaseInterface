using System.Collections;
using System.Reflection;
using MySql.Data.MySqlClient;

namespace StupidSimpleMySqlDatabaseInterface;

public static class DbInterface
{
        public const string Connection =
        "Server=%SERVER%; Port=%PORT%; Database=%DB%; UID=%USERNAME%; password=%PASSWORD%;";
    private static string connection { get; set; }
    public static void Initialize(string server, string port, string db, string username, string password)
    {
        Console.WriteLine($"Server: {server}");
        Console.WriteLine($"Port: {port}");
        
        var connection = Connection.Replace("%SERVER%", server)
            .Replace("%PORT%", port)
            .Replace("%DB%", db)
            .Replace("%USERNAME%", username)
            .Replace("%PASSWORD%", password);
        
        
        DbInterface.connection = connection;
        
        Console.WriteLine("connected");
        
        CacheTables();
    }

    private static Dictionary<string, List<object>> _cacheTables = new(); // String = table name, List = table data

    static void CacheTables()
    {
        var tables = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsSubclassOf(typeof(CommonTable)) && t != typeof(CommonTable));

        var genericGetAll = typeof(DbInterface).GetMethod("GetAll");
        
        foreach (var table in tables)
        {
            var tableName = table.GetProperty("TableName")?.GetValue(null) as string;
            if (tableName == "")
                continue;

            var method = genericGetAll.MakeGenericMethod(table);
            
            var list = method.Invoke(null, new object[] {tableName}); // is of type List<table>
            
            // we can not cast this to List<object> because of type safety, so we have to do it like this
            var objectList = new List<object>();
            foreach (var item in (IEnumerable) list)
            {
                objectList.Add(item);
            }
            
            _cacheTables[tableName] = objectList;
        }
        
        Console.WriteLine($"Cached {_cacheTables.Count} tables with {_cacheTables.Sum(x => x.Value.Count)} entries.");
    }

    public static void RefreshCache()
    {
        _cacheTables.Clear();
        CacheTables();
    }
    
    public static List<T>? GetCache<T>(string tableName)
    {
        if (_cacheTables.ContainsKey(tableName))
            return _cacheTables[tableName].Cast<T>().ToList();
        
        Console.WriteLine($"Table {tableName} not found in cache.");
        
        return null;
    }
    
    public static MySqlConnection GetConnection()
    {
        return new MySqlConnection(connection);
    }

    public static T ReadObject<T>(MySqlDataReader reader)
    {
        var type = typeof(T);
        var obj = Activator.CreateInstance(type);
        
        foreach (var property in type.GetProperties())
        {
            var fieldName = property.GetCustomAttribute<DbFieldName>()?.Name;
            if (fieldName == null)
                continue;
            
            var value = reader.IsDBNull(reader.GetOrdinal(fieldName)) ? null : reader[fieldName];
            
            property.SetValue(obj, value);
        }
        
        return (T) obj!;
    }

    public static T Get<T, V>(string table, string column, V toMatch)
    {
        using (var connection = GetConnection())
        {
            var query = $"SELECT * FROM {Sanitize(table)} WHERE {Sanitize(column)} = @toMatch"; // hopefully no SQL injection here
            var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@toMatch", toMatch);
            
            connection.Open();
            
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read())
                    return default!;
                
                return ReadObject<T>(reader);
            }
            
        }
    }
    
    public static string Sanitize(string input)
    {
        return input.Replace("'", "''");
    }

    public static List<T> GetList<T, V>(string table, string column, V toMatch)
    {
        var list = new List<T>();
        
        using (var connection = GetConnection())
        {
            var query = $"SELECT * FROM {Sanitize(table)} WHERE {Sanitize(column)} = @toMatch"; // hopefully no SQL injection here
            var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@toMatch", toMatch);
            
            connection.Open();
            
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(ReadObject<T>(reader));
                }
            }
            
            connection.Close();
        }

        return list;
    }
    
    public static List<T> GetList<T,V,V1>(string table, string column, V toMatch, string column1, V1 toMatch1)
    {
        var list = new List<T>();
        
        using (var connection = GetConnection())
        {
            var query = $"SELECT * FROM {Sanitize(table)} WHERE {Sanitize(column)} = @toMatch AND {Sanitize(column1)} = @toMatch1"; // hopefully no SQL injection here
            var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@toMatch", toMatch);
            command.Parameters.AddWithValue("@toMatch1", toMatch1);
            
            connection.Open();
            
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(ReadObject<T>(reader));
                }
            }
            
            connection.Close();
        }

        return list;
    }

    public static List<T> GetRange<T, V>(string table, string column, V toMatchA, V toMatchB)
    {
        // Check if something is in a range
        var list = new List<T>();
        
        using (var connection = GetConnection())
        {
            var query = $"SELECT * FROM {Sanitize(table)} WHERE {Sanitize(column)} >= @toMatchA AND {Sanitize(column)} <= @toMatchB"; // hopefully no SQL injection here
            var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@toMatchA", toMatchA);
            command.Parameters.AddWithValue("@toMatchB", toMatchB);
            
            connection.Open();
            
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(ReadObject<T>(reader));
                }
            }
            
            connection.Close();
        }
        
        return list;
    }

    public static List<T> GetAll<T>(string table)
    {
        var list = new List<T>();
        
        using (var connection = GetConnection())
        {
            var query = $"SELECT * FROM {Sanitize(table)}"; // hopefully no SQL injection here
            var command = new MySqlCommand(query, connection);
            
            connection.Open();
            
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(ReadObject<T>(reader));
                }
            }
            
            connection.Close();
        }
        
        return list;
    }
    
    public static bool Update(CommonTable toUpdate, string tableName)
    {
        var type = toUpdate.GetType();
        var properties = type.GetProperties();
        
        var query = $"UPDATE {Sanitize(tableName)} SET ";
        var first = true;
        
        foreach (var property in properties)
        {
            var fieldName = property.GetCustomAttribute<DbFieldName>()?.Name;
            if (fieldName == null)
                continue;
            
            if (!first)
                query += ", ";
            first = false;
            
            query += $"{Sanitize(fieldName)} = @{Sanitize(fieldName)}";
        }
        
        query += $" WHERE id = {toUpdate.Id}";
        
        using (var connection = GetConnection())
        {
            var command = new MySqlCommand(query, connection);
            
            foreach (var property in properties)
            {
                var fieldName = property.GetCustomAttribute<DbFieldName>()?.Name;
                if (fieldName == null)
                    continue;
                
                command.Parameters.AddWithValue($"@{Sanitize(fieldName)}", property.GetValue(toUpdate));
            }
            
            connection.Open();
            var result = command.ExecuteNonQuery();
            connection.Close();
            
            return result > 0;
        }
    }

    public static void Insert(CommonTable user, string tableName)
    {
        var type = user.GetType();
        var properties = type.GetProperties();
        
        var query = $"INSERT INTO {Sanitize(tableName)} (";
        var values = "VALUES (";
        var first = true;
        
        foreach (var property in properties)
        {
            var fieldName = property.GetCustomAttribute<DbFieldName>()?.Name;
            if (fieldName == null)
                continue;
            
            if (!first)
            {
                query += ", ";
                values += ", ";
            }
            first = false;
            
            query += Sanitize(fieldName);
            values += $"@{Sanitize(fieldName)}";
        }
        
        query += ") ";
        values += ")";
        
        query += values;
        
        using (var connection = GetConnection())
        {
            var command = new MySqlCommand(query, connection);
            
            foreach (var property in properties)
            {
                var fieldName = property.GetCustomAttribute<DbFieldName>()?.Name;
                if (fieldName == null)
                    continue;
                
                command.Parameters.AddWithValue($"@{Sanitize(fieldName)}", property.GetValue(user));
            }
            
            connection.Open();
            command.ExecuteNonQuery();
            connection.Close();
        }
    }

    public static void DeleteById(CommonTable item, string tableName)
    {
        // Get by id and delete
        using (var connection = GetConnection())
        {
            var query = $"DELETE FROM {Sanitize(tableName)} WHERE id = {item.Id}";
            var command = new MySqlCommand(query, connection);
            
            connection.Open();
            command.ExecuteNonQuery();
            connection.Close();
        }
    }
}