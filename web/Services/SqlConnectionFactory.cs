using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SpotifyStatisticsWebApp.Services;

public static class SqlConnectionFactory
{
    public static string DefaultConnection(IConfiguration config)
    {
        var raw = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        return new SqlConnectionStringBuilder(raw)
        {
            Encrypt = true,
            TrustServerCertificate = true,
        }.ConnectionString;
    }
}
