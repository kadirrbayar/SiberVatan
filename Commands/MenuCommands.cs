using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using SiberVatan.Attributes;
using SiberVatan.Helpers;
using SiberVatan.Interfaces;

namespace SiberVatan
{
    public static partial class Commands
    {
        [Command(Trigger = "menu", InGroupOnly = true, GroupAdminOnly = true)]
        public static async Task Menu(Update update, string[] args)
        {
            var chatId = update.Message.Chat.Id;
            var userId = update.Message.From.Id;

            using var scope = Program.host.Services.CreateScope();
            var redis = ServiceHelper.GetService<IRedisHelper>(scope);
            await GroupSettingsHelper.InitializeDefaultSettingsAsync(chatId, redis);

            var groupName = await redis.HashGetAsync($"chat:{chatId}:details", "name");
            var menuText = GetLocaleString("mainMenu", groupName);
            var menu = GenerateMainMenuAsync(chatId);
            try
            {
                await Bot.Send(menuText, userId, customMenu: menu);
                if (update.Message.Chat.Type != ChatType.Private)
                    await Bot.Send(GetLocaleString("menuSentPM"), chatId);
            }
            catch
            {
                await Bot.Send(GetLocaleString("menuPMRequired"), chatId);
            }
        }

        // ==================== MAIN MENU ====================
        public static InlineKeyboardMarkup GenerateMainMenuAsync(long chatId)
        {
            var buttons = new List<InlineKeyboardButton[]>
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("generalButton"), $"menu|general|{chatId}"),
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("antiButton"), $"menu|anti|{chatId}")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("warnsButton"), $"menu|warns|{chatId}"),
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("floodButton"), $"menu|flood|{chatId}")
                },
                new[] { InlineKeyboardButton.WithCallbackData(GetLocaleString("closeButton"), "menu|close") }
            };

            return new InlineKeyboardMarkup(buttons);
        }

        // ==================== GENERAL MENU ====================
        public static async Task<InlineKeyboardMarkup> GenerateGeneralMenuAsync(long chatId, IRedisHelper redis)
        {
            var welcome = await redis.HashGetAsync($"chat:{chatId}:settings", "Welcome") ?? "yes";
            var deleteWelcome = await redis.HashGetAsync($"chat:{chatId}:settings", "DeleteLastWelcome") ?? "no";
            var extralist = await redis.HashGetAsync($"chat:{chatId}:settings", "Extralist") ?? "no";
            var extra = await redis.HashGetAsync($"chat:{chatId}:settings", "Extra") ?? "no";
            var captcha = await redis.HashGetAsync($"chat:{chatId}:settings", "NewUsersCaptcha") ?? "no";
            var forward = await redis.HashGetAsync($"chat:{chatId}:settings", "AllowChannelForward") ?? "yes";
            var forwardAction = await redis.HashGetAsync($"chat:{chatId}:char", "Forward") ?? "warn";
            //var cas = await redis.HashGetAsync($"chat:{chatId}:settings", "CASBan") ?? "no";

            var buttons = new List<InlineKeyboardButton[]>
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("welcomeLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetToggleIcon(welcome), $"menu|general|{chatId}|toggle|Welcome")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("deleteWelcomeLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetToggleIcon(deleteWelcome), $"menu|general|{chatId}|toggle|DeleteLastWelcome")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("extralistLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(extralist == "yes" ? "👤" : "👥", $"menu|general|{chatId}|toggle|Extralist")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("extraLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(extra == "yes" ? "👤" : "👥", $"menu|general|{chatId}|toggle|Extra")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("captchaLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetToggleIcon(captcha), $"menu|general|{chatId}|toggle|NewUsersCaptcha")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("forwardLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetToggleIcon(forward), $"menu|general|{chatId}|toggle|AllowChannelForward")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("forwardActionLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(forwardAction), $"menu|general|{chatId}|cycle|Forward")
                },
                new[] { InlineKeyboardButton.WithCallbackData(GetLocaleString("backButton"), $"menu|main|{chatId}") }
            };

            return new InlineKeyboardMarkup(buttons);
        }

        // ==================== ANTI (LENGTH & MEDIA) MENU ====================
        public static async Task<InlineKeyboardMarkup> GenerateAntiMenuAsync(long chatId, IRedisHelper redis)
        {
            
            var photoAction = await redis.HashGetAsync($"chat:{chatId}:media", "photo") ?? "allowed";
            var videoAction = await redis.HashGetAsync($"chat:{chatId}:media", "video") ?? "allowed";
            var audioAction = await redis.HashGetAsync($"chat:{chatId}:media", "audio") ?? "allowed";
            var voiceAction = await redis.HashGetAsync($"chat:{chatId}:media", "voice") ?? "allowed";
            var documentAction = await redis.HashGetAsync($"chat:{chatId}:media", "document") ?? "allowed";
            var stickerAction = await redis.HashGetAsync($"chat:{chatId}:media", "sticker") ?? "allowed";

            var videonoteAction = await redis.HashGetAsync($"chat:{chatId}:media", "videonote") ?? "allowed";
            var contactAction = await redis.HashGetAsync($"chat:{chatId}:media", "contact") ?? "allowed";
            var locationAction = await redis.HashGetAsync($"chat:{chatId}:media", "location") ?? "allowed";
            var venueAction = await redis.HashGetAsync($"chat:{chatId}:media", "venue") ?? "allowed";
            var pollAction = await redis.HashGetAsync($"chat:{chatId}:media", "poll") ?? "allowed";
            var gameAction = await redis.HashGetAsync($"chat:{chatId}:media", "game") ?? "allowed";
            
            var apkAction = await redis.HashGetAsync($"chat:{chatId}:media", "apk") ?? "allowed";
            var diceAction = await redis.HashGetAsync($"chat:{chatId}:media", "dice") ?? "allowed";
            var animationAction = await redis.HashGetAsync($"chat:{chatId}:media", "animation") ?? "allowed";
            var urlAction = await redis.HashGetAsync($"chat:{chatId}:media", "url") ?? "allowed";
            var mediaAction = await redis.HashGetAsync($"chat:{chatId}:media", "action") ?? "warn";

            var buttons = new List<InlineKeyboardButton[]>
            {

                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("photoLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(photoAction), $"menu|anti|{chatId}|toggleMedia|photo")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("videoLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(videoAction), $"menu|anti|{chatId}|toggleMedia|video")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("audioLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(audioAction), $"menu|anti|{chatId}|toggleMedia|audio")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("voiceLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(voiceAction), $"menu|anti|{chatId}|toggleMedia|voice")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("documentLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(documentAction), $"menu|anti|{chatId}|toggleMedia|document")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("stickerLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(stickerAction), $"menu|anti|{chatId}|toggleMedia|sticker")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("videonoteLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(videonoteAction), $"menu|anti|{chatId}|toggleMedia|videonote")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("contactLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(contactAction), $"menu|anti|{chatId}|toggleMedia|contact")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("locationLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(locationAction), $"menu|anti|{chatId}|toggleMedia|location")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("venueLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(venueAction), $"menu|anti|{chatId}|toggleMedia|venue")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("pollLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(pollAction), $"menu|anti|{chatId}|toggleMedia|poll")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("gameLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(gameAction), $"menu|anti|{chatId}|toggleMedia|game")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("apkLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(apkAction), $"menu|anti|{chatId}|toggleMedia|apk")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("diceLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(diceAction), $"menu|anti|{chatId}|toggleMedia|dice")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("animationLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(animationAction), $"menu|anti|{chatId}|toggleMedia|animation")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("urlLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(urlAction), $"menu|anti|{chatId}|toggleMedia|url")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("antiMediaActionLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(mediaAction), $"menu|anti|{chatId}|cycleMedia|media")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("antiTextButton"), $"menu|antitext|{chatId}")
                },
                new[] { InlineKeyboardButton.WithCallbackData(GetLocaleString("backButton"), $"menu|main|{chatId}") }
            };

            return new InlineKeyboardMarkup(buttons);
        }

        // ==================== WARNS MENU ====================
        public static async Task<InlineKeyboardMarkup> GenerateWarnsMenuAsync(long chatId, IRedisHelper redis)
        {
            var warnAction = await redis.HashGetAsync($"chat:{chatId}:warnsettings", "action") ?? "kick";
            var maxWarns = await redis.HashGetAsync($"chat:{chatId}:warnsettings", "mediamax") ?? "5";

            var buttons = new List<InlineKeyboardButton[]>
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("warnActionLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(warnAction), $"menu|warns|{chatId}|cycle|WarnAction")
                },
                new[] { InlineKeyboardButton.WithCallbackData(GetLocaleString("maxWarnsLabel"), "menu_ignore") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("➖", $"menu|warns|{chatId}|dec|MaxWarns"),
                    InlineKeyboardButton.WithCallbackData(maxWarns, "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData("➕", $"menu|warns|{chatId}|inc|MaxWarns")
                },
                new[] { InlineKeyboardButton.WithCallbackData(GetLocaleString("backButton"), $"menu|main|{chatId}") }
            };

            return new InlineKeyboardMarkup(buttons);
        }

        // ==================== FLOOD MENU ====================
        public static async Task<InlineKeyboardMarkup> GenerateFloodMenuAsync(long chatId, IRedisHelper redis)
        {
            var flood = await redis.HashGetAsync($"chat:{chatId}:settings", "Flood") ?? "no";
            var floodAction = await redis.HashGetAsync($"chat:{chatId}:flood", "ActionFlood") ?? "mute";
            var maxFlood = await redis.HashGetAsync($"chat:{chatId}:flood", "MaxFlood") ?? "5";

            var buttons = new List<InlineKeyboardButton[]>
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("antiFloodLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetToggleIcon(flood),
                        $"menu|flood|{chatId}|toggle|AntiFlood")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("floodActionLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(floodAction),
                        $"menu|flood|{chatId}|cycle|FloodAction")
                },
                new[] { InlineKeyboardButton.WithCallbackData(GetLocaleString("messages5sLabel"), "menu_ignore") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("➖", $"menu|flood|{chatId}|dec|MaxFlood"),
                    InlineKeyboardButton.WithCallbackData(maxFlood, "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData("➕", $"menu|flood|{chatId}|inc|MaxFlood")
                },
                new[] { InlineKeyboardButton.WithCallbackData(GetLocaleString("backButton"), $"menu|main|{chatId}") }
            };

            return new InlineKeyboardMarkup(buttons);
        }

        // ==================== ANTI TEXT MENU ====================
        public static async Task<InlineKeyboardMarkup> GenerateAntiTextMenuAsync(long chatId, IRedisHelper redis)
        {
            var textEnabled = await redis.HashGetAsync($"chat:{chatId}:antitextlength", "enabled") ?? "no";
            var textAction = await redis.HashGetAsync($"chat:{chatId}:antitextlength", "action") ?? "warn";
            var maxLength = await redis.HashGetAsync($"chat:{chatId}:antitextlength", "maxlength") ?? "4000";
            var maxLines = await redis.HashGetAsync($"chat:{chatId}:antitextlength", "maxlines") ?? "50";

            var buttons = new List<InlineKeyboardButton[]>
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("antiTextLengthToggleLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetToggleIcon(textEnabled), $"menu|antitext|{chatId}|toggle|enabled")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(GetLocaleString("antiTextLengthActionLabel"), "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData(GroupSettingsHelper.GetActionIcon(textAction), $"menu|antitext|{chatId}|cycle|action")
                },
                new[] { InlineKeyboardButton.WithCallbackData(GetLocaleString("antiTextMaxLengthLabel"), "menu_ignore") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("➖", $"menu|antitext|{chatId}|dec|maxlength"),
                    InlineKeyboardButton.WithCallbackData(maxLength, "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData("➕", $"menu|antitext|{chatId}|inc|maxlength")
                },
                new[] { InlineKeyboardButton.WithCallbackData(GetLocaleString("antiTextMaxLinesLabel"), "menu_ignore") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("➖", $"menu|antitext|{chatId}|dec|maxlines"),
                    InlineKeyboardButton.WithCallbackData(maxLines, "menu_ignore"),
                    InlineKeyboardButton.WithCallbackData("➕", $"menu|antitext|{chatId}|inc|maxlines")
                },
                new[] { InlineKeyboardButton.WithCallbackData(GetLocaleString("backButton"), $"menu|anti|{chatId}") }
            };

            return new InlineKeyboardMarkup(buttons);
        }
    }
}