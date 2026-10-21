using Npgsql;

namespace Kratix.ERP.Data;

// Hosts such as Neon hand out "postgresql://user:password@host/db?sslmode=require"; Npgsql wants "Host=...;...".
// Either form is accepted.
public static class DatabaseUrl
{
    public static NpgsqlConnectionStringBuilder ToConnectionString(string value)
    {
        if (!value.StartsWith("postgres://") && !value.StartsWith("postgresql://"))
            return new NpgsqlConnectionStringBuilder(value);

        var uri = new Uri(value);
        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,
            SslMode = SslMode.Require,
        };
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var (key, val) = (pair.Split('=', 2)[0], Uri.UnescapeDataString(pair.Split('=', 2).ElementAtOrDefault(1) ?? ""));
            if (key == "sslmode" && Enum.TryParse<SslMode>(val.Replace("-", ""), true, out var mode))
                builder.SslMode = mode;
        }
        return builder;
    }
}
