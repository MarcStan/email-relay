using System;
using System.IO;

namespace EmailRelay.Logic.Extensions
{
    public static class StreamExtensions
    {
        public static string ConvertToBase64(this Stream stream)
        {
            if (stream is MemoryStream ms)
            {
                ms.Position = 0;
                var bytes = ms.ToArray();
                return Convert.ToBase64String(bytes);
            }
            using (var s = new MemoryStream())
            {
                if (stream.CanSeek)
                    stream.Position = 0;

                stream.CopyTo(s);
                var bytes = s.ToArray();
                return Convert.ToBase64String(bytes);
            }
        }
    }
}
