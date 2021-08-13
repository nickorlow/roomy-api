using System;

namespace RoomyAPI
{
    public class Rule
    {
        public Guid Id { get; set; }
        public Guid HomeId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }
}