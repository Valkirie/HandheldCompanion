using Newtonsoft.Json;

namespace HandheldCompanion.Helpers
{
    public static class CloningHelper
    {
        public static T DeepClone<T>(T obj)
        {
            JsonSerializerSettings settings = new()
            {
                TypeNameHandling = TypeNameHandling.All,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };

            string jsonString = JsonConvert.SerializeObject(obj, Formatting.Indented, settings);
            return JsonConvert.DeserializeObject<T>(jsonString, settings) ?? obj;
        }
    }
}
