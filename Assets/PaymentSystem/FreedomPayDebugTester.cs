using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

/// <summary>
/// Диагностический тестер для отладки проблем Freedom Pay
/// </summary>
public class FreedomPayDebugTester : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private string[] testUrls = new string[]
    {
        "https://api.freedompay.uz",
        "https://sandbox.freedompay.uz", 
        "https://test.freedompay.uz",
        "https://demo.freedompay.uz"
    };
    
    [SerializeField] private string[] testMerchantIds = new string[]
    {
        "552170",      // Наш основной ID
        "test",        // Возможный тестовый ID
        "demo",        // Возможный демо ID
        "sandbox"      // Возможный sandbox ID
    };
    
    [Header("Current Settings")]
    [SerializeField] private string receiveSecretKey = "wUQ18x3bzP86MUzn";
    
    [ContextMenu("🔍 Тест доступности API")]
    public void TestApiAvailability()
    {
        Debug.Log("=== ТЕСТ ДОСТУПНОСТИ API FREEDOM PAY ===");
        StartCoroutine(TestAllEndpoints());
    }
    
    [ContextMenu("🧪 Тест разных Merchant ID")]
    public void TestDifferentMerchantIds()
    {
        Debug.Log("=== ТЕСТ РАЗНЫХ MERCHANT ID ===");
        StartCoroutine(TestAllMerchantIds());
    }
    
    [ContextMenu("🌐 Проверка сети")]
    public void TestNetworkConnectivity()
    {
        Debug.Log("=== ПРОВЕРКА СЕТЕВОГО ПОДКЛЮЧЕНИЯ ===");
        StartCoroutine(TestNetworkConnection());
    }
    
    [ContextMenu("📞 Информация для поддержки")]
    public void GenerateSupportInfo()
    {
        Debug.Log("=== ИНФОРМАЦИЯ ДЛЯ ПОДДЕРЖКИ FREEDOM PAY ===");
        Debug.Log($"Merchant ID: 552170");
        Debug.Log($"Используемый API: init_payment.php");
        Debug.Log($"Ошибка: 10000 - Ошибка оплаты, сервис недоступен");
        Debug.Log($"Дата тестирования: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Debug.Log($"Тестовая сумма: 1000 сум");
        Debug.Log($"Формат подписи: init_payment.php;merchant_id;amount;currency;order_id;description;salt;language");
        Debug.Log($"Контакты поддержки: support@freedompay.uz");
        Debug.Log("Рекомендация: Обратиться в техподдержку для активации тестового режима");
    }
    
    private IEnumerator TestAllEndpoints()
    {
        foreach (string baseUrl in testUrls)
        {
            yield return TestEndpoint(baseUrl);
            yield return new WaitForSeconds(1f);
        }
    }
    
    private IEnumerator TestEndpoint(string baseUrl)
    {
        string url = $"{baseUrl}/init_payment.php";
        Debug.Log($"[Test] Проверка endpoint: {url}");
        
        // Простой GET запрос для проверки доступности
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = 10;
        
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"[Test] ✅ {baseUrl} - доступен (код: {request.responseCode})");
        }
        else if (request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log($"[Test] ⚠️ {baseUrl} - HTTP ошибка {request.responseCode}");
        }
        else
        {
            Debug.Log($"[Test] ❌ {baseUrl} - недоступен ({request.error})");
        }
    }
    
    private IEnumerator TestAllMerchantIds()
    {
        foreach (string merchantId in testMerchantIds)
        {
            yield return TestMerchantId(merchantId);
            yield return new WaitForSeconds(2f);
        }
    }
    
    private IEnumerator TestMerchantId(string testMerchantId)
    {
        string url = "https://api.freedompay.uz/init_payment.php";
        string orderId = $"test_{testMerchantId}_{System.DateTime.Now.Ticks}";
        int amount = 1000;
        string description = "Test";
        string salt = System.Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16);
        
        // Формируем подпись
        string dataToSign = $"init_payment.php;{testMerchantId};{amount};UZS;{orderId};{description};{salt};ru";
        string signature = ComputeMD5Hash(dataToSign + receiveSecretKey);
        
        Debug.Log($"[Test] Тестируем Merchant ID: {testMerchantId}");
        
        // Создаем POST запрос
        WWWForm form = new WWWForm();
        form.AddField("pg_merchant_id", testMerchantId);
        form.AddField("pg_amount", amount.ToString());
        form.AddField("pg_currency", "UZS");
        form.AddField("pg_order_id", orderId);
        form.AddField("pg_description", description);
        form.AddField("pg_salt", salt);
        form.AddField("pg_language", "ru");
        form.AddField("pg_sig", signature);
        
        UnityWebRequest request = UnityWebRequest.Post(url, form);
        request.timeout = 15;
        
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            string response = request.downloadHandler.text;
            Debug.Log($"[Test] Merchant ID {testMerchantId} ответ: {response}");
            
            if (response.Contains("<pg_status>ok</pg_status>"))
            {
                Debug.Log($"[Test] ✅ Merchant ID {testMerchantId} - успех!");
            }
            else if (response.Contains("<pg_error_code>"))
            {
                string errorCode = ExtractXmlValue(response, "pg_error_code");
                string errorDesc = ExtractXmlValue(response, "pg_error_description");
                Debug.Log($"[Test] ❌ Merchant ID {testMerchantId} - ошибка {errorCode}: {errorDesc}");
            }
        }
        else
        {
            Debug.LogError($"[Test] ❌ Merchant ID {testMerchantId} - сетевая ошибка: {request.error}");
        }
    }
    
    private IEnumerator TestNetworkConnection()
    {
        // Тест подключения к Google (для проверки интернета)
        UnityWebRequest googleTest = UnityWebRequest.Get("https://www.google.com");
        googleTest.timeout = 5;
        
        yield return googleTest.SendWebRequest();
        
        if (googleTest.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[Test] ✅ Интернет подключение работает");
        }
        else
        {
            Debug.Log("[Test] ❌ Проблемы с интернет подключением");
            yield break;
        }
        
        // Тест DNS резолвинга для freedompay.uz
        UnityWebRequest dnsTest = UnityWebRequest.Get("https://freedompay.uz");
        dnsTest.timeout = 10;
        
        yield return dnsTest.SendWebRequest();
        
        if (dnsTest.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[Test] ✅ freedompay.uz доступен");
        }
        else
        {
            Debug.Log($"[Test] ⚠️ freedompay.uz недоступен: {dnsTest.error}");
        }
    }
    
    private string ExtractXmlValue(string xml, string tagName)
    {
        string startTag = $"<{tagName}>";
        string endTag = $"</{tagName}>";
        
        int startIndex = xml.IndexOf(startTag);
        if (startIndex == -1) return "";
        
        startIndex += startTag.Length;
        int endIndex = xml.IndexOf(endTag, startIndex);
        if (endIndex == -1) return "";
        
        return xml.Substring(startIndex, endIndex - startIndex);
    }
    
    private string ComputeMD5Hash(string input)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            return System.BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }
} 