using System;

namespace RoomyAPI
{
    public class GroceryItem
    { 
        public Guid? HomeId { get; set;}   
        public Guid? Id { get; set; }
        public Guid? BuyerId { get; set; } //TODO: Write an interface for who can be a buyer
        public  decimal? Price { get; set; }
        public int? Quantity { get; set; }
        public string? Name { get; set; }
        public string? Emoji { get; set; }
    }
}