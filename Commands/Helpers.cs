using System.Xml.Linq;
using SiberVatan.Helpers;

namespace SiberVatan
{
    public static partial class Commands
    {
        public static string GetLocaleString(string key, params object[] args)
        {
            try
            {
                string language = "tr"; // Default language
                var files = Directory.GetFiles(Bot.LanguageDirectory, "*.yaml");
                var file = files.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == language);
                XDocument? doc = file != null ? LanguageHelper.Load(file) : Bot.English;

                var strings = doc?.Descendants("string").FirstOrDefault(x =>
                    string.Equals(x.Attribute("key")?.Value, key, StringComparison.OrdinalIgnoreCase));
                strings ??= Bot.English?.Descendants("string").FirstOrDefault(x =>
                    string.Equals(x.Attribute("key")?.Value, key, StringComparison.OrdinalIgnoreCase));

                if (strings == null)
                {
                    return $"Error: Localization key not found. Key: {key}";
                }

                var values = strings.Descendants("value").ToArray();
                if (values.Length == 0)
                {
                    return $"Error: Localization values not found. Key: {key}";
                }

                var choice = Bot.R.Next(values.Length);
                var selected = values[choice];

                try
                {
                    var formattedText = string.Format(selected.Value, args).Replace("\\n", Environment.NewLine);
                    return formattedText;
                }
                catch
                {
                    return $"Error: Argument mismatch for localization. Key: {key}";
                }
            }
            catch (Exception ex)
            {
                return $"Error: An unexpected error occurred. Details: {ex.Message}";
            }
        }
    }
}
