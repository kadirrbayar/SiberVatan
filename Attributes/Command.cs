namespace SiberVatan.Attributes
{
    [AttributeUsage(AttributeTargets.All)]
    public class Command : Attribute
    {
        /// <summary>
        /// The string to trigger the command
        /// </summary>
        public string? Trigger { get; set; }
        /// <summary>
        /// Is this command limited to group admins only
        /// </summary>
        public bool GroupAdminOnly { get; set; } = false;
        /// <summary>
        /// Developer only command
        /// </summary>
        public bool DevOnly { get; set; } = false;
        public bool InGroupOnly { get; set; } = false;
    }
}
