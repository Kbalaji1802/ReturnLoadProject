using Npgsql;

namespace ReturnLoad.Infrastructure.Persistence;

/// <summary>
/// Normalises a PostgreSQL connection string into the keyword/value form Npgsql accepts.
///
/// Managed Postgres providers (Render, Neon, Heroku, Railway, Supabase) hand out a URI —
/// <c>postgresql://user:password@host:5432/database?sslmode=require</c> — but Npgsql only
/// parses keyword/value strings, so pasting the provider's value straight into
/// <c>ConnectionStrings__ReturnLoadDatabase</c> fails at first connection. Converting here
/// means the deploy config can be the exact string the provider shows, with no manual
/// rewriting step to get wrong.
/// </summary>
public static class PostgresConnectionString
{
    /// <summary>
    /// Returns <paramref name="value"/> unchanged when it is already keyword/value, or the
    /// converted equivalent when it is a <c>postgres://</c> / <c>postgresql://</c> URI.
    /// </summary>
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        Uri uri = new(value);

        NpgsqlConnectionStringBuilder builder = new()
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            // The leading '/' is part of the URI path, not the database name.
            Database = uri.AbsolutePath.TrimStart('/'),
        };

        // UserInfo is percent-encoded in a URI; generated passwords routinely contain
        // characters (@ : / +) that must be decoded before Npgsql sees them.
        string[] userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length > 0 && userInfo[0].Length > 0)
        {
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
        }

        if (userInfo.Length > 1)
        {
            builder.Password = Uri.UnescapeDataString(userInfo[1]);
        }

        // Carry over query parameters (sslmode, channel_binding, options, …). Npgsql's
        // builder understands the provider's snake_case spellings via its keyword aliases.
        foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] kv = pair.Split('=', 2);
            if (kv.Length != 2)
            {
                continue;
            }

            string key = Uri.UnescapeDataString(kv[0]);
            string parameterValue = Uri.UnescapeDataString(kv[1]);

            try
            {
                builder[key] = parameterValue;
            }
            catch (ArgumentException)
            {
                // An unknown provider-specific parameter must not sink the whole connection.
            }
        }

        // Managed providers require TLS and none of them present a locally-trusted CA, so
        // default to the mode that encrypts without demanding chain validation. An explicit
        // sslmode in the URI is honoured above and left alone.
        if (!builder.ContainsKey("SSL Mode"))
        {
            builder.SslMode = SslMode.Require;
        }

        return builder.ConnectionString;
    }
}
