using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoLib.Models;

namespace AutoLib.Core
{
    public static class ScriptSerializer
    {
        private static JsonSerializerOptions options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static void Save(AutomationScript script, string filePath)
        {
            string json = JsonSerializer.Serialize(script, options);
            File.WriteAllText(filePath, json);
        }

        public static AutomationScript Load(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<AutomationScript>(json, options);
        }
    }
}