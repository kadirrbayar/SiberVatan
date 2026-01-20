using Telegram.Bot.Types;

namespace SiberVatan.Models
{
    /// <summary>
    /// Represents a single message from a user for spam detection
    /// </summary>
    public class UserMessage(Message m)
    {
        public DateTime Time { get; set; } = m.Date;
        public bool Replied { get; set; }
    }

    public class SpamDetector
    {
        public bool NotifiedAdmin { get; set; } = false;
        public int Warns { get; set; } = 0;
        public HashSet<UserMessage> Messages { get; set; } = [];
    }
}
