using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace GreeAC.Library.Helpers
{
    public class EncryptedData
    {
        [JsonProperty("pack")]
        public string Pack { get; set; }

        [JsonProperty("tag")]
        public string Tag { get; set; }
    }

    public static class EncryptionHelper
    {
        private const string GenericKeyPrivate = "a3K8Bx%2r8Y7#xDh";
        private const string GenericGcmKey = "{yxAHAY_Lm6pbC/<";
        private static readonly byte[] GcmIv = new byte[]
        {
            0x54, 0x40, 0x78, 0x44, 0x49, 0x67, 0x5a, 0x51,
            0x6c, 0x5e, 0x63, 0x13
        };
        private static readonly byte[] GcmAad = Encoding.UTF8.GetBytes("qualcomm-test");

        // Public property for GenericKey
        public static string GenericKey => GenericKeyPrivate;

        // ECB Encryption Methods
        public static string EncryptEcb(string data, string key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                {
                    var dataBytes = Encoding.UTF8.GetBytes(data);
                    var encrypted = encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
                    return Convert.ToBase64String(encrypted);
                }
            }
        }

        public static string DecryptEcb(string encryptedData, string key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                {
                    var dataBytes = Convert.FromBase64String(encryptedData);
                    var decrypted = decryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
                    var result = Encoding.UTF8.GetString(decrypted);

                    // Find the last closing brace to remove padding
                    int lastBrace = result.LastIndexOf('}');
                    if (lastBrace >= 0)
                    {
                        result = result.Substring(0, lastBrace + 1);
                    }

                    return result;
                }
            }
        }

        public static string EncryptEcbGeneric(string data)
        {
            return EncryptEcb(data, GenericKeyPrivate);
        }

        public static string DecryptEcbGeneric(string encryptedData)
        {
            return DecryptEcb(encryptedData, GenericKeyPrivate);
        }

        // GCM Encryption Methods
        public static EncryptedData EncryptGcm(string data, string key)
        {
            using (var aes = new AesGcm(Encoding.UTF8.GetBytes(key)))
            {
                var plaintext = Encoding.UTF8.GetBytes(data);
                var ciphertext = new byte[plaintext.Length];
                var tag = new byte[16];

                aes.Encrypt(GcmIv, plaintext, ciphertext, tag, GcmAad);

                return new EncryptedData
                {
                    Pack = Convert.ToBase64String(ciphertext),
                    Tag = Convert.ToBase64String(tag)
                };
            }
        }

        public static string DecryptGcm(string encryptedPack, string tagString, string key)
        {
            using (var aes = new AesGcm(Encoding.UTF8.GetBytes(key)))
            {
                var ciphertext = Convert.FromBase64String(encryptedPack);
                var tag = Convert.FromBase64String(tagString);
                var plaintext = new byte[ciphertext.Length];

                aes.Decrypt(GcmIv, ciphertext, tag, plaintext, GcmAad);

                var result = Encoding.UTF8.GetString(plaintext);
                return result.Replace("\0", "").Replace("\xff", "");
            }
        }

        public static EncryptedData EncryptGcmGeneric(string data)
        {
            return EncryptGcm(data, GenericGcmKey);
        }

        public static string DecryptGcmGeneric(string encryptedPack, string tag)
        {
            return DecryptGcm(encryptedPack, tag, GenericGcmKey);
        }
    }
}