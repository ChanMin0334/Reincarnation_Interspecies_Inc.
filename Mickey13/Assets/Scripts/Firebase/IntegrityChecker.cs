using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// 세이브 데이터 무결성 검증
/// MD5 체크섬 및 게임 로직 검증
/// </summary>
public class IntegrityChecker : Singleton<IntegrityChecker>
{
    private const string SecretSaltEnvVar = "MICKEY13_SECRET_SALT";
    [SerializeField] private string fallbackSecretSalt = string.Empty;

    private string secretSalt;

    /// <summary>
    /// 체크섬 생성
    /// </summary>
    public string GenerateChecksum(string data)
    {
        if (string.IsNullOrEmpty(data))
            return string.Empty;

        try
        {
            string saltedData = data + GetSecretSalt();
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(saltedData));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Integrity] 체크섬 생성 실패: {ex.Message}");
            return string.Empty;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        secretSalt = LoadSecretSalt();
        if (string.IsNullOrEmpty(secretSalt))
        {
            Debug.LogWarning("[Integrity] Secret salt not configured. Set MICKEY13_SECRET_SALT before shipping builds.");
        }
    }

    private string LoadSecretSalt()
    {
        string envSalt = Environment.GetEnvironmentVariable(SecretSaltEnvVar);
        if (!string.IsNullOrEmpty(envSalt))
        {
            return envSalt;
        }
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(fallbackSecretSalt))
        {
            return fallbackSecretSalt;
        }
#endif
        return string.Empty;
    }

    private string GetSecretSalt()
    {
        if (string.IsNullOrEmpty(secretSalt))
        {
            secretSalt = LoadSecretSalt();
        }

        return secretSalt ?? string.Empty;
    }

    /// <summary>
    /// 체크섬 검증
    /// </summary>
    public bool VerifyChecksum(string data, string checksum)
    {
        if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(checksum))
            return false;

        string calculatedChecksum = GenerateChecksum(data);
        bool isValid = calculatedChecksum.Equals(checksum, StringComparison.OrdinalIgnoreCase);

        if (!isValid)
        {
            Debug.LogWarning("[Integrity] 체크섬 불일치!");
        }

        return isValid;
    }

    /// <summary>
    /// 게임 로직 무결성 검증
    /// </summary>
    public bool ValidateGameLogic(string saveJson)
    {
        if (string.IsNullOrEmpty(saveJson))
            return false;

        try
        {
            if (!JsonSaveSystem.TryDeserialize(saveJson, out var data) || data == null)
            {
                LogValidationSnapshot(null, "세이브 데이터 파싱 실패");
                return false;
            }

            // 재화는 게임 흐름상 0 이하도 허용한다.
            BigNumeric goldValue = data.goldSave != null ? data.goldSave.value : 0;
            BigNumeric soulstoneValue = data.soulstoneSave != null ? data.soulstoneSave.value : 0;
            long diamondValue = data.diamond;

            if (data.StageLevel < 1 || data.StageLevel > 10000)
            {
                LogValidationSnapshot(data, $"비정상 스테이지 레벨 감지: {data.StageLevel}");
                return false;
            }

            if (data.nextBossKm < 0 || data.nextBossKm > 1000000)
            {
                LogValidationSnapshot(data, $"비정상 보스 위치 감지: {data.nextBossKm}");
                return false;
            }

            if (data.nextMidBossKm < 0)
            {
                LogValidationSnapshot(data, $"비정상 중간보스 위치 감지(음수): {data.nextMidBossKm}");
                return false;
            }

            int maxListCount = 500;

            if (data.charSaveDatas != null && data.charSaveDatas.Count > maxListCount)
            {
                LogValidationSnapshot(data, $"비정상 캐릭터 저장 개수 감지: {data.charSaveDatas?.Count ?? 0}");
                return false;
            }

            if (data.artifactSaveDatas != null && data.artifactSaveDatas.Count > maxListCount)
            {
                LogValidationSnapshot(data, $"비정상 유물 저장 개수 감지: {data.artifactSaveDatas?.Count ?? 0}");
                return false;
            }

            if (data.runeSaveDatas != null && data.runeSaveDatas.Count > maxListCount)
            {
                LogValidationSnapshot(data, $"비정상 룬 저장 개수 감지: {data.runeSaveDatas?.Count ?? 0}");
                return false;
            }

            if (data.questSaveDatas != null && data.questSaveDatas.Count > maxListCount)
            {
                LogValidationSnapshot(data, $"비정상 퀘스트 저장 개수 감지: {data.questSaveDatas?.Count ?? 0}");
                return false;
            }

            if (data.activeQuestSaveDatas != null && data.activeQuestSaveDatas.Count > maxListCount)
            {
                LogValidationSnapshot(data, $"비정상 진행중 퀘스트 개수 감지: {data.activeQuestSaveDatas?.Count ?? 0}");
                return false;
            }

            if (data.maxAchievementKmEver < data.curAchievementKm)
            {
                LogValidationSnapshot(data, $"누적 이동거리보다 현재 이동거리가 큽니다: {data.curAchievementKm}/{data.maxAchievementKmEver}");
                return false;
            }

            Debug.Log("[Integrity] 게임 로직 검증 통과");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Integrity] 게임 로직 검증 실패: {ex.Message}");
            return false;
        }
    }

    private void LogValidationSnapshot(UserSaveData data, string reason)
    {
        if (data == null)
        {
            Debug.LogWarning($"[Integrity] {reason} (데이터 null)");
            return;
        }

        int charCount = data.charSaveDatas?.Count ?? 0;
        int artifactCount = data.artifactSaveDatas?.Count ?? 0;
        int runeCount = data.runeSaveDatas?.Count ?? 0;
        int questCount = data.questSaveDatas?.Count ?? 0;
        int activeQuestCount = data.activeQuestSaveDatas?.Count ?? 0;

        string snapshot =
            $"골드:{data.goldSave?.value ?? 0} 환생석:{data.soulstoneSave?.value ?? 0} 다이아:{data.diamond} " +
            $"Stage:{data.StageLevel} curKm:{data.curAchievementKm} maxKm:{data.maxAchievementKmEver} " +
            $"nextMid:{data.nextMidBossKm} nextBoss:{data.nextBossKm} " +
            $"캐릭:{charCount} 유물:{artifactCount} 룬:{runeCount} 퀘:{questCount} 진행퀘:{activeQuestCount}";

        Debug.LogWarning($"[Integrity] {reason}\n[Integrity] Snapshot => {snapshot}");
    }

    /// <summary>
    /// 전체 무결성 검증 (체크섬 + 게임 로직)
    /// </summary>
    public bool FullValidation(string saveJson, string checksum)
    {
        if (!VerifyChecksum(saveJson, checksum))
        {
            Debug.LogError("[Integrity] 체크섬 검증 실패");
            return false;
        }

        if (!ValidateGameLogic(saveJson))
        {
            Debug.LogError("[Integrity] 게임 로직 검증 실패");
            return false;
        }

        Debug.Log("[Integrity] 전체 무결성 검증 통과");
        return true;
    }
}
