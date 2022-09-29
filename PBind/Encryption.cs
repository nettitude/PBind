using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal static class Encryption
{
    internal static string Encrypt(string key, string un, bool comp = false, byte[] unByte = null)
    {
        var byEnc = unByte ?? Encoding.UTF8.GetBytes(un);

        if (comp)
            byEnc = GzipCompress(byEnc);

        try
        {
            var a = CreateEncryptionAlgorithm(key, null);
            var f = a.CreateEncryptor().TransformFinalBlock(byEnc, 0, byEnc.Length);
            return Convert.ToBase64String(Utils.CombineArrays(a.IV, f));
        }
        catch
        {
            var a = CreateEncryptionAlgorithm(key, null, false);
            var f = a.CreateEncryptor().TransformFinalBlock(byEnc, 0, byEnc.Length);
            return Convert.ToBase64String(Utils.CombineArrays(a.IV, f));
        }
    }

    internal static string Decrypt(string key, string ciphertext)
    {
        var rawCipherText = Convert.FromBase64String(ciphertext);
        var iv = new byte[16];
        Array.Copy(rawCipherText, iv, 16);
        try
        {
            var algorithm = Encryption.CreateEncryptionAlgorithm(key, Convert.ToBase64String(iv));
            var decrypted = algorithm.CreateDecryptor().TransformFinalBlock(rawCipherText, 16, rawCipherText.Length - 16);
            return Encoding.UTF8.GetString(decrypted.Where(x => x > 0).ToArray());
        }
        catch
        {
            var algorithm = Encryption.CreateEncryptionAlgorithm(key, Convert.ToBase64String(iv), false);
            var decrypted = algorithm.CreateDecryptor().TransformFinalBlock(rawCipherText, 16, rawCipherText.Length - 16);
            return Encoding.UTF8.GetString(decrypted.Where(x => x > 0).ToArray());
        }
        finally
        {
            Array.Clear(rawCipherText, 0, rawCipherText.Length);
            Array.Clear(iv, 0, 16);
        }
    }

    private static SymmetricAlgorithm CreateEncryptionAlgorithm(string key, string iv, bool rij = true)
    {
        SymmetricAlgorithm algorithm;
        if (rij)
            algorithm = new RijndaelManaged();
        else
            algorithm = new AesCryptoServiceProvider();

        algorithm.Mode = CipherMode.CBC;
        algorithm.Padding = PaddingMode.Zeros;
        algorithm.BlockSize = 128;
        algorithm.KeySize = 256;

        if (null != iv)
            algorithm.IV = Convert.FromBase64String(iv);
        else
            algorithm.GenerateIV();

        if (null != key)
            algorithm.Key = Convert.FromBase64String(key);

        return algorithm;
    }

    private static byte[] GzipCompress(byte[] raw)
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
}