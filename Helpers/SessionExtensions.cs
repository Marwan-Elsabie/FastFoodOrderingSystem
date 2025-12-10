using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace FastFoodOrderingSystem.Helpers
{
    public static class SessionExtensions
    {
        public static void SetObject<T>(this ISession session, string key, T value)
        {
            if (value == null)
            {
                session.Remove(key);
                return;
            }

            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static T? GetObject<T>(this ISession session, string key)
        {
            var data = session.GetString(key);
            if (string.IsNullOrEmpty(data))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(data);
            }
            catch
            {
                return default;
            }
        }
    }
}