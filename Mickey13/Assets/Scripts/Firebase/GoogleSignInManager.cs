using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Google 로그인 관리자 (Android Native)
/// </summary>
public class GoogleSignInManager : Singleton<GoogleSignInManager>
{
    private const string WebClientIdEnvVar = "MICKEY13_GOOGLE_WEB_CLIENT_ID";
    [SerializeField] private string fallbackWebClientId = string.Empty;

    private string webClientId;
    
    public bool IsInitialized { get; private set; } = true;
    public bool IsSignedIn => FirebaseAuthManager.Instance.IsSignedIn;

    public event Action<string> SignInFailed;

    protected override void Awake()
    {
        base.Awake();
        webClientId = LoadWebClientId();
        if (string.IsNullOrEmpty(webClientId))
        {
            Debug.LogWarning("[Google Sign-In] Web client ID not configured. Set MICKEY13_GOOGLE_WEB_CLIENT_ID before shipping builds.");
        }
        Debug.Log("[Google Sign-In] Android Native Google Sign-In 초기화");
    }

    /// <summary>
    /// Google 로그인 (Android Native)
    /// </summary>
    public void SignInWithGoogle()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("[Google Sign-In] Android Google 로그인 시도...");
        string clientId = GetWebClientId();
        if (string.IsNullOrEmpty(clientId))
        {
            Debug.LogError("[Google Sign-In] Google Web Client ID 누락. 로그인 요청을 중단합니다.");
            return;
        }
        AndroidJavaClass jc = new AndroidJavaClass("com.Mickey_13.GoogleSignInHelper");
        jc.CallStatic("SignIn", clientId);
#else
        Debug.LogWarning("[Google Sign-In] Unity Editor에서는 익명 로그인으로 대체");
        _ = SignInAnonymously();
#endif
    }

    /// <summary>
    /// Google 로그인 성공 콜백 (Java에서 호출)
    /// </summary>
    public async void OnGoogleSignInSuccess(string idToken)
    {
        Debug.Log("[Google Sign-In] ID Token 수신 성공!");
        
        bool success = await FirebaseAuthManager.Instance.SignInWithGoogle(idToken, null);
        
        if (success)
        {
            Debug.Log("[Google Sign-In] ✅ Firebase 연동 성공!");
        }
        else
        {
            Debug.LogError("[Google Sign-In] ❌ Firebase 연동 실패");
            SignInFailed?.Invoke("Firebase sign-in failed");
        }
    }

    /// <summary>
    /// Google 로그인 실패 콜백 (Java에서 호출)
    /// </summary>
    public void OnGoogleSignInFailed(string error)
    {
        Debug.LogError($"[Google Sign-In] ❌ Google 로그인 실패: {error}");
        SignInFailed?.Invoke(error);
    }

    /// <summary>
    /// Editor용 익명 로그인
    /// </summary>
    private async Task SignInAnonymously()
    {
        bool success = await FirebaseAuthManager.Instance.SignInAnonymously();
        
        if (success)
        {
            Debug.Log("[Google Sign-In] ✅ 익명 로그인 성공");
            await SaveSyncManager.Instance.InitializeCloudSave();
        }
        else
        {
            Debug.LogError("[Google Sign-In] ❌ 익명 로그인 실패");
        }
    }

    /// <summary>
    /// 로그아웃
    /// </summary>
    public void SignOut()
    {
        FirebaseAuthManager.Instance.SignOut();
        Debug.Log("[Google Sign-In] 로그아웃 완료");
    }

    private string LoadWebClientId()
    {
        string envValue = Environment.GetEnvironmentVariable(WebClientIdEnvVar);
        if (!string.IsNullOrEmpty(envValue))
        {
            return envValue;
        }
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(fallbackWebClientId))
        {
            return fallbackWebClientId;
        }
#endif
        return string.Empty;
    }

    private string GetWebClientId()
    {
        if (string.IsNullOrEmpty(webClientId))
        {
            webClientId = LoadWebClientId();
        }

        return webClientId;
    }
}
