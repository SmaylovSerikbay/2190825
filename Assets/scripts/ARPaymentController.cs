using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// AR Payment Controller - управляет доступом к AR функциям через систему подписки FreedomPay
/// Точная копия SimplePaymentTest но для основной AR сцены
/// 
/// НОВАЯ ФУНКЦИОНАЛЬНОСТЬ:
/// - Возможность прикрепить Canvas который будет скрыт при активации подписки
/// - Автоматический поиск Canvas по имени если не привязан
/// - Методы для управления видимостью Canvas (ShowCanvas, HideCanvas, IsCanvasVisible)
/// </summary>
public class ARPaymentController : MonoBehaviour
{
    [Header("UI Components")]
    public Button payButton;
    public TMP_Text statusText;
    
    [Header("AR Scene Elements")]
    [SerializeField] private GameObject arSessionOrigin;
    [SerializeField] private GameObject arSession;
    [SerializeField] private GameObject uiCanvas;
    [SerializeField] private GameObject arElements;
    [SerializeField] private Text loadingText;
    
    [Header("Performance Components")]
    [SerializeField] private ARObjectManager arObjectManager;
    [SerializeField] private ARPerformanceManager performanceManager;
    
    [Header("Canvas to Hide on Activation")]
    [SerializeField] private Canvas canvasToHide; // Canvas который будет скрыт при активации подписки
    [SerializeField] private string canvasToHideName = ""; // Альтернативно: имя Canvas для поиска
    
    [Header("Debug Canvas Controls")]
    [SerializeField] private bool enableDebugControls = true; // Включить кнопки отладки в Inspector
    
    // ПРИМЕР ИСПОЛЬЗОВАНИЯ:
    // 1. Перетащите Canvas в поле canvasToHide в Inspector
    // 2. ИЛИ укажите имя Canvas в поле canvasToHideName (например: "MainMenuCanvas")
    // 3. При активации подписки Canvas автоматически скрывается
    // 4. Используйте ShowCanvas() и HideCanvas() для ручного управления
    
    [Header("Payment Settings")]
    [SerializeField] private int subscriptionPrice = 5000; // Цена подписки в сумах (будет обновлена из Firebase)
    [SerializeField] private int subscriptionDurationMinutes = 30; // Длительность подписки в минутах (будет обновлена из Firebase)
    
    [Header("Button Text Settings")]
    [SerializeField] private Color buttonTextColor = Color.black; // Цвет текста кнопки
    
    private void Start()
    {
        // Загружаем настройки подписки из Firebase
        LoadSubscriptionSettings();
        
        // Автоматически находим кнопку если не привязана
        if (payButton == null)
        {
            GameObject payButtonObj = GameObject.Find("PayButton");
            if (payButtonObj != null)
            {
                payButton = payButtonObj.GetComponent<Button>();
                Debug.Log("[ARPaymentController] 🔗 Кнопка найдена автоматически");
            }
        }
        
        // Автоматически находим текст статуса если не привязан
        if (statusText == null)
        {
            GameObject statusTextObj = GameObject.Find("StatusText");
            if (statusTextObj != null)
            {
                statusText = statusTextObj.GetComponent<TMP_Text>();
                Debug.Log("[ARPaymentController] 📝 Текст статуса найден автоматически");
            }
        }
        
        // Настраиваем UI
        if (payButton != null)
        {
            // Очищаем старые слушатели и добавляем новый
            payButton.onClick.RemoveAllListeners();
            payButton.onClick.AddListener(StartPaymentTest);
            Debug.Log("[ARPaymentController] ✅ Кнопка привязана к StartPaymentTest");
            
            // Настраиваем текст кнопки
            UpdateButtonText();
        }
        else
        {
            Debug.LogError("[ARPaymentController] ❌ PayButton не найден!");
        }
        
        // Подписываемся на события
        if (FreedomPayManager.Instance != null)
        {
            FreedomPayManager.Instance.OnPaymentSuccess += OnPaymentSuccess;
            FreedomPayManager.Instance.OnPaymentFailed += OnPaymentFailed;
            FreedomPayManager.Instance.OnPaymentPending += OnPaymentPending;
            FreedomPayManager.Instance.OnOTPActivation += OnOTPActivation;
        }
        
        // Автопоиск AR элементов если не привязаны
        if (arSessionOrigin == null)
        {
            GameObject arOrigin = GameObject.Find("AR Session Origin");
            if (arOrigin != null) arSessionOrigin = arOrigin;
        }
        
        if (arSession == null)
        {
            GameObject arSess = GameObject.Find("AR Session");
            if (arSess != null) arSession = arSess;
        }
        
        if (uiCanvas == null)
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas != null) uiCanvas = canvas;
        }
        
        // Автоматически находим Canvas для скрытия если не привязан
        if (canvasToHide == null && !string.IsNullOrEmpty(canvasToHideName))
        {
            Canvas foundCanvas = GameObject.Find(canvasToHideName)?.GetComponent<Canvas>();
            if (foundCanvas != null)
            {
                canvasToHide = foundCanvas;
                Debug.Log($"[ARPaymentController] 🔍 Canvas найден автоматически: {canvasToHideName}");
            }
            else
            {
                Debug.LogWarning($"[ARPaymentController] ⚠️ Canvas по имени не найден: {canvasToHideName}");
            }
        }
        
        // Логируем статус Canvas
        if (canvasToHide != null)
        {
            Debug.Log($"[ARPaymentController] ✅ Canvas привязан: {canvasToHide.name}");
        }
        else if (!string.IsNullOrEmpty(canvasToHideName))
        {
            Debug.Log($"[ARPaymentController] 🔍 Canvas будет искаться по имени: {canvasToHideName}");
        }
        else
        {
            Debug.LogWarning("[ARPaymentController] ⚠️ Canvas для скрытия не указан!");
        }
        
        // Исправляем проблему с двумя Audio Listener
        FixAudioListeners();
        
        // Отключаем VideoSpawner по умолчанию (включится только при активной подписке)
        VideoSpawner videoSpawner = FindObjectOfType<VideoSpawner>();
        if (videoSpawner != null)
        {
            videoSpawner.enabled = false;
            Debug.Log("[ARPaymentController] 🔄 VideoSpawner отключен по умолчанию");
        }
        
        // Автопоиск компонентов производительности
        if (arObjectManager == null)
        {
            arObjectManager = FindObjectOfType<ARObjectManager>();
            if (arObjectManager != null)
            {
                Debug.Log("[ARPaymentController] ✅ ARObjectManager найден автоматически");
            }
        }
        
        if (performanceManager == null)
        {
            performanceManager = FindObjectOfType<ARPerformanceManager>();
            if (performanceManager != null)
            {
                Debug.Log("[ARPaymentController] ✅ ARPerformanceManager найден автоматически");
            }
        }
        
        // Проверяем текущий статус подписки при старте
        CheckCurrentSubscription();
        
        // Дополнительная проверка через 2 секунды после старта (для надежности на мобильных)
        Invoke(nameof(DelayedStartupCheck), 2f);
        
        // Дополнительное обновление текста кнопки через 1 секунду (для надежности)
        Invoke(nameof(UpdateButtonText), 1f);
    }
    
    private void OnDestroy()
    {
        // Отписываемся от событий FreedomPay
        if (FreedomPayManager.Instance != null)
        {
            FreedomPayManager.Instance.OnPaymentSuccess -= OnPaymentSuccess;
            FreedomPayManager.Instance.OnPaymentFailed -= OnPaymentFailed;
            FreedomPayManager.Instance.OnPaymentPending -= OnPaymentPending;
            FreedomPayManager.Instance.OnOTPActivation -= OnOTPActivation;
        }
        
        // Отписываемся от событий SubscriptionManager
        SubscriptionManager.OnSubscriptionSettingsLoaded -= OnSubscriptionSettingsLoaded;
        SubscriptionManager.OnSubscriptionSettingsError -= OnSubscriptionSettingsError;
    }
    
    /// <summary>
    /// Вызывается когда приложение получает/теряет фокус
    /// Важно для восстановления AR при возвращении в приложение на мобильных
    /// </summary>
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            Debug.Log("[ARPaymentController] 📱 Приложение получило фокус - проверяем AR состояние");
            
            // Проверяем подписку при возвращении фокуса
            Invoke(nameof(CheckCurrentSubscription), 0.5f);
            
            // Дополнительная проверка через 2 секунды
            Invoke(nameof(DelayedStartupCheck), 2f);
        }
    }
    
    /// <summary>
    /// Вызывается при паузе/возобновлении приложения
    /// </summary>
    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus) // Приложение возобновилось
        {
            Debug.Log("[ARPaymentController] 📱 Приложение возобновилось - проверяем AR состояние");
            
            // Проверяем подписку при возобновлении
            Invoke(nameof(CheckCurrentSubscription), 0.5f);
            
            // Дополнительная проверка через 2 секунды
            Invoke(nameof(DelayedStartupCheck), 2f);
        }
    }
    
    public void StartPaymentTest()
    {
        Debug.Log("=== НАЧАЛО ОПЛАТЫ ПОДПИСКИ ===");
        
        // Проверяем наличие FreedomPayManager
        if (FreedomPayManager.Instance == null)
        {
            UpdateStatus("❌ ОШИБКА: FreedomPayManager не найден!", Color.red);
            Debug.LogError("FreedomPayManager не найден в сцене!");
            return;
        }
        
        // Генерируем уникальный ID заказа
        string orderId = "ar_subscription_" + System.DateTime.Now.Ticks.ToString();
        
        Debug.Log($"💰 Подписка на AR: {subscriptionPrice} сум на {subscriptionDurationMinutes} минут");
        Debug.Log($"📋 ID заказа: {orderId}");
        
        UpdateStatus($"🚀 Creating payment {subscriptionPrice} sums...", Color.yellow);
        
        // Отключаем кнопку на время обработки
        if (payButton != null)
            payButton.interactable = false;
        
        try
        {
            // Инициируем платеж
            FreedomPayManager.Instance.InitiatePayment(subscriptionPrice, "Подписка на AR функции", orderId);
            Debug.Log("✅ Платеж инициирован успешно");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Ошибка при инициации платежа: {e.Message}");
            UpdateStatus($"❌ Error: {e.Message}", Color.red);
            
            // Включаем кнопку обратно
            if (payButton != null)
                payButton.interactable = true;
        }
    }
    
    private void OnPaymentSuccess(string orderId)
    {
        Debug.Log($"🎉 SUCCESS! Subscription activated: {orderId}");
        UpdateStatus($"🎉 Subscription activated for {subscriptionDurationMinutes} minutes!", Color.green);
        
        // Сохраняем время оплаты для доступа к AR
        PlayerPrefs.SetString("SubscriptionEnd", System.DateTime.Now.AddMinutes(subscriptionDurationMinutes).ToBinary().ToString());
        PlayerPrefs.SetInt("PaidAmount", subscriptionPrice);
        PlayerPrefs.Save();
        
        Debug.Log("💾 Subscription data saved");
        
        // Включаем кнопку обратно
        if (payButton != null)
            payButton.interactable = true;
        
        // Активируем AR элементы
        ActivateARScene();
        
        // Обновляем статус подписки
        Invoke(nameof(CheckCurrentSubscription), 1f);
    }
    
    private void OnPaymentFailed(string error)
    {
        Debug.LogError($"❌ ОШИБКА ПЛАТЕЖА: {error}");
        UpdateStatus($"❌ Ошибка: {error}", Color.red);
        
        // Включаем кнопку обратно
        if (payButton != null)
            payButton.interactable = true;
    }
    
    private void OnPaymentPending(string status)
    {
        Debug.Log($"⏳ Обработка платежа: {status}");
        UpdateStatus($"⏳ {status}", Color.yellow);
    }
    
    private void OnOTPActivation(int durationMinutes)
    {
        Debug.Log($"🔑 Активация AR через OTP: {durationMinutes} минут");
        
        // Обновляем настройки подписки
        subscriptionDurationMinutes = durationMinutes;
        
        // Активируем AR точно так же как при успешной оплате
        OnPaymentSuccess("OTP_" + System.DateTime.Now.Ticks);
    }
    
    private void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        
        Debug.Log($"[ARPaymentController] {message}");
    }
    
    /// <summary>
    /// Показать Canvas обратно (если нужно)
    /// </summary>
    public void ShowCanvas()
    {
        if (canvasToHide != null)
        {
            canvasToHide.gameObject.SetActive(true);
            Debug.Log($"[ARPaymentController] ✅ Прикрепленный Canvas показан: {canvasToHide.name}");
        }
        else if (!string.IsNullOrEmpty(canvasToHideName))
        {
            Canvas foundCanvas = GameObject.Find(canvasToHideName)?.GetComponent<Canvas>();
            if (foundCanvas != null)
            {
                foundCanvas.gameObject.SetActive(true);
                Debug.Log($"[ARPaymentController] ✅ Canvas по имени показан: {canvasToHideName}");
            }
        }
    }
    
    /// <summary>
    /// Скрыть Canvas (если нужно)
    /// </summary>
    public void HideCanvas()
    {
        if (canvasToHide != null)
        {
            canvasToHide.gameObject.SetActive(false);
            Debug.Log($"[ARPaymentController] ❌ Прикрепленный Canvas скрыт: {canvasToHide.name}");
        }
        else if (!string.IsNullOrEmpty(canvasToHideName))
        {
            Canvas foundCanvas = GameObject.Find(canvasToHideName)?.GetComponent<Canvas>();
            if (foundCanvas != null)
            {
                foundCanvas.gameObject.SetActive(false);
                Debug.Log($"[ARPaymentController] ❌ Canvas по имени скрыт: {canvasToHideName}");
            }
        }
    }
    
    /// <summary>
    /// Принудительно скрыть Canvas (использует корутину)
    /// </summary>
    public void ForceHideCanvas()
    {
        Debug.Log("[ARPaymentController] 🔄 Принудительное скрытие Canvas...");
        StartCoroutine(ForceHideCanvasAfterDelay());
    }
    
    // Методы для отладки в Inspector
    [ContextMenu("Debug: Show Canvas")]
    private void DebugShowCanvas()
    {
        if (enableDebugControls)
        {
            Debug.Log("[ARPaymentController] 🧪 Debug: Показать Canvas");
            ShowCanvas();
        }
    }
    
    [ContextMenu("Debug: Hide Canvas")]
    private void DebugHideCanvas()
    {
        if (enableDebugControls)
        {
            Debug.Log("[ARPaymentController] 🧪 Debug: Скрыть Canvas");
            HideCanvas();
        }
    }
    
    [ContextMenu("Debug: Force Hide Canvas")]
    private void DebugForceHideCanvas()
    {
        if (enableDebugControls)
        {
            Debug.Log("[ARPaymentController] 🧪 Debug: Принудительно скрыть Canvas");
            ForceHideCanvas();
        }
    }
    
    [ContextMenu("Debug: Check Canvas Status")]
    private void DebugCheckCanvasStatus()
    {
        if (enableDebugControls)
        {
            Debug.Log($"[ARPaymentController] 🧪 Debug: Статус Canvas");
            Debug.Log($"[ARPaymentController] 🧪 canvasToHide: {(canvasToHide != null ? canvasToHide.name : "NULL")}");
            Debug.Log($"[ARPaymentController] 🧪 canvasToHideName: '{canvasToHideName}'");
            Debug.Log($"[ARPaymentController] 🧪 IsCanvasVisible: {IsCanvasVisible()}");
        }
    }
    
    /// <summary>
    /// Проверить статус Canvas
    /// </summary>
    public bool IsCanvasVisible()
    {
        if (canvasToHide != null)
        {
            return canvasToHide.gameObject.activeInHierarchy;
        }
        else if (!string.IsNullOrEmpty(canvasToHideName))
        {
            Canvas foundCanvas = GameObject.Find(canvasToHideName)?.GetComponent<Canvas>();
            return foundCanvas != null && foundCanvas.gameObject.activeInHierarchy;
        }
        return false;
    }
    
    /// <summary>
    /// Принудительно скрыть Canvas через задержку
    /// </summary>
    private IEnumerator ForceHideCanvasAfterDelay()
    {
        yield return new WaitForSeconds(0.1f); // Ждем 0.1 секунды
        
        Debug.Log("[ARPaymentController] 🔄 Принудительная проверка Canvas...");
        
        if (canvasToHide != null)
        {
            if (canvasToHide.gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"[ARPaymentController] ⚠️ Canvas все еще активен, принудительно скрываем: {canvasToHide.name}");
                canvasToHide.gameObject.SetActive(false);
                
                // Проверяем еще раз
                yield return new WaitForSeconds(0.1f);
                if (canvasToHide.gameObject.activeInHierarchy)
                {
                    Debug.LogError($"[ARPaymentController] ❌ Canvas НЕ удается скрыть! {canvasToHide.name}");
                }
                else
                {
                    Debug.Log($"[ARPaymentController] ✅ Canvas успешно скрыт принудительно: {canvasToHide.name}");
                }
            }
            
            // Дополнительная защита: скрываем Canvas еще несколько раз с интервалами
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.2f);
                if (canvasToHide.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning($"[ARPaymentController] 🔄 Дополнительное скрытие Canvas (попытка {i + 1}): {canvasToHide.name}");
                    canvasToHide.gameObject.SetActive(false);
                }
            }
        }
        else if (!string.IsNullOrEmpty(canvasToHideName))
        {
            Canvas foundCanvas = GameObject.Find(canvasToHideName)?.GetComponent<Canvas>();
            if (foundCanvas != null && foundCanvas.gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"[ARPaymentController] ⚠️ Canvas по имени все еще активен, принудительно скрываем: {canvasToHideName}");
                foundCanvas.gameObject.SetActive(false);
                
                // Проверяем еще раз
                yield return new WaitForSeconds(0.1f);
                if (foundCanvas.gameObject.activeInHierarchy)
                {
                    Debug.LogError($"[ARPaymentController] ❌ Canvas по имени НЕ удается скрыть! {canvasToHideName}");
                }
                else
                {
                    Debug.Log($"[ARPaymentController] ✅ Canvas по имени успешно скрыт принудительно: {canvasToHideName}");
                }
            }
            
            // Дополнительная защита: скрываем Canvas еще несколько раз с интервалами
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.2f);
                if (foundCanvas != null && foundCanvas.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning($"[ARPaymentController] 🔄 Дополнительное скрытие Canvas по имени (попытка {i + 1}): {canvasToHideName}");
                    foundCanvas.gameObject.SetActive(false);
                }
            }
        }
    }
    
    /// <summary>
    /// Переинициализация видео из кэша с задержкой для устранения зависания
    /// </summary>
    private IEnumerator ReinitializeCachedVideosAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        
        if (arObjectManager != null)
        {
            Debug.Log("[ARPaymentController] 🔄 Переинициализация видео из кэша для устранения зависания...");
            arObjectManager.ReinitializeCachedVideos();
        }
    }
    
    /// <summary>
    /// Отложенная проверка AR объектов
    /// </summary>
    private IEnumerator DelayedARCheck()
    {
        yield return new WaitForSeconds(2f);
        
        if (arObjectManager != null)
        {
            int activeCount = arObjectManager.GetActiveObjectCount();
            Debug.Log($"[ARPaymentController] 🔍 Отложенная проверка: активных объектов {activeCount}");
            
            if (activeCount == 0)
            {
                Debug.Log("[ARPaymentController] ⚠️ Объекты не активировались, повторная попытка...");
                arObjectManager.ForceActivateAllObjects();
            }
        }
    }
    
    /// <summary>
    /// Перезапуск видео с задержкой для устранения зависания
    /// </summary>
    private IEnumerator RestartVideosAfterDelay()
    {
        yield return new WaitForSeconds(1f);
        
        if (arObjectManager != null)
        {
            Debug.Log("[ARPaymentController] 🔄 Перезапуск всех видео для устранения зависания...");
            arObjectManager.RestartAllVideos();
        }
    }
    

    
    /// <summary>
    /// Проверка текущего статуса подписки
    /// </summary>
    private void CheckCurrentSubscription()
    {
        string subscriptionEndString = PlayerPrefs.GetString("SubscriptionEnd", "");
        
        if (string.IsNullOrEmpty(subscriptionEndString))
        {
            Debug.Log("📋 Subscription: Inactive");
            UpdateStatus("Subscription required for AR access", Color.white);
            ShowPaymentScreen();
            
            // Обновляем текст кнопки для надежности
            UpdateButtonText();
            return;
        }
        
        try
        {
            long subscriptionEndBinary = System.Convert.ToInt64(subscriptionEndString);
            System.DateTime subscriptionEnd = System.DateTime.FromBinary(subscriptionEndBinary);
            System.TimeSpan timeRemaining = subscriptionEnd - System.DateTime.Now;
            
            if (timeRemaining.TotalMinutes > 0)
            {
                string remainingText = $"{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";
                
                Debug.Log($"✅ Subscription active. Remaining: {remainingText}");
                UpdateStatus($"✅ AR available: {remainingText}", Color.green);
                
                // Проверяем, нужно ли активировать AR сцену
                bool shouldActivateAR = true;
                
                // Если Canvas уже скрыт, не активируем AR повторно
                if (canvasToHide != null && !canvasToHide.gameObject.activeInHierarchy)
                {
                    Debug.Log($"[ARPaymentController] 🔍 Canvas {canvasToHide.name} уже скрыт, пропускаем активацию AR");
                    shouldActivateAR = false;
                }
                else if (!string.IsNullOrEmpty(canvasToHideName))
                {
                    Canvas foundCanvas = GameObject.Find(canvasToHideName)?.GetComponent<Canvas>();
                    if (foundCanvas != null && !foundCanvas.gameObject.activeInHierarchy)
                    {
                        Debug.Log($"[ARPaymentController] 🔍 Canvas {canvasToHideName} уже скрыт, пропускаем активацию AR");
                        shouldActivateAR = false;
                    }
                }
                
                if (shouldActivateAR)
                {
                    // ПРИНУДИТЕЛЬНО активируем AR сцену
                    Debug.Log("[ARPaymentController] 🔄 ПРИНУДИТЕЛЬНАЯ АКТИВАЦИЯ AR СЦЕНЫ");
                    ActivateARScene();
                    
                    // Дополнительная проверка через 1 секунду
                    Invoke(nameof(ForceActivateARIfNeeded), 1f);
                }
                else
                {
                    Debug.Log("[ARPaymentController] 🔍 AR сцена уже активна, пропускаем повторную активацию");
                }
                
                // УБЕЖДАЕМСЯ что кнопка оплаты скрыта
                if (payButton != null && payButton.gameObject.activeInHierarchy)
                {
                    payButton.gameObject.SetActive(false);
                    Debug.Log("[ARPaymentController] ❌ Кнопка оплаты скрыта (подписка активна)");
                }
            }
            else
            {
                Debug.Log("❌ Subscription expired");
                UpdateStatus("❌ Subscription expired. Payment required", Color.gray);
                ShowPaymentScreen();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Ошибка проверки подписки: {e.Message}");
            ShowPaymentScreen();
        }
    }
    
    /// <summary>
    /// Активировать AR сцену
    /// </summary>
    private void ActivateARScene()
    {
        Debug.Log("[ARPaymentController] 📱 Активация AR сцены");
        
        // ПРИНУДИТЕЛЬНО скрываем UI оплаты
        GameObject paymentUI = GameObject.Find("PaymentUI");
        if (paymentUI != null) 
        {
            paymentUI.SetActive(false);
            Debug.Log("[ARPaymentController] ❌ PaymentUI полностью скрыт");
        }
        
        // Скрываем прикрепленный Canvas если он указан
        Debug.Log($"[ARPaymentController] 🔍 Проверяем Canvas для скрытия...");
        Debug.Log($"[ARPaymentController] 🔍 canvasToHide: {(canvasToHide != null ? canvasToHide.name : "NULL")}");
        Debug.Log($"[ARPaymentController] 🔍 canvasToHideName: '{canvasToHideName}'");
        
        if (canvasToHide != null)
        {
            Debug.Log($"[ARPaymentController] 🔍 Найден прикрепленный Canvas: {canvasToHide.name}");
            Debug.Log($"[ARPaymentController] 🔍 Canvas активен: {canvasToHide.gameObject.activeInHierarchy}");
            
            canvasToHide.gameObject.SetActive(false);
            Debug.Log($"[ARPaymentController] ❌ Прикрепленный Canvas скрыт: {canvasToHide.name}");
            
            // Проверяем что действительно скрылся
            if (canvasToHide.gameObject.activeInHierarchy)
            {
                Debug.LogError($"[ARPaymentController] ❌ Canvas НЕ скрылся! {canvasToHide.name} все еще активен!");
            }
            else
            {
                Debug.Log($"[ARPaymentController] ✅ Canvas успешно скрыт: {canvasToHide.name}");
            }
        }
        else if (!string.IsNullOrEmpty(canvasToHideName))
        {
            Debug.Log($"[ARPaymentController] 🔍 Ищем Canvas по имени: {canvasToHideName}");
            
            // Пытаемся найти Canvas по имени если не привязан
            GameObject foundObject = GameObject.Find(canvasToHideName);
            if (foundObject != null)
            {
                Debug.Log($"[ARPaymentController] 🔍 Найден объект: {foundObject.name}");
                
                Canvas foundCanvas = foundObject.GetComponent<Canvas>();
                if (foundCanvas != null)
                {
                    Debug.Log($"[ARPaymentController] 🔍 Найден Canvas компонент: {foundCanvas.name}");
                    Debug.Log($"[ARPaymentController] 🔍 Canvas активен: {foundCanvas.gameObject.activeInHierarchy}");
                    
                    foundCanvas.gameObject.SetActive(false);
                    Debug.Log($"[ARPaymentController] ❌ Canvas по имени скрыт: {canvasToHideName}");
                    
                    // Проверяем что действительно скрылся
                    if (foundCanvas.gameObject.activeInHierarchy)
                    {
                        Debug.LogError($"[ARPaymentController] ❌ Canvas НЕ скрылся! {canvasToHideName} все еще активен!");
                    }
                    else
                    {
                        Debug.Log($"[ARPaymentController] ✅ Canvas успешно скрыт: {canvasToHideName}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ARPaymentController] ⚠️ Объект найден, но не содержит Canvas компонент: {foundObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[ARPaymentController] ⚠️ Canvas по имени не найден: {canvasToHideName}");
            }
        }
        else
        {
            Debug.LogWarning("[ARPaymentController] ⚠️ Canvas для скрытия не указан!");
        }
        
        // Принудительно скрываем Canvas через несколько кадров (на случай если что-то мешает)
        if (canvasToHide != null || !string.IsNullOrEmpty(canvasToHideName))
        {
            StartCoroutine(ForceHideCanvasAfterDelay());
        }
        
        // Скрываем OTP InputField если он есть
        GameObject otpPanel = GameObject.Find("OTP Input Panel");
        if (otpPanel != null)
        {
            otpPanel.SetActive(false);
            Debug.Log("[ARPaymentController] ❌ OTP Input Panel скрыт");
        }
        
        // Ищем и скрываем все InputField (TMP) связанные с OTP
        TMP_InputField[] allInputFields = FindObjectsOfType<TMP_InputField>(true);
        foreach (TMP_InputField inputField in allInputFields)
        {
            if (inputField.name.Contains("OTP") || inputField.name.Contains("otp"))
            {
                inputField.gameObject.SetActive(false);
                Debug.Log($"[ARPaymentController] ❌ OTP InputField скрыт: {inputField.name}");
            }
        }
        
        // Скрываем кнопку и статус текст отдельно
        if (payButton != null) 
        {
            payButton.gameObject.SetActive(false);
            Debug.Log("[ARPaymentController] ❌ PayButton скрыт");
        }
        
        if (statusText != null) 
        {
            statusText.gameObject.SetActive(false);
            Debug.Log("[ARPaymentController] ❌ StatusText скрыт");
        }
        
        // Показываем AR элементы с подробным логированием
        if (arSessionOrigin != null) 
        {
            arSessionOrigin.SetActive(true);
            Debug.Log($"[ARPaymentController] ✅ AR Session Origin активирован: {arSessionOrigin.name} = {arSessionOrigin.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("[ARPaymentController] ❌ arSessionOrigin = NULL!");
        }
        
        if (arSession != null) 
        {
            arSession.SetActive(true);
            Debug.Log($"[ARPaymentController] ✅ AR Session активирован: {arSession.name} = {arSession.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("[ARPaymentController] ❌ arSession = NULL!");
        }
        
        // Активируем Canvas объекты кроме PaymentUI и canvasToHide
        Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas canvas in allCanvases)
        {
            bool isPaymentUI = canvas.gameObject == paymentUI;
            bool isCanvasToHide = canvas == canvasToHide;
            
            if (!isPaymentUI && !isCanvasToHide)
            {
                canvas.gameObject.SetActive(true);
                Debug.Log($"[ARPaymentController] ✅ Canvas {canvas.gameObject.name} активирован");
            }
            else if (isCanvasToHide)
            {
                Debug.Log($"[ARPaymentController] ❌ Canvas {canvas.gameObject.name} ПРОПУЩЕН (должен быть скрыт)");
            }
            else if (isPaymentUI)
            {
                Debug.Log($"[ARPaymentController] ❌ Canvas {canvas.gameObject.name} ПРОПУЩЕН (PaymentUI)");
            }
        }
        
        // Активируем UI объект для AR функций
        GameObject uiObject = GameObject.Find("UI");
        if (uiObject != null)
        {
            uiObject.SetActive(true);
            Debug.Log("[ARPaymentController] ✅ UI объект показан");
        }
        else
        {
            Debug.LogWarning("[ARPaymentController] ⚠️ UI объект не найден!");
        }
        
        // Активируем все Button объекты в основном UI (кроме PaymentUI и кнопки оплаты)
        Button[] allButtons = FindObjectsOfType<Button>(true);
        foreach (Button btn in allButtons)
        {
            // Исключаем кнопку оплаты и кнопки в PaymentUI
            bool isPayButton = btn == payButton;
            bool isInPaymentUI = paymentUI != null && btn.transform.IsChildOf(paymentUI.transform);
            
            if (!isPayButton && !isInPaymentUI)
            {
                btn.gameObject.SetActive(true);
                Debug.Log($"[ARPaymentController] ✅ Кнопка {btn.gameObject.name} активирована");
            }
            else if (isPayButton)
            {
                Debug.Log($"[ARPaymentController] ❌ Кнопка оплаты {btn.gameObject.name} ПРОПУЩЕНА (подписка активна)");
            }
        }
        
        if (arElements != null) 
        {
            arElements.SetActive(true);
            Debug.Log("[ARPaymentController] ✅ AR Elements активированы");
        }
        
        // Активируем текст загрузки в ОСНОВНОМ Canvas
        GameObject loadingTextObj = GameObject.Find("Text (Legacy)");
        if (loadingTextObj != null)
        {
            Text loadingTextComponent = loadingTextObj.GetComponent<Text>();
            if (loadingTextComponent != null)
            {
                loadingTextComponent.text = "AR режим активен!\nПодписка действует";
                loadingTextObj.SetActive(true);
                Debug.Log("[ARPaymentController] ✅ Loading Text обновлен");
            }
        }
        
        // Включаем VideoSpawner при активации AR
        VideoSpawner videoSpawner = FindObjectOfType<VideoSpawner>();
        if (videoSpawner != null)
        {
            videoSpawner.enabled = true;
            Debug.Log("[ARPaymentController] ✅ VideoSpawner включен");
        }
        
        // ДОПОЛНИТЕЛЬНАЯ ЛОГИКА ДЛЯ МОБИЛЬНЫХ УСТРОЙСТВ
        // Принудительно активируем все потенциальные AR объекты
        GameObject[] allGameObjects = FindObjectsOfType<GameObject>(true);
        foreach (GameObject obj in allGameObjects)
        {
            // Активируем объекты связанные с AR (но не PaymentUI)
            if ((obj.name.Contains("AR") || obj.name.Contains("Camera") || obj.name.Contains("UI")) && 
                !obj.name.Contains("Payment") && 
                obj != payButton?.gameObject && 
                obj != statusText?.gameObject)
            {
                if (!obj.activeInHierarchy)
                {
                    obj.SetActive(true);
                    Debug.Log($"[ARPaymentController] 🔄 Принудительно активирован: {obj.name}");
                }
            }
        }
        
        Debug.Log("[ARPaymentController] 🎉 AR сцена полностью активирована!");
    }
    
    /// <summary>
    /// Показать экран оплаты
    /// </summary>
    private void ShowPaymentScreen()
    {
        Debug.Log("[ARPaymentController] 💳 Показ экрана оплаты");
        
        // ПРИНУДИТЕЛЬНО показываем UI оплаты
        GameObject paymentUI = GameObject.Find("PaymentUI");
        if (paymentUI != null) 
        {
            paymentUI.SetActive(true);
            Debug.Log("[ARPaymentController] ✅ PaymentUI показан");
        }
        
        // Показываем кнопку и статус текст отдельно
        if (payButton != null) 
        {
            payButton.gameObject.SetActive(true);
            Debug.Log("[ARPaymentController] ✅ PayButton показан");
        }
        
        if (statusText != null) 
        {
            statusText.gameObject.SetActive(true);
            Debug.Log("[ARPaymentController] ✅ StatusText показан");
        }
        
        // Скрываем AR элементы
        if (arSessionOrigin != null) 
        {
            arSessionOrigin.SetActive(false);
            Debug.Log("[ARPaymentController] ❌ AR Session Origin отключен");
        }
        
        if (arSession != null) 
        {
            arSession.SetActive(false);
            Debug.Log("[ARPaymentController] ❌ AR Session отключен");
        }
        
        // Скрываем основной Canvas с AR UI полностью
        GameObject mainCanvas = GameObject.Find("Canvas");
        if (mainCanvas != null && mainCanvas != paymentUI) 
        {
            mainCanvas.SetActive(false);
            Debug.Log("[ARPaymentController] ❌ Основной Canvas скрыт");
        }
        
        // Дополнительно скрываем все кнопки которые могут быть в UI
        GameObject uiObject = GameObject.Find("UI");
        if (uiObject != null)
        {
            uiObject.SetActive(false);
            Debug.Log("[ARPaymentController] ❌ UI объект скрыт");
        }
        
        if (arElements != null) 
        {
            arElements.SetActive(false);
            Debug.Log("[ARPaymentController] ❌ AR Elements отключены");
        }
        
        // Скрываем loading text из основного Canvas
        GameObject loadingTextObj = GameObject.Find("Text (Legacy)");
        if (loadingTextObj != null)
        {
            loadingTextObj.SetActive(false);
            Debug.Log("[ARPaymentController] ❌ Loading Text скрыт");
        }
        
        // Отключаем VideoSpawner при показе экрана оплаты
        VideoSpawner videoSpawner = FindObjectOfType<VideoSpawner>();
        if (videoSpawner != null)
        {
            videoSpawner.enabled = false;
            Debug.Log("[ARPaymentController] ❌ VideoSpawner отключен");
        }
    }
    
    /// <summary>
    /// Проверка подписки каждые 5 секунд
    /// </summary>
    void Update()
    {
        // Проверяем подписку каждые 5 секунд
        if (Time.frameCount % (int)(5f * 60f) == 0) // ~5 секунд при 60 FPS
        {
            string subscriptionEndString = PlayerPrefs.GetString("SubscriptionEnd", "");
            if (!string.IsNullOrEmpty(subscriptionEndString))
            {
                try
                {
                    long subscriptionEndBinary = System.Convert.ToInt64(subscriptionEndString);
                    System.DateTime subscriptionEnd = System.DateTime.FromBinary(subscriptionEndBinary);
                    System.TimeSpan timeRemaining = subscriptionEnd - System.DateTime.Now;
                    
                    if (timeRemaining.TotalMinutes > 0)
                    {
                        string remainingText = $"{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";
                        UpdateStatus($"✅ AR available: {remainingText}", Color.green);
                        
                        // Проверяем что AR действительно активен, если нет - активируем
                        GameObject arOrigin = GameObject.Find("AR Session Origin");
                        GameObject arSess = GameObject.Find("AR Session");
                        
                        bool arIsInactive = (arOrigin != null && !arOrigin.activeInHierarchy) || 
                                          (arSess != null && !arSess.activeInHierarchy);
                                          
                        if (arIsInactive)
                        {
                            Debug.Log("[ARPaymentController] ⚠️ AR неактивен при активной подписке! Активируем...");
                            ActivateARScene();
                            // Дополнительная принудительная активация через секунду
                            Invoke(nameof(ForceActivateAR), 1f);
                        }
                        
                        // ПРОВЕРЯЕМ что кнопка оплаты скрыта при активной подписке
                        if (payButton != null && payButton.gameObject.activeInHierarchy)
                        {
                            payButton.gameObject.SetActive(false);
                            Debug.Log("[ARPaymentController] ❌ Кнопка оплаты скрыта (подписка активна)");
                        }
                        
                        // ПРОВЕРЯЕМ что PaymentUI скрыт при активной подписке
                        GameObject paymentUI = GameObject.Find("PaymentUI");
                        if (paymentUI != null && paymentUI.activeInHierarchy)
                        {
                            paymentUI.SetActive(false);
                            Debug.Log("[ARPaymentController] ❌ PaymentUI скрыт (подписка активна)");
                        }
                    }
                    else
                    {
                        // Подписка истекла во время использования AR
                        Debug.Log("[ARPaymentController] ⏰ Subscription expired during use");
                        UpdateStatus("⏰ Subscription expired", Color.red);
                        ShowPaymentScreen();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Ошибка проверки подписки: {e.Message}");
                }
            }
        }
    }
    
    // Методы для отладки (можно вызвать через Inspector)
    [ContextMenu("Clear Subscription Data")]
    public void ClearSubscriptionData()
    {
        PlayerPrefs.DeleteKey("SubscriptionEnd");
        PlayerPrefs.DeleteKey("PaidAmount");
        PlayerPrefs.Save();
        Debug.Log("🗑️ Subscription data cleared");
        UpdateStatus("🗑️ Data cleared", Color.gray);
        CheckCurrentSubscription();
    }
    
    [ContextMenu("Test Browser Open")]
    public void TestBrowserOpen()
    {
        string testUrl = "https://google.com";
        Application.OpenURL(testUrl);
        Debug.Log($"🌐 Тест открытия браузера: {testUrl}");
    }
    
    [ContextMenu("Activate AR Scene")]
    public void TestActivateAR()
    {
        ActivateARScene();
    }
    
    [ContextMenu("Show Payment Screen")]
    public void TestShowPayment()
    {
        ShowPaymentScreen();
    }
    
    [ContextMenu("Run Payment Test")]
    public void RunPaymentTestFromMenu()
    {
        StartPaymentTest();
    }
    
    [ContextMenu("Check Subscription Status")]
    public void CheckSubscriptionFromMenu()
    {
        CheckCurrentSubscription();
    }
    
    [ContextMenu("Test Mobile Startup Check")]
    public void TestMobileStartupCheck()
    {
        DelayedStartupCheck();
    }
    
    [ContextMenu("Log Object States")]
    public void LogObjectStates()
    {
        Debug.Log("=== СОСТОЯНИЕ AR ОБЪЕКТОВ ===");
        
        GameObject arOrigin = GameObject.Find("AR Session Origin");
        Debug.Log($"AR Session Origin: {(arOrigin != null ? $"найден, активен = {arOrigin.activeInHierarchy}" : "НЕ НАЙДЕН")}");
        
        GameObject arSess = GameObject.Find("AR Session");
        Debug.Log($"AR Session: {(arSess != null ? $"найден, активен = {arSess.activeInHierarchy}" : "НЕ НАЙДЕН")}");
        
        GameObject uiObj = GameObject.Find("UI");
        Debug.Log($"UI объект: {(uiObj != null ? $"найден, активен = {uiObj.activeInHierarchy}" : "НЕ НАЙДЕН")}");
        
        GameObject canvasObj = GameObject.Find("Canvas");
        Debug.Log($"Canvas: {(canvasObj != null ? $"найден, активен = {canvasObj.activeInHierarchy}" : "НЕ НАЙДЕН")}");
        
        GameObject paymentUI = GameObject.Find("PaymentUI");
        Debug.Log($"PaymentUI: {(paymentUI != null ? $"найден, активен = {paymentUI.activeInHierarchy}" : "НЕ НАЙДЕН")}");
        
        Debug.Log($"PayButton: {(payButton != null ? $"найден, активен = {payButton.gameObject.activeInHierarchy}" : "НЕ НАЙДЕН")}");
        
        VideoSpawner videoSpawner = FindObjectOfType<VideoSpawner>();
        Debug.Log($"VideoSpawner: {(videoSpawner != null ? $"найден, enabled = {videoSpawner.enabled}" : "НЕ НАЙДЕН")}");
        
        // Дополнительная диагностика AR компонентов
        if (arObjectManager != null)
        {
            Debug.Log($"ARObjectManager: найден, активных объектов = {arObjectManager.GetActiveObjectCount()}");
        }
        else
        {
            Debug.LogWarning("ARObjectManager: НЕ НАЙДЕН!");
        }
        
        if (performanceManager != null)
        {
            Debug.Log($"ARPerformanceManager: найден, уровень качества = {performanceManager.GetCurrentQualityLevel() + 1}");
        }
        else
        {
            Debug.LogWarning("ARPerformanceManager: НЕ НАЙДЕН!");
        }
        
        Debug.Log("=== КОНЕЦ ДИАГНОСТИКИ ===");
    }
    
    [ContextMenu("Force Update Subscription Settings")]
    public void ForceUpdateSubscriptionSettings()
    {
        Debug.Log("[ARPaymentController] 🔧 ПРИНУДИТЕЛЬНОЕ ОБНОВЛЕНИЕ НАСТРОЕК ПОДПИСКИ!");
        
        if (SubscriptionManager.Instance != null)
        {
            int oldPrice = subscriptionPrice;
            int oldDuration = subscriptionDurationMinutes;
            
            subscriptionPrice = (int)SubscriptionManager.Instance.GetSubscriptionPrice();
            subscriptionDurationMinutes = SubscriptionManager.Instance.GetSubscriptionDurationMinutes();
            
            Debug.Log($"[ARPaymentController] 🔄 Принудительное обновление:");
            Debug.Log($"  Старые значения: {oldPrice} сум, {oldDuration} минут");
            Debug.Log($"  Новые значения: {subscriptionPrice} сум, {subscriptionDurationMinutes} минут");
            Debug.Log($"  Валюта: {SubscriptionManager.Instance.GetCurrency()}");
        }
        else
        {
            Debug.LogError("[ARPaymentController] ❌ SubscriptionManager не найден!");
        }
    }
    
    [ContextMenu("Force Activate AR NOW")]
    public void ForceActivateAR()
    {
        Debug.Log("[ARPaymentController] 🚀 ПРИНУДИТЕЛЬНАЯ АКТИВАЦИЯ AR ЧЕРЕЗ MENU!");
        
        // Принудительно находим и активируем объекты
        GameObject arOrigin = GameObject.Find("AR Session Origin");
        if (arOrigin != null)
        {
            arOrigin.SetActive(true);
            Debug.Log($"[ARPaymentController] ✅ ПРИНУДИТЕЛЬНО активирован: {arOrigin.name}");
        }
        
        GameObject arSess = GameObject.Find("AR Session");  
        if (arSess != null)
        {
            arSess.SetActive(true);
            Debug.Log($"[ARPaymentController] ✅ ПРИНУДИТЕЛЬНО активирован: {arSess.name}");
        }
        
        GameObject uiObj = GameObject.Find("UI");
        if (uiObj != null)
        {
            uiObj.SetActive(true);
            Debug.Log($"[ARPaymentController] ✅ ПРИНУДИТЕЛЬНО активирован: {uiObj.name}");
        }
        
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj != null)
        {
            canvasObj.SetActive(true);
            Debug.Log($"[ARPaymentController] ✅ ПРИНУДИТЕЛЬНО активирован: {canvasObj.name}");
        }
        
        // УБЕЖДАЕМСЯ что кнопка оплаты скрыта при активной подписке
        if (payButton != null)
        {
            payButton.gameObject.SetActive(false);
            Debug.Log($"[ARPaymentController] ❌ Кнопка оплаты скрыта (подписка активна)");
        }
        
        // УБЕЖДАЕМСЯ что PaymentUI скрыт
        GameObject paymentUI = GameObject.Find("PaymentUI");
        if (paymentUI != null)
        {
            paymentUI.SetActive(false);
            Debug.Log($"[ARPaymentController] ❌ PaymentUI скрыт (подписка активна)");
        }
        
                    // Принудительно активируем AR объекты
            if (arObjectManager != null)
            {
                Debug.Log("[ARPaymentController] 🚀 Принудительная активация AR объектов...");
                
                // ИСПРАВЛЕНО: Сначала загружаем из кэша для быстрого старта
                arObjectManager.LoadFromCache();
                
                arObjectManager.ForceActivateAllObjects();
                
                // ИСПРАВЛЕНО: Перезапускаем все видео для устранения зависания
                StartCoroutine(RestartVideosAfterDelay());
                
                // ИСПРАВЛЕНО: Дополнительно переинициализируем видео из кэша для устранения зависания
                StartCoroutine(ReinitializeCachedVideosAfterDelay());
                
                // Дополнительная проверка через 2 секунды
                StartCoroutine(DelayedARCheck());
            }
        
        Debug.Log("[ARPaymentController] 🎉 ПРИНУДИТЕЛЬНАЯ АКТИВАЦИЯ ЗАВЕРШЕНА!");
    }
    
    /// <summary>
    /// Проверяет активацию AR объектов и принудительно активирует если нужно
    /// </summary>
    private void ForceActivateARIfNeeded()
    {
        Debug.Log("[ARPaymentController] 🔍 Проверка активации AR объектов...");
        
        bool needsForcing = false;
        
        // Проверяем AR Session Origin
        GameObject arOrigin = GameObject.Find("AR Session Origin");
        if (arOrigin != null && !arOrigin.activeInHierarchy)
        {
            Debug.Log("[ARPaymentController] ⚠️ AR Session Origin неактивен!");
            needsForcing = true;
        }
        
        // Проверяем AR Session
        GameObject arSess = GameObject.Find("AR Session");
        if (arSess != null && !arSess.activeInHierarchy)
        {
            Debug.Log("[ARPaymentController] ⚠️ AR Session неактивен!");
            needsForcing = true;
        }
        
        // Проверяем UI объект
        GameObject uiObj = GameObject.Find("UI");
        if (uiObj != null && !uiObj.activeInHierarchy)
        {
            Debug.Log("[ARPaymentController] ⚠️ UI объект неактивен!");
            needsForcing = true;
        }
        
        if (needsForcing)
        {
            Debug.Log("[ARPaymentController] 🚨 ТРЕБУЕТСЯ ПРИНУДИТЕЛЬНАЯ АКТИВАЦИЯ!");
            ForceActivateAR();
        }
        else
        {
            Debug.Log("[ARPaymentController] ✅ Все AR объекты активны корректно");
        }
    }
    
    /// <summary>
    /// Дополнительная проверка при старте приложения (особенно важно для мобильных устройств)
    /// </summary>
    private void DelayedStartupCheck()
    {
        Debug.Log("[ARPaymentController] 📱 Дополнительная проверка при старте приложения...");
        
        string subscriptionEndString = PlayerPrefs.GetString("SubscriptionEnd", "");
        if (!string.IsNullOrEmpty(subscriptionEndString))
        {
            try
            {
                long subscriptionEndBinary = System.Convert.ToInt64(subscriptionEndString);
                System.DateTime subscriptionEnd = System.DateTime.FromBinary(subscriptionEndBinary);
                System.TimeSpan timeRemaining = subscriptionEnd - System.DateTime.Now;
                
                if (timeRemaining.TotalMinutes > 0)
                {
                    string remainingText = $"{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";
                    Debug.Log($"[ARPaymentController] ✅ Подписка активна при старте - осталось: {remainingText}");
                    
                    // Обновляем статус
                    UpdateStatus($"✅ AR available: {remainingText}", Color.green);
                    
                    // Принудительно активируем AR при старте если подписка активна
                    Debug.Log("[ARPaymentController] 🚀 ПРИНУДИТЕЛЬНАЯ АКТИВАЦИЯ AR ПРИ СТАРТЕ!");
                    ActivateARScene();
                    
                    // Дополнительная принудительная активация через секунду
                    Invoke(nameof(ForceActivateAR), 1f);
                    
                    // И еще одна через 3 секунды для полной уверенности
                    Invoke(nameof(ForceActivateAR), 3f);
                    
                    // Проверяем состояние объектов
                    Invoke(nameof(LogObjectStates), 4f);
                }
                else
                {
                    Debug.Log("[ARPaymentController] ❌ Subscription expired at startup");
                    ShowPaymentScreen();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Ошибка проверки подписки при старте: {e.Message}");
                ShowPaymentScreen();
            }
        }
        else
        {
            Debug.Log("[ARPaymentController] 📋 Нет подписки при старте");
            ShowPaymentScreen();
        }
    }
    
    /// <summary>
    /// Исправляет проблему с несколькими Audio Listener в сцене
    /// </summary>
    private void FixAudioListeners()
    {
        // Находим все Audio Listener в сцене
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        
        if (listeners.Length > 1)
        {
            Debug.LogWarning($"[ARPaymentController] ⚠️ Найдено {listeners.Length} Audio Listener. Исправляем...");
            
            // Оставляем активным только Audio Listener на AR Camera
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                
                // Если это AR Camera - оставляем включенным
                if (listener.gameObject.name.Contains("AR Camera"))
                {
                    listener.enabled = true;
                    Debug.Log($"[ARPaymentController] ✅ Audio Listener на {listener.gameObject.name} оставлен активным");
                }
                else
                {
                    // Все остальные отключаем
                    listener.enabled = false;
                    Debug.Log($"[ARPaymentController] ❌ Audio Listener на {listener.gameObject.name} отключен");
                }
            }
        }
        else if (listeners.Length == 1)
        {
            Debug.Log("[ARPaymentController] ✅ Audio Listener настроен правильно");
        }
        else
        {
            Debug.LogError("[ARPaymentController] ❌ Не найдено ни одного Audio Listener!");
        }
    }
    
    /// <summary>
    /// Загрузка настроек подписки из SubscriptionManager
    /// </summary>
    private void LoadSubscriptionSettings()
    {
        Debug.Log("[ARPaymentController] 🔧 Загрузка настроек подписки...");
        Debug.Log($"[ARPaymentController] 📋 Текущие значения: {subscriptionPrice} сум, {subscriptionDurationMinutes} минут");
        
        // Подписываемся на события SubscriptionManager
        SubscriptionManager.OnSubscriptionSettingsLoaded += OnSubscriptionSettingsLoaded;
        SubscriptionManager.OnSubscriptionSettingsError += OnSubscriptionSettingsError;
        
        // Проверяем, есть ли уже SubscriptionManager в сцене
        if (SubscriptionManager.Instance != null)
        {
            // Используем уже загруженные настройки
            int newPrice = (int)SubscriptionManager.Instance.GetSubscriptionPrice();
            int newDuration = SubscriptionManager.Instance.GetSubscriptionDurationMinutes();
            
            Debug.Log($"[ARPaymentController] 🔥 SubscriptionManager найден!");
            Debug.Log($"  SubscriptionManager цена: {newPrice} {SubscriptionManager.Instance.GetCurrency()}");
            Debug.Log($"  SubscriptionManager длительность: {newDuration} минут");
            
            // ПРИНУДИТЕЛЬНО обновляем значения
            subscriptionPrice = newPrice;
            subscriptionDurationMinutes = newDuration;
            
            Debug.Log($"[ARPaymentController] ✅ Значения ПРИНУДИТЕЛЬНО обновлены:");
            Debug.Log($"  Новая цена: {subscriptionPrice} {SubscriptionManager.Instance.GetCurrency()}");
            Debug.Log($"  Новая длительность: {subscriptionDurationMinutes} минут");
        }
        else
        {
            Debug.LogWarning("[ARPaymentController] ⚠️ SubscriptionManager не найден, используем значения по умолчанию");
            Debug.LogWarning($"[ARPaymentController] 📋 Значения по умолчанию: {subscriptionPrice} сум, {subscriptionDurationMinutes} минут");
        }
        
        // Дополнительная попытка через 2 секунды
        Invoke(nameof(RetryLoadSubscriptionSettings), 2f);
    }
    
    /// <summary>
    /// Повторная попытка загрузки настроек подписки
    /// </summary>
    private void RetryLoadSubscriptionSettings()
    {
        Debug.Log("[ARPaymentController] 🔄 Повторная попытка загрузки настроек...");
        
        if (SubscriptionManager.Instance != null)
        {
            int newPrice = (int)SubscriptionManager.Instance.GetSubscriptionPrice();
            int newDuration = SubscriptionManager.Instance.GetSubscriptionDurationMinutes();
            
            if (newPrice != subscriptionPrice || newDuration != subscriptionDurationMinutes)
            {
                Debug.Log($"[ARPaymentController] 🔄 Обнаружены новые настройки при повторной попытке:");
                Debug.Log($"  Старые: {subscriptionPrice} сум, {subscriptionDurationMinutes} минут");
                Debug.Log($"  Новые: {newPrice} сум, {newDuration} минут");
                
                subscriptionPrice = newPrice;
                subscriptionDurationMinutes = newDuration;
                
                Debug.Log("[ARPaymentController] ✅ Настройки обновлены при повторной попытке!");
            }
        }
        else
        {
            Debug.LogError("[ARPaymentController] ❌ SubscriptionManager все еще не найден!");
        }
    }
    
    /// <summary>
    /// Обработчик успешной загрузки настроек подписки
    /// </summary>
    private void OnSubscriptionSettingsLoaded(float price, int durationMinutes, string currency)
    {
        subscriptionPrice = (int)price;
        subscriptionDurationMinutes = durationMinutes;
        
        Debug.Log($"[ARPaymentController] 🔥 Настройки подписки обновлены из Firebase:");
        Debug.Log($"  Цена: {subscriptionPrice} {currency}");
        Debug.Log($"  Длительность: {subscriptionDurationMinutes} минут");
        
        // Обновляем UI если нужно
        UpdateSubscriptionDisplay();
        
        // Обновляем текст кнопки
        UpdateButtonText();
    }
    
    /// <summary>
    /// Обработчик ошибки загрузки настроек подписки
    /// </summary>
    private void OnSubscriptionSettingsError()
    {
        Debug.LogWarning("[ARPaymentController] ⚠️ Ошибка загрузки настроек подписки из Firebase, используем значения по умолчанию");
        Debug.Log($"[ARPaymentController] 📋 Значения по умолчанию: {subscriptionPrice} сум, {subscriptionDurationMinutes} минут");
    }
    
    /// <summary>
    /// Обновление отображения информации о подписке
    /// </summary>
    private void UpdateSubscriptionDisplay()
    {
        // Обновляем статус текст если он существует
        if (statusText != null && SubscriptionManager.Instance != null)
        {
            string formattedPrice = SubscriptionManager.Instance.GetFormattedPrice();
            string formattedDuration = SubscriptionManager.Instance.GetFormattedDuration();
            
            // Обновляем только если подписка неактивна
            if (!IsSubscriptionActive())
            {
                UpdateStatus($"Subscription: {formattedPrice} for {formattedDuration}", Color.white);
            }
        }
        
        // Обновляем текст кнопки
        UpdateButtonText();
    }
    
    /// <summary>
    /// Обновляет текст кнопки оплаты (поддерживает Text Legacy и TMP_Text)
    /// </summary>
    private void UpdateButtonText()
    {
        if (payButton != null)
        {
            Debug.Log($"[ARPaymentController] 🔍 Поиск текста кнопки в: {payButton.name}");
            
            // Сначала пробуем найти TMP_Text (TextMeshPro)
            TMP_Text tmpButtonText = payButton.GetComponentInChildren<TMP_Text>();
            if (tmpButtonText != null)
            {
                Debug.Log($"[ARPaymentController] ✅ Найден TMP_Text: {tmpButtonText.name}");
                tmpButtonText.text = "Pay";
                tmpButtonText.color = buttonTextColor;
                Debug.Log($"[ARPaymentController] 🔄 TMP_Text кнопки обновлен: {tmpButtonText.text}, цвет: {buttonTextColor}");
                return;
            }
            else
            {
                Debug.Log("[ARPaymentController] ❌ TMP_Text не найден");
            }
            
            // Если TMP_Text не найден, ищем Text (Legacy)
            UnityEngine.UI.Text legacyButtonText = payButton.GetComponentInChildren<UnityEngine.UI.Text>();
            if (legacyButtonText != null)
            {
                Debug.Log($"[ARPaymentController] ✅ Найден Text (Legacy): {legacyButtonText.name}");
                Debug.Log($"[ARPaymentController] 🔍 Text активен: {legacyButtonText.gameObject.activeInHierarchy}");
                Debug.Log($"[ARPaymentController] 🔍 Text компонент активен: {legacyButtonText.enabled}");
                
                legacyButtonText.text = "Pay";
                legacyButtonText.color = buttonTextColor;
                Debug.Log($"[ARPaymentController] 🔄 Text (Legacy) кнопки обновлен: {legacyButtonText.text}, цвет: {buttonTextColor}");
                
                // Проверяем что Text остался активным после обновления
                Debug.Log($"[ARPaymentController] 🔍 После обновления - Text активен: {legacyButtonText.gameObject.activeInHierarchy}");
                Debug.Log($"[ARPaymentController] 🔍 После обновления - Text компонент активен: {legacyButtonText.enabled}");
                
                // Если Text отключился, принудительно включаем его
                if (!legacyButtonText.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning("[ARPaymentController] ⚠️ Text отключился после обновления! Принудительно включаем...");
                    legacyButtonText.gameObject.SetActive(true);
                    Debug.Log($"[ARPaymentController] ✅ Text принудительно включен: {legacyButtonText.gameObject.activeInHierarchy}");
                }
                
                return;
            }
            else
            {
                Debug.Log("[ARPaymentController] ❌ Text (Legacy) не найден");
            }
            
            // Если ничего не найдено, выводим все дочерние объекты
            Debug.LogWarning("[ARPaymentController] ⚠️ Не найден текст кнопки (ни TMP_Text, ни Text Legacy)");
            Debug.LogWarning($"[ARPaymentController] 🔍 Дочерние объекты кнопки {payButton.name}:");
            foreach (Transform child in payButton.transform)
            {
                Debug.LogWarning($"  - {child.name} (активен: {child.gameObject.activeInHierarchy})");
                if (child.GetComponent<TMP_Text>() != null)
                    Debug.LogWarning($"    -> TMP_Text компонент найден");
                if (child.GetComponent<UnityEngine.UI.Text>() != null)
                    Debug.LogWarning($"    -> Text (Legacy) компонент найден");
            }
        }
        else
        {
            Debug.LogError("[ARPaymentController] ❌ payButton = NULL!");
        }
    }
    
    /// <summary>
    /// Проверка активности подписки
    /// </summary>
    private bool IsSubscriptionActive()
    {
        string subscriptionEndString = PlayerPrefs.GetString("SubscriptionEnd", "");
        
        if (string.IsNullOrEmpty(subscriptionEndString))
            return false;
        
        try
        {
            long subscriptionEndBinary = System.Convert.ToInt64(subscriptionEndString);
            System.DateTime subscriptionEnd = System.DateTime.FromBinary(subscriptionEndBinary);
            return subscriptionEnd > System.DateTime.Now;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Активирует AR через OTP код (точно как при успешной оплате)
    /// </summary>
    public void ActivateARWithOTP(int durationMinutes)
    {
        Debug.Log($"[ARPaymentController] 🔑 Активация AR через OTP на {durationMinutes} минут");
        
        // Сохраняем время оплаты для доступа к AR (точно как в OnPaymentSuccess)
        PlayerPrefs.SetString("SubscriptionEnd", System.DateTime.Now.AddMinutes(durationMinutes).ToBinary().ToString());
        PlayerPrefs.SetInt("PaidAmount", subscriptionPrice);
        PlayerPrefs.Save();
        
        Debug.Log($"[ARPaymentController] ✅ Подписка активирована через OTP до: {System.DateTime.Now.AddMinutes(durationMinutes)}");
        
        // Обновляем статус (точно как в OnPaymentSuccess)
        UpdateStatus($"🎉 Subscription activated for {durationMinutes} minutes!", Color.green);
        
        // Активируем AR элементы (точно как в OnPaymentSuccess)
        ActivateARScene();
        
        // Обновляем статус подписки (точно как в OnPaymentSuccess)
        Invoke(nameof(CheckCurrentSubscription), 1f);
        
        Debug.Log($"[ARPaymentController] 🔑 OTP активация завершена - AR доступен на {durationMinutes} минут");
    }
    
    // Методы для управления производительностью AR
    [ContextMenu("Optimize AR Performance")]
    public void OptimizeARPerformance()
    {
        if (performanceManager != null)
        {
            // Принудительно устанавливаем оптимальный уровень качества
            performanceManager.ForceQualityLevel(1); // Среднее качество для баланса
            Debug.Log("[ARPaymentController] ⚡ Производительность AR оптимизирована");
        }
        else
        {
            Debug.LogWarning("[ARPaymentController] ⚠️ ARPerformanceManager не найден");
        }
    }
    
    [ContextMenu("Set High Quality AR")]
    public void SetHighQualityAR()
    {
        if (performanceManager != null)
        {
            performanceManager.ForceQualityLevel(0); // Высокое качество
            Debug.Log("[ARPaymentController] 🎯 Установлено высокое качество AR");
        }
    }
    
    [ContextMenu("Set Low Quality AR")]
    public void SetLowQualityAR()
    {
        if (performanceManager != null)
        {
            performanceManager.ForceQualityLevel(3); // Низкое качество для экономии ресурсов
            Debug.Log("[ARPaymentController] 🔋 Установлено низкое качество AR для экономии батареи");
        }
    }
    
    [ContextMenu("Reset Performance Settings")]
    public void ResetPerformanceSettings()
    {
        if (performanceManager != null)
        {
            performanceManager.ResetThermalStress();
            Debug.Log("[ARPaymentController] 🔄 Настройки производительности сброшены");
        }
    }
    
    /// <summary>
    /// Получает информацию о производительности AR
    /// </summary>
    public string GetARPerformanceInfo()
    {
        if (performanceManager == null)
            return "ARPerformanceManager не найден";
        
        string info = $"FPS: {performanceManager.GetCurrentFPS():F1}\n";
        info += $"Качество: {performanceManager.GetCurrentQualityLevel() + 1}/4\n";
        info += $"Тепловой стресс: {performanceManager.GetThermalStress():F2}\n";
        
        if (arObjectManager != null)
        {
            info += $"Активных объектов: {arObjectManager.GetActiveObjectCount()}\n";
            info += $"Размер пула: {arObjectManager.GetPoolSize()}";
        }
        
        return info;
    }
} 