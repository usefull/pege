namespace Pege.Extensions
{
    /// <summary>
    /// Методы расширения для <see cref="HttpContext"/>.
    /// </summary>
    internal static class HttpContextExtensions
    {
        /// <summary>
        /// Метод определения IP-адреса клиента.
        /// </summary>
        public static string? GetClientIp(this HttpContext context)
        {
            // Cloudflare
            if (context.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfIp)
                && !string.IsNullOrEmpty(cfIp))
                return cfIp;

            // X-Forwarded-For
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded)
                && !string.IsNullOrEmpty(forwarded))
            {
                var first = forwarded.ToString().Split(',')[0].Trim();
                if (!string.IsNullOrEmpty(first))
                    return first;
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }
}
