using System.Text;

namespace Pege.Extensions
{
    internal static class StringExtensions
    {
        public static byte[] ToIcyMetadata(this string metaString)
        {
            byte[] metaBytes = Encoding.UTF8.GetBytes(metaString);

            // Математический расчет блоков по 16 байт
            int numChunks = (metaBytes.Length + 15) / 16;
            int totalMetaLength = numChunks * 16;

            // Быстрое выделение массива без предварительного зануления со стороны CLR
            byte[] result = GC.AllocateUninitializedArray<byte>(1 + totalMetaLength);
            result[0] = (byte)numChunks;

            // Быстрое копирование байт строки через Span на уровне процессора
            metaBytes.AsSpan().CopyTo(result.AsSpan(1));

            // Важно: заполняем нулями только оставшийся "хвост" массива, 
            // так как мы использовали неинициализированную память
            int bytesWritten = 1 + metaBytes.Length;
            int bytesLeft = result.Length - bytesWritten;
            if (bytesLeft > 0)
            {
                result.AsSpan(bytesWritten).Clear();
            }

            return result;
        }

        public static bool IsMultipartContentType(this string contentType) =>
            !string.IsNullOrEmpty(contentType) && contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Метод определения геоинформации по IP-адресу.
        /// </summary>
        /// <param name="ip">IP-адрес</param>
        public static async Task<string> GetGeoFromIp(this string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return string.Empty;

            var ipapiClient = new IPAPIClient.IPAPIClient();

            IPAPIClient.Result? result = null;
            try
            {
                result = await ipapiClient.SearchAsync(ip);
            }
            catch { }

            return string.Join(", ", new[] {
                string.IsNullOrWhiteSpace(result?.Country) ? null : result?.Country,
                string.IsNullOrWhiteSpace(result?.District) ? null : result?.District,
                string.IsNullOrWhiteSpace(result?.City) ? null : result?.City,
                string.IsNullOrWhiteSpace(result?.Organization) ? null : result?.Organization,
            }.Where(i => i != null));
        }
    }
}