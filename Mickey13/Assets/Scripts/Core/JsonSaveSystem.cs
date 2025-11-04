using UnityEngine;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System;


public class JsonSaveSystem
{
    private static readonly string savePath = Path.Combine(Application.persistentDataPath, "user.json");
    private const string EncryptionKeyEnvVar = "MICKEY13_SAVE_ENCRYPTION_KEY";

    private static string cachedEncryptionKey;

    public static string SerializeToJson(UserSaveData data, bool prettyPrint = false)
    {
        return JsonUtility.ToJson(data, prettyPrint);
    }

    public static bool TryDeserialize(string json, out UserSaveData data)
    {
        data = null;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            data = JsonUtility.FromJson<UserSaveData>(json);
            return data != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"세이브 JSON 파싱 실패: {ex.Message}");
            return false;
        }
    }

    public static bool TryDecryptToJson(string encrypted, out string json)
    {
        json = null;

        if (string.IsNullOrEmpty(encrypted))
            return false;

        try
        {
            json = Decrypt(encrypted);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"세이브 복호화 실패: {ex.Message}");
            return false;
        }
    }

    public static string EncryptToString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        if (!HasEncryptionKey())
        {
            Debug.LogWarning("[JsonSaveSystem] Encryption key not configured. 평문 데이터를 반환합니다.");
            return plainText;
        }

        return Encrypt(plainText);
    }

    public static void Save(UserSaveData data)
    {
        string json = SerializeToJson(data, true);

        bool useEncryption = true;
        var userInstance = Singleton<User>.InstanceIfInitialized;
        if (userInstance != null && userInstance.debugmode)
        {
            useEncryption = false;
        }

        bool canEncrypt = HasEncryptionKey();

        if (useEncryption && canEncrypt)
        {
            string encryptedJson = Encrypt(json);
            File.WriteAllText(savePath, encryptedJson);
            Debug.Log($"세이브 완료 (암호화됨): {savePath}");
        }
        else
        {
            if (useEncryption && !canEncrypt)
            {
                Debug.LogWarning("[JsonSaveSystem] Encryption key not configured. 데이터가 평문으로 저장됩니다.");
            }
            File.WriteAllText(savePath, json);
            Debug.Log($"세이브 완료 (평문): {savePath}");
        }
    }

    public static UserSaveData Load()
    {
        if (!File.Exists(savePath))
        {
            Debug.Log("세이브파일이 없습니다. 새로 생성합니다.");
            return new UserSaveData();
        }

        string fileContent = File.ReadAllText(savePath);
        
        // 먼저 평문 JSON으로 파싱 시도
        try
        {
            UserSaveData data = JsonUtility.FromJson<UserSaveData>(fileContent);
            // null 체크로 유효성 검증
            if (data != null && !string.IsNullOrEmpty(data.goldSave.ToString()))
            {
                Debug.Log("세이브 로드 완료 (평문)");
                return data;
            }
        }
        catch (Exception)
        {
            // 평문으로 파싱 실패 시 복호화 시도
        }
        
        // 복호화 시도
        if (!HasEncryptionKey())
        {
            Debug.LogError("세이브 파일을 복호화할 암호화 키가 설정되지 않았습니다.");
            return new UserSaveData();
        }

        try
        {
            string json = Decrypt(fileContent);
            UserSaveData data = JsonUtility.FromJson<UserSaveData>(json);
            Debug.Log("세이브 로드 완료 (복호화)");
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"세이브 파일 로드 실패: {e.Message}");
            return new UserSaveData();
        }
    }
    
    private static string Encrypt(string plainText)
    {
        using (DESCryptoServiceProvider des = new DESCryptoServiceProvider())
        {
            des.Key = GetEncryptionKeyBytes(true);
            des.IV = GetEncryptionKeyBytes(false);
            des.Mode = CipherMode.CBC;
            des.Padding = PaddingMode.PKCS7;
            
            using (ICryptoTransform encryptor = des.CreateEncryptor())
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                return Convert.ToBase64String(encryptedBytes);
            }
        }
    }
    
    private static string Decrypt(string encryptedText)
    {
        if (!HasEncryptionKey())
        {
            throw new InvalidOperationException("Encryption key not configured");
        }
        
        using (DESCryptoServiceProvider des = new DESCryptoServiceProvider())
        {
            des.Key = GetEncryptionKeyBytes(true);
            des.IV = GetEncryptionKeyBytes(false);
            des.Mode = CipherMode.CBC;
            des.Padding = PaddingMode.PKCS7;
            
            using (ICryptoTransform decryptor = des.CreateDecryptor())
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
        }
    }

    private static byte[] GetEncryptionKeyBytes(bool forKey)
    {
        string keyString = GetEncryptionKey();
        if (string.IsNullOrEmpty(keyString) || keyString.Length < 16)
        {
            throw new InvalidOperationException("Encryption key must be at least 16 characters.");
        }

        string segment = forKey ? keyString.Substring(0, 8) : keyString.Substring(8, 8);
        return Encoding.UTF8.GetBytes(segment);
    }

    private static string GetEncryptionKey()
    {
        if (!string.IsNullOrEmpty(cachedEncryptionKey))
        {
            return cachedEncryptionKey;
        }

        string envKey = Environment.GetEnvironmentVariable(EncryptionKeyEnvVar);
        if (!string.IsNullOrEmpty(envKey))
        {
            cachedEncryptionKey = envKey.Trim();
            return cachedEncryptionKey;
        }

        cachedEncryptionKey = string.Empty;
        return cachedEncryptionKey;
    }

    private static bool HasEncryptionKey()
    {
        string key = GetEncryptionKey();
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (key.Length < 16)
        {
            Debug.LogError("[JsonSaveSystem] Encryption key must be at least 16 characters.");
            return false;
        }

        return true;
    }
}
