using UnityEngine;
using Firebase;
using Firebase.Database;
using System;
using System.Collections;

/// <summary>
/// Manages subscription settings from Firebase
/// Gets price and duration from Django admin panel
/// </summary>
public class SubscriptionManager : MonoBehaviour
{
    [Header("Subscription Settings")]
    public float subscriptionPrice = 5000f;
    public int subscriptionDurationMinutes = 30;
    public string currency = "UZS";
    public bool isSubscriptionActive = true;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Events
    public static event Action<float, int, string> OnSubscriptionSettingsLoaded;
    public static event Action OnSubscriptionSettingsError;
    
    // Singleton
    public static SubscriptionManager Instance { get; private set; }
    
    private DatabaseReference subscriptionRef;
    
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        StartCoroutine(InitializeFirebaseAndLoad());
    }
    
    private System.Collections.IEnumerator InitializeFirebaseAndLoad()
    {
        Debug.Log("[SubscriptionManager] Инициализация Firebase...");
        
        // Ждем инициализации Firebase
        var dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => dependencyTask.IsCompleted);
        
        if (dependencyTask.Result == DependencyStatus.Available)
        {
            Debug.Log("[SubscriptionManager] Firebase успешно инициализирован!");
            
            // Инициализируем Firebase с правильным URL
            var options = FirebaseApp.DefaultInstance.Options;
            Debug.Log($"[SubscriptionManager] Database URL: {options.DatabaseUrl}");
            
            LoadSubscriptionSettings();
        }
        else
        {
            Debug.LogError("[SubscriptionManager] Не удалось инициализировать Firebase: " + dependencyTask.Result);
            OnSubscriptionSettingsError?.Invoke();
        }
    }
    
    /// <summary>
    /// Обработчик изменений в реальном времени
    /// </summary>
    private void HandleValueChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError("[SubscriptionManager] ❌ Ошибка при получении обновлений: " + args.DatabaseError.Message);
            return;
        }

        Debug.Log("[SubscriptionManager] 🔄 ПОЛУЧЕНО ОБНОВЛЕНИЕ ИЗ FIREBASE!");
        ProcessFirebaseData(args.Snapshot);
    }

    /// <summary>
    /// Обработка данных из Firebase
    /// </summary>
    private void ProcessFirebaseData(DataSnapshot snapshot)
    {
        if (snapshot.Exists)
        {
            try
            {
                var priceValue = snapshot.Child("price").Value;
                var durationValue = snapshot.Child("duration_minutes").Value;
                var currencyValue = snapshot.Child("currency").Value;
                var activeValue = snapshot.Child("is_active").Value;

                if (priceValue != null && durationValue != null)
                {
                    subscriptionPrice = Convert.ToSingle(priceValue);
                    subscriptionDurationMinutes = Convert.ToInt32(durationValue);
                    currency = currencyValue?.ToString() ?? "UZS";
                    isSubscriptionActive = activeValue != null ? Convert.ToBoolean(activeValue) : true;

                    Debug.Log("[SubscriptionManager] 🔄 Settings updated from Firebase:");
                    Debug.Log($"  Price: {subscriptionPrice} {currency}");
                    Debug.Log($"  Duration: {subscriptionDurationMinutes} minutes");
                    Debug.Log($"  Active: {isSubscriptionActive}");

                    // Уведомляем подписчиков об обновлении
                    OnSubscriptionSettingsLoaded?.Invoke(subscriptionPrice, subscriptionDurationMinutes, currency);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[SubscriptionManager] ❌ Ошибка обработки данных: " + ex.Message);
                OnSubscriptionSettingsError?.Invoke();
            }
        }
        else
        {
            Debug.LogWarning("[SubscriptionManager] ⚠️ Данные подписки не найдены в Firebase");
            OnSubscriptionSettingsError?.Invoke();
        }
    }

    [ContextMenu("Force Reload from Firebase")]
    public void ForceReloadFromFirebase()
    {
        Debug.Log("[SubscriptionManager] 🔧 ПРИНУДИТЕЛЬНАЯ ПЕРЕЗАГРУЗКА ИЗ FIREBASE!");
        LoadSubscriptionSettings();
    }
    
    /// <summary>
    /// Load subscription settings from Firebase
    /// </summary>
    public void LoadSubscriptionSettings()
    {
        if (debugMode)
            Debug.Log("[SubscriptionManager] Loading subscription settings from Firebase...");
        
        try
        {
            // Check Firebase initialization
            if (FirebaseApp.DefaultInstance == null)
            {
                Debug.LogError("[SubscriptionManager] Firebase не инициализирован!");
                OnSubscriptionSettingsError?.Invoke();
                return;
            }
            
            Debug.Log("[SubscriptionManager] Firebase App инициализирован: " + FirebaseApp.DefaultInstance.Name);
            
            // Get Firebase database reference with explicit URL
            FirebaseDatabase database;
            try 
            {
                // Пытаемся получить экземпляр с явным URL
                database = FirebaseDatabase.GetInstance(FirebaseApp.DefaultInstance, "https://comeback-2a6b2-default-rtdb.firebaseio.com");
                Debug.Log("[SubscriptionManager] ✅ Используем явный Database URL");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[SubscriptionManager] ⚠️ Не удалось использовать явный URL, используем DefaultInstance: " + ex.Message);
                // Fallback to default instance
                database = FirebaseDatabase.DefaultInstance;
            }
            subscriptionRef = database.GetReference("subscription_settings");
            
            // Подписываемся на изменения в реальном времени
            subscriptionRef.ValueChanged += HandleValueChanged;
            
            // Также делаем первоначальную загрузку
            subscriptionRef.GetValueAsync().ContinueWith(task => {
                if (task.IsFaulted)
                {
                    Debug.LogError("[SubscriptionManager] Failed to load subscription settings: " + task.Exception);
                    OnSubscriptionSettingsError?.Invoke();
                    return;
                }
                
                if (task.IsCompleted)
                {
                    Debug.Log("[SubscriptionManager] 📥 Первоначальная загрузка данных");
                    ProcessFirebaseData(task.Result);
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError("[SubscriptionManager] Exception loading subscription settings: " + e.Message);
            OnSubscriptionSettingsError?.Invoke();
        }
    }
    
    /// <summary>
    /// Get current subscription price for Freedom Pay
    /// </summary>
    public float GetSubscriptionPrice()
    {
        return subscriptionPrice;
    }
    
    /// <summary>
    /// Get subscription duration in minutes
    /// </summary>
    public int GetSubscriptionDurationMinutes()
    {
        return subscriptionDurationMinutes;
    }
    
    /// <summary>
    /// Cleanup when object is destroyed
    /// </summary>
    void OnDestroy()
    {
        if (subscriptionRef != null)
        {
            subscriptionRef.ValueChanged -= HandleValueChanged;
            Debug.Log("[SubscriptionManager] 🧹 Отписались от обновлений Firebase");
        }
    }
    
    /// <summary>
    /// Get subscription duration in seconds (for timer)
    /// </summary>
    public int GetSubscriptionDurationSeconds()
    {
        return subscriptionDurationMinutes * 60;
    }
    
    /// <summary>
    /// Get currency code
    /// </summary>
    public string GetCurrency()
    {
        return currency;
    }
    
    /// <summary>
    /// Check if subscription system is active
    /// </summary>
    public bool IsSubscriptionActive()
    {
        return isSubscriptionActive;
    }
    
    /// <summary>
    /// Get formatted price string for UI
    /// </summary>
    public string GetFormattedPrice()
    {
        return $"{subscriptionPrice:F0} {currency}";
    }
    
    /// <summary>
    /// Get formatted duration string for UI
    /// </summary>
    public string GetFormattedDuration()
    {
        if (subscriptionDurationMinutes < 60)
        {
            return $"{subscriptionDurationMinutes} minutes";
        }
        else
        {
            int hours = subscriptionDurationMinutes / 60;
            int minutes = subscriptionDurationMinutes % 60;
            
            if (minutes == 0)
                return $"{hours} час{(hours > 1 ? "ов" : "")}";
            else
                return $"{hours} час{(hours > 1 ? "ов" : "")} {minutes} минут";
        }
    }
}
