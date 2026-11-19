using RSBot.Python.Models;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace RSBot.Python.API.Handler
{
    public static class PluginMetaReader
    {
        private static readonly Regex Assignment = new(
        "^(?<key>NAME|DESCRIPTION|AUTHOR|VERSION|GUI)\\s*=\\s*(?<value>.+?)\\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Header = new(
            "^\\s*#\\s*(?<key>Name|Description|Author|Version|GUI)\\s*:\\s*(?<value>.+?)\\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static PythonPluginInfo ReadPythonPluginInfo(string filePath)
        {
            string text = File.ReadAllText(filePath);
            string fileName = Path.GetFileName(filePath);
            string name = Path.GetFileNameWithoutExtension(filePath);
            string description = string.Empty;
            string author = "Unknown";
            string version = "—";
            bool hasGui = false;

            foreach (Match match in Assignment.Matches(text))
                Apply(match.Groups["key"].Value, CleanString(match.Groups["value"].Value), ref name, ref description, ref author, ref version, ref hasGui);

            foreach (Match match in Header.Matches(text))
                Apply(match.Groups["key"].Value, CleanString(match.Groups["value"].Value), ref name, ref description, ref author, ref version, ref hasGui);

            if (!hasGui)
                hasGui = Regex.IsMatch(text, @"\bOasisBot\s*\.\s*GUI\s*\(", RegexOptions.IgnoreCase) || Regex.IsMatch(text, @"\RSBot\s*\.\s*GUI\s*\(", RegexOptions.IgnoreCase);

            return new PythonPluginInfo
            {
                Id = GetNewId(fileName),
                FileName = fileName,
                FullPath = filePath,
                Name = name,
                Description = description,
                Author = author,
                Version = version,
                HasGui = hasGui
            };
        }

        public static string GetNewId(string fileName) =>
        "plugin_" + Regex.Replace(Path.GetFileNameWithoutExtension(fileName), "[^A-Za-z0-9_]", "_");

        private static string CleanString(string value)
        {
            value = value.Trim();
            if ((value.StartsWith("\"") && value.EndsWith("\"")) || (value.StartsWith("'") && value.EndsWith("'")))
                value = value[1..^1];
            return value.Trim();
        }

        private static void Apply(string key, string value, ref string name, ref string description, ref string author, ref string version, ref bool gui)
        {
            switch (key.Trim().ToUpperInvariant())
            {
                case "NAME": name = value; break;
                case "DESCRIPTION": description = value; break;
                case "AUTHOR": author = value; break;
                case "VERSION": version = value; break;
                case "GUI": gui = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase); break;
            }
        }
    }
}
