using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using AppleAuth;
using AppleAuth.TokenObjects;
using Golap.AppleAuth;
using Golap.AppleAuth.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
namespace RoomyAPI.Controllers
{
    /// <summary>
    /// Mostly a playground for testing/verifying sso stuff, nothing should get called here in actual usage.
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class HomeController : ControllerBase
    {
        //TODO VALIDATION ON ALL ENDPOINTS
        [HttpPost]
        [Route("{userId}")]
        public async Task<object> CreateHome(string userId, Home newHome) //TODO: we shouldn't have UserId here
        {
            User creator = await RoomyAPI.User.GetUserAuthorized(userId, HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"]);

            newHome = await Home.CreateHome(newHome);
            await creator.JoinHome(newHome.Id);
            return Created($"https://{HttpContext.Request.Host}/home/{newHome.Id}",newHome);
        }

        [HttpGet]
        [Route("{homeId}/chores")]
        public async Task<object> GetChores(string homeId, string userIdS = null)
        {
            User user = await RoomyAPI.User.GetUserAuthorized(HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"]);
            if (user.HomeId == null)
                throw new Exception();

            
            List<Chore> chores = await (await Home.GetHome(user.HomeId ?? new Guid())).GetChores();
            if (userIdS != null)
            {
                Guid userId = Guid.Parse(userIdS);
                chores = chores.Where(x => x.UserId == userId).ToList();
            }

            return Ok(chores);
        }

        [HttpPost]
        [Route("{homeId}/chores")]
        public async Task<object> CreateChore(ChoreRequest chore)
        {
            User user = await RoomyAPI.User.GetUserAuthorized(HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"]);
            if (user.HomeId == null)
                throw new Exception();
            Home home = await Home.GetHome(user.HomeId ?? new Guid());

            ChoreFormula formula = await chore.ToChoreFormula();
            await home.AddChoreFormula(formula);
            
            return Ok();
        }

        [HttpGet]
        [Route("{homeId}/groceries")]
        public async Task<object> GetHomeGroceries(string homeId)
        { 
            User user = await RoomyAPI.User.GetUserAuthorized(HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"]);
            if (user.HomeId == null || user.HomeId != Guid.Parse(homeId))
                throw new Exception();
            return Ok(await (await Home.GetHome(user.HomeId ?? new Guid())).GetGroceryItems());
        }
        
        [HttpPatch]
        [Route("{homeId}/chores/{choreId}")]
        public async Task<object> UpdateChore(string homeId, string choreId, ChoreFormula chore)
        {
            User user = await RoomyAPI.User.GetUserAuthorized( HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"]);
            
            if (user.HomeId == null || user.HomeId != Guid.Parse(homeId))
                throw new Exception();
            Home home = await Home.GetHome(user.HomeId ?? new Guid());
            await home.UpdateChore(chore, choreId);
            return Ok();
        }
        
        [HttpPost]
        [Route("{homeId}/groceries")]
        public async Task<object> CreateGroceryItem(string homeId, GroceryItem item)
        {
            User user = await RoomyAPI.User.GetUserAuthorized(HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"]);
            
            if (user.HomeId == null || user.HomeId != Guid.Parse(homeId))
                throw new Exception();
            
            Home home = await Home.GetHome(user.HomeId ?? new Guid());
            await home.AddGroceryItem(item, user);
            return Ok();
        }
        
        [HttpPatch]
        [Route("{homeId}/groceries/{groceryId}")]
        public async Task<object> UpdateGroceryItem(string homeId, string groceryId, GroceryItem item)
        {
            User user = await RoomyAPI.User.GetUserAuthorized(HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"]);
            
            if (user.HomeId == null || user.HomeId != Guid.Parse(homeId))
                throw new Exception();
            
            // line is redundant though it helps fit REST standards
            item.Id = Guid.Parse(groceryId);
            
            Home home = await Home.GetHome(user.HomeId ?? new Guid());
            await home.UpdateGroceryItem(item, user);
            return Ok();
        }

        [HttpDelete]
        [Route("{homeId}/groceries/{groceryId}")]
        public async Task<object> DeleteGroceryItem(string homeId, string groceryId)
        {
            User user = await RoomyAPI.User.GetUserAuthorized(HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"]);
            
            if (user.HomeId == null || user.HomeId != Guid.Parse(homeId))
                throw new Exception();
            
            Home home = await Home.GetHome(user.HomeId ?? new Guid());
            
            
            await home.DeleteGroceryItem(Guid.Parse(groceryId));
            return Ok();
        }
        
        [HttpGet]
        [Route("{homeId}/rules")]
        public async Task<object> GetRules(string homeId)
        { 
            User user = await RoomyAPI.User.GetUserAuthorized(HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"]);
            if (user.HomeId == null || user.HomeId != Guid.Parse(homeId))
                throw new Exception();
            return Ok(await (await Home.GetHome(user.HomeId ?? new Guid())).GetRules());
        }
        
        [HttpPatch]
        [Route("{homeId}/rules/{ruleId}")]
        public async Task<object> UpdateRule(string homeId, string ruleId, Rule rule)
        {
            User user = await RoomyAPI.User.GetUserAuthorized(HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"]);
            
            if (user.HomeId == null || user.HomeId != Guid.Parse(homeId))
                throw new Exception();
            
            // line is redundant though it helps fit REST standards
            rule.Id = Guid.Parse(ruleId);
            
            Home home = await Home.GetHome(user.HomeId ?? new Guid());
            
            if (user.HomeId == null || user.HomeId != Guid.Parse(homeId))
                throw new Exception();

            await home.UpdateRule(rule);
            return Ok();
        }

        
        [HttpPost]
        [Route("{homeId}/rules")]
        public async Task<object> CreateRule(string homeId, Rule rule)
        {
            User user = await RoomyAPI.User.GetUserAuthorized(HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"]);
            
            if (user.HomeId == null || user.HomeId != Guid.Parse(homeId))
                throw new Exception();
            
            Home home = await Home.GetHome(user.HomeId ?? new Guid());
            await home.CreateRule(rule);
            return Ok();
        }
        
        [HttpDelete]
        [Route("{homeId}/rules/{ruleId}")]
        public async Task<object> DeleteRule(string homeId, string ruleId)
        {
            User user = await RoomyAPI.User.GetUserAuthorized(HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"]);
            
            if (user.HomeId == null || user.HomeId != Guid.Parse(homeId))
                throw new Exception();
            
            Home home = await Home.GetHome(user.HomeId ?? new Guid());
            
            
            await home.DeleteRule(Guid.Parse(ruleId));
            return Ok();
        }
    }
}