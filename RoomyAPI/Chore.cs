using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RoomyAPI
{
    public class Chore
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string Emoji { get; set; }
        public DateTime Date { get; set; }
    }

    public class ChoreRequest
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string Name { get; set;  }
        public string Emoji { get; set; }
        public string Date { get; set; }
        public string Repeats { get; set; }
        public string Duration { get; set; }

        public async Task<ChoreFormula> ToChoreFormula()
        {
            ChoreFormula formula = new ChoreFormula()
            {
                Id = Guid.Empty, 
                UserId = Guid.Parse(UserId),
                Name = Name,
                Emoji = Emoji,
                CompletedDate = DateTime.UtcNow,
                FirstDate = DateTime.Parse(Date),
                Repetition = RoomyChoreTimeSpan.Parse(Repeats),
                Duration = Duration
            };
            

            return formula;
        }
    }
    
    public class ChoreFormula
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public Guid HomeId { get; set; }
        
        public string Name { get; set; }
        public string Duration { get; set; }
        [MinLength(1)]
        [MaxLength(1)] //TODO: Add this everywhere
        public string Emoji { get; set; }
        public DateTime? FirstDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public RoomyChoreTimeSpan Repetition { get; set; }

        public static async Task<List<Chore>> ToChores(List<ChoreFormula> formulas)
        {
            DateTime limit = DateTime.UtcNow.AddDays(14);
            List<Chore> chores = new List<Chore>();
            foreach (ChoreFormula formula in formulas)
            {
                for (DateTime time = formula.CompletedDate ?? throw  new Exception();
                    time < limit;
                    time = time.Add(formula.Repetition.RepeatingTime))
                {
                    chores.Add(new Chore()
                    {
                        Name = formula.Name,
                        Date = time,
                        Emoji = formula.Emoji,
                        Id = formula.Id,
                        UserId = formula.UserId ?? throw new Exception()
                    });
                }
            }

            return chores.OrderByDescending(x => x.Date).ToList();
        }
    }

    public class RoomyChoreTimeSpan
    {
        public TimeSpan RepeatingTime { get; set; }

        public static RoomyChoreTimeSpan Parse(TimeSpan length)
        {
            return new RoomyChoreTimeSpan()
            {
                RepeatingTime = length
            };
        }

        public static RoomyChoreTimeSpan Parse(string length)
        {
            TimeSpan repeating = new TimeSpan();
            switch (length.ToLower())
            {
                case "never":
                    return null;
                
                case "daily":
                    repeating = new TimeSpan(TimeSpan.TicksPerDay);
                    break;
                
                case "halfdaily":
                    repeating = new TimeSpan(TimeSpan.TicksPerDay*2);
                    break;

                case "weekly":
                    repeating = new TimeSpan(TimeSpan.TicksPerDay * 7);
                    break;
                
                case "bimonthly":
                    repeating = new TimeSpan(TimeSpan.TicksPerDay * 14);
                    break;
                
                case "monthly":
                    repeating = new TimeSpan(TimeSpan.TicksPerDay * 31); // Don't use fr math here, just detect it's a month then use init datetime
                    break;
                    
            }

            return new RoomyChoreTimeSpan
            {
                RepeatingTime = repeating
            };
        }
    }
}