using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Простой тестер платежной системы Freedom Pay
/// </summary>
public class SimplePaymentTest : MonoBehaviour
{
    [Header("UI Components")]
    public Button payButton;
    public TMP_Text statusText;
    
    [Header("Test Settings")]
    public int testAmount = 1000; // Тестовая сумма в сумах
    
    private void Start()
    {
        // Настраиваем UI
        if (payButton != null)
        {
            payButton.onClick.AddListener(StartPaymentTest);
            
            // Настраиваем текст кнопки
            TMP_Text buttonText = payButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = $"Оплатить {testAmount} сум";
            }
        }
        
        // Подписываемся на события
        if (FreedomPayManager.Instance != null)
        {
            FreedomPayManager.Instance.OnPaymentSuccess += OnPaymentSuccess;
            FreedomPayManager.Instance.OnPaymentFailed += OnPaymentFailed;
            FreedomPayManager.Instance.OnPaymentPending += OnPaymentPending;
        }
        
        UpdateStatus("Готов к тестированию платежа", Color.white);
        
        // Проверяем текущий статус подписки
        CheckCurrentSubscription();
    }
    
    private void OnDestroy()
    {
        // Отписываемся от событий
        if (FreedomPayManager.Instance != null)
        {
            FreedomPayManager.Instance.OnPaymentSuccess -= OnPaymentSuccess;
            FreedomPayManager.Instance.OnPaymentFailed -= OnPaymentFailed;
            FreedomPayManager.Instance.OnPaymentPending -= OnPaymentPending;
        }
    }
    
    public void StartPaymentTest()
    {
        Debug.Log("=== НАЧАЛО ТЕСТИРОВАНИЯ ПЛАТЕЖА ===");
        
        // Проверяем наличие FreedomPayManager
        if (FreedomPayManager.Instance == null)
        {
            UpdateStatus("❌ ОШИБКА: FreedomPayManager не найден!", Color.red);
            Debug.LogError("FreedomPayManager не найден в сцене!");
            return;
        }
        
        // Генерируем уникальный ID заказа
        string orderId = "test_" + System.DateTime.Now.Ticks.ToString();
        int amountInTiyin = testAmount * 100; // Конвертируем сумы в тийины
        
        Debug.Log($"💰 Тестовый платеж: {testAmount} сум ({amountInTiyin} тийин)");
        Debug.Log($"📋 ID заказа: {orderId}");
        
        UpdateStatus($"🚀 Создание платежа {testAmount} сум...", Color.yellow);
        
        // Отключаем кнопку на время обработки
        if (payButton != null)
            payButton.interactable = false;
        
        try
        {
            // Инициируем платеж
            FreedomPayManager.Instance.InitiatePayment(testAmount, "Тестовый платеж Freedom Pay", orderId);
            Debug.Log("✅ Платеж инициирован успешно");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Ошибка при инициации платежа: {e.Message}");
            UpdateStatus($"❌ Ошибка: {e.Message}", Color.red);
            
            // Включаем кнопку обратно
            if (payButton != null)
                payButton.interactable = true;
        }
    }
    
    private void OnPaymentSuccess(string orderId)
    {
        Debug.Log($"🎉 УСПЕХ! Платеж завершен: {orderId}");
        UpdateStatus("🎉 Платеж успешно завершен!", Color.green);
        
        // Сохраняем время оплаты для 30-минутного доступа
        PlayerPrefs.SetString("LastPaymentTime", System.DateTime.Now.ToBinary().ToString());
        PlayerPrefs.SetInt("PaidAmount", testAmount);
        PlayerPrefs.Save();
        
        Debug.Log("💾 Данные оплаты сохранены");
        
        // Включаем кнопку обратно
        if (payButton != null)
            payButton.interactable = true;
        
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
    
    private void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        
        Debug.Log($"[SimplePaymentTest] {message}");
    }
    
    private void CheckCurrentSubscription()
    {
        string lastPaymentTimeString = PlayerPrefs.GetString("LastPaymentTime", "");
        
        if (string.IsNullOrEmpty(lastPaymentTimeString))
        {
            Debug.Log("📋 Подписка: Неактивна");
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
                string remainingText = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                
                Debug.Log($"✅ Подписка активна. Осталось: {remainingText}");
                UpdateStatus($"✅ Подписка активна: {remainingText}", Color.green);
            }
            else
            {
                Debug.Log("❌ Подписка истекла");
                UpdateStatus("❌ Подписка истекла", Color.gray);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Ошибка проверки подписки: {e.Message}");
        }
    }
    
    // Методы для отладки (можно вызвать через Inspector)
    [ContextMenu("Clear Payment Data")]
    public void ClearPaymentData()
    {
        PlayerPrefs.DeleteKey("LastPaymentTime");
        PlayerPrefs.DeleteKey("PaidAmount");
        PlayerPrefs.Save();
        Debug.Log("🗑️ Данные платежей очищены");
        UpdateStatus("🗑️ Данные очищены", Color.gray);
        CheckCurrentSubscription();
    }
    
    [ContextMenu("Test Browser Open")]
    public void TestBrowserOpen()
    {
        string testUrl = "https://google.com";
        Application.OpenURL(testUrl);
        Debug.Log($"🌐 Тест открытия браузера: {testUrl}");
    }
    
    [ContextMenu("Run Payment Test")]
    public void RunPaymentTestFromMenu()
    {
        Debug.Log("🚀 Запуск теста платежа из контекстного меню...");
        StartPaymentTest();
    }
} 