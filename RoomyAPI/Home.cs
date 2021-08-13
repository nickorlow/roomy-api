using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace RoomyAPI
{
    public class Home
    {
        public Guid Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Name { get; set; }
        public Location HomeLocation { get; set; }

        private Home(Guid homeId)
        {
            Id = homeId;
        }
        
        public static async Task<Home> CreateHome(Home home)
        {
            home.HomeLocation = await Location.ParseFromId(home.HomeLocation.GoogleLocationId);
            home.Id = Guid.NewGuid();
            await using var cmd = new NpgsqlCommand("CALL create_home(@home_id, @name, @google_location_id, @longitude, @latitude)", RoomyEnv.Database.DatabaseConnection);
            cmd.CommandType = CommandType.Text;
            
            cmd.Parameters.AddWithValue("home_id",NpgsqlDbType.Uuid, home.Id);
            cmd.Parameters.AddWithValue("name", home.Name);
            cmd.Parameters.AddWithValue("google_location_id", home.HomeLocation.GoogleLocationId);
            cmd.Parameters.AddWithValue("longitude",NpgsqlDbType.Double, home.HomeLocation.Longitude);
            cmd.Parameters.AddWithValue("latitude",NpgsqlDbType.Double, home.HomeLocation.Latitude);
            await cmd.ExecuteNonQueryAsync();

            return home;
        }
        
        private async Task Initialize()
        {
            await using (var cmd = new NpgsqlCommand("SELECT * FROM get_home(@identifier)",
                RoomyEnv.Database.DatabaseConnection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("identifier", NpgsqlDbType.Uuid, Id);

                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
                HomeLocation = new Location();
                if (reader.Read())
                {
                    Id = (Guid) reader["id"];
                    CreatedDate = (DateTime) reader["created_date"];
                    
                    Name = (string) reader["name"];
                    HomeLocation.Latitude = (double) reader["latitude"];
                    HomeLocation.Longitude = (double) reader["longitude"];
                    HomeLocation.GoogleLocationId = (string) reader["google_location_id"];
                }
                else
                {
                    throw new Exceptions.HomeDoesNotExistException();
                }
            }
        }

        public static async Task<Home> GetHome(Guid homeId)
        {
            Home ret = new Home(homeId);
            await ret.Initialize();
            return ret;
        }

        public async Task<List<Chore>> GetChores()
        {
            List<ChoreFormula> formulas = new List<ChoreFormula>();
            await using (var cmd = new NpgsqlCommand("SELECT * FROM get_home_chores(@identifier)",
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

        public async Task AddChoreFormula(ChoreFormula formula)
        {

            if (!(await GetRoomys()).Exists(x => x.Id == formula.UserId))
                throw new Exception(); //TODO Create exception for not-in-house-user
            
            await using var cmd = new NpgsqlCommand("CALL create_chore(@home_id, @name, @emoji, @first_date, @user_id, @completed_date, @instructions, @repetition, @duration)", RoomyEnv.Database.DatabaseConnection);
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddWithValue("home_id",NpgsqlDbType.Uuid, Id);
            cmd.Parameters.AddWithValue("name",NpgsqlDbType.Text, formula.Name);
            cmd.Parameters.AddWithValue("emoji",NpgsqlDbType.Char, formula.Emoji);
            cmd.Parameters.AddWithValue("first_date",NpgsqlDbType.Timestamp, formula.FirstDate);
            cmd.Parameters.AddWithValue("user_id",NpgsqlDbType.Uuid, formula.UserId);
            cmd.Parameters.AddWithValue("completed_date",NpgsqlDbType.Timestamp, formula.CompletedDate);
            cmd.Parameters.AddWithValue("instructions",NpgsqlDbType.Text, ""); // not adding (yet)
            cmd.Parameters.AddWithValue("repetition",NpgsqlDbType.Numeric, formula.Repetition.RepeatingTime.TotalMilliseconds);
            cmd.Parameters.AddWithValue("duration",NpgsqlDbType.Text, formula.Duration);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateChore(ChoreFormula chore, string choreId)
        {
            try
            {
                await using var cmd = new NpgsqlCommand("CALL update_chore(@chore_id, @home_id, @completed_date, @name, @emoji, @first_date, @user_id, @instructions, @repetition, @duration)", RoomyEnv.Database.DatabaseConnection);
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("chore_id",NpgsqlDbType.Uuid, Guid.Parse(choreId));
                cmd.Parameters.AddWithValue("home_id",NpgsqlDbType.Uuid, Id);
                
                
                if (chore.CompletedDate != null)
                    cmd.Parameters.AddWithValue("completed_date", NpgsqlDbType.Timestamp, chore.CompletedDate);
                else
                    cmd.Parameters.AddWithValue("completed_date", DBNull.Value);
            
                if (chore.Name != null)
                    cmd.Parameters.AddWithValue("name",NpgsqlDbType.Text, chore.Name);
                else
                    cmd.Parameters.AddWithValue("name", DBNull.Value);
            
                if (chore.Emoji != null)
                    cmd.Parameters.AddWithValue("emoji",NpgsqlDbType.Char, chore.Emoji);
                else
                    cmd.Parameters.AddWithValue("emoji", DBNull.Value);
            
                if (chore.FirstDate != null)
                    cmd.Parameters.AddWithValue("first_date", NpgsqlDbType.Timestamp, chore.FirstDate);
                else
                    cmd.Parameters.AddWithValue("first_date", DBNull.Value);
                
                if (chore.UserId != null)
                    cmd.Parameters.AddWithValue("user_id", NpgsqlDbType.Uuid, chore.UserId);
                else
                    cmd.Parameters.AddWithValue("user_id", DBNull.Value);
                
                //if (chore.Instructions != null)
                //    cmd.Parameters.AddWithValue("instructions", NpgsqlDbType.Text, item.Quantity);
                //else
                cmd.Parameters.AddWithValue("instructions", DBNull.Value);
                
                if (chore.Repetition != null)
                    cmd.Parameters.AddWithValue("repetition", NpgsqlDbType.Numeric, chore.Repetition);
                else
                    cmd.Parameters.AddWithValue("repetition", DBNull.Value);
                
                if (chore.Duration != null)
                    cmd.Parameters.AddWithValue("duration", NpgsqlDbType.Text, chore.Duration);
                else
                    cmd.Parameters.AddWithValue("duration", DBNull.Value);

                await cmd.ExecuteNonQueryAsync(); 
            }
            catch (PostgresException e)
            {
                
            }
        }
        
        public async Task AddGroceryItem(GroceryItem item, User requestingUser)
        {

            if ((!(await GetRoomys()).Exists(x => x.Id == requestingUser.Id)) || (item.BuyerId != requestingUser.Id && item.BuyerId != Id))
                throw new Exception(); //TODO Create exception for not-in-house-user
            
            await using var cmd = new NpgsqlCommand("CALL create_grocery_item(@home_id, @buyer_id, @price, @quantity, @name, @emoji)", RoomyEnv.Database.DatabaseConnection);
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddWithValue("home_id",NpgsqlDbType.Uuid, Id);
            cmd.Parameters.AddWithValue("name",NpgsqlDbType.Text, item.Name);
            cmd.Parameters.AddWithValue("emoji",NpgsqlDbType.Char, item.Emoji);
            cmd.Parameters.AddWithValue("buyer_id", NpgsqlDbType.Uuid, item.BuyerId);
            cmd.Parameters.AddWithValue("price", NpgsqlDbType.Numeric, item.Price);
            cmd.Parameters.AddWithValue("quantity", NpgsqlDbType.Integer, item.Quantity);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateGroceryItem(GroceryItem item, User requestingUser)
        {
            if (item.BuyerId != null && ((!(await GetRoomys()).Exists(x => x.Id == requestingUser.Id)) || (item.BuyerId != requestingUser.Id && item.BuyerId != Id)))
                throw new Exception(); //TODO Create exception for not-in-house-user
            
            await using var cmd = new NpgsqlCommand("CALL update_grocery_item(@item_id, @home_id, @buyer_id, @price, @quantity, @name, @emoji)", RoomyEnv.Database.DatabaseConnection);
            cmd.CommandType = CommandType.Text;
            
            cmd.Parameters.AddWithValue("home_id", NpgsqlDbType.Uuid, Id);
            cmd.Parameters.AddWithValue("item_id", NpgsqlDbType.Uuid, item.Id);

            if (item.BuyerId != null)
                cmd.Parameters.AddWithValue("buyer_id", NpgsqlDbType.Uuid, item.BuyerId);
            else
                cmd.Parameters.AddWithValue("buyer_id", DBNull.Value);
            
            if (item.Name != null)
                cmd.Parameters.AddWithValue("name",NpgsqlDbType.Text, item.Name);
            else
                cmd.Parameters.AddWithValue("name", DBNull.Value);
            
            if (item.Emoji != null)
                cmd.Parameters.AddWithValue("emoji",NpgsqlDbType.Char, item.Emoji);
            else
                cmd.Parameters.AddWithValue("emoji", DBNull.Value);
            
            if (item.Price != null)
                cmd.Parameters.AddWithValue("price", NpgsqlDbType.Numeric, item.Price);
            else
                cmd.Parameters.AddWithValue("price", DBNull.Value);
            
            if (item.Quantity != null)
                cmd.Parameters.AddWithValue("quantity", NpgsqlDbType.Integer, item.Quantity);
            else
                cmd.Parameters.AddWithValue("quantity", DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        
        public async Task DeleteGroceryItem(Guid GroceryId)
        {
            await using var cmd = new NpgsqlCommand("CALL delete_grocery_item(@item_id, @home_id)", RoomyEnv.Database.DatabaseConnection);
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddWithValue("item_id", NpgsqlDbType.Uuid, GroceryId);
            cmd.Parameters.AddWithValue("home_id", NpgsqlDbType.Uuid, Id);
            await cmd.ExecuteNonQueryAsync();
        }
        
        public async Task CreateRule(Rule rule)
        {
            await using var cmd = new NpgsqlCommand("CALL create_rule(@home_id, @title, @description)", RoomyEnv.Database.DatabaseConnection);
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddWithValue("home_id",NpgsqlDbType.Uuid, Id);
            cmd.Parameters.AddWithValue("title",NpgsqlDbType.Text, rule.Title);
            cmd.Parameters.AddWithValue("description",NpgsqlDbType.Char, rule.Description);
            await cmd.ExecuteNonQueryAsync();
        }
        
        public async Task<List<Rule>> GetRules()
        {
            List<Rule> rules = new List<Rule>();
            await using (var cmd = new NpgsqlCommand("SELECT * FROM get_home_rules(@identifier)",
                RoomyEnv.Database.DatabaseConnection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("identifier", NpgsqlDbType.Uuid, Id);

                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (reader.Read())
                {
                    rules.Add(
                    new(){
                        Title = reader["title"].ToString(),
                        Description = reader["description"].ToString(),
                        Id = (Guid) reader["id"],
                        HomeId = (Guid) reader["home_id"]
                    });
                }
            }

            return rules;
        }
        
        
        public async Task UpdateRule(Rule rule)
        {
            await using var cmd = new NpgsqlCommand("CALL update_rule(@rule_id, @home_id, @title, @description)", RoomyEnv.Database.DatabaseConnection);
            cmd.CommandType = CommandType.Text;
            
            cmd.Parameters.AddWithValue("home_id", NpgsqlDbType.Uuid, Id);
            cmd.Parameters.AddWithValue("rule_id", NpgsqlDbType.Uuid, rule.Id);

         
            if (rule.Title != null)
                cmd.Parameters.AddWithValue("title", NpgsqlDbType.Text, rule.Title);
            else
                cmd.Parameters.AddWithValue("title", DBNull.Value);
            
            if (rule.Description != null)
                cmd.Parameters.AddWithValue("description", NpgsqlDbType.Text, rule.Description);
            else
                cmd.Parameters.AddWithValue("description", DBNull.Value);
            
            await cmd.ExecuteNonQueryAsync();
        }
        
        public async Task DeleteRule(Guid RuleId)
        {
            await using var cmd = new NpgsqlCommand("CALL delete_rule(@rule_id, @home_id)", RoomyEnv.Database.DatabaseConnection);
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddWithValue("rule_id", NpgsqlDbType.Uuid, RuleId);
            cmd.Parameters.AddWithValue("home_id", NpgsqlDbType.Uuid, Id);
            await cmd.ExecuteNonQueryAsync();
        }

        
        
        public async Task<List<User>> GetRoomys()
        {
            List<User> Roomys = new List<User>();
            await using (var cmd = new NpgsqlCommand("SELECT * FROM get_user_by_home(@identifier)",
                RoomyEnv.Database.DatabaseConnection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("identifier", NpgsqlDbType.Uuid, Id);

                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (reader.Read())
                {
                    Roomys.Add(await User.FromReader(reader));
                }
            }

            return Roomys;
        }

        public async Task<List<GroceryItem>> GetGroceryItems()
        {
            List<GroceryItem> items = new List<GroceryItem>();
            await using (var cmd = new NpgsqlCommand("SELECT * FROM get_home_groceries(@identifier)",
                RoomyEnv.Database.DatabaseConnection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("identifier", NpgsqlDbType.Uuid, Id);

                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (reader.Read())
                {
                    items.Add(new GroceryItem()
                    {
                        Id = (Guid) reader["id"],
                        BuyerId = (Guid) reader["buyer_id"],
                        HomeId = Id,
                        Emoji = reader["emoji"].ToString(),
                        Name = reader["name"].ToString(),
                        Price = (decimal) reader["price"],
                        Quantity = (int) reader["quantity"]
                    });
                }
            }

            return items;
        }
                
        public class Exceptions
        {
            public class HomeDoesNotExistException : RoomyException
            {
                public HomeDoesNotExistException(): base("This home does not exist.", HttpStatusCode.NotFound) {}
            }
            
        }
        
    }
}