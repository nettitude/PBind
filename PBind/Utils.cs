using System;
using System.IO;
using System.IO.Compression;

internal static class Utils
{
    internal static byte[] CombineArrays(byte[] first, byte[] second)
    {
        var combined = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, combined, 0, first.Length);
        Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);
        return combined;
    }
    
    internal static byte[] Compress(byte[] raw)
    {
        using (var memory = new MemoryStream())
        {
            using (var gzip = new GZipStream(memory, CompressionMode.Compress, true))
            {
                gzip.Write(raw, 0, raw.Length);
            }

            return memory.ToArray();
        }
    }

#if DEBUG
    internal static void TrimmedPrint(string message, string trimText, bool verbose = false)
    {
        if (trimText.Length > 200 && !verbose)
        {
            Console.WriteLine($"{message}\n{trimText.Substring(0, 200)}...");
        }
        else
        {
            Console.WriteLine($"{message}\n{trimText}");
        }
    }
#endif
}