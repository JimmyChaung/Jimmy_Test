using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public class EncryptService
{
    private static readonly string storedSalt = "6B2fA9gG4E=";
    private static readonly string pwd_FAQ = "T2V8lQ+KN8TPcV2Q88UJNWeLDahsajZrrFDBuuutFxg=";
    private static readonly string tool_key = "A7F2D849C31B05EC4D29E3F8156AB970";
    private static string HashPasswordWithSalt(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] saltedPassword = Encoding.UTF8.GetBytes(password + storedSalt);
            byte[] hashBytes = sha256.ComputeHash(saltedPassword);
            return Convert.ToBase64String(hashBytes);
        }
    }
    public static bool FAQ(string password)
    {
        string hashedPassword = HashPasswordWithSalt(password);
        return hashedPassword == pwd_FAQ;
    }

    // 工具帳號用解編碼
    public static string Encrypt_Tool_Account(string plainText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(tool_key);
            aes.GenerateIV();
            ICryptoTransform encryptor = aes.CreateEncryptor();

            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(aes.IV, 0, aes.IV.Length);
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (StreamWriter writer = new StreamWriter(cs))
                {
                    writer.Write(plainText);
                }
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }
    public static string Decrypt_Tool_Account(string encryptedBase64)
    {
        byte[] fullCipher = Convert.FromBase64String(encryptedBase64);
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(tool_key);
            byte[] iv = new byte[16];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aes.IV = iv;

            ICryptoTransform decryptor = aes.CreateDecryptor();
            using (MemoryStream ms = new MemoryStream(fullCipher, 16, fullCipher.Length - 16))
            using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (StreamReader reader = new StreamReader(cs))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
