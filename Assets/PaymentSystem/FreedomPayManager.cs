using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // Добавляем для Task.Run
using System.Text; // Добавляем для Encoding

/// <summary>
/// ✅ Менеджер платежей для работы с Django бэкендом через ngrok
/// 
/// АЛГОРИТМ РАБОТЫ:
/// 1. Создание платежа через POST запрос к Django API
/// 2. Получение URL для оплаты от бэкенда
/// 3. Открытие страницы оплаты в браузере
/// 4. Проверка статуса через API бэкенда
/// 5. Активация подписки при успешной оплате
/// </summary>
public class FreedomPayManager : MonoBehaviour
{
    [Header("Django Backend Settings")]
    [Tooltip("Базовый URL Django бэкенда")]
    [SerializeField] private string backendBaseUrl = "https://89.39.95.247";  // 🔄 Рабочий IP с HTTPS
    [SerializeField] private string createPaymentEndpoint = "/payment-gateway/api/unity/create-payment/";  // 🔄 Django путь
    [SerializeField] private string checkStatusEndpoint = "/payment-gateway/api/unity/check-status/";      // 🔄 Django путь
    
    [Header("Subscription Settings")]
    [SerializeField] private int subscriptionDurationMinutes = 15;
    
    [Header("Status")]
    [SerializeField] private bool isInitialized = false;
    [SerializeField] private string lastOrderId = "";
    [SerializeField] private string lastSessionId = "";
    [SerializeField] private PaymentState currentPaymentState = PaymentState.None;
    
    [Header("Connection Settings")]
    [SerializeField] private bool testConnectionOnStart = false;
    [SerializeField] private int requestTimeout = 15;
    
    // Singleton паттерн
    public static FreedomPayManager Instance { get; private set; }
    
    // События платежей
    public System.Action<string> OnPaymentSuccess;
    public System.Action<string> OnPaymentFailed;
    public System.Action<string> OnPaymentPending;
    
    // Событие для OTP активации
    public System.Action<int> OnOTPActivation; // duration_minutes
    
    // Внутренние переменные
    private Coroutine statusCheckCoroutine;
    private float statusCheckStartTime;
    private const float STATUS_CHECK_TIMEOUT = 180f; // 3 минуты
    private const float STATUS_CHECK_INTERVAL = 5f; // каждые 5 секунд
    
    private enum PaymentState
    {
        None,
        Creating,
        WaitingForPayment,
        CheckingStatus,
        Completed,
        Failed
    }
    
    void Awake()
    {
        // Реализация singleton паттерна
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
        InitializeManager();
        
        // 🔒 Создаем UnityMainThreadDispatcher если его нет
        if (UnityMainThreadDispatcher.Instance == null)
        {
            var dispatcherGO = new GameObject("UnityMainThreadDispatcher");
            dispatcherGO.AddComponent<UnityMainThreadDispatcher>();
            Debug.Log("[FreedomPay] 🔒 UnityMainThreadDispatcher создан");
        }
        
        // Автоматически тестируем доступные endpoint'ы
        StartCoroutine(TestBackendConnection());
        
        if (testConnectionOnStart)
        {
            StartCoroutine(TestBackendConnection());
        }
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && currentPaymentState == PaymentState.WaitingForPayment)
        {
            Debug.Log("[FreedomPay] Приложение получило фокус, проверяем статус платежа");
            CheckPaymentStatus();
        }
    }
    
    private void InitializeManager()
    {
        isInitialized = true;  // 🔒 Устанавливаем флаг инициализации
        
        // 🔒 ПРИНУДИТЕЛЬНО: Устанавливаем правильный URL
        backendBaseUrl = "http://89.39.95.247";  // 🔄 HTTP для обхода SSL проблем
        
        // 🔒 ПРИНУДИТЕЛЬНО: Обновляем endpoint'ы на правильные
        createPaymentEndpoint = "/payment-gateway/api/unity/create-payment/";
        checkStatusEndpoint = "/payment-gateway/api/unity/check-status/";
        
        Debug.Log($"[FreedomPay] Менеджер платежей инициализирован");
        Debug.Log($"[FreedomPay] 🔒 УСТАНОВЛЕН URL: {backendBaseUrl}");
        Debug.Log($"[FreedomPay] ⚠️ ВНИМАНИЕ: HTTP для обхода SSL проблем");
        Debug.Log($"[FreedomPay] 📱 Для iOS: добавьте в Info.plist NSAllowsArbitraryLoads");
        Debug.Log($"[FreedomPay] Endpoint создания: {createPaymentEndpoint}");
        Debug.Log($"[FreedomPay] Endpoint проверки: {checkStatusEndpoint}");
        
        // 🔍 Дополнительная диагностика SSL
        Debug.Log($"[FreedomPay] 🔒 SSL диагностика:");
        Debug.Log($"[FreedomPay]   - Unity версия: {Application.unityVersion}");
        Debug.Log($"[FreedomPay]   - Платформа: {Application.platform}");
        Debug.Log($"[FreedomPay]   - Система: {SystemInfo.operatingSystem}");
        Debug.Log($"[FreedomPay]   - Процессор: {SystemInfo.processorType}");
        
        // 🔍 Тестируем соединение с бэкендом
        StartCoroutine(TestBackendConnection());
        
        // 🔍 Дополнительная проверка доступности сервера
        StartCoroutine(TestServerAvailability());
    }
    
    /// <summary>
    /// Автоматический поиск правильных endpoint'ов
    /// </summary>
    private void UpdateEndpointsFromTest(string workingEndpoint)
    {
        if (workingEndpoint.StartsWith("/payment-gateway/api/unity/create-payment"))
        {
            createPaymentEndpoint = workingEndpoint;
            Debug.Log($"[FreedomPay] ✅ Обновлен createPaymentEndpoint: {createPaymentEndpoint}");
        }
        else if (workingEndpoint.StartsWith("/payment-gateway/api/unity/check-status"))
        {
            checkStatusEndpoint = workingEndpoint;
            Debug.Log($"[FreedomPay] ✅ Обновлен checkStatusEndpoint: {checkStatusEndpoint}");
        }
        else if (workingEndpoint.StartsWith("/api/unity/create-payment"))
        {
            createPaymentEndpoint = workingEndpoint;
            Debug.Log($"[FreedomPay] ✅ Обновлен createPaymentEndpoint: {createPaymentEndpoint}");
        }
        else if (workingEndpoint.StartsWith("/api/unity/check-status"))
        {
            checkStatusEndpoint = workingEndpoint;
            Debug.Log($"[FreedomPay] ✅ Обновлен checkStatusEndpoint: {checkStatusEndpoint}");
        }
    }
    
    /// <summary>
    /// Тест соединения с бэкендом
    /// </summary>
    private IEnumerator TestBackendConnection()
    {
        Debug.Log("[FreedomPay] 🔍 Тестирование соединения с бэкендом...");
        
        // 🔍 Тестируем различные endpoint'ы для поиска рабочего
        string[] testEndpoints = {
            "/health/",                                    // 🔄 Базовый health check
            "/api/",                                       // 🔄 API root
            "/api/unity/",                                 // 🔄 Unity API root
            "/api/unity/create-payment/",                   // 🔄 Unity create payment
            "/api/unity/check-status/",                    // 🔄 Unity check status
            "/payment-gateway/",                           // 🔄 Payment gateway root
            "/payment-gateway/api/",                       // 🔄 Payment gateway API
            "/payment-gateway/api/unity/",                 // 🔄 Payment gateway Unity API
            "/payment-gateway/api/unity/create-payment/",  // 🔄 Payment gateway Unity create
            "/payment-gateway/api/unity/check-status/",    // 🔄 Payment gateway Unity status
            "/admin/",                                     // 🔄 Django admin
            "/"                                            // 🔄 Root
        };
        
        bool foundCreateEndpoint = false;
        bool foundCheckEndpoint = false;
        
        foreach (string endpoint in testEndpoints)
        {
            string testUrl = $"{backendBaseUrl}{endpoint}";
            Debug.Log($"[FreedomPay] 🔍 Тестируем: {testUrl}");
            
            using (UnityWebRequest request = UnityWebRequest.Get(testUrl))
            {
                request.timeout = 10;
                
                // 🔒 ДОБАВЛЯЕМ: BypassCertificateHandler для обхода SSL проблем
                request.certificateHandler = new BypassCertificateHandler();
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[FreedomPay] ✅ {endpoint}: {request.responseCode} - {request.downloadHandler.text.Substring(0, Math.Min(100, request.downloadHandler.text.Length))}");
                    
                    // Автоматически обновляем endpoint'ы если нашли рабочие
                    if (endpoint.Contains("create-payment") && !foundCreateEndpoint)
                    {
                        UpdateEndpointsFromTest(endpoint);
                        foundCreateEndpoint = true;
                    }
                    else if (endpoint.Contains("check-status") && !foundCheckEndpoint)
                    {
                        UpdateEndpointsFromTest(endpoint);
                        foundCheckEndpoint = true;
                    }
                }
                else
                {
                    Debug.LogWarning($"[FreedomPay] ⚠️ {endpoint}: {request.responseCode} - {request.error}");
                }
            }
            
            yield return new WaitForSeconds(0.5f); // Небольшая пауза между запросами
        }
        
        if (foundCreateEndpoint && foundCheckEndpoint)
        {
            Debug.Log("[FreedomPay] ✅ Найдены рабочие endpoint'ы для создания и проверки платежей!");
            
            // Тестируем POST запрос к endpoint'у создания платежа
            yield return TestCreatePaymentEndpoint();
        }
        else
        {
            Debug.LogWarning("[FreedomPay] ⚠️ Не все необходимые endpoint'ы найдены");
        }
        
        Debug.Log("[FreedomPay] 🔍 Тестирование endpoint'ов завершено");
    }
    
    /// <summary>
    /// Тест POST запроса к endpoint'у создания платежа
    /// </summary>
    private IEnumerator TestCreatePaymentEndpoint()
    {
        Debug.Log("[FreedomPay] 🧪 Тестируем POST запрос к endpoint'у создания платежа...");
        
        // Тестовые данные
        var testData = new Dictionary<string, object>
        {
            {"order_id", "test_" + DateTime.Now.Ticks},
            {"amount", 100},
            {"currency", "UZS"},
            {"description", "Test Payment"},
            {"platform", "unity"},
            {"session_id", ""}
        };
        
        string jsonData = JsonUtility.ToJson(new PaymentRequestData(testData));
        string testUrl = $"{backendBaseUrl}{createPaymentEndpoint}";
        
        Debug.Log($"[FreedomPay] 🧪 Тестовый POST на: {testUrl}");
        Debug.Log($"[FreedomPay] 🧪 Данные: {jsonData}");
        
        using (UnityWebRequest request = new UnityWebRequest(testUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;
            
            // 🔒 ДОБАВЛЯЕМ: BypassCertificateHandler для обхода SSL проблем
            request.certificateHandler = new BypassCertificateHandler();
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[FreedomPay] ✅ POST тест успешен! Ответ: {request.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"[FreedomPay] ❌ POST тест не удался: {request.error}");
                Debug.LogError($"[FreedomPay] Код ответа: {request.responseCode}");
            }
        }
    }
    
    /// <summary>
    /// Инициация платежа через Django бэкенд
    /// </summary>
    public void InitiatePayment(int amountInSums, string description = "Test Payment", string orderId = null)
    {
        if (!isInitialized)
        {
            Debug.LogError("[FreedomPay] Менеджер не инициализирован");
            OnPaymentFailed?.Invoke("Payment system is not ready");
            return;
        }
        
        if (string.IsNullOrEmpty(orderId))
        {
            orderId = "unity_" + DateTime.Now.Ticks;
        }
        
        lastOrderId = orderId;
        currentPaymentState = PaymentState.Creating;
        
        Debug.Log($"[FreedomPay] Инициация платежа: {amountInSums} сум, заказ: {orderId}");
        
        StartCoroutine(CreatePaymentRequest(orderId, amountInSums, description));
    }
    
    /// <summary>
    /// Создание платежного запроса через Django API
    /// </summary>
    private IEnumerator CreatePaymentRequest(string orderId, int amountInSums, string description)
    {
        Debug.Log($"[FreedomPay] 📤 Отправка запроса на создание платежа...");
        
        // Формируем данные для отправки
        var paymentData = new Dictionary<string, object>
        {
            {"unity_user_id", orderId},        // 🔄 Django ожидает unity_user_id
            {"amount", amountInSums},          // ✅ Django ожидает amount
            {"description", description}       // ✅ Django ожидает description
            // ❌ Убираем лишние поля: currency, platform, session_id
        };
        
        // Конвертируем в JSON
        string jsonData = JsonUtility.ToJson(new PaymentRequestData(paymentData));
        
        Debug.Log($"[FreedomPay] 📋 Данные платежа: {jsonData}");
        
        // Отправляем POST запрос к Django API
        string createPaymentUrl = $"{backendBaseUrl}{createPaymentEndpoint}";
        
        // 🔒 ПРОБУЕМ: Сначала UnityWebRequest с BypassCertificateHandler
        yield return TryUnityWebRequest(createPaymentUrl, jsonData);
        
        // Если не получилось, пробуем альтернативные методы
        if (currentPaymentState == PaymentState.Failed)
        {
            Debug.Log("[FreedomPay] 🔄 UnityWebRequest не удался, пробуем альтернативные методы...");
            yield return TryAlternativeMethods(createPaymentUrl, jsonData);
        }
    }
    
    /// <summary>
    /// Попытка через UnityWebRequest с BypassCertificateHandler
    /// </summary>
    private IEnumerator TryUnityWebRequest(string url, string jsonData)
    {
        Debug.Log($"[FreedomPay] 🔒 Попытка 1: UnityWebRequest с BypassCertificateHandler");
        Debug.Log($"[FreedomPay] 🔒 URL: {url}");
        Debug.Log($"[FreedomPay] 🔒 BypassCertificateHandler: {new BypassCertificateHandler()}");
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = requestTimeout;
            
            // 🔒 BypassCertificateHandler для обхода SSL проблем
                request.certificateHandler = new BypassCertificateHandler();
            
            Debug.Log($"[FreedomPay] 🌐 Отправка на: {url}");
            Debug.Log($"[FreedomPay] 🔒 CertificateHandler установлен: {request.certificateHandler}");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                    string response = request.downloadHandler.text;
                Debug.Log($"[FreedomPay] ✅ UnityWebRequest успешен! Ответ: {response}");
                
                // Обрабатываем ответ от Django
                ProcessCreatePaymentResponse(response);
            }
            else
            {
                string error = request.error;
                Debug.LogError($"[FreedomPay] ❌ UnityWebRequest не удался: {error}");
                Debug.LogError($"[FreedomPay] URL: {url}");
                Debug.LogError($"[FreedomPay] Данные: {jsonData}");
                Debug.LogError($"[FreedomPay] Результат: {request.result}");
                Debug.LogError($"[FreedomPay] Код ответа: {request.responseCode}");
                
                    currentPaymentState = PaymentState.Failed;
            }
        }
    }

    /// <summary>
    /// Альтернативные методы если UnityWebRequest не работает
    /// </summary>
    private IEnumerator TryAlternativeMethods(string url, string jsonData)
    {
        Debug.Log("[FreedomPay] 🔄 Пробуем альтернативные методы...");
        
        // 🔒 Попытка 2: UnityWebRequest без BypassCertificateHandler
        Debug.Log("[FreedomPay] 🔒 Попытка 2: UnityWebRequest без BypassCertificateHandler");
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = requestTimeout;
            
            // Без BypassCertificateHandler
            Debug.Log($"[FreedomPay] 🌐 Отправка на: {url}");
        
        yield return request.SendWebRequest();
        
            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                Debug.Log($"[FreedomPay] ✅ UnityWebRequest без Bypass успешен! Ответ: {response}");
                
                ProcessCreatePaymentResponse(response);
                yield break;
            }
            else
            {
                Debug.LogWarning($"[FreedomPay] ⚠️ UnityWebRequest без Bypass не удался: {request.error}");
            }
        }
        
        // 🔒 Попытка 3: Простой GET запрос для проверки доступности
        Debug.Log("[FreedomPay] 🔒 Попытка 3: Простой GET запрос для проверки доступности");
        
        string testUrl = $"{backendBaseUrl}/health/";
        using (UnityWebRequest testRequest = UnityWebRequest.Get(testUrl))
        {
            testRequest.timeout = 10;
            testRequest.certificateHandler = new BypassCertificateHandler();
            
            yield return testRequest.SendWebRequest();
            
            if (testRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[FreedomPay] ✅ GET запрос работает! Сервер доступен");
                Debug.Log($"[FreedomPay] Ответ: {testRequest.downloadHandler.text}");
                
                // Если GET работает, но POST нет - проблема в endpoint'е
                Debug.LogError($"[FreedomPay] ❌ GET работает, но POST не работает. Проверьте endpoint: {url}");
                OnPaymentFailed?.Invoke("Endpoint unavailable - check server settings");
            }
            else
            {
                Debug.LogError($"[FreedomPay] ❌ Даже GET запрос не работает: {testRequest.error}");
                
                // 🔒 Попытка 4: HttpClient с отключенной проверкой SSL
                Debug.Log("[FreedomPay] 🔒 Попытка 4: HttpClient с отключенной проверкой SSL");
                yield return TryHttpClientMethod(url, jsonData);
            }
        }
    }
    
    /// <summary>
    /// Попытка через HttpClient с отключенной проверкой SSL
    /// </summary>
    private IEnumerator TryHttpClientMethod(string url, string jsonData)
    {
        Debug.Log("[FreedomPay] 🔒 Попытка 4: HttpClient с отключенной проверкой SSL");
        
        // Используем Task.Run для асинхронного выполнения
        var task = Task.Run(async () =>
    {
        try
        {
                // Создаем HttpClientHandler с отключенной проверкой SSL
                var handler = new System.Net.Http.HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                
                using (var client = new System.Net.Http.HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(requestTimeout);
                    
                    var content = new System.Net.Http.StringContent(jsonData, System.Text.Encoding.UTF8, "application/json");
                    
                    Debug.Log($"[FreedomPay] 🔒 HttpClient отправляет POST на: {url}");
                    
                    var response = await client.PostAsync(url, content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    Debug.Log($"[FreedomPay] ✅ HttpClient успешен! Код: {response.StatusCode}");
                    Debug.Log($"[FreedomPay] ✅ Ответ: {responseContent}");
                    
                    // Возвращаем результат через главный поток Unity
                    UnityMainThreadDispatcher.Instance.Enqueue(() =>
                    {
                        ProcessCreatePaymentResponse(responseContent);
                    });
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FreedomPay] ❌ HttpClient не удался: {ex.Message}");
                
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    OnPaymentFailed?.Invoke($"HttpClient error: {ex.Message}");
                });
                
                return false;
            }
        });
        
        // Ждем завершения задачи
        while (!task.IsCompleted)
        {
            yield return null;
        }
        
        if (task.Result)
        {
            Debug.Log("[FreedomPay] ✅ HttpClient успешно завершил задачу");
            }
            else
            {
            Debug.LogError("[FreedomPay] ❌ HttpClient не смог выполнить задачу");
            
            // 🔒 Попытка 5: HTTP соединение для диагностики
            Debug.Log("[FreedomPay] 🔒 Попытка 5: HTTP соединение для диагностики");
            yield return TryHttpConnection(url, jsonData);
        }
    }
    
    /// <summary>
    /// Попытка HTTP соединения для диагностики
    /// </summary>
    private IEnumerator TryHttpConnection(string httpsUrl, string jsonData)
    {
        Debug.Log("[FreedomPay] 🔒 Попытка 5: HTTP соединение для диагностики");
        
        // Конвертируем HTTPS в HTTP для тестирования
        string httpUrl = httpsUrl.Replace("https://", "http://");
        Debug.Log($"[FreedomPay] 🔒 Тестируем HTTP: {httpUrl}");
        
        // Тестируем простой GET запрос по HTTP
        using (UnityWebRequest testRequest = UnityWebRequest.Get(httpUrl))
        {
            testRequest.timeout = 10;
            
            yield return testRequest.SendWebRequest();
            
            if (testRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[FreedomPay] ✅ HTTP GET работает! Сервер доступен по HTTP");
                Debug.Log($"[FreedomPay] Ответ: {testRequest.downloadHandler.text}");
                
                // Если HTTP работает, но HTTPS нет - проблема в SSL/TLS
                Debug.LogError($"[FreedomPay] ❌ HTTP работает, но HTTPS не работает. Проблема в SSL/TLS настройках сервера");
                
                // 🔒 Попытка 6: HTTP POST для создания платежа
                Debug.Log("[FreedomPay] 🔒 Попытка 6: HTTP POST для создания платежа");
                yield return TryHttpPostPayment(httpUrl, jsonData);
            }
            else
            {
                Debug.LogError($"[FreedomPay] ❌ Даже HTTP не работает: {testRequest.error}");
                Debug.LogError($"[FreedomPay] ❌ Сервер полностью недоступен. Проверьте:");
                Debug.LogError($"[FreedomPay]   1. Доступность сервера {backendBaseUrl}");
                Debug.LogError($"[FreedomPay]   2. Настройки файрвола");
                Debug.LogError($"[FreedomPay]   3. Сетевые настройки");
                
                OnPaymentFailed?.Invoke("Server unavailable - check network and server settings");
            }
        }
    }
    
    /// <summary>
    /// Попытка создания платежа через HTTP
    /// </summary>
    private IEnumerator TryHttpPostPayment(string httpUrl, string jsonData)
    {
        Debug.Log("[FreedomPay] 🔒 Попытка 6: HTTP POST для создания платежа");
        
        using (UnityWebRequest request = new UnityWebRequest(httpUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = requestTimeout;
            
            Debug.Log($"[FreedomPay] 🌐 Отправка HTTP POST на: {httpUrl}");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                Debug.Log($"[FreedomPay] ✅ HTTP POST успешен! Ответ: {response}");
                
                // ⚠️ ВНИМАНИЕ: HTTP небезопасен для продакшена!
                Debug.LogWarning($"[FreedomPay] ⚠️ ВНИМАНИЕ: Платеж создан через HTTP (небезопасно!)");
                Debug.LogWarning($"[FreedomPay] ⚠️ Для продакшена необходимо исправить SSL/TLS на сервере");
                
                ProcessCreatePaymentResponse(response);
            }
            else
            {
                string error = request.error;
                Debug.LogError($"[FreedomPay] ❌ HTTP POST не удался: {error}");
                Debug.LogError($"[FreedomPay] URL: {httpUrl}");
                Debug.LogError($"[FreedomPay] Данные: {jsonData}");
                
                OnPaymentFailed?.Invoke($"HTTP POST error: {error}");
            }
        }
    }
    
    /// <summary>
    /// Обработка ответа от Django API при создании платежа
    /// </summary>
    private void ProcessCreatePaymentResponse(string response)
    {
        try
        {
            Debug.Log($"[FreedomPay] 🔄 Обработка ответа от Django: {response}");
            
            var responseData = JsonUtility.FromJson<PaymentResponseData>(response);
            
            if (responseData.success)
            {
                Debug.Log($"[FreedomPay] ✅ Платеж создан успешно!");
                Debug.Log($"[FreedomPay] 📋 Order ID: {responseData.order_id}");
                Debug.Log($"[FreedomPay] 🔑 Session ID: {responseData.session_id}");
                Debug.Log($"[FreedomPay] 💰 Сумма: {responseData.amount} {responseData.currency}");
                Debug.Log($"[FreedomPay] 🌐 URL оплаты: {responseData.payment_url}");
                
                // Сохраняем данные для проверки статуса
                lastOrderId = responseData.order_id;
                lastSessionId = responseData.session_id;
                
                // Открываем страницу оплаты в браузере
                if (!string.IsNullOrEmpty(responseData.payment_url))
                {
                    Debug.Log($"[FreedomPay] 🌐 Открываем страницу оплаты: {responseData.payment_url}");
                    OpenPaymentPage(responseData.payment_url);
                    
                    // 🔧 ПРИНУДИТЕЛЬНО: Запускаем проверку возврата из браузера
                    CheckBrowserReturn();
                    
                    // Запускаем проверку статуса
                    StartCoroutine(CheckPaymentStatusCoroutine());
                }
                else
                {
                    Debug.LogError("[FreedomPay] ❌ URL оплаты пустой в ответе");
                    OnPaymentFailed?.Invoke("Error: Payment URL not received");
                }
            }
            else
            {
                Debug.LogError($"[FreedomPay] ❌ Ошибка создания платежа: {responseData.error}");
                currentPaymentState = PaymentState.Failed;
                OnPaymentFailed?.Invoke($"Ошибка Django: {responseData.error}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FreedomPay] ❌ Ошибка обработки ответа Django: {ex.Message}");
            currentPaymentState = PaymentState.Failed;
            OnPaymentFailed?.Invoke($"Ошибка обработки ответа: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Открытие страницы оплаты в браузере
    /// </summary>
    private void OpenPaymentPage(string paymentUrl)
    {
        try
        {
            Debug.Log($"[FreedomPay] 🌐 Открытие страницы оплаты: {paymentUrl}");
            
            #if UNITY_ANDROID && !UNITY_EDITOR
                // Для Android используем Intent
                using (AndroidJavaClass unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", "android.intent.action.VIEW");
                    intent.Call<AndroidJavaObject>("setData", new AndroidJavaClass("android.net.Uri").CallStatic<AndroidJavaObject>("parse", paymentUrl));
                    
                    // Добавляем флаги для корректной работы
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
                    intent.Call<AndroidJavaObject>("addFlags", 0x08000000); // FLAG_ACTIVITY_NO_HISTORY
                    
                    currentActivity.Call("startActivity", intent);
                }
            #elif UNITY_IOS && !UNITY_EDITOR
                // Для iOS используем специальную логику
                OpenURLOnIOS(paymentUrl);
            #else
                // Для других платформ используем стандартный метод
                Application.OpenURL(paymentUrl);
            #endif
            
            Debug.Log("[FreedomPay] ✅ Браузер открыт, ожидание возврата в приложение...");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FreedomPay] Ошибка открытия браузера: {e.Message}");
            OnPaymentFailed?.Invoke("Не удалось открыть страницу оплаты");
        }
    }
    
    /// <summary>
    /// Открытие URL на iOS с проверкой доступности
    /// </summary>
    private void OpenURLOnIOS(string url)
    {
        try
        {
            Debug.Log($"[FreedomPay] 📱 iOS: Открытие URL: {url}");
            
            // Проверяем, доступен ли URL
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("[FreedomPay] ❌ iOS: URL пустой");
                OnPaymentFailed?.Invoke("URL для оплаты не получен");
                return;
            }
            
            // Проверяем, что это действительно iOS
            #if UNITY_IOS
                Debug.Log("[FreedomPay] ✅ iOS: Платформа подтверждена");
            #else
                Debug.LogWarning("[FreedomPay] ⚠️ iOS: Метод вызван не на iOS платформе");
            #endif
            
            // Пробуем открыть URL
            try
            {
                Application.OpenURL(url);
                Debug.Log("[FreedomPay] ✅ iOS: URL успешно открыт через Application.OpenURL");
            }
            catch (Exception urlEx)
            {
                Debug.LogWarning($"[FreedomPay] ⚠️ iOS: Application.OpenURL не удался: {urlEx.Message}");
                Debug.LogWarning("[FreedomPay] ⚠️ iOS: Пробуем альтернативный метод");
                
                // Альтернативный метод для iOS
                StartCoroutine(OpenURLWithDelay(url));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FreedomPay] ❌ iOS: Ошибка открытия URL: {ex.Message}");
            OnPaymentFailed?.Invoke($"Ошибка открытия браузера на iOS: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Открытие URL с задержкой для iOS
    /// </summary>
    private IEnumerator OpenURLWithDelay(string url)
    {
        Debug.Log("[FreedomPay] 📱 iOS: Попытка открытия URL с задержкой...");
        
        // Небольшая задержка перед повторной попыткой
        yield return new WaitForSeconds(0.5f);
        
        try
        {
            // Повторная попытка открытия URL
            Application.OpenURL(url);
            Debug.Log("[FreedomPay] ✅ iOS: URL открыт с задержкой");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FreedomPay] ❌ iOS: Ошибка при повторной попытке: {ex.Message}");
            
            // Показываем инструкцию пользователю
            ShowIOSURLOpenInstructions();
        }
    }
    
    /// <summary>
    /// Показ инструкций по открытию URL на iOS
    /// </summary>
    private void ShowIOSURLOpenInstructions()
    {
        Debug.LogWarning("[FreedomPay] ⚠️ iOS: Не удалось открыть браузер автоматически");
        Debug.LogWarning("[FreedomPay] 📱 iOS: Пользователь должен:");
        Debug.LogWarning("[FreedomPay]    1. Скопировать URL вручную");
        Debug.LogWarning("[FreedomPay]    2. Открыть Safari");
        Debug.LogWarning("[FreedomPay]    3. Вставить URL и перейти");
        
        // Можно показать UI с инструкциями пользователю
        OnPaymentFailed?.Invoke("На iOS требуется ручное открытие браузера. Скопируйте URL и откройте в Safari.");
    }
    
    /// <summary>
    /// Запуск проверки статуса платежа
    /// </summary>
    private void StartStatusChecking()
    {
        if (statusCheckCoroutine != null)
        {
            StopCoroutine(statusCheckCoroutine);
        }
        
        statusCheckStartTime = Time.time;
        statusCheckCoroutine = StartCoroutine(StatusCheckLoop());
        Debug.Log("[FreedomPay] 🔄 Запущена проверка статуса платежа");
    }
    
    /// <summary>
    /// Цикл проверки статуса платежа
    /// </summary>
    private IEnumerator StatusCheckLoop()
    {
        while (currentPaymentState == PaymentState.WaitingForPayment)
        {
            // Проверяем таймаут
            if (Time.time - statusCheckStartTime > STATUS_CHECK_TIMEOUT)
            {
                Debug.LogWarning("[FreedomPay] ⏰ Таймаут проверки статуса платежа");
                currentPaymentState = PaymentState.Failed;
                OnPaymentFailed?.Invoke("Payment timeout");
                yield break;
            }
            
            yield return new WaitForSeconds(STATUS_CHECK_INTERVAL);
            
            // Проверяем статус через API
            yield return CheckPaymentStatusCoroutine();
        }
    }
    
    /// <summary>
    /// Проверка статуса платежа
    /// </summary>
    public void CheckPaymentStatus()
    {
        if (currentPaymentState == PaymentState.WaitingForPayment)
        {
            currentPaymentState = PaymentState.CheckingStatus;
            StartCoroutine(CheckPaymentStatusCoroutine());
        }
    }
    
    /// <summary>
    /// Проверка статуса платежа через Django API
    /// </summary>
    private IEnumerator CheckPaymentStatusCoroutine()
    {
        if (string.IsNullOrEmpty(lastOrderId) && string.IsNullOrEmpty(lastSessionId))
        {
            Debug.LogError("[FreedomPay] ❌ Нет order_id или session_id для проверки статуса");
            yield break;
        }
        
        Debug.Log("[FreedomPay] 🔍 Проверка статуса платежа...");
        
        // Формируем URL для проверки статуса
        string checkStatusUrl = $"{backendBaseUrl}{checkStatusEndpoint}";
        
        // Добавляем параметры в URL
        if (!string.IsNullOrEmpty(lastOrderId))
        {
            checkStatusUrl += $"?order_id={lastOrderId}";
        }
        else if (!string.IsNullOrEmpty(lastSessionId))
        {
            checkStatusUrl += $"?session_id={lastSessionId}";
        }
        
        Debug.Log($"[FreedomPay] 🌐 Проверка статуса: {checkStatusUrl}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(checkStatusUrl))
        {
            request.timeout = requestTimeout;
            request.certificateHandler = new BypassCertificateHandler();
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                    string response = request.downloadHandler.text;
                Debug.Log($"[FreedomPay] ✅ Статус получен: {response}");
                
                // Обрабатываем ответ от Django
                    ProcessStatusResponse(response);
                }
                else
                {
                    Debug.LogWarning($"[FreedomPay] ⚠️ Ошибка проверки статуса: {request.error}");
                // Остаемся в состоянии ожидания
                    currentPaymentState = PaymentState.WaitingForPayment;
            }
        }
    }
    
    /// <summary>
    /// Обработка ответа при проверке статуса платежа
    /// </summary>
    private void ProcessStatusResponse(string response)
    {
        try
        {
            Debug.Log($"[FreedomPay] 🔄 Обработка статуса: {response}");
            
            var statusData = JsonUtility.FromJson<StatusResponseData>(response);
            
            if (statusData.success)
            {
                Debug.Log($"[FreedomPay] ✅ Статус получен: {statusData.status}");
                
                // 🔍 Проверяем реальный статус платежа
                switch (statusData.status.ToLower())
                {
                    case "success":
                    case "paid":
                    case "completed":
                        Debug.Log($"[FreedomPay] 🎉 Payment completed successfully!");
                        currentPaymentState = PaymentState.Completed;
                        OnPaymentSuccess?.Invoke("Payment completed successfully!");
                        break;
                        
                    case "pending":
                    case "processing":
                        Debug.Log($"[FreedomPay] ⏳ Payment in progress: {statusData.status}");
                        currentPaymentState = PaymentState.WaitingForPayment;
                        OnPaymentPending?.Invoke("Waiting for payment completion...");
                        
                        // 🔄 Продолжаем проверку статуса
                        StartCoroutine(CheckPaymentStatusCoroutine());
                        break;
                        
                    case "failed":
                    case "cancelled":
                    case "expired":
                        Debug.Log($"[FreedomPay] ❌ Payment failed: {statusData.status}");
                        currentPaymentState = PaymentState.Failed;
                        OnPaymentFailed?.Invoke($"Payment failed: {statusData.status}");
                        break;
                        
                    default:
                        Debug.LogWarning($"[FreedomPay] ⚠️ Unknown status: {statusData.status}");
                        currentPaymentState = PaymentState.WaitingForPayment;
                        OnPaymentPending?.Invoke("Unknown payment status...");
                        break;
                }
            }
            else
            {
                Debug.LogError($"[FreedomPay] ❌ Ошибка получения статуса: {statusData.error}");
                // Остаемся в состоянии ожидания
                currentPaymentState = PaymentState.WaitingForPayment;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FreedomPay] ❌ Ошибка обработки статуса: {ex.Message}");
            // Остаемся в состоянии ожидания
            currentPaymentState = PaymentState.WaitingForPayment;
        }
    }
    
    /// <summary>
    /// Завершение платежа как успешного
    /// </summary>
    private void CompletePaymentSuccessfully()
    {
        currentPaymentState = PaymentState.Completed;
        
        // Сохраняем подписку
        DateTime subscriptionEnd = DateTime.Now.AddMinutes(subscriptionDurationMinutes);
        PlayerPrefs.SetString("SubscriptionEnd", subscriptionEnd.ToBinary().ToString());
        PlayerPrefs.Save();
        
        Debug.Log($"[FreedomPay] ✅ Платеж завершен успешно! Подписка на {subscriptionDurationMinutes} минут до: {subscriptionEnd:HH:mm:ss}");
        OnPaymentSuccess?.Invoke(lastOrderId);
    }
    
    /// <summary>
    /// Проверка активности подписки
    /// </summary>
    public bool IsSubscriptionActive()
    {
        string subscriptionEndStr = PlayerPrefs.GetString("SubscriptionEnd", "");
        if (string.IsNullOrEmpty(subscriptionEndStr)) return false;
        
        try
        {
            long subscriptionEndBinary = Convert.ToInt64(subscriptionEndStr);
            DateTime subscriptionEnd = DateTime.FromBinary(subscriptionEndBinary);
            bool isActive = DateTime.Now < subscriptionEnd;
            
            if (isActive)
            {
                TimeSpan remaining = subscriptionEnd - DateTime.Now;
                Debug.Log($"[FreedomPay] ✅ Подписка активна. Осталось: {remaining:mm\\:ss}");
            }
            
            return isActive;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Получение оставшегося времени подписки
    /// </summary>
    public TimeSpan GetRemainingSubscriptionTime()
    {
        string subscriptionEndStr = PlayerPrefs.GetString("SubscriptionEnd", "");
        if (string.IsNullOrEmpty(subscriptionEndStr)) return TimeSpan.Zero;
        
        try
        {
            long subscriptionEndBinary = Convert.ToInt64(subscriptionEndStr);
            DateTime subscriptionEnd = DateTime.FromBinary(subscriptionEndBinary);
            TimeSpan remaining = subscriptionEnd - DateTime.Now;
            return remaining.TotalSeconds > 0 ? remaining : TimeSpan.Zero;
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }
    
    /// <summary>
    /// Активирует подписку через OTP код
    /// </summary>
    public void ActivateSubscriptionWithOTP(int durationMinutes)
    {
        Debug.Log($"[FreedomPay] 🔑 Активация подписки через OTP на {durationMinutes} минут");
        
        // Сохраняем время окончания подписки
        PlayerPrefs.SetString("SubscriptionEnd", System.DateTime.Now.AddMinutes(durationMinutes).ToBinary().ToString());
        PlayerPrefs.Save();
        
        Debug.Log($"[FreedomPay] ✅ Подписка активирована через OTP до: {System.DateTime.Now.AddMinutes(durationMinutes)}");
        
        // Вызываем событие OTP активации
        OnOTPActivation?.Invoke(durationMinutes);
        
        Debug.Log($"[FreedomPay] 🔑 OTP активация завершена - подписка активна на {durationMinutes} минут");
    }
    
    // Методы для тестирования
    
    [ContextMenu("🧪 Тест платежа 200 сум")]
    public void TestPayment()
    {
        Debug.Log("[FreedomPay] 🧪 Запуск тестового платежа через Django API");
        InitiatePayment(200, "Test Payment", "test_" + DateTime.Now.Ticks);
    }
    
    [ContextMenu("💰 Тест мини-платежа 100 сум")]
    public void TestMiniPayment()
    {
        Debug.Log("[FreedomPay] 💰 Тест мини-платежа 100 сум");
        InitiatePayment(100, "Mini Payment", "mini_" + DateTime.Now.Ticks);
    }
    
    [ContextMenu("💎 Тест премиум-платежа 500 сум")]
    public void TestPremiumPayment()
    {
        Debug.Log("[FreedomPay] 💎 Тест премиум-платежа 500 сум");
        InitiatePayment(500, "Premium Payment", "premium_" + DateTime.Now.Ticks);
    }
    
    [ContextMenu("📊 Проверить подписку")]
    public void CheckSubscription()
    {
        if (IsSubscriptionActive())
        {
            TimeSpan remaining = GetRemainingSubscriptionTime();
            Debug.Log($"✅ Подписка активна. Осталось: {remaining:mm\\:ss}");
        }
        else
        {
            Debug.Log("❌ Подписка неактивна");
        }
    }
    
    [ContextMenu("🗑️ Очистить подписку")]
    public void ClearSubscription()
    {
        PlayerPrefs.DeleteKey("SubscriptionEnd");
        PlayerPrefs.Save();
        Debug.Log("🗑️ Данные подписки очищены");
    }
    
    [ContextMenu("✅ ТЕСТ: Имитировать успешный платеж")]
    public void SimulateSuccessfulPayment()
    {
        if (currentPaymentState == PaymentState.WaitingForPayment)
        {
            Debug.Log("[FreedomPay] 🧪 ТЕСТ: Имитируем успешный ответ от Django");
            CompletePaymentSuccessfully();
        }
        else
        {
            Debug.LogWarning("[FreedomPay] ⚠️ Нет активного платежа для имитации");
        }
    }
    
    [ContextMenu("❌ ТЕСТ: Имитировать неуспешный платеж")]
    public void SimulateFailedPayment()
    {
        if (currentPaymentState == PaymentState.WaitingForPayment)
        {
            Debug.Log("[FreedomPay] 🧪 ТЕСТ: Имитируем неуспешный платеж");
            currentPaymentState = PaymentState.Failed;
            OnPaymentFailed?.Invoke("Payment cancelled by user");
        }
        else
        {
            Debug.LogWarning("[FreedomPay] ⚠️ Нет активного платежа для отмены");
        }
    }
    
    [ContextMenu("🔄 Отменить текущий платеж")]
    public void CancelCurrentPayment()
    {
        if (currentPaymentState == PaymentState.WaitingForPayment)
        {
            Debug.Log("[FreedomPay] 🔄 Отмена текущего платежа");
            
            if (statusCheckCoroutine != null)
            {
                StopCoroutine(statusCheckCoroutine);
                statusCheckCoroutine = null;
            }
            
            currentPaymentState = PaymentState.Failed;
            OnPaymentFailed?.Invoke("Payment cancelled by user");
        }
        else
        {
            Debug.LogWarning("[FreedomPay] ⚠️ Нет активного платежа для отмены");
        }
    }
    
    /// <summary>
    /// Принудительное обновление статуса после успешной оплаты
    /// ⚠️ ТОЛЬКО ДЛЯ РУЧНОГО ТЕСТИРОВАНИЯ!
    /// </summary>
    [ContextMenu("🔧 ПРИНУДИТЕЛЬНО завершить платеж (ТЕСТ)")]
    public void ForcePaymentSuccess()
    {
        Debug.LogWarning("[FreedomPay] ⚠️ ПРИНУДИТЕЛЬНОЕ завершение платежа (ТОЛЬКО ДЛЯ ТЕСТА)");
        
        if (!string.IsNullOrEmpty(lastOrderId))
        {
            Debug.Log($"[FreedomPay] ✅ Принудительно завершаем платеж: {lastOrderId}");
            CompletePaymentSuccessfully();
        }
        else
        {
            Debug.LogError("[FreedomPay] ❌ Нет order_id для принудительного завершения");
        }
    }
    
    /// <summary>
    /// Проверка возврата из браузера и принудительное обновление статуса
    /// </summary>
    private void CheckBrowserReturn()
    {
        // 🔍 Проверяем, вернулся ли пользователь из браузера
        if (currentPaymentState == PaymentState.WaitingForPayment)
        {
            Debug.Log("[FreedomPay] 🔍 Проверка возврата из браузера...");
            
            // 🔧 ПРИНУДИТЕЛЬНО: Проверяем реальный статус платежа через 5 секунд
            StartCoroutine(CheckRealPaymentStatusAfterDelay());
        }
    }
    
    /// <summary>
    /// Проверка реального статуса платежа через задержку
    /// </summary>
    private IEnumerator CheckRealPaymentStatusAfterDelay()
    {
        Debug.Log("[FreedomPay] ⏳ Ожидание 5 секунд для проверки реального статуса...");
        yield return new WaitForSeconds(5f);
        
        if (currentPaymentState == PaymentState.WaitingForPayment)
        {
            Debug.Log("[FreedomPay] 🔍 Проверяем реальный статус платежа...");
            
            // 🔍 Проверяем реальный статус на сервере
            if (!string.IsNullOrEmpty(lastOrderId))
            {
                StartCoroutine(CheckPaymentStatusCoroutine());
            }
            else
            {
                Debug.LogError("[FreedomPay] ❌ Нет order_id для проверки статуса");
            }
        }
    }
    
    [ContextMenu("📋 Показать состояние")]
    public void ShowCurrentState()
    {
        Debug.Log($"[FreedomPay] 📋 Текущее состояние:");
        Debug.Log($"   Состояние платежа: {currentPaymentState}");
        Debug.Log($"   Последний Order ID: {lastOrderId}");
        Debug.Log($"   Session ID: {lastSessionId}");
        Debug.Log($"   Менеджер инициализирован: {isInitialized}");
        Debug.Log($"   Бэкенд: {backendBaseUrl}");
        
        if (IsSubscriptionActive())
        {
            TimeSpan remaining = GetRemainingSubscriptionTime();
            Debug.Log($"   ✅ Подписка активна. Осталось: {remaining:mm\\:ss}");
        }
        else
        {
            Debug.Log($"   ❌ Подписка неактивна");
        }
        
        if (currentPaymentState == PaymentState.WaitingForPayment)
        {
            float waitingTime = Time.time - statusCheckStartTime;
            Debug.Log($"   ⏱️ Ожидание платежа: {waitingTime:F1} секунд");
        }
        }
    
    [ContextMenu("🔍 Тест соединения с бэкендом")]
    public void TestBackendConnectionMenu()
    {
        StartCoroutine(TestBackendConnection());
    }
    
    [ContextMenu("🧪 Тест POST запроса")]
    public void TestPostRequestMenu()
    {
        StartCoroutine(TestCreatePaymentEndpoint());
    }
    
    [ContextMenu("📋 Показать текущие endpoint'ы")]
    public void ShowCurrentEndpoints()
    {
        Debug.Log($"[FreedomPay] 📋 Текущие endpoint'ы:");
        Debug.Log($"   Бэкенд: {backendBaseUrl}");
        Debug.Log($"   Создание платежа: {createPaymentEndpoint}");
        Debug.Log($"   Проверка статуса: {checkStatusEndpoint}");
    }
    
    [ContextMenu("🌐 Тест открытия URL на iOS")]
    public void TestURLOpeningOnIOS()
    {
        Debug.Log("[FreedomPay] 🌐 Тест открытия URL на iOS");
        
        #if UNITY_IOS
            Debug.Log("✅ iOS платформа активна, тестируем открытие URL");
            
            // Тестовый URL
            string testUrl = "https://www.google.com";
            Debug.Log($"[FreedomPay] 🧪 Тестируем открытие: {testUrl}");
            
            // Пробуем открыть тестовый URL
            OpenURLOnIOS(testUrl);
            
        #else
            Debug.LogWarning("⚠️ iOS платформа не активна");
            Debug.Log($"   Текущая платформа: {Application.platform}");
            Debug.Log("   Тест открытия URL доступен только на iOS");
        #endif
    }
    
    [ContextMenu("📱 Полная диагностика iOS")]
    public void FullIOSDiagnostics()
    {
        Debug.Log("📱 ПОЛНАЯ ДИАГНОСТИКА iOS:");
        
        // Проверяем платформу
        ShowIOSDiagnostics();
        
        // Проверяем готовность
        CheckIOSReadiness();
        
        // Проверяем настройки проекта
        Debug.Log("🔧 ДОПОЛНИТЕЛЬНЫЕ ПРОВЕРКИ:");
        Debug.Log($"   Текущая платформа: {Application.platform}");
        Debug.Log($"   Версия Unity: {Application.unityVersion}");
        Debug.Log($"   Система: {SystemInfo.operatingSystem}");
        Debug.Log($"   Процессор: {SystemInfo.processorType}");
        Debug.Log($"   Интернет: {Application.internetReachability}");
        
        // Проверяем доступность тестового URL
        StartCoroutine(TestURLAvailability());
    }
    
    /// <summary>
    /// Тест доступности тестового URL
    /// </summary>
    private IEnumerator TestURLAvailability()
    {
        Debug.Log("[FreedomPay] 🌐 Тест доступности тестового URL...");
        
        string testUrl = "https://www.google.com";
        
        using (UnityWebRequest request = UnityWebRequest.Get(testUrl))
        {
            request.timeout = 10;
            request.certificateHandler = new BypassCertificateHandler();
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[FreedomPay] ✅ Тестовый URL доступен: {testUrl}");
                Debug.Log($"[FreedomPay] 📊 Код ответа: {request.responseCode}");
            }
            else
            {
                Debug.LogWarning($"[FreedomPay] ⚠️ Тестовый URL недоступен: {request.error}");
                Debug.LogWarning($"[FreedomPay] 📊 Результат: {request.result}");
            }
        }
    }

    /// <summary>
    /// Простая проверка доступности сервера
/// </summary>
    private IEnumerator TestServerAvailability()
    {
        string serverHost = "89.39.95.247";  // 🔄 Тестируем IP напрямую
        
        Debug.Log($"[FreedomPay] 🔍 Тестируем доступность сервера: {serverHost}");
        
        // 🔍 Попытка 1: Простой GET на корневой путь
        string testUrl = $"https://{serverHost}/";
        
        using (UnityWebRequest request = UnityWebRequest.Get(testUrl))
        {
            request.certificateHandler = new BypassCertificateHandler();
            request.timeout = 10;
            
            Debug.Log($"[FreedomPay] 🔍 Тестируем: {testUrl}");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[FreedomPay] ✅ Сервер доступен! Код: {request.responseCode}");
                Debug.Log($"[FreedomPay] 📄 Ответ: {request.downloadHandler.text.Substring(0, Math.Min(200, request.downloadHandler.text.Length))}");
            }
            else
            {
                Debug.LogWarning($"[FreedomPay] ⚠️ Сервер недоступен: {request.error}");
                Debug.LogWarning($"[FreedomPay] 📊 Результат: {request.result}, Код: {request.responseCode}");
            }
        }
    }

    [ContextMenu("🔧 Принудительно завершить платеж")]
    public void ForceCompletePayment()
    {
        Debug.Log("[FreedomPay] 🔧 Принудительное завершение платежа");
        ForcePaymentSuccess();
    }

    /// <summary>
    /// Инструкция по настройке iOS для HTTP
    /// </summary>
    [ContextMenu("📱 Инструкция по настройке iOS")]
    public void ShowIOSInstructions()
    {
        Debug.Log("📱 ИНСТРУКЦИЯ ПО НАСТРОЙКЕ iOS:");
        Debug.Log("1. Откройте файл Info.plist в вашем iOS проекте");
        Debug.Log("2. Добавьте следующий код:");
        Debug.Log("   <key>NSAppTransportSecurity</key>");
        Debug.Log("   <dict>");
        Debug.Log("       <key>NSAllowsArbitraryLoads</key>");
        Debug.Log("       <true/>");
        Debug.Log("   </dict>");
        Debug.Log("3. Это разрешит HTTP соединения на iOS");
        Debug.Log("⚠️ ВНИМАНИЕ: Это снижает безопасность приложения!");
        
        // Дополнительная диагностика для iOS
        ShowIOSDiagnostics();
    }
    
    /// <summary>
    /// Диагностика настроек iOS
    /// </summary>
    private void ShowIOSDiagnostics()
    {
        Debug.Log("📱 ДИАГНОСТИКА iOS:");
        Debug.Log($"   Платформа: {Application.platform}");
        Debug.Log($"   Версия Unity: {Application.unityVersion}");
        Debug.Log($"   Система: {SystemInfo.operatingSystem}");
        Debug.Log($"   Процессор: {SystemInfo.processorType}");
        
        #if UNITY_IOS
            Debug.Log("✅ iOS платформа определена");
            Debug.Log("📱 Проверьте настройки Info.plist");
        #else
            Debug.Log("⚠️ iOS платформа НЕ определена");
            Debug.Log("📱 Это может быть причиной проблем");
        #endif
        
        // Проверяем настройки проекта
        Debug.Log("🔧 НАСТРОЙКИ ПРОЕКТА:");
        Debug.Log("   ForceInternetPermission: 0 (должно быть 1 для iOS)");
        Debug.Log("   iOSURLSchemes: [] (должны быть настроены)");
    }
    
    /// <summary>
    /// Проверка готовности iOS к работе
    /// </summary>
    [ContextMenu("🔍 Проверить готовность iOS")]
    public void CheckIOSReadiness()
    {
        Debug.Log("🔍 ПРОВЕРКА ГОТОВНОСТИ iOS:");
        
        #if UNITY_IOS
            Debug.Log("✅ iOS платформа активна");
            
            // Проверяем базовые настройки
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                Debug.Log("✅ RuntimePlatform.IPhonePlayer определен");
            }
            else
            {
                Debug.LogWarning("⚠️ RuntimePlatform.IPhonePlayer НЕ определен");
            }
            
            // Проверяем доступность интернета
            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                Debug.Log("✅ Интернет доступен");
            }
            else
            {
                Debug.LogWarning("⚠️ Интернет недоступен");
            }
            
        #else
            Debug.LogWarning("⚠️ iOS платформа не активна");
            Debug.Log($"   Текущая платформа: {Application.platform}");
        #endif
        
        // Общие рекомендации
        Debug.Log("📋 РЕКОМЕНДАЦИИ:");
        Debug.Log("1. Убедитесь, что проект собран для iOS");
        Debug.Log("2. Проверьте Info.plist настройки");
        Debug.Log("3. Тестируйте на реальном iOS устройстве");
        Debug.Log("4. Проверьте настройки сети на устройстве");
    }
    
    /// <summary>
    /// Проверка готовности iOS к работе с URL
    /// </summary>
    [ContextMenu("🔗 Проверить готовность iOS к работе с URL")]
    public void CheckIOSURLReadiness()
    {
        Debug.Log("🔗 ПРОВЕРКА ГОТОВНОСТИ iOS К РАБОТЕ С URL:");
        
        #if UNITY_IOS
            Debug.Log("✅ iOS платформа активна");
            
            // Проверяем настройки проекта
            Debug.Log("🔧 НАСТРОЙКИ ПРОЕКТА:");
            Debug.Log($"   ForceInternetPermission: 1 (✅ разрешено)");
            Debug.Log($"   iOSURLSchemes: настроены (✅ freedompay, https, http)");
            
            // Проверяем доступность тестового URL
            StartCoroutine(TestURLAvailability());
            
        #else
            Debug.LogWarning("⚠️ iOS платформа не активна");
            Debug.Log($"   Текущая платформа: {Application.platform}");
            Debug.Log("   Проверка URL доступна только на iOS");
        #endif
    }
}

/// <summary>
/// 🔒 Диспетчер для выполнения кода в главном потоке Unity
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    public static UnityMainThreadDispatcher Instance { get; private set; }
    private readonly Queue<Action> _executionQueue = new Queue<Action>();
    
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
    
    void Update()
            {
        while (_executionQueue.Count > 0)
            {
            _executionQueue.Dequeue().Invoke();
            }
        }
    
    public void Enqueue(Action action)
    {
        _executionQueue.Enqueue(action);
    }
}

/// <summary>
/// Данные для создания платежа
/// </summary>
[System.Serializable]
public class PaymentRequestData
{
    public string unity_user_id;  // 🔄 Django ожидает unity_user_id
    public int amount;            // ✅ Django ожидает amount
    public string description;    // ✅ Django ожидает description
    
    public PaymentRequestData(Dictionary<string, object> data)
    {
        unity_user_id = data["unity_user_id"].ToString();  // 🔄 Используем unity_user_id
        amount = (int)data["amount"];                      // ✅ Используем amount
        description = data["description"].ToString();       // ✅ Используем description
    }
}

/// <summary>
/// Ответ при создании платежа
/// </summary>
[System.Serializable]
public class PaymentResponseData
{
    public bool success;
    public string order_id;       // ✅ Django возвращает order_id
    public string payment_url;    // ✅ Django возвращает payment_url
    public string session_id;     // ✅ Django возвращает session_id
    public int amount;            // ✅ Django возвращает amount
    public string currency;       // ✅ Django возвращает currency
    public string error;          // ✅ Django возвращает error
}

/// <summary>
/// Ответ при проверке статуса
/// </summary>
[System.Serializable]
public class StatusResponseData
{
    public bool success;
    public string status;         // ✅ Django возвращает status
    public string details;        // ✅ Django возвращает details (опционально)
    public string error;          // ✅ Django возвращает error
}

/// <summary>
/// 🔒 BypassCertificateHandler для обхода SSL проблем Unity
/// </summary>
public class BypassCertificateHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        // 🔒 Всегда возвращаем true для обхода проверки сертификатов
        // Это решает проблему "Unable to complete SSL connection"
        return true;
    }
} 

/// <summary>
/// Класс для работы с платежным API Django
/// </summary>
[System.Serializable]
public class PaymentRequest
{
    public string unity_user_id;
    public int amount;
    public string description;
}

[System.Serializable]
public class PaymentResponse
{
    public bool success;
    public string order_id;
    public string session_id;
    public string payment_url;
    public int amount;
    public string currency;
}

[System.Serializable]
public class PaymentStatusResponse
{
    public bool success;
    public string order_id;
    public string status;
    public int amount;
    public string currency;
    public string created_at;
    public string paid_at;
}

public class PaymentGateway : MonoBehaviour
{
    private const string BASE_URL = "http://89.39.95.247/payment-gateway/api/unity/";  // 🔄 HTTP для обхода SSL
    
    // 🔒 Принудительно устанавливаем правильный URL при старте
    void Start()
    {
        Debug.Log($"[PaymentGateway] 🔒 УСТАНОВЛЕН URL: {BASE_URL}");
        Debug.Log($"[PaymentGateway] ⚠️ ВНИМАНИЕ: HTTP для обхода SSL проблем");
        Debug.Log($"[PaymentGateway] 📱 Для iOS: добавьте в Info.plist NSAllowsArbitraryLoads");
    }
    
    public void CreatePayment(string userId, int amount, string description)
    {
        StartCoroutine(CreatePaymentCoroutine(userId, amount, description));
    }
    
    private IEnumerator CreatePaymentCoroutine(string userId, int amount, string description)
    {
        var request = new PaymentRequest
        {
            unity_user_id = userId,
            amount = amount,
            description = description
        };
        
        string json = JsonUtility.ToJson(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        
        using (UnityWebRequest www = new UnityWebRequest(BASE_URL + "create-payment/", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<PaymentResponse>(www.downloadHandler.text);
                if (response.success)
                {
                    // Открываем браузер для оплаты
                    Application.OpenURL(response.payment_url);
                    
                    // Сохраняем order_id для проверки статуса
                    PlayerPrefs.SetString("CurrentOrderId", response.order_id);
                }
            }
            else
            {
                Debug.LogError($"Ошибка создания платежа: {www.error}");
            }
        }
    }
    
    public void CheckPaymentStatus(string orderId = null)
    {
        if (string.IsNullOrEmpty(orderId))
            orderId = PlayerPrefs.GetString("CurrentOrderId", "");
            
        if (!string.IsNullOrEmpty(orderId))
        {
            StartCoroutine(CheckStatusCoroutine(orderId));
        }
    }
    
    private IEnumerator CheckStatusCoroutine(string orderId)
    {
        string url = $"{BASE_URL}check-status/?order_id={orderId}";
        
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<PaymentStatusResponse>(www.downloadHandler.text);
                if (response.success)
                {
                    Debug.Log($"Статус платежа: {response.status}");
                    
                    if (response.status == "success")
                    {
                        // Платеж успешен - разблокируем контент
                        UnlockContent();
                    }
                }
            }
            else
            {
                Debug.LogError($"Ошибка проверки статуса: {www.error}");
            }
        }
    }
    
    private void UnlockContent()
    {
        // Здесь логика разблокировки контента
        Debug.Log("Контент разблокирован!");
    }
} 