using System.Xml.Linq;
using YamlDotNet.Serialization;

namespace SiberVatan.Helpers
{
    public class LanguageHelper
    {
        private static readonly Deserializer Deserializer = new();

        public static XDocument Load(string path)
        {
            using var reader = File.OpenText(path);
            var yamlObject = Deserializer.Deserialize<Dictionary<object, object>>(reader);

            var language = (Dictionary<object, object>)yamlObject["language"];
            var attArray = new[]
            {
                new XAttribute("base", (string)language["base"]),
                new XAttribute("variant", (string)language["variant"])
            };

            var root = new XElement("strings",
                new XElement("language", attArray)
            );

            if (yamlObject.TryGetValue("strings", out var stringsObj) &&
                stringsObj is Dictionary<object, object> stringsDict)
            {
                FlattenYaml(stringsDict, root);
            }

            return new XDocument(root);
        }

        private static void FlattenYaml(Dictionary<object, object> dict, XElement root)
        {
            foreach (var entry in dict)
            {
                string key = entry.Key.ToString()!;

                if (entry.Value is Dictionary<object, object> nestedDict)
                {
                    FlattenYaml(nestedDict, root);
                }
                else if (entry.Value is List<object> values)
                {
                    var stringElem = new XElement("string", new XAttribute("key", key));
                    foreach (var val in values)
                    {
                        stringElem.Add(new XElement("value", val.ToString()));
                    }
                    root.Add(stringElem);
                }
            }
        }
    }
}
