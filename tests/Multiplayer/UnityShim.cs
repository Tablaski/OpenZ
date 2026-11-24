// Test harness only. The game uses Unity's own Team and JsonUtility implementations.
namespace OpenZ { public enum Team { Neutral = 0, Red = 1, Blue = 2, Green = 3, Yellow = 4, Purple = 5, Teal = 6, White = 7, Black = 8 } }
namespace UnityEngine
{
    public static class JsonUtility
    {
        public static string ToJson(object value, bool prettyPrint = false) => System.Text.Json.JsonSerializer.Serialize(value,
            new System.Text.Json.JsonSerializerOptions { IncludeFields = true, WriteIndented = prettyPrint });
        public static T FromJson<T>(string json)
        {
            try { return System.Text.Json.JsonSerializer.Deserialize<T>(json, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }); }
            catch (System.Text.Json.JsonException error) { throw new System.ArgumentException("Invalid JSON.", error); }
        }
    }
}
