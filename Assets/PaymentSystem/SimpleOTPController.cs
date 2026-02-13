using System.Collections;
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;

/// <summary>
/// Простой контроллер OTP - просто включает/выключает весь Fading объект через Firebase
/// </summary>
public class SimpleOTPController : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private GameObject fadingObject; // Объект "Fading" который нужно включать/выключать
    
    [Header("Firebase Settings")]
    [SerializeField] private string firebaseDatabaseUrl = "https://comeback-2a6b2-default-rtdb.firebaseio.com";
    [SerializeField] private string otpSettingsPath = "otp_settings";
    
    [Header("Fallback")]
    [SerializeField] private bool defaultEnabled = true; // По умолчанию включен
    
    [Header("Debug")]
    [SerializeField] private bool enableLogs = true;
    
    private DatabaseReference settingsRef;
    private bool isFirebaseReady = false;
    
    // Singleton
    public static SimpleOTPController Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Автопоиск Fading объекта
        if (fadingObject == null)
        {
            fadingObject = GameObject.Find("Fading");
        }
        
        if (fadingObject == null)
        {
            Log("❌ Объект 'Fading' не найден!");
            return;
        }
        
        Log("✅ Fading объект найден: " + fadingObject.name);
        
        // Устанавливаем начальное состояние
        fadingObject.SetActive(defaultEnabled);
        
        // Инициализируем Firebase
        StartCoroutine(InitFirebase());
    }
    
    IEnumerator InitFirebase()
    {
        Log("🔥 Инициализация Firebase...");
        
        var task = FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => task.IsCompleted);
        
        if (task.Exception != null)
        {
            Log("❌ Ошибка Firebase: " + task.Exception.Message);
            yield break;
        }
        
        if (task.Result != DependencyStatus.Available)
        {
            Log("❌ Firebase недоступен: " + task.Result);
            yield break;
        }
        
        try
        {
            FirebaseDatabase database = FirebaseDatabase.GetInstance(firebaseDatabaseUrl);
            settingsRef = database.GetReference(otpSettingsPath);
            isFirebaseReady = true;
            Log("✅ Firebase готов!");
            
            // Подписываемся на изменения
            settingsRef.ValueChanged += OnSettingsChanged;
            
            // Загружаем начальные настройки
            LoadSettings();
        }
        catch (System.Exception e)
        {
            Log("❌ Ошибка инициализации: " + e.Message);
        }
    }
    
    void LoadSettings()
    {
        if (!isFirebaseReady) return;
        
        settingsRef.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Exception != null)
            {
                Log("❌ Ошибка загрузки: " + task.Exception.Message);
                return;
            }
            
            if (task.Result.Exists)
            {
                ProcessSettings(task.Result);
            }
            else
            {
                Log("📝 Настройки не найдены, создаем по умолчанию...");
                CreateDefaultSettings();
            }
        });
    }
    
    void OnSettingsChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.Snapshot.Exists)
        {
            Log("🔄 Получены новые настройки из Firebase");
            ProcessSettings(args.Snapshot);
        }
    }
    
    void ProcessSettings(DataSnapshot snapshot)
    {
        try
        {
            bool enabled = true; // По умолчанию включен
            
            if (snapshot.HasChild("enabled"))
            {
                string value = snapshot.Child("enabled").Value.ToString().ToLower();
                enabled = (value == "true" || value == "1");
            }
            
            Log($"📋 Настройка из Firebase: enabled = {enabled}");
            
            // Применяем настройку
            if (fadingObject != null)
            {
                fadingObject.SetActive(enabled);
                Log($"🔄 Fading объект: {(enabled ? "включен" : "выключен")}");
            }
        }
        catch (System.Exception e)
        {
            Log("❌ Ошибка обработки настроек: " + e.Message);
        }
    }
    
    void CreateDefaultSettings()
    {
        if (!isFirebaseReady) return;
        
        var settings = new System.Collections.Generic.Dictionary<string, object>
        {
            ["enabled"] = defaultEnabled,
            ["created_at"] = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
        
        settingsRef.SetValueAsync(settings).ContinueWithOnMainThread(task =>
        {
            if (task.Exception != null)
            {
                Log("❌ Ошибка создания настроек: " + task.Exception.Message);
            }
            else
            {
                Log("✅ Настройки по умолчанию созданы в Firebase");
            }
        });
    }
    
    void Log(string message)
    {
        if (enableLogs)
        {
            Debug.Log($"[SimpleOTPController] {message}");
        }
    }
    
    // Публичные методы
    
    /// <summary>
    /// Принудительно обновить настройки
    /// </summary>
    [ContextMenu("Refresh Settings")]
    public void RefreshSettings()
    {
        Log("🔄 Принудительное обновление...");
        LoadSettings();
    }
    
    /// <summary>
    /// Включить Fading локально
    /// </summary>
    [ContextMenu("Enable Fading")]
    public void EnableFading()
    {
        if (fadingObject != null)
        {
            fadingObject.SetActive(true);
            Log("✅ Fading включен локально");
        }
    }
    
    /// <summary>
    /// Выключить Fading локально
    /// </summary>
    [ContextMenu("Disable Fading")]
    public void DisableFading()
    {
        if (fadingObject != null)
        {
            fadingObject.SetActive(false);
            Log("❌ Fading выключен локально");
        }
    }
    
    /// <summary>
    /// Получить статус Firebase
    /// </summary>
    public bool IsFirebaseReady()
    {
        return isFirebaseReady;
    }
    
    /// <summary>
    /// Получить статус Fading объекта
    /// </summary>
    public bool IsFadingActive()
    {
        return fadingObject != null && fadingObject.activeInHierarchy;
    }
    
    void OnDestroy()
    {
        if (settingsRef != null)
        {
            settingsRef.ValueChanged -= OnSettingsChanged;
        }
    }
}
