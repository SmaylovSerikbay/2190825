using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Анализатор подписи Freedom Pay для понимания правильного алгоритма
/// </summary>
public class SignatureAnalyzer : MonoBehaviour
{
    [Header("Рабочие данные из личного кабинета")]
    [SerializeField] private string workingMerchantId = "552170";
    [SerializeField] private string workingAmount = "1000";
    [SerializeField] private string workingCurrency = "UZS";
    [SerializeField] private string workingDescription = "sadas";
    [SerializeField] private string workingSalt = "5kqQUImDRGHmFsRH";
    [SerializeField] private string workingLanguage = "ru";
    [SerializeField] private string workingSignature = "90efed8d022f586f431193a390f08456";
    
    [Header("Секретные ключи")]
    [SerializeField] private string receiveSecretKey = "wUQ18x3bzP86MUzn";
    [SerializeField] private string payoutSecretKey = "lvA1DXTL8ILLj0P";
    
    [ContextMenu("🔍 Анализировать рабочую подпись")]
    public void AnalyzeWorkingSignature()
    {
        Debug.Log("=== АНАЛИЗ РАБОЧЕЙ ПОДПИСИ ===");
        Debug.Log($"Рабочая подпись: {workingSignature}");
        
        // Пробуем разные варианты алгоритма подписи
        TestSignatureVariation1();
        TestSignatureVariation2();
        TestSignatureVariation3();
        TestSignatureVariation4();
        TestSignatureVariation5();
        TestSignatureVariation6();
    }
    
    /// <summary>
    /// Вариант 1: payment.php;merchant_id;amount;currency;description;salt;language
    /// </summary>
    private void TestSignatureVariation1()
    {
        string dataToSign = $"payment.php;{workingMerchantId};{workingAmount};{workingCurrency};{workingDescription};{workingSalt};{workingLanguage}";
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveSecretKey);
        string sig2 = ComputeMD5Hash(dataToSign + payoutSecretKey);
        
        Debug.Log($"[Test1] Строка: {dataToSign}");
        Debug.Log($"[Test1] С receive ключом: {sig1} {(sig1 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"[Test1] С payout ключом: {sig2} {(sig2 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
    }
    
    /// <summary>
    /// Вариант 2: без payment.php
    /// </summary>
    private void TestSignatureVariation2()
    {
        string dataToSign = $"{workingMerchantId};{workingAmount};{workingCurrency};{workingDescription};{workingSalt};{workingLanguage}";
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveSecretKey);
        string sig2 = ComputeMD5Hash(dataToSign + payoutSecretKey);
        
        Debug.Log($"[Test2] Строка: {dataToSign}");
        Debug.Log($"[Test2] С receive ключом: {sig1} {(sig1 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"[Test2] С payout ключом: {sig2} {(sig2 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
    }
    
    /// <summary>
    /// Вариант 3: другой порядок параметров
    /// </summary>
    private void TestSignatureVariation3()
    {
        string dataToSign = $"{workingMerchantId};{workingAmount};{workingCurrency};{workingSalt};{workingDescription};{workingLanguage}";
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveSecretKey);
        string sig2 = ComputeMD5Hash(dataToSign + payoutSecretKey);
        
        Debug.Log($"[Test3] Строка: {dataToSign}");
        Debug.Log($"[Test3] С receive ключом: {sig1} {(sig1 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"[Test3] С payout ключом: {sig2} {(sig2 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
    }
    
    /// <summary>
    /// Вариант 4: с payment_origin=merchant_cabinet
    /// </summary>
    private void TestSignatureVariation4()
    {
        string dataToSign = $"payment.php;{workingMerchantId};{workingAmount};{workingCurrency};{workingDescription};{workingSalt};{workingLanguage};merchant_cabinet";
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveSecretKey);
        string sig2 = ComputeMD5Hash(dataToSign + payoutSecretKey);
        
        Debug.Log($"[Test4] Строка: {dataToSign}");
        Debug.Log($"[Test4] С receive ключом: {sig1} {(sig1 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"[Test4] С payout ключом: {sig2} {(sig2 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
    }
    
    /// <summary>
    /// Вариант 5: Алфавитный порядок параметров
    /// </summary>
    private void TestSignatureVariation5()
    {
        // pg_amount, pg_currency, pg_description, pg_language, pg_merchant_id, pg_salt
        string dataToSign = $"{workingAmount};{workingCurrency};{workingDescription};{workingLanguage};{workingMerchantId};{workingSalt}";
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveSecretKey);
        string sig2 = ComputeMD5Hash(dataToSign + payoutSecretKey);
        
        Debug.Log($"[Test5] Строка: {dataToSign}");
        Debug.Log($"[Test5] С receive ключом: {sig1} {(sig1 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"[Test5] С payout ключом: {sig2} {(sig2 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
    }
    
    /// <summary>
    /// Вариант 6: Форматы URL параметров
    /// </summary>
    private void TestSignatureVariation6()
    {
        // Пробуем формат как в URL
        var parameters = new List<string>
        {
            $"pg_merchant_id={workingMerchantId}",
            $"pg_amount={workingAmount}",
            $"pg_currency={workingCurrency}",
            $"pg_description={workingDescription}",
            $"pg_salt={workingSalt}",
            $"pg_language={workingLanguage}",
            "payment_origin=merchant_cabinet"
        };
        
        string dataToSign = string.Join("&", parameters);
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveSecretKey);
        string sig2 = ComputeMD5Hash(dataToSign + payoutSecretKey);
        
        Debug.Log($"[Test6] Строка: {dataToSign}");
        Debug.Log($"[Test6] С receive ключом: {sig1} {(sig1 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"[Test6] С payout ключом: {sig2} {(sig2 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
    }
    
    [ContextMenu("🧪 Генерировать тестовую подпись")]
    public void GenerateTestSignature()
    {
        Debug.Log("=== ГЕНЕРАЦИЯ ТЕСТОВОЙ ПОДПИСИ ===");
        
        string testDescription = "Test Payment";
        string testSalt = GenerateSalt();
        string testOrderId = $"test_{System.DateTime.Now.Ticks}";
        
        Debug.Log($"Test Order ID: {testOrderId}");
        Debug.Log($"Test Salt: {testSalt}");
        
        // Используем найденный правильный алгоритм (когда найдем)
        string dataToSign = $"payment.php;{workingMerchantId};{workingAmount};{workingCurrency};{testDescription};{testSalt};{workingLanguage}";
        string signature = ComputeMD5Hash(dataToSign + receiveSecretKey);
        
        Debug.Log($"Строка для подписи: {dataToSign}");
        Debug.Log($"Подпись: {signature}");
        
        // Генерируем полный URL
        string url = $"https://api.freedompay.uz/payment.php?" +
                    $"pg_merchant_id={workingMerchantId}&" +
                    $"pg_amount={workingAmount}&" +
                    $"pg_currency={workingCurrency}&" +
                    $"pg_order_id={testOrderId}&" +
                    $"pg_description={UnityEngine.Networking.UnityWebRequest.EscapeURL(testDescription)}&" +
                    $"pg_salt={testSalt}&" +
                    $"pg_language={workingLanguage}&" +
                    $"payment_origin=merchant_cabinet&" +
                    $"pg_sig={signature}";
        
        Debug.Log($"Тестовый URL: {url}");
    }
    
    [ContextMenu("🔄 Проверить с исправленным ключом")]
    public void TestWithCorrectedKey()
    {
        Debug.Log("=== ТЕСТ С ВОЗМОЖНО ИСПРАВЛЕННЫМ КЛЮЧОМ ===");
        
        // В логах видели: lvA1DXTL8ILLLj0P (с тремя L)
        string possibleCorrectedKey = "lvA1DXTL8ILLLj0P";
        
        string dataToSign = $"payment.php;{workingMerchantId};{workingAmount};{workingCurrency};{workingDescription};{workingSalt};{workingLanguage}";
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveSecretKey);
        string sig2 = ComputeMD5Hash(dataToSign + payoutSecretKey);
        string sig3 = ComputeMD5Hash(dataToSign + possibleCorrectedKey);
        
        Debug.Log($"[Corrected] Строка: {dataToSign}");
        Debug.Log($"[Corrected] С receive ключом: {sig1} {(sig1 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"[Corrected] С payout ключом: {sig2} {(sig2 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"[Corrected] С исправленным ключом: {sig3} {(sig3 == workingSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
    }
    
    [ContextMenu("🔧 Тест Cabinet Fallback")]
    public void TestCabinetFallback()
    {
        Debug.Log("=== ТЕСТ CABINET FALLBACK ===");
        
        var manager = FreedomPayManager.Instance;
        if (manager == null)
        {
            Debug.LogError("FreedomPayManager не найден!");
            return;
        }
        
        Debug.Log("🚀 Запускаем тестовый платеж с fallback...");
        manager.InitiatePayment(1000, "Cabinet Fallback Test");
    }
    
    [ContextMenu("📋 Показать рекомендации")]
    public void ShowRecommendations()
    {
        Debug.Log("=== 📞 РЕКОМЕНДАЦИИ ДЛЯ РЕШЕНИЯ ПРОБЛЕМЫ ПОДПИСИ ===");
        Debug.Log("");
        Debug.Log("1. ОБРАТИТЬСЯ В ТЕХПОДДЕРЖКУ FREEDOM PAY:");
        Debug.Log("   📧 Email: support@freedompay.uz");
        Debug.Log("   🏢 Через личный кабинет Freedom Pay");
        Debug.Log("   🆔 Указать Merchant ID: 552170");
        Debug.Log("");
        Debug.Log("2. ЗАПРОСИТЬ ДОКУМЕНТАЦИЮ:");
        Debug.Log("   📚 Алгоритм формирования подписи для payment.php");
        Debug.Log("   📱 Примеры кода для mobile интеграции");
        Debug.Log("   🔗 API endpoints для проверки статуса");
        Debug.Log("");
        Debug.Log("3. СТАТУС ПРОЕКТА:");
        Debug.Log("   ✅ Unity интеграция готова (85%)");
        Debug.Log("   ✅ UI/UX реализован");
        Debug.Log("   ✅ Fallback механизм добавлен");
        Debug.Log("   ❌ Официальный алгоритм подписи - ТРЕБУЕТСЯ");
        Debug.Log("");
        Debug.Log("🎯 Проект готов к релизу после получения правильного алгоритма!");
    }
    
    private string GenerateSalt()
    {
        return System.Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16);
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