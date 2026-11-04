using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;

namespace UPKPro.Data;

/// <summary>
/// Akses database PostgreSQL (pengganti TADOConnection + DSN ODBC "upkpropg" di Delphi).
/// Connection string dibaca dari appsettings.json -> ConnectionStrings:UpkPro,
/// atau dari environment variable UPKPRO_CONNECTION jika diisi.
/// </summary>
public static class Database
{
    private static readonly Lazy<string> _connectionString = new(LoadConnectionString);

    public static string ConnectionString => _connectionString.Value;

    public static NpgsqlConnection Open()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public static async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    public static IEnumerable<T> Query<T>(string sql, object? param = null)
    {
        using var conn = Open();
        return conn.Query<T>(sql, param).AsList();
    }

    public static T? QuerySingleOrDefault<T>(string sql, object? param = null)
    {
        using var conn = Open();
        return conn.QuerySingleOrDefault<T>(sql, param);
    }

    public static int Execute(string sql, object? param = null)
    {
        using var conn = Open();
        return conn.Execute(sql, param);
    }

    /// <summary>
    /// Mengisi DataTable, berguna untuk binding ke DataGridView (padanan TADOQuery + TDBGrid).
    /// </summary>
    public static DataTable QueryTable(string sql, object? param = null)
    {
        using var conn = Open();
        using var reader = conn.ExecuteReader(sql, param);
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    public static bool TestConnection(out string? error)
    {
        try
        {
            using var conn = Open();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string LoadConnectionString()
    {
        var env = Environment.GetEnvironmentVariable("UPKPRO_CONNECTION");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
            throw new InvalidOperationException($"File konfigurasi tidak ditemukan: {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
            cs.TryGetProperty("UpkPro", out var value) &&
            value.GetString() is { Length: > 0 } s)
            return s;

        throw new InvalidOperationException("ConnectionStrings:UpkPro belum diisi di appsettings.json.");
    }
}
