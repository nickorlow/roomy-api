using System;
using System.Collections;
using System.Net;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RoomyAPI
{
    [JsonObject(MemberSerialization.OptIn)]
    public class RoomyException : Exception
    {
        [JsonInclude]
        [JsonProperty("ErrorMessage")]
        public string ErrorMessage { get; private set; }
        
        [JsonInclude]
        [JsonProperty("ErrorStackTrace")]
        public string ErrorStackTrace { get; private set; }
        
        [JsonInclude]
        [JsonProperty("StatusCode")]
        public HttpStatusCode StatusCode { get; private set; }

        public RoomyException(string errorMessage, HttpStatusCode statusCode, string errorStackTrace = null) : base(errorMessage)
        {
            ErrorMessage = errorMessage;
            StatusCode = statusCode;
            ErrorStackTrace = errorStackTrace ?? StackTrace ?? "None";
        }
    }
}