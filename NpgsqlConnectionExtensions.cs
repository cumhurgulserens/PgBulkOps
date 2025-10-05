using Npgsql;
using System.Reflection;
using System.Text;

namespace PgBulkOps;

public static class NpgsqlConnectionExtensions
{
    /// <summary>
    /// Generic Bulk Insert (Binary COPY kullanır).
    /// </summary>
    public static async Task BulkInsertAsync<T>(
        this NpgsqlConnection connection,
        IEnumerable<T> entities,
        string tableName,
        Action<BulkOptions>? configure = null)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var options = new BulkOptions();
        configure?.Invoke(options);

        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var colNames = string.Join(", ", props.Select(p => $"\"{ResolveColumnName(p, options)}\""));
        var copyCommand = $"COPY {tableName} ({colNames}) FROM STDIN (FORMAT BINARY)";

        try
        {
            await using var writer = await connection.BeginBinaryImportAsync(copyCommand);

            int count = 0;
            foreach (var entity in entities)
            {
                await writer.StartRowAsync();

                foreach (var prop in props)
                {
                    var value = prop.GetValue(entity);
                    await writer.WriteAsync(value);
                }

                count++;
                if (count % options.BatchSize == 0)
                    options.OnProgress?.Invoke(new BulkProgress(count));
            }

            await writer.CompleteAsync();
            options.OnProgress?.Invoke(new BulkProgress(count));
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"BulkInsert failed for table {tableName}. " +
                $"Entity type: {typeof(T).Name}. Inner: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generic Bulk Update (COPY + temp table).
    /// </summary>
    public static async Task BulkUpdateAsync<T>(
        this NpgsqlConnection connection,
        IEnumerable<T> entities,
        string tableName,
        string keyColumn,
        Action<BulkOptions>? configure = null)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var options = new BulkOptions();
        configure?.Invoke(options);

        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var tempTable = $"temp_{Guid.NewGuid():N}";

        try
        {
            // 1. Temp tabloyu yarat
            var colDefs = string.Join(", ",
                props.Select(p => $"\"{ResolveColumnName(p, options)}\" {GetPgType(p.PropertyType)}"));
            var createSql = $"CREATE TEMP TABLE {tempTable} ({colDefs}) ON COMMIT DROP;";
            await using (var createCmd = new NpgsqlCommand(createSql, connection))
            {
                await createCmd.ExecuteNonQueryAsync();
            }

            // 2. COPY ile temp tabloya yükle
            var colNames = string.Join(", ", props.Select(p => $"\"{ResolveColumnName(p, options)}\""));
            var copyCommand = $"COPY {tempTable} ({colNames}) FROM STDIN (FORMAT BINARY)";
            await using (var writer = await connection.BeginBinaryImportAsync(copyCommand))
            {
                int count = 0;
                foreach (var entity in entities)
                {
                    await writer.StartRowAsync();
                    foreach (var prop in props)
                    {
                        var value = prop.GetValue(entity);
                        await writer.WriteAsync(value);
                    }
                    count++;
                    if (count % options.BatchSize == 0)
                        options.OnProgress?.Invoke(new BulkProgress(count));
                }
                await writer.CompleteAsync();
                options.OnProgress?.Invoke(new BulkProgress(count));
            }

            // 3. Asıl tabloyu güncelle
            var setClause = string.Join(", ",
                props.Where(p => !string.Equals(ResolveColumnName(p, options), keyColumn, StringComparison.OrdinalIgnoreCase))
                     .Select(p => $"\"{ResolveColumnName(p, options)}\" = tmp.\"{ResolveColumnName(p, options)}\""));

            var updateSql = $@" UPDATE {tableName} SET {setClause} FROM {tempTable} tmp WHERE {tableName}.{keyColumn} = tmp.{keyColumn};";

            await using (var updateCmd = new NpgsqlCommand(updateSql, connection))
            {
                await updateCmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"BulkUpdate failed for table {tableName}, key column {keyColumn}. " +
                $"Entity type: {typeof(T).Name}. Inner: {ex.Message}", ex);
        }
    }

    private static string ResolveColumnName(PropertyInfo prop, BulkOptions options)
    {
        return options.UseSnakeCase
            ? ToSnakeCase(prop.Name)
            : prop.Name;
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append('_');

            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    // basit tip map
    private static string GetPgType(Type type)
    {
        if (type == typeof(int)) return "int4";
        if (type == typeof(long)) return "int8";
        if (type == typeof(string)) return "text";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(decimal)) return "numeric";
        if (type == typeof(DateTime) || type == typeof(DateTime?)) return "timestamptz";
        return "text";
    }
}
