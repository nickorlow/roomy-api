using System;
using System.Net;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RoomyAPI
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        
        public ErrorHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        
        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (RoomyException ex)
            {
                await HandleExceptionAsync(httpContext, ex);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(httpContext, ex);
            }
        }
        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            RoomyException roomyException =
                new RoomyException($"Internal Server Error ({exception.Message})", HttpStatusCode.InternalServerError);
            LogErrorAsync(context, exception);
            return HandleExceptionAsync(context, roomyException, false);
        }
        
        private Task HandleExceptionAsync(HttpContext context, RoomyException exception, bool log = true)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)exception.StatusCode;
            
            // This is to prevent reverse engineering attacks
            if(exception.StatusCode == HttpStatusCode.Unauthorized || exception.StatusCode == HttpStatusCode.InternalServerError)
                Thread.Sleep(new Random((int) (((DateTime.UtcNow-DateTime.UnixEpoch).TotalMilliseconds)%4.393)).Next(500,5000));
            if (log)
                LogErrorAsync(context, exception);
            return context.Response.WriteAsync(JsonConvert.SerializeObject(exception));
        }

        public async Task LogErrorAsync(HttpContext c, Exception e)
        {
            // TODO
        }
    }
}