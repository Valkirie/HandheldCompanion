using Newtonsoft.Json;

namespace HandheldCompanion.Helpers
{
    public static class CloningHelper
    {
        public static T DeepClone<T>(T obj)
        {
            string jsonString = JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            return JsonConvert.DeserializeObject<T>(jsonString, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        }
    }
}
