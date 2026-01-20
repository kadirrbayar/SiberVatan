using Microsoft.Extensions.Caching.Memory;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SiberVatan.Helpers
{
  internal static class UpdateHelper
  {
    public static readonly MemoryCache AdminCache = new(new MemoryCacheOptions());
    internal static long[] Devs { get; }

    static UpdateHelper()
    {
      var adminStr = Environment.GetEnvironmentVariable("ADMIN_USER_IDS");
      var adminIds = adminStr.Split(',').Select(x => long.TryParse(x.Trim(), out var id) ? id : 0).Where(x => x != 0).ToArray();
      Log.Information("Loaded {AdminCount} global admins: {AdminIds}", adminIds.Length, string.Join(", ", adminIds));
      if (adminIds.Length == 0)
        Log.Warning("No global admins configured in .env file!");
      Devs = adminIds;
    }

    internal static bool IsGroupAdmin(Update update)
    {
      return IsGroupAdmin(update.Message.From.Id, update.Message.Chat.Id);
    }

    internal static bool IsGroupAdmin(long user, long group)
    {
      string itemIndex = $"{group}";
      if (!AdminCache.TryGetValue(itemIndex, out List<long>? admins))
      {
        var policy = new MemoryCacheEntryOptions { AbsoluteExpiration = DateTime.UtcNow.AddMinutes(60) };
        try
        {
          var t = Bot.Api.GetChatAdministrators(group).Result;
          admins = [.. t.Where(x => !string.IsNullOrEmpty(x.User.FirstName)).Select(x => x.User.Id)];
          AdminCache.Set(itemIndex, admins, policy);
        }
        catch (Exception ex)
        {
          Log.Warning(ex, "Failed to get admin list for group {GroupId}", group);
          return false;
        }
      }

      return admins.Any(x => x == user);
    }
  }
}
