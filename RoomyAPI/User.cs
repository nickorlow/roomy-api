using System;
using System.Collections.Generic;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AppleAuth.TokenObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json.Linq;
using Npgsql;
using NpgsqlTypes;

namespace RoomyAPI
{
    public class User
    {
        public Guid Id { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string EmailAddress { get; private set; }
        public Guid? HomeId { get; private set; }
        public DateTime CreatedDate { get; private set; }
        public DateTime SubscriptionExpirationDate { get; private set; }
        public bool IsPremium => SubscriptionExpirationDate > DateTime.UtcNow;

        public string AuthProvider { get; private set; }
        private string AuthKey { get; set; }


        private async Task Initialize()
        {
            // TODO: Implement Guid lookups somewhere


            await using (var cmd = new NpgsqlCommand("SELECT * FROM get_user(@identifier)",
                RoomyEnv.Database.DatabaseConnection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("identifier", NpgsqlDbType.Uuid, Id);

                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (reader.Read())
                {
                    Id = (Guid) reader["id"];
                    FirstName = (string) reader["first_name"];
                    LastName = (string) reader["last_name"];
                    EmailAddress = (string) reader["email_address"];
                    HomeId = (Guid?) (reader["home_id"] != DBNull.Value ? reader["home_id"] : null);
                    CreatedDate = (DateTime) reader["created_date"];
                    SubscriptionExpirationDate = (DateTime) reader["subscription_exp_date"];
                    AuthKey = (string) reader["auth_key"];
                    AuthProvider = (string) reader["auth_provider"];
                }
                else
                {
                    //User doesn't exist
                    throw new Exceptions.UserDoesNotExistException();
                }
            }
        }

        public static async Task<User> FromReader(NpgsqlDataReader reader)
        {
            User u = new User();
            u.Id = (Guid) reader["id"];
            u.FirstName = (string) reader["first_name"];
            u.LastName = (string) reader["last_name"];
            u.EmailAddress = (string) reader["email_address"];
            u.HomeId = (Guid?) (reader["home_id"] != DBNull.Value ? reader["home_id"] : null);
            u.CreatedDate = (DateTime) reader["created_date"];
            u.SubscriptionExpirationDate = (DateTime) reader["subscription_exp_date"];
            u.AuthKey = (string) reader["auth_key"];
            u.AuthProvider = (string) reader["auth_provider"];
            return u;
        }
        
        public static async Task<CreatedUserResponse> CreateWithApple(InitialTokenResponse tokenResponse)
        {
            User u = new User();
            // TODO: Implement Guid lookups somewhere
            string refreshToken = await u.GetAppleRefreshTokenAndMountInfo(tokenResponse);
            u.AuthProvider = "apple";

            try
            {
               
                    await using var cmd = new NpgsqlCommand("CALL create_user(@email_address, @first_name, @last_name, @auth_provider, @auth_key, @subscription_exp_date)", RoomyEnv.Database.DatabaseConnection);
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.AddWithValue("email_address", u.EmailAddress);
                    cmd.Parameters.AddWithValue("first_name", u.FirstName);
                    cmd.Parameters.AddWithValue("last_name", u.LastName);
                    cmd.Parameters.AddWithValue("auth_provider", u.AuthProvider);
                    cmd.Parameters.AddWithValue("auth_key",parameterType: NpgsqlDbType.Varchar, u.AuthKey);
                    cmd.Parameters.AddWithValue("subscription_exp_date",NpgsqlDbType.Date, DateTime.UtcNow.AddMonths(3));
                    await cmd.ExecuteNonQueryAsync();
                
            }
            catch (PostgresException e)
            {
                // Don't throw an error if we've already gotten their auth info because we can use this function to 
                // re-auth if the user loses their key too.
                if (e.SqlState != "23505" || e.ConstraintName != "auth_key_unique")
                    throw;
            }

            // Fetch so we get db-parsed info
            // TODO: Set theory wise we may want to merge these 2 sql requests
            return new CreatedUserResponse()
            {
                CreatedUser = await GetUser(u.AuthKey),
                RefreshToken = refreshToken
            };
        }
        
        
        public async Task JoinHome(Guid homeId)
        {
            try
            {
                await using var cmd = new NpgsqlCommand("CALL join_home(@user_id, @home_id)", RoomyEnv.Database.DatabaseConnection);
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("user_id",NpgsqlDbType.Uuid, Id);
                cmd.Parameters.AddWithValue("home_id",NpgsqlDbType.Uuid, homeId);
                await cmd.ExecuteNonQueryAsync();
                HomeId = homeId;
            }
            catch (PostgresException e)
            {
                
            }
        }

        public async Task ReportLocation(Location location)
        {
            await using var cmd = new NpgsqlCommand("CALL report_location(@user_id, @longitude, @latitude)", RoomyEnv.Database.DatabaseConnection);
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddWithValue("user_id",NpgsqlDbType.Uuid, Id);
            cmd.Parameters.AddWithValue("longitude",NpgsqlDbType.Double, location.Longitude);
            cmd.Parameters.AddWithValue("latitude",NpgsqlDbType.Double, location.Latitude);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Not elegant in design
        /// Verifies user auth w/ apple and pulls their details for a first time signup
        /// also returns the refresh token which is what we need to verify again in the
        /// future.
        /// </summary>
        /// <param name="response">The token response from apple.</param>
        /// <returns>The refresh token</returns>
        private async Task<string> GetAppleRefreshTokenAndMountInfo(InitialTokenResponse response)
        {
            // We can also check email below as it should match with the below
            JObject user = JObject.Parse(response.user);  
            FirstName = user["name"]["firstName"].ToString();
            LastName = user["name"]["lastName"].ToString();

            if (FirstName.ToLower() == "tyler" && LastName.ToLower() == "mcarty")
                throw new Exceptions.AssHoleException(); //TODO: Blacklist their Apple ID
            
            var client = RoomyEnv.Apple.AuthClient;
            var token = await  client.GetAccessTokenAsync(response.code);
            var stream =token.IdToken;  
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(stream);
            var tokenS = jsonToken as JwtSecurityToken;

            AuthKey = tokenS.Payload.Sub;
            EmailAddress = tokenS.Payload["email"].ToString();

            return token.RefreshToken;
        }

        public async Task<LocationReport> GetLatestLocation()
        {
            await using (var cmd = new NpgsqlCommand("SELECT * FROM get_latest_locaton(@identifier)",
                RoomyEnv.Database.DatabaseConnection))
            {
                LocationReport lr = new LocationReport();
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("identifier", NpgsqlDbType.Uuid, Id);

                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (reader.Read())
                {
                    lr.Id = (Guid) reader["id"];
                    lr.ReportDateTime = (DateTime) reader["report_time"];
                    lr.Latitude = (double) reader["latitude"];
                    lr.Longitude = (double) reader["longitude"];
                }
                else
                {
                    throw new Exceptions.UserDoesNotReportLocationException();
                }
                return lr;
            }
        }

        public async Task<bool> IsHome()
        {
            LocationReport current = await GetLatestLocation();
            Home myHome = await Home.GetHome(HomeId ?? new Guid()); //TODO: More elegance here
            if (DateTime.UtcNow - current.ReportDateTime > new TimeSpan(TimeSpan.TicksPerHour*2)) return false; // TODO: Return stuff over 2 hours maybe? 
            return DistanceBetweenPlaces(current.Longitude, current.Latitude, myHome.HomeLocation.Longitude, myHome.HomeLocation.Latitude) > .100;
            // Closer than 100m and we're considered home
        }
        
        public static double DistanceBetweenPlaces(double lon1, double lat1, double lon2, double lat2)
        {
            double R = 6371; // km

            double sLat1 = Math.Sin(Radians(lat1));
            double sLat2 = Math.Sin(Radians(lat2));
            double cLat1 = Math.Cos(Radians(lat1));
            double cLat2 = Math.Cos(Radians(lat2));
            double cLon = Math.Cos(Radians(lon1) - Radians(lon2));

            double cosD = sLat1*sLat2 + cLat1*cLat2*cLon;

            double d = Math.Acos(cosD);

            double dist = R * d;

            return dist;
        }
        
        public static double Radians(double x)
        {
            return x * Math.PI / 180;
        }
        
        /// <summary>
        /// Not elegant in design
        /// Verifies user auth w/ apple and pulls their details for a first time signup
        /// also returns the refresh token which is what we need to verify again in the
        /// future.
        /// </summary>
        /// <param name="response">The token response from apple.</param>
        /// <returns>The refresh token</returns>
        private static async Task<string> GetAppleSubFromRefresh(string refreshToken)
        {
            var client = RoomyEnv.Apple.AuthClient;
            var token = await  client.RefreshTokenAsync(refreshToken);
            var stream =token.IdToken;  
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(stream);
            var tokenS = jsonToken as JwtSecurityToken;

            return tokenS.Payload.Sub;;
        }

        /// <summary>
        /// Wrap our Initialize() function into a static function
        /// </summary>
        /// <param name="authKey">The auth key associated with the account</param>
        /// <returns>The user with the matching auth key</returns>
        private static async Task<User> GetUser(string authKey)
        {
            User newUser = new User() { AuthKey = authKey };
            await newUser.Initialize();
            return newUser;
        }
        
        /// <summary>
        /// Wrap our Initialize() function into a static function
        /// </summary>
        /// <param name="authKey">The auth key associated with the account</param>
        /// <returns>The user with the matching auth key</returns>
        private static async Task<User> GetUser(Guid userId)
        {
            User newUser = new User() { Id = userId };
            await newUser.Initialize();
            return newUser;
        }
        
        public async Task<List<Chore>> GetChores()
        {
            List<ChoreFormula> formulas = new List<ChoreFormula>();
            await using (var cmd = new NpgsqlCommand("SELECT * FROM get_user_chores(@identifier)",
                RoomyEnv.Database.DatabaseConnection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("identifier", NpgsqlDbType.Uuid, Id);

                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (reader.Read())
                {
                    ChoreFormula form = new ChoreFormula();
                    form.CompletedDate = (DateTime) reader["completed_date"];
                    form.Emoji = reader["emoji"].ToString();
                    form.Duration = reader["duration"].ToString();
                    form.FirstDate = (DateTime) reader["first_date"];
                    form.HomeId = (Guid) reader["home_id"];
                    form.Id = (Guid) reader["id"];
                    form.Name = reader["name"].ToString();
                    form.Repetition =
                        RoomyChoreTimeSpan.Parse(TimeSpan.FromMilliseconds((double.Parse(reader["repetition"].ToString()))));
                    form.UserId = (Guid) reader["user_id"];
                   
                    formulas.Add(form);
                    
                }
            }
            return await ChoreFormula.ToChores(formulas);
        }
        

        public static async Task<User> GetUserAuthorized(string authHeader, string authProvider, string userId = null) // authProvider is unnecessary if we get the user before calling out to SSO. This is more performant though and accounts for database not doing so well
        {
            userId ??= authHeader.Split(':')[0];
            authHeader = authHeader.Split(':')[1]; // so we don't mess up the rest of the code
            if (authProvider != "apple")
                throw new Exceptions.UnsupportedAuthProviderException();
            
            Task<User> userTask = GetUser(Guid.Parse(userId));
            if (string.IsNullOrWhiteSpace(authHeader))
                throw new Exceptions.InvalidAuthorizationException();
            
            // If you're testing for scaling, this things causes a 100ms delay
            // Maybe if auth was fully restructured we could cut it out
            // It's calling out to an Apple API which should be able to handle load fine so we should be good
            // (famous last words)
            string userAuthKey = GetAppleSubFromRefresh(authHeader).Result;

            User u =  await userTask;
                
            if (u.AuthKey != userAuthKey)
                throw new Exceptions.InvalidAuthorizationException();

            return u;
        }

        public class Exceptions
        {
            public class UserDoesNotExistException : RoomyException
            {
                public UserDoesNotExistException(): base("This user does not exist.", HttpStatusCode.NotFound) {}
            }
            
            public class UnsupportedAuthProviderException : RoomyException
            {
                public UnsupportedAuthProviderException(): base("Roomy does not support the authorization provider specified.", HttpStatusCode.BadRequest) {}
            }

            public class InvalidAuthorizationException : RoomyException
            {
                public InvalidAuthorizationException(): base("Incorrect authorization information supplied.", HttpStatusCode.Unauthorized) {}
            }
            
            public class UserDoesNotReportLocationException : RoomyException
            {
                public UserDoesNotReportLocationException(): base("This user does not report their location.", HttpStatusCode.BadRequest) {}
            }
            
            public class AssHoleException : RoomyException
            {
                public AssHoleException(): base("Assholes aren't allowed to use Roomy.", HttpStatusCode.Forbidden) {}
            }
        }

    }

    public class CreatedUserResponse
    {
        public User CreatedUser { get; set; }
        public string RefreshToken { get; set; }
    }
    
    
}