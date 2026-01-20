using Telegram.Bot.Types.ReplyMarkups;
using System.Text.RegularExpressions;

namespace SiberVatan.Helpers
{
    public static class MessageHelper
    {
        /// <summary>
        /// Smart HTML escape - preserves valid HTML tags, escapes standalone special characters
        /// </summary>
        public static string SmartHtmlEscape(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Valid Telegram HTML tags
            var validTags = new[]
            {
                "b", "strong", "i", "em", "u", "ins", "s", "strike", "del",
                "code", "pre", "a", "tg-spoiler", "tg-emoji",
                "blockquote", "span"
            };

            text = Regex.Replace(text, @"&(?!(?:amp|lt|gt|quot|#\d+|#x[0-9a-fA-F]+);)", "&amp;");
            var tagPattern = $@"</?(?:{string.Join("|", validTags)})(?:\s[^>]*)?>";
            var parts = new List<string>();
            var lastIndex = 0;

            foreach (Match match in Regex.Matches(text, tagPattern))
            {
                if (match.Index > lastIndex)
                {
                    var beforeTag = text[lastIndex..match.Index];
                    beforeTag = beforeTag.Replace("<", "&lt;").Replace(">", "&gt;");
                    parts.Add(beforeTag);
                }

                parts.Add(match.Value);
                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                var afterTag = text[lastIndex..];
                afterTag = afterTag.Replace("<", "&lt;").Replace(">", "&gt;");
                parts.Add(afterTag);
            }

            return string.Join("", parts);
        }

        /// <summary>
        /// Parses buttons from plain text
        /// Button syntax: {[Label](URL), [Label2](URL2)}
        /// </summary>
        public static (string textWithoutButtons, InlineKeyboardMarkup? replyMarkup) ParseButtonsFromPlainText(
            string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return (string.Empty, null);

            var finalText = plainText;
            var buttonsLayout = new List<List<InlineKeyboardButton>>();

            var patternBlock = @"\{(.+?)\}";
            var foundBlocks = Regex.Matches(finalText, patternBlock, RegexOptions.Singleline);
            finalText = Regex.Replace(finalText, patternBlock, "").Trim();

            foreach (Match blockMatch in foundBlocks)
            {
                var blockContent = blockMatch.Groups[1].Value;
                var row = new List<InlineKeyboardButton>();

                var items = Regex.Matches(blockContent, @"\[(.+?)\]\((.+?)\)");
                foreach (Match item in items)
                {
                    var name = item.Groups[1].Value.Trim();
                    var url = item.Groups[2].Value.Trim();

                    row.Add(InlineKeyboardButton.WithUrl(name, url));
                }

                if (row.Count > 0)
                    buttonsLayout.Add(row);
            }

            var replyMarkup = buttonsLayout.Count > 0 ? new InlineKeyboardMarkup(buttonsLayout) : null;

            // Smart escape: preserve HTML tags, escape standalone < > &
            finalText = SmartHtmlEscape(finalText);
            return (finalText, replyMarkup);
        }

        public static (List<string> targets, string? rawContent) ParseTargetsAndContent(string[] args)
        {
            var targets = new List<string>();
            string? rawContent = null;

            if (args.Length < 2 || string.IsNullOrEmpty(args[1]))
                return (targets, rawContent);

            var fullText = args[1].Trim();
            var spaceIndex = fullText.IndexOf(' ');

            if (spaceIndex > 0)
            {
                var targetsRaw = fullText[..spaceIndex];
                rawContent = fullText[(spaceIndex + 1)..].Trim();
                targets = targetsRaw.Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();
            }
            else
            {
                targets = fullText.Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();
            }

            return (targets, rawContent);
        }

        /// <summary>
        /// Format welcome message with user and group placeholders
        /// </summary>
        public static string FormatWelcomeMessage(string message, Telegram.Bot.Types.User user,
            Telegram.Bot.Types.Chat chat)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            // Create mention link for user
            var userName = user.FirstName;
            if (!string.IsNullOrEmpty(user.LastName))
                userName += " " + user.LastName;

            // HTML escape name and title
            userName = SmartHtmlEscape(userName);
            var groupTitle = SmartHtmlEscape(chat.Title ?? "");

            // Create HTML mention link
            var mentionLink = $"<a href=\"tg://user?id={user.Id}\">{userName}</a>";

            // Replace placeholders
            message = message.Replace("$name", mentionLink);
            message = message.Replace("$username", user.Username ?? "");
            message = message.Replace("$id", user.Id.ToString());
            message = message.Replace("$language", user.LanguageCode ?? "");
            message = message.Replace("$title", groupTitle);

            return message;
        }
    }
}
