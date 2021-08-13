using System;
using Golap.AppleAuth;
using Golap.AppleAuth.Entities;
using Npgsql;

namespace RoomyAPI
{
    public class RoomyEnv
    {
        public class Apple
        {
            // Not putting in env vars because this (test) information isn't very sensitive and non env-specific and I don't want to 
            // deal with headaches. 
            private static string TeamId = Environment.GetEnvironmentVariable("APPLE_TEAM_ID");
            private static string ClientId = Environment.GetEnvironmentVariable("APPLE_CLIENT_ID");
            private static string RedirectUri = Environment.GetEnvironmentVariable("APPLE_REDIR_UI");
            private static string KeyId = Environment.GetEnvironmentVariable("APPLE_KEY_ID");
            private static string KeyData = Environment.GetEnvironmentVariable("APPLE_KEY_DATA");
            private static AppleAuthSetting AuthSetting = new AppleAuthSetting(TeamId, ClientId, RedirectUri);
            private static AppleKeySetting KeySetting = new AppleKeySetting(KeyId, KeyData);
            public static AppleAuthClient AuthClient = new AppleAuthClient(AuthSetting, KeySetting);
        }

        public class Google
        {
            public static string MapsAPIKey = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        }
        
        public class Database
        {
            private static readonly string PostgresUsername = Environment.GetEnvironmentVariable("DB_USERNAME");
            private static readonly string PostgresPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
            private static readonly string PostgresDatabaseName = Environment.GetEnvironmentVariable("DB_NAME");
            private static readonly string PostgresAddress = Environment.GetEnvironmentVariable("DB_ADDRESS");
            private static NpgsqlConnectionStringBuilder PostgresConnectionString = new NpgsqlConnectionStringBuilder()
            {
                Database = PostgresDatabaseName,
                Username = PostgresUsername,
                Password = PostgresPassword,
                Host = PostgresAddress,
                MinPoolSize = 5,
                ApplicationName = "roomy-api",
                Pooling = true
            };

            public static NpgsqlConnection DatabaseConnection = new NpgsqlConnection(RoomyEnv.Database.PostgresConnectionString.ToString());

            static Database()
            {
                DatabaseConnection.Open();
            }
        }
    }
}