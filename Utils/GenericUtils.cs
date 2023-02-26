using System.Security.Cryptography;
using System.Text;

namespace MultiplayerARPG
{
    public static class GenericUtils
    {
        public static string GetUniqueId(int length = 16, string mask = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890_-")
        {
            return Nanoid.Nanoid.Generate(mask, length);
        }

        public static string GetMD5(this string text)
        {
            // byte array representation of that string
            byte[] encodedPassword = new UTF8Encoding().GetBytes(text);

            // need MD5 to calculate the hash
            byte[] hash = ((HashAlgorithm)CryptoConfig.CreateFromName("MD5")).ComputeHash(encodedPassword);

            // string representation (similar to UNIX format)
            return System.BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
        }
    }
}
