using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using SiberVatan.Attributes;
using SiberVatan.Helpers;
using SiberVatan.Interfaces;

namespace SiberVatan
{
    public static partial class CallbackCommand
    {
        [CallBack(Trigger = "menu_ignore")]
        public static Task MenuIgnoreHandler(CallbackQuery callback, string[] args)
        {
            return Bot.ReplyToCallback(callback, edit: false);
        }

        [CallBack(Trigger = "menu")]
        public static async Task MenuHandler(CallbackQuery callback, string[] args)
        {
            if (args.Length < 2) return;
            var subMenu = args[1];
            if (subMenu == "close")
            {
                var thankYouText = Commands.GetLocaleString("ThankYou");
                await Bot.ReplyToCallback(callback, thankYouText, edit: true);
                return;
            }

            if (args.Length < 3) return;
            if (!long.TryParse(args[2], out var chatId)) return;

            using var scope = Program.host.Services.CreateScope();
            var redis = ServiceHelper.GetService<IRedisHelper>(scope);

            if (args.Length == 3)
            {
                string menuText;
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup? menu;

                switch (subMenu)
                {
                    case "general":
                        menuText = Commands.GetLocaleString("generalMenu");
                        menu = await Commands.GenerateGeneralMenuAsync(chatId, redis);
                        break;
                    case "anti":
                        menuText = Commands.GetLocaleString("antiMenu");
                        menu = await Commands.GenerateAntiMenuAsync(chatId, redis);
                        break;
                    case "warns":
                        menuText = Commands.GetLocaleString("warnsMenu");
                        menu = await Commands.GenerateWarnsMenuAsync(chatId, redis);
                        break;
                    case "flood":
                        menuText = Commands.GetLocaleString("floodMenu");
                        menu = await Commands.GenerateFloodMenuAsync(chatId, redis);
                        break;
                    case "antitext":
                        menuText = Commands.GetLocaleString("antiTextMenu");
                        menu = await Commands.GenerateAntiTextMenuAsync(chatId, redis);
                        break;
                    default:
                        var groupName = await redis.HashGetAsync($"chat:{chatId}:details", "name");
                        menuText = Commands.GetLocaleString("mainMenu", groupName);
                        menu = Commands.GenerateMainMenuAsync(chatId);
                        break;
                }

                await Bot.ReplyToCallback(callback, menuText, replyMarkup: menu);
                return;
            }

            if (args.Length > 3)
            {
                var action = args[3];
                if (subMenu == "general")
                {
                    var settingKey = args[4];
                    if (action == "toggle")
                    {
                        var current = await redis.HashGetAsync($"chat:{chatId}:settings", settingKey);
                        var currentStr = current ?? "no";
                        var newValue = currentStr == "yes" ? "no" : "yes";
                        await redis.HashSetAsync($"chat:{chatId}:settings", settingKey, newValue);
                    }
                    else if (action == "cycle")
                    {
                        var charKey = settingKey;
                        var current = await redis.HashGetAsync($"chat:{chatId}:char", charKey);
                        var newAction = GroupSettingsHelper.GetNextAction(current ?? "kick");
                        await redis.HashSetAsync($"chat:{chatId}:char", charKey, newAction);
                    }

                    await Bot.ReplyToCallback(callback, Commands.GetLocaleString("generalMenu"),
                        replyMarkup: await Commands.GenerateGeneralMenuAsync(chatId, redis));
                }
                else if (subMenu == "anti")
                {
                    var key = args[4];
                    if (action == "length")
                    {
                        var current = await redis.HashGetAsync($"chat:{chatId}:antitextlength", "enabled");
                        var newStatus = GroupSettingsHelper.ToggleYesNo(current ?? "no");
                        await redis.HashSetAsync($"chat:{chatId}:antitextlength", "enabled", newStatus);
                    }
                    else if (action == "lengthAction")
                    {
                        var current = await redis.HashGetAsync($"chat:{chatId}:antitextlength", "action");
                        var newValue = GroupSettingsHelper.GetNextAction(current ?? "kick");
                        await redis.HashSetAsync($"chat:{chatId}:antitextlength", "action", newValue);
                    }
                    else if (action == "cycleMedia")
                    {
                        var current = await redis.HashGetAsync($"chat:{chatId}:media", "action");
                        var newAction = GroupSettingsHelper.GetNextAction(current ?? "kick");
                        await redis.HashSetAsync($"chat:{chatId}:media", "action", newAction);
                    }
                    else if (action == "toggleMedia")
                    {
                        var current = await redis.HashGetAsync($"chat:{chatId}:media", key);
                        var newStatus = GroupSettingsHelper.ToggleMediaStatus(current ?? "allowed");
                        await redis.HashSetAsync($"chat:{chatId}:media", key, newStatus);
                    }

                    await Bot.ReplyToCallback(callback, Commands.GetLocaleString("antiMenu"),
                        replyMarkup: await Commands.GenerateAntiMenuAsync(chatId, redis));
                }
                else if (subMenu == "warns")
                {
                    const int minWarn = 3;
                    const int maxWarn = 8;

                    if (action == "cycle")
                    {
                        var current = await redis.HashGetAsync($"chat:{chatId}:warnsettings", "action");
                        var newAction = GroupSettingsHelper.GetNextAction(current ?? "kick");
                        if (newAction == "warn")
                        {
                            newAction = GroupSettingsHelper.GetNextAction(newAction);
                        }

                        await redis.HashSetAsync($"chat:{chatId}:warnsettings", "action", newAction);
                    }
                    else if (action == "inc")
                    {
                        var currentInc = await redis.HashIncrementAsync($"chat:{chatId}:warnsettings", "mediamax");
                        if (currentInc > maxWarn)
                            await redis.HashSetAsync($"chat:{chatId}:warnsettings", "mediamax", minWarn.ToString());
                    }
                    else if (action == "dec")
                    {
                        var currentDec = await redis.HashIncrementAsync($"chat:{chatId}:warnsettings", "mediamax", -1);
                        if (currentDec < minWarn)
                            await redis.HashSetAsync($"chat:{chatId}:warnsettings", "mediamax", maxWarn.ToString());
                    }

                    await Bot.ReplyToCallback(callback, Commands.GetLocaleString("warnsMenu"),
                        replyMarkup: await Commands.GenerateWarnsMenuAsync(chatId, redis));
                }
                else if (subMenu == "antitext")
                {
                    if (action == "toggle")
                    {
                        var current = await redis.HashGetAsync($"chat:{chatId}:antitextlength", "enabled");
                        var newStatus = GroupSettingsHelper.ToggleYesNo(current ?? "no");
                        await redis.HashSetAsync($"chat:{chatId}:antitextlength", "enabled", newStatus);
                    }
                    else if (action == "cycle")
                    {
                        var current = await redis.HashGetAsync($"chat:{chatId}:antitextlength", "action");
                        var newAction = GroupSettingsHelper.GetNextAction(current ?? "kick");
                        await redis.HashSetAsync($"chat:{chatId}:antitextlength", "action", newAction);
                    }
                    else if (action == "inc" || action == "dec")
                    {
                        var key = args[4];
                        if (key == "maxlength")
                        {
                            var minValue = 500;
                            var maxValue = 4000;
                            if (action == "inc")
                            {
                                var currentInc = await redis.HashIncrementAsync($"chat:{chatId}:antitextlength",
                                    "maxlength", 500);
                                if (currentInc > maxValue)
                                    await redis.HashSetAsync($"chat:{chatId}:antitextlength", "maxlength",
                                        minValue.ToString());
                            }
                            else if (action == "dec")
                            {
                                var currentDec = await redis.HashIncrementAsync($"chat:{chatId}:antitextlength",
                                    "maxlength", -500);
                                if (currentDec < minValue)
                                    await redis.HashSetAsync($"chat:{chatId}:antitextlength", "maxlength",
                                        maxValue.ToString());
                            }
                        }
                        else if (key == "maxlines")
                        {
                            var minValue = 10;
                            var maxValue = 50;

                            if (action == "inc")
                            {
                                var currentInc = await redis.HashIncrementAsync($"chat:{chatId}:antitextlength",
                                    "maxlines", 10);
                                if (currentInc > maxValue)
                                    await redis.HashSetAsync($"chat:{chatId}:antitextlength", "maxlines",
                                        minValue.ToString());
                            }
                            else if (action == "dec")
                            {
                                var currentDec = await redis.HashIncrementAsync($"chat:{chatId}:antitextlength",
                                    "maxlines", -10);
                                if (currentDec < minValue)
                                    await redis.HashSetAsync($"chat:{chatId}:antitextlength", "maxlines",
                                        maxValue.ToString());
                            }
                        }
                    }

                    await Bot.ReplyToCallback(callback, Commands.GetLocaleString("antiTextMenu"),
                        replyMarkup: await Commands.GenerateAntiTextMenuAsync(chatId, redis));
                }
                else if (subMenu == "flood")
                {
                    if (action == "toggle")
                    {
                        var current = await redis.HashGetAsync($"chat:{chatId}:settings", "Flood");
                        var newStatus = GroupSettingsHelper.ToggleYesNo(current ?? "no");
                        await redis.HashSetAsync($"chat:{chatId}:settings", "Flood", newStatus);
                    }
                    else if (action == "cycle")
                    {
                        var current = await redis.HashGetAsync($"chat:{chatId}:flood", "ActionFlood");
                        var newAction = GroupSettingsHelper.GetNextAction(current ?? "kick");
                        await redis.HashSetAsync($"chat:{chatId}:flood", "ActionFlood", newAction);
                    }
                    else if (action is "inc" or "dec")
                    {
                        const int maxFlood = 30;
                        const int minFlood = 5;

                        if (action == "inc")
                        {
                            var currentInc = await redis.HashIncrementAsync($"chat:{chatId}:flood", "MaxFlood");
                            if (currentInc > maxFlood)
                                await redis.HashSetAsync($"chat:{chatId}:flood", "MaxFlood", minFlood.ToString());
                        }
                        else if (action == "dec")
                        {
                            var currentDec = await redis.HashIncrementAsync($"chat:{chatId}:flood", "MaxFlood", -1);
                            if (currentDec < minFlood)
                                await redis.HashSetAsync($"chat:{chatId}:flood", "MaxFlood", maxFlood.ToString());
                        }
                    }

                    await Bot.ReplyToCallback(callback, Commands.GetLocaleString("floodMenu"),
                        replyMarkup: await Commands.GenerateFloodMenuAsync(chatId, redis));
                }
            }
        }

        [CallBack(Trigger = "grp_page")]
        public static async Task GroupPageHandler(CallbackQuery callback, string[] args)
        {
            if (args.Length < 2 || !int.TryParse(args[1], out var page)) return;

            using var scope = Program.host.Services.CreateScope();
            var redis = ServiceHelper.GetService<IRedisHelper>(scope);
            var groups = await RegistrationHelper.GetAllGroups(redis);

            await Commands.SendGroupListEdit(callback.Message.Chat.Id, callback.Message.MessageId, redis, groups, page);
            await Bot.ReplyToCallback(callback, edit: false);
        }

        [CallBack(Trigger = "grp_sel")]
        public static async Task GroupSelectHandler(CallbackQuery callback, string[] args)
        {
            if (args.Length < 2 || !long.TryParse(args[1], out var groupId)) return;

            try
            {
                using var scope = Program.host.Services.CreateScope();
                var redis = ServiceHelper.GetService<IRedisHelper>(scope);

                var groupTitle = await RegistrationHelper.GetGroupTitle(redis, groupId);

                // Get member count
                int memberCount;
                try
                {
                    memberCount = await Bot.Api.GetChatMemberCount(groupId);
                }
                catch
                {
                    memberCount = 0;
                }

                // Get registrations and attendances
                var registrations = await RegistrationHelper.GetAllRegistrations(redis, groupId);
                var attendances = await RegistrationHelper.GetAllAttendances(redis, groupId);

                var registeredCount = registrations.Count;
                var unregisteredCount = Math.Max(0, memberCount - registeredCount);

                // Generate CSV
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("User ID,Telegram Name,Username,Real Name,Attendance,Group ID");

                foreach (var (userId, realName) in registrations)
                {
                    var userInfo = await RegistrationHelper.GetUserInfo(redis, long.Parse(userId));
                    var telegramName =
                        $"{userInfo.GetValueOrDefault("first_name", "Unknown")} {userInfo.GetValueOrDefault("last_name", "")}"
                            .Trim();
                    var username = userInfo.GetValueOrDefault("username", "None");
                    var attendance = attendances.GetValueOrDefault(userId, "Unknown");

                    csv.AppendLine($"{userId},{telegramName},{username},{realName},{attendance},{groupId}");
                }

                var csvBytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                var csvStream = new MemoryStream(csvBytes);

                var caption = Commands.GetLocaleString("csvCaption", groupTitle, memberCount.ToString(),
                    registeredCount.ToString(), unregisteredCount.ToString());

                await Bot.Api.SendDocument(
                    chatId: callback.Message.Chat.Id,
                    document: new InputFileStream(csvStream, $"{groupTitle}_Users.csv"),
                    caption: caption,
                    parseMode: ParseMode.Html
                );

                await Bot.ReplyToCallback(callback, edit: false);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to generate CSV report");
                await Bot.ReplyToCallback(callback, Commands.GetLocaleString("extraError", ex.Message),
                    showAlert: true);
            }
        }

        [CallBack(Trigger = "mod", GroupAdminOnly = true)]
        public static async Task ModerationCallback(CallbackQuery callback, string[] args)
        {
            if (args.Length < 3) return;

            var action = args[1];
            if (!long.TryParse(args[2], out var userId)) return;

            var chatId = callback.Message.Chat.Id;

            using var scope = Program.host.Services.CreateScope();
            var redis = ServiceHelper.GetService<IRedisHelper>(scope);

            // Get user info for message
            var userInfo = await RegistrationHelper.GetUserInfo(redis, userId);
            var userName = userInfo.GetValueOrDefault("first_name", userId.ToString());

            string message = "";

            try
            {
                if (action == "unban")
                {
                    await Bot.Api.UnbanChatMember(chatId, userId, onlyIfBanned: true);
                    message = Commands.GetLocaleString("userUnbanned", userName);
                }
                else if (action == "unmute")
                {
                    var permissions = new ChatPermissions
                    {
                        CanSendMessages = true,
                        CanSendAudios = true,
                        CanSendDocuments = true,
                        CanSendPhotos = true,
                        CanSendVideos = true,
                        CanSendVideoNotes = true,
                        CanSendVoiceNotes = true,
                        CanSendPolls = true,
                        CanSendOtherMessages = true,
                        CanAddWebPagePreviews = true
                    };
                    await Bot.Api.RestrictChatMember(chatId, userId, permissions);
                    message = Commands.GetLocaleString("userUnmuted", userName);
                }
                else if (action == "remwarn")
                {
                    var currentWarns = await redis.HashGetAsync($"chat:{chatId}:warns", userId.ToString());
                    if (!string.IsNullOrEmpty(currentWarns) && int.TryParse(currentWarns, out var w) && w > 0)
                    {
                        var warnCount = await redis.HashIncrementAsync($"chat:{chatId}:warns", userId.ToString(), -1);
                        if (warnCount <= 0)
                        {
                            await redis.HashDeleteAsync($"chat:{chatId}:warns", userId.ToString());
                        }

                        message = Commands.GetLocaleString("warnRemoved", userName);
                    }
                    else
                    {
                        await Bot.Api.AnswerCallbackQuery(callback.Id, "User has no warnings.", showAlert: true);
                        return;
                    }
                }

                if (!string.IsNullOrEmpty(message))
                {
                    var currentText = callback.Message.Text ?? callback.Message.Caption ?? "";

                    await Bot.Api.EditMessageText(
                        chatId: chatId,
                        messageId: callback.Message.MessageId,
                        text: currentText + "\n\n" + message,
                        parseMode: ParseMode.Html,
                        replyMarkup: null
                    );
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Moderation action failed");
                await Bot.ReplyToCallback(callback, "Action failed: " + ex.Message, showAlert: true);
            }
        }

        [CallBack(Trigger = "reg")]
        public static async Task RegistrationCallback(CallbackQuery callback, string[] args)
        {
            if (args.Length < 2) return;
            var answer = args[1];
            var userId = callback.From.Id;

            if (!Commands.UserStates.TryGetValue(userId, out var state) ||
                state.Step != Commands.RegistrationStep.AwaitingAttendance)
            {
                await Bot.Api.AnswerCallbackQuery(callback.Id, Commands.GetLocaleString("sessionExpired"),
                    showAlert: true);
                return;
            }

            try
            {
                using var scope = Program.host.Services.CreateScope();
                var redis = ServiceHelper.GetService<IRedisHelper>(scope);

                var answerText = answer switch
                {
                    "yes" => Commands.GetLocaleString("btnYes"),
                    "no" => Commands.GetLocaleString("btnNo"),
                    "maybe" => Commands.GetLocaleString("btnMaybe"),
                    _ => Commands.GetLocaleString("unknown")
                };

                // Pass answerText (attendance) to RegisterUser
                var success = await RegistrationHelper.RegisterUser(redis, state.GroupId, userId, state.Name,
                    callback.From, answerText);
                if (success)
                {
                    await Bot.Api.EditMessageText(
                        chatId: callback.Message.Chat.Id,
                        messageId: callback.Message.MessageId,
                        text: Commands.GetLocaleString("registrationCompleteWithAttendance", state.Name, answerText),
                        parseMode: ParseMode.Html
                    );
                }
                else
                {
                    await Bot.Api.AnswerCallbackQuery(callback.Id, Commands.GetLocaleString("registrationFailed"),
                        showAlert: true);
                }

                Commands.UserStates.Remove(userId);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Registration callback failed");
                await Bot.Api.AnswerCallbackQuery(callback.Id, Commands.GetLocaleString("errorOccurred"),
                    showAlert: true);
            }
        }
    }
}
