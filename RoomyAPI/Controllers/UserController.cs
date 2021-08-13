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
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        [HttpGet]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
        [Route("{userId}")]
        public async Task<object> GetUser(string userId)
        {
            return Ok(await RoomyAPI.User.GetUserAuthorized( HttpContext.Request.Headers["Authorization"], HttpContext.Request.Headers["Authorization-Provider"], userId));
        }
        
        [HttpPost]
        [Route("apple")]
        public async Task<object> CreateUser(InitialTokenResponse response)
        {
            CreatedUserResponse newUser = await RoomyAPI.User.CreateWithApple(response);
            return Created($"https://{HttpContext.Request.Host}/user/{newUser.CreatedUser.Id}", newUser);
        }

        [HttpPut]
        [Route("{userId}")]
        public async Task<object> JoinHouse(string userId, Home home)
        {
            User user = await RoomyAPI.User.GetUserAuthorized( HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"], userId);
            await user.JoinHome(home.Id);
            return Ok(user);
        }

        [HttpPut]
        [Route("{userId}/location")]
        public async Task<object> ReportLocation(string userId, Location currentLocation)
        {
            // Do it this way so the request works fast
            ReportLocationAsync(userId, currentLocation).ConfigureAwait(false);
            return Accepted();
        }

        private async Task ReportLocationAsync(string userId, Location currentLocation)
        {
            User user = await RoomyAPI.User.GetUserAuthorized( HttpContext.Request.Headers["Authorization"],
                HttpContext.Request.Headers["Authorization-Provider"], userId);
            await user.ReportLocation(currentLocation);
        }
    }
}