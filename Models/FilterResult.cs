namespace SiberVatan.Models;

/// <summary>
/// Result of a content filter check
/// </summary>
public class FilterResult
{
    /// <summary>
    /// Whether a violation was detected
    /// </summary>
    public bool IsViolation { get; set; }

    /// <summary>
    /// Type of violation (flood, length, media, rtl, arabic, etc.)
    /// </summary>
    public string ViolationType { get; set; } = string.Empty;

    /// <summary>
    /// Action to take (warn, kick, ban, tempban, none)
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Whether the action was successfully executed
    /// </summary>
    public bool ActionExecuted { get; set; }

    /// <summary>
    /// Result or error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional context (e.g., strike count, max strikes)
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = [];

    /// <summary>
    /// Create a non-violation result
    /// </summary>
    public static FilterResult NoViolation() => new() { IsViolation = false };

    /// <summary>
    /// Create a violation result
    /// </summary>
    public static FilterResult Violation(string type, string action, string message = "")
    {
        return new FilterResult
        {
            IsViolation = true,
            ViolationType = type,
            Action = action,
            Message = message
        };
    }
}
