using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PaymentTester : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button showPaymentButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject paymentPanel;
    
    [Header("Payment Panel Components")]
    [SerializeField] private Button payButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_InputField amountInput;
    [SerializeField] private TMP_Text priceDisplayText;
    [SerializeField] private TMP_Text paymentStatusText;
    
    [Header("Settings")]
    [SerializeField] private int[] predefinedAmounts = { 500, 1000, 2000, 5000 };
    [SerializeField] private int currentAmountIndex = 1; // 1000 сум по умолчанию
    
    private PaymentUI paymentUI;
    private bool isPaymentInProgress = false;
    
    void Start()
    {
        InitializeUI();
        CheckSubscriptionStatus();
        SubscribeToEvents();
    }
    
    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
    
    private void InitializeUI()
    {
        // Инициализация компонента PaymentUI
        paymentUI = GetComponent<PaymentUI>();
        if (paymentUI == null)
        {
            paymentUI = gameObject.AddComponent<PaymentUI>();
        }
        
        // Настройка кнопки показа оплаты
        if (showPaymentButton != null)
        {
            showPaymentButton.onClick.AddListener(ShowPaymentPanel);
        }
        
        // Настройка панели оплаты
        if (paymentPanel != null)
        {
            paymentPanel.SetActive(false);
        }
        
        // Настройка кнопки оплаты
        if (payButton != null)
        {
            payButton.onClick.AddListener(ProcessPayment);
        }
        
        // Настройка кнопки закрытия
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePaymentPanel);
        }
        
        // Настройка поля ввода суммы
        if (amountInput != null)
        {
            amountInput.text = predefinedAmounts[currentAmountIndex].ToString();
            amountInput.onValueChanged.AddListener(OnAmountChanged);
        }
        
        UpdatePriceDisplay();
        UpdateStatusDisplay();
    }
    
    private void SubscribeToEvents()
    {
        if (FreedomPayManager.Instance != null)
        {
            FreedomPayManager.Instance.OnPaymentSuccess += HandlePaymentSuccess;
            FreedomPayManager.Instance.OnPaymentFailed += HandlePaymentFailed;
            FreedomPayManager.Instance.OnPaymentPending += HandlePaymentPending;
        }
    }
    
    private void UnsubscribeFromEvents()
    {
        if (FreedomPayManager.Instance != null)
        {
            FreedomPayManager.Instance.OnPaymentSuccess -= HandlePaymentSuccess;
            FreedomPayManager.Instance.OnPaymentFailed -= HandlePaymentFailed;
            FreedomPayManager.Instance.OnPaymentPending -= HandlePaymentPending;
        }
    }
    
    /// <summary>
    /// Показать панель оплаты
    /// </summary>
    public void ShowPaymentPanel()
    {
        if (paymentPanel != null)
        {
            paymentPanel.SetActive(true);
            UpdatePaymentPanelStatus("Готов к оплате", Color.white);
        }
        
        Debug.Log("[PaymentTester] Панель оплаты открыта");
    }
    
    /// <summary>
    /// Скрыть панель оплаты
    /// </summary>
    public void ClosePaymentPanel()
    {
        if (paymentPanel != null)
        {
            paymentPanel.SetActive(false);
        }
        
        isPaymentInProgress = false;
        UpdatePayButton(true);
        
        Debug.Log("[PaymentTester] Панель оплаты закрыта");
    }
    
    /// <summary>
    /// Обработать платеж
    /// </summary>
    public void ProcessPayment()
    {
        if (isPaymentInProgress)
        {
            Debug.Log("[PaymentTester] Платеж уже в процессе");
            return;
        }
        
        int amount;
        if (!int.TryParse(amountInput.text, out amount) || amount <= 0)
        {
            UpdatePaymentPanelStatus("Введите корректную сумму", Color.red);
            return;
        }
        
        // Проверяем, что FreedomPayManager существует
        if (FreedomPayManager.Instance == null)
        {
            UpdatePaymentPanelStatus("Ошибка: FreedomPayManager не найден", Color.red);
            return;
        }
        
        // Обновляем UI
        isPaymentInProgress = true;
        UpdatePayButton(false);
        UpdatePaymentPanelStatus("Инициация платежа...", Color.yellow);
        
        // Конвертируем сумы в тийины
        int amountInTiyin = amount * 100;
        string orderId = "test_order_" + DateTime.Now.Ticks.ToString();
        
        // Инициируем платеж
        FreedomPayManager.Instance.InitiatePayment(amount, "Тестовый платеж ComeBack", orderId);
        
        Debug.Log($"[PaymentTester] Инициирован тестовый платеж: {amount} сум, заказ: {orderId}");
    }
    
    /// <summary>
    /// Обработчик изменения суммы
    /// </summary>
    private void OnAmountChanged(string value)
    {
        UpdatePriceDisplay();
    }
    
    /// <summary>
    /// Обновить отображение цены
    /// </summary>
    private void UpdatePriceDisplay()
    {
        if (priceDisplayText != null && amountInput != null)
        {
            int amount;
            if (int.TryParse(amountInput.text, out amount) && amount > 0)
            {
                priceDisplayText.text = $"К оплате: {amount:N0} сум";
            }
            else
            {
                priceDisplayText.text = "К оплате: 0 сум";
            }
        }
    }
    
    /// <summary>
    /// Обновить кнопку оплаты
    /// </summary>
    private void UpdatePayButton(bool enabled)
    {
        if (payButton != null)
        {
            payButton.interactable = enabled;
            
            TMP_Text buttonText = payButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = enabled ? "Оплатить" : "Обработка...";
            }
        }
    }
    
    /// <summary>
    /// Обновить статус в панели оплаты
    /// </summary>
    private void UpdatePaymentPanelStatus(string message, Color color)
    {
        if (paymentStatusText != null)
        {
            paymentStatusText.text = message;
            paymentStatusText.color = color;
        }
    }
    
    /// <summary>
    /// Обновить основной статус
    /// </summary>
    private void UpdateStatusDisplay()
    {
        if (statusText != null)
        {
            if (HasActiveSubscription())
            {
                TimeSpan remaining = GetRemainingSubscriptionTime();
                statusText.text = $"Подписка активна. Осталось: {remaining.Minutes:D2}:{remaining.Seconds:D2}";
                statusText.color = Color.green;
            }
            else
            {
                statusText.text = "Подписка неактивна. Требуется оплата.";
                statusText.color = Color.red;
            }
        }
    }
    
    /// <summary>
    /// Проверить статус подписки
    /// </summary>
    private void CheckSubscriptionStatus()
    {
        UpdateStatusDisplay();
        
        // Обновляем статус каждые 5 секунд
        InvokeRepeating("UpdateStatusDisplay", 5f, 5f);
    }
    
    /// <summary>
    /// Обработчик успешного платежа
    /// </summary>
    private void HandlePaymentSuccess(string orderId)
    {
        Debug.Log($"[PaymentTester] Платеж успешен: {orderId}");
        
        isPaymentInProgress = false;
        UpdatePayButton(true);
        UpdatePaymentPanelStatus("Платеж успешно завершен!", Color.green);
        UpdateStatusDisplay();
        
        // Закрыть панель через 3 секунды
        Invoke("ClosePaymentPanel", 3f);
    }
    
    /// <summary>
    /// Обработчик неуспешного платежа
    /// </summary>
    private void HandlePaymentFailed(string error)
    {
        Debug.LogError($"[PaymentTester] Ошибка платежа: {error}");
        
        isPaymentInProgress = false;
        UpdatePayButton(true);
        UpdatePaymentPanelStatus($"Ошибка: {error}", Color.red);
    }
    
    /// <summary>
    /// Обработчик ожидающего платежа
    /// </summary>
    private void HandlePaymentPending(string status)
    {
        Debug.Log($"[PaymentTester] Платеж в ожидании: {status}");
        
        UpdatePaymentPanelStatus($"Обработка платежа... ({status})", Color.yellow);
    }
    
    /// <summary>
    /// Проверить активную подписку
    /// </summary>
    private bool HasActiveSubscription()
    {
        string lastPaymentTimeString = PlayerPrefs.GetString("LastPaymentTime", "");
        
        if (string.IsNullOrEmpty(lastPaymentTimeString))
        {
            return false;
        }
        
        try
        {
            long lastPaymentTimeBinary = Convert.ToInt64(lastPaymentTimeString);
            DateTime lastPaymentTime = DateTime.FromBinary(lastPaymentTimeBinary);
            
            TimeSpan timeSincePayment = DateTime.Now - lastPaymentTime;
            return timeSincePayment.TotalMinutes < 30;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Получить оставшееся время подписки
    /// </summary>
    private TimeSpan GetRemainingSubscriptionTime()
    {
        string lastPaymentTimeString = PlayerPrefs.GetString("LastPaymentTime", "");
        
        if (string.IsNullOrEmpty(lastPaymentTimeString))
        {
            return TimeSpan.Zero;
        }
        
        try
        {
            long lastPaymentTimeBinary = Convert.ToInt64(lastPaymentTimeString);
            DateTime lastPaymentTime = DateTime.FromBinary(lastPaymentTimeBinary);
            
            TimeSpan subscriptionDuration = TimeSpan.FromMinutes(30);
            TimeSpan elapsed = DateTime.Now - lastPaymentTime;
            
            return subscriptionDuration - elapsed;
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }
    
    /// <summary>
    /// Метод для быстрого тестирования (можно вызвать из кнопки)
    /// </summary>
    public void QuickTest()
    {
        Debug.Log("[PaymentTester] Быстрый тест платежной системы");
        
        if (FreedomPayManager.Instance != null)
        {
            Debug.Log("[PaymentTester] FreedomPayManager найден");
            Debug.Log($"[PaymentTester] FreedomPayManager найден: {FreedomPayManager.Instance != null}");
        }
        else
        {
            Debug.LogError("[PaymentTester] FreedomPayManager не найден!");
        }
        
        ShowPaymentPanel();
    }
    
    /// <summary>
    /// Очистить данные подписки (для тестирования)
    /// </summary>
    public void ClearSubscriptionData()
    {
        PlayerPrefs.DeleteKey("LastPaymentTime");
        PlayerPrefs.DeleteKey("PaidAmount");
        PlayerPrefs.Save();
        
        UpdateStatusDisplay();
        
        Debug.Log("[PaymentTester] Данные подписки очищены");
    }
} 