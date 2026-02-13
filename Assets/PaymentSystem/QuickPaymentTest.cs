using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuickPaymentTest : MonoBehaviour
{
    [Header("UI References")]
    public Button testButton;
    public TMP_Text statusText;
    public TMP_Text logText;
    
    [Header("Test Settings")]
    public int testAmount = 1000; // Тестовая сумма в сумах
    
    private string logMessages = "";
    
    void Start()
    {
        if (testButton != null)
        {
            testButton.onClick.AddListener(RunPaymentTest);
        }
        
        UpdateStatus("Готов к тестированию");
        
        // Подписываемся на события платежной системы
        if (FreedomPayManager.Instance != null)
        {
            FreedomPayManager.Instance.OnPaymentSuccess += OnPaymentSuccess;
            FreedomPayManager.Instance.OnPaymentFailed += OnPaymentFailed;
            FreedomPayManager.Instance.OnPaymentPending += OnPaymentPending;
        }
        
        LogMessage("QuickPaymentTest инициализирован");
    }
    
    void OnDestroy()
    {
        if (FreedomPayManager.Instance != null)
        {
            FreedomPayManager.Instance.OnPaymentSuccess -= OnPaymentSuccess;
            FreedomPayManager.Instance.OnPaymentFailed -= OnPaymentFailed;
            FreedomPayManager.Instance.OnPaymentPending -= OnPaymentPending;
        }
    }
    
    public void RunPaymentTest()
    {
        LogMessage("=== НАЧАЛО ТЕСТИРОВАНИЯ ===");
        
        // Проверяем FreedomPayManager
        if (FreedomPayManager.Instance == null)
        {
            LogMessage("❌ ОШИБКА: FreedomPayManager не найден!");
            UpdateStatus("Ошибка: FreedomPayManager не найден");
            return;
        }
        
        LogMessage("✅ FreedomPayManager найден");
        
        // Генерируем уникальный ID заказа
        string orderId = "test_" + System.DateTime.Now.Ticks.ToString();
        int amountInTiyin = testAmount * 100;
        
        LogMessage($"💰 Тестовый платеж: {testAmount} сум ({amountInTiyin} тийин)");
        LogMessage($"📋 ID заказа: {orderId}");
        
        UpdateStatus("Инициация тестового платежа...");
        
        // Запускаем платеж
        try
        {
            FreedomPayManager.Instance.InitiatePayment(testAmount, "Тестовый платеж Freedom Pay", orderId);
            LogMessage("🚀 Платеж инициирован");
        }
        catch (System.Exception e)
        {
            LogMessage($"❌ Ошибка при инициации: {e.Message}");
            UpdateStatus("Ошибка инициации платежа");
        }
    }
    
    private void OnPaymentSuccess(string orderId)
    {
        LogMessage($"✅ УСПЕХ! Платеж завершен: {orderId}");
        UpdateStatus("Платеж успешно завершен!");
        
        // Проверяем сохранение данных
        string savedTime = PlayerPrefs.GetString("LastPaymentTime", "");
        LogMessage($"💾 Время оплаты сохранено: {!string.IsNullOrEmpty(savedTime)}");
    }
    
    private void OnPaymentFailed(string error)
    {
        LogMessage($"❌ ОШИБКА: {error}");
        UpdateStatus($"Ошибка платежа: {error}");
    }
    
    private void OnPaymentPending(string status)
    {
        LogMessage($"⏳ Ожидание: {status}");
        UpdateStatus($"Обработка: {status}");
    }
    
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = $"Статус: {message}";
        }
        
        Debug.Log($"[QuickPaymentTest] {message}");
    }
    
    private void LogMessage(string message)
    {
        logMessages += $"{System.DateTime.Now:HH:mm:ss} - {message}\n";
        
        if (logText != null)
        {
            logText.text = logMessages;
        }
        
        Debug.Log($"[QuickPaymentTest] {message}");
        
        // Ограничиваем размер лога
        if (logMessages.Length > 2000)
        {
            logMessages = logMessages.Substring(logMessages.Length - 1500);
        }
    }
    
    [ContextMenu("Clear Logs")]
    public void ClearLogs()
    {
        logMessages = "";
        if (logText != null)
        {
            logText.text = "";
        }
        LogMessage("Логи очищены");
    }
    
    [ContextMenu("Clear Payment Data")]
    public void ClearPaymentData()
    {
        PlayerPrefs.DeleteKey("LastPaymentTime");
        PlayerPrefs.DeleteKey("PaidAmount");
        PlayerPrefs.Save();
        LogMessage("Данные платежей очищены");
    }
    
    [ContextMenu("Check Subscription")]
    public void CheckSubscription()
    {
        string lastPaymentTimeString = PlayerPrefs.GetString("LastPaymentTime", "");
        
        if (string.IsNullOrEmpty(lastPaymentTimeString))
        {
            LogMessage("📋 Подписка: Неактивна (нет данных)");
            return;
        }
        
        try
        {
            long lastPaymentTimeBinary = System.Convert.ToInt64(lastPaymentTimeString);
            System.DateTime lastPaymentTime = System.DateTime.FromBinary(lastPaymentTimeBinary);
            System.TimeSpan timeSincePayment = System.DateTime.Now - lastPaymentTime;
            
            if (timeSincePayment.TotalMinutes < 30)
            {
                System.TimeSpan remaining = System.TimeSpan.FromMinutes(30) - timeSincePayment;
                LogMessage($"✅ Подписка активна. Осталось: {remaining.Minutes:D2}:{remaining.Seconds:D2}");
            }
            else
            {
                LogMessage("❌ Подписка истекла");
            }
        }
        catch (System.Exception e)
        {
            LogMessage($"❌ Ошибка проверки подписки: {e.Message}");
        }
    }
} 