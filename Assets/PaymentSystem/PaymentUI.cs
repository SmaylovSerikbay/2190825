using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PaymentUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject paymentPanel;
    [SerializeField] private Button payButton;
    [SerializeField] private TMP_InputField amountInput;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text priceDisplayText;
    [SerializeField] private Button closeButton;
    
    [Header("Payment Settings")]
    [SerializeField] private int defaultAmountInSums = 1000; // Сумма по умолчанию в сумах
    [SerializeField] private string paymentDescription = "Доступ к AR объектам";
    
    [Header("UI Messages")]
    [SerializeField] private string successMessage = "Платеж успешно завершен!";
    [SerializeField] private string failureMessage = "Ошибка платежа";
    [SerializeField] private string pendingMessage = "Обработка платежа...";
    [SerializeField] private string processingMessage = "Пожалуйста, завершите оплату в браузере";
    
    private bool isPaymentInProgress = false;
    
    void Start()
    {
        InitializeUI();
        SubscribeToEvents();
    }
    
    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
    
    private void InitializeUI()
    {
        // Настройка кнопок
        if (payButton != null)
        {
            payButton.onClick.AddListener(OnPayButtonClicked);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePaymentPanel);
        }
        
        // Настройка поля ввода суммы
        if (amountInput != null)
        {
            amountInput.text = defaultAmountInSums.ToString();
            amountInput.onValueChanged.AddListener(OnAmountChanged);
        }
        
        // Обновление отображения цены
        UpdatePriceDisplay();
        
        // Обновление статуса
        UpdateStatusText("Готов к оплате", Color.white);
        
        // Скрыть панель по умолчанию
        if (paymentPanel != null)
        {
            paymentPanel.SetActive(false);
        }
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
            UpdateStatusText("Готов к оплате", Color.white);
            SetPaymentButtonState(true);
        }
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
        SetPaymentButtonState(true);
    }
    
    /// <summary>
    /// Обработчик нажатия кнопки оплаты
    /// </summary>
    private void OnPayButtonClicked()
    {
        if (isPaymentInProgress)
        {
            return;
        }
        
        int amountInSums;
        if (!int.TryParse(amountInput.text, out amountInSums) || amountInSums <= 0)
        {
            UpdateStatusText("Введите корректную сумму", Color.red);
            return;
        }
        
        // Конвертируем сумы в тийины (1 сум = 100 тийин)
        int amountInTiyin = amountInSums * 100;
        
        // Генерируем уникальный ID заказа
        string orderId = "order_" + DateTime.Now.Ticks.ToString();
        
        // Обновляем UI
        isPaymentInProgress = true;
        SetPaymentButtonState(false);
        UpdateStatusText(processingMessage, Color.yellow);
        
        // Инициируем платеж
        FreedomPayManager.Instance.InitiatePayment(amountInSums, paymentDescription, orderId);
        
        Debug.Log($"[PaymentUI] Инициирован платеж: {amountInSums} сум ({amountInTiyin} тийин), заказ: {orderId}");
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
    /// Обновить текст статуса
    /// </summary>
    private void UpdateStatusText(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
    }
    
    /// <summary>
    /// Установить состояние кнопки оплаты
    /// </summary>
    private void SetPaymentButtonState(bool enabled)
    {
        if (payButton != null)
        {
            payButton.interactable = enabled;
            
            // Изменяем текст кнопки
            TMP_Text buttonText = payButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = enabled ? "Оплатить" : "Обработка...";
            }
        }
    }
    
    /// <summary>
    /// Обработчик успешного платежа
    /// </summary>
    private void HandlePaymentSuccess(string orderId)
    {
        Debug.Log($"[PaymentUI] Платеж успешен: {orderId}");
        
        isPaymentInProgress = false;
        SetPaymentButtonState(true);
        UpdateStatusText(successMessage, Color.green);
        
        // Можно добавить логику для предоставления доступа к контенту
        GrantAccess();
        
        // Автоматически закрыть панель через несколько секунд
        Invoke("ClosePaymentPanel", 3f);
    }
    
    /// <summary>
    /// Обработчик неуспешного платежа
    /// </summary>
    private void HandlePaymentFailed(string error)
    {
        Debug.LogError($"[PaymentUI] Ошибка платежа: {error}");
        
        isPaymentInProgress = false;
        SetPaymentButtonState(true);
        UpdateStatusText($"{failureMessage}: {error}", Color.red);
    }
    
    /// <summary>
    /// Обработчик ожидающего платежа
    /// </summary>
    private void HandlePaymentPending(string status)
    {
        Debug.Log($"[PaymentUI] Платеж в ожидании: {status}");
        
        UpdateStatusText($"{pendingMessage} ({status})", Color.yellow);
    }
    
    /// <summary>
    /// Предоставить доступ к контенту после успешной оплаты
    /// </summary>
    private void GrantAccess()
    {
        // Сохраняем время последней оплаты
        PlayerPrefs.SetString("LastPaymentTime", DateTime.Now.ToBinary().ToString());
        PlayerPrefs.SetInt("PaidAmount", int.Parse(amountInput.text));
        PlayerPrefs.Save();
        
        Debug.Log("[PaymentUI] Доступ предоставлен после успешной оплаты");
        
        // Уведомляем другие компоненты о предоставлении доступа
        VideoSpawner videoSpawner = FindFirstObjectByType<VideoSpawner>();
        if (videoSpawner != null)
        {
            // VideoSpawner будет автоматически проверять подписку
            Debug.Log("[PaymentUI] Уведомляем VideoSpawner о успешной оплате");
        }
    }
    
    /// <summary>
    /// Проверить, есть ли активная подписка
    /// </summary>
    public bool HasActiveSubscription()
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
            
            // Проверяем, прошло ли 30 минут с последней оплаты
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
    public TimeSpan GetRemainingSubscriptionTime()
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
} 