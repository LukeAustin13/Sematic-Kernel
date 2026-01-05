using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows.Media.Imaging;

namespace SematicKernelWpf.Model
{
    public static class ImageDecoder
    {
        public static async Task<byte[]> ImageStringToBytesAsync(string imageResult, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(imageResult))
                throw new ArgumentException("Image result was empty.");

            // Sometimes providers return a URL
            if (imageResult.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                using var http = new HttpClient();
                return await http.GetByteArrayAsync(imageResult, ct);
            }

            // Sometimes it’s a data URI: data:image/png;base64,....
            const string prefix = "base64,";
            var idx = imageResult.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            var b64 = idx >= 0 ? imageResult[(idx + prefix.Length)..] : imageResult;

            return Convert.FromBase64String(b64);
        }

        public static BitmapImage BytesToBitmapImage(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze(); // important for WPF threading/binding
            return bmp;
        }

    }
}
