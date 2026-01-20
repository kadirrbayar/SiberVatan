using SiberVatan.Helpers;

namespace SiberVatan.Models
{
    class Command
    {
        public string Trigger { get; set; } = "";
        public bool GroupAdminOnly { get; set; }
        public bool DevOnly { get; set; }
        public bool InGroupOnly { get; set; } 
        public Bot.ChatCommandMethodAsync? MethodAsync { get; set; }
    }

    class CallBack
    {
        public string? Trigger { get; set; }
        public bool GroupAdminOnly { get; set; }
        public bool DevOnly { get; set; }
        public bool UserOnly { get; set; }
        public Bot.ChatCallbackMethodAsync? MethodAsync { get; set; }
    }
}
