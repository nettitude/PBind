using System;

internal static class Utils
{
    internal static byte[] CombineArrays(byte[] first, byte[] second)
    {
        var ret = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, ret, 0, first.Length);
        Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
        return ret;
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