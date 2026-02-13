using UnityEngine;

/// <summary>
/// Автоматический запускатель анализа подписи для Unity
/// </summary>
public class SignatureTestRunner : MonoBehaviour
{
    [ContextMenu("🚀 Запустить полный анализ подписи")]
    public void RunFullSignatureAnalysis()
    {
        Debug.Log("=== 🔍 АВТОМАТИЧЕСКИЙ АНАЛИЗ ПОДПИСИ FREEDOM PAY ===");
        
        // Данные из рабочей ссылки
        string workingMerchantId = "552170";
        string workingAmount = "1000";
        string workingCurrency = "UZS";
        string workingDescription = "sadas";
        string workingSalt = "5kqQUImDRGHmFsRH";
        string workingLanguage = "ru";
        string workingSignature = "90efed8d022f586f431193a390f08456";
        
        string receiveSecretKey = "wUQ18x3bzP86MUzn";
        string payoutSecretKey = "lvA1DXTL8ILLj0P";
        
        Debug.Log($"🎯 Цель: найти алгоритм для подписи {workingSignature}");
        Debug.Log("");
        
        // Тест 1: Стандартный алгоритм payment.php
        TestVariant1(workingMerchantId, workingAmount, workingCurrency, workingDescription, workingSalt, workingLanguage, workingSignature, receiveSecretKey, payoutSecretKey);
        
        // Тест 2: Без payment.php в начале
        TestVariant2(workingMerchantId, workingAmount, workingCurrency, workingDescription, workingSalt, workingLanguage, workingSignature, receiveSecretKey, payoutSecretKey);
        
        // Тест 3: Другой порядок параметров
        TestVariant3(workingMerchantId, workingAmount, workingCurrency, workingDescription, workingSalt, workingLanguage, workingSignature, receiveSecretKey, payoutSecretKey);
        
        // Тест 4: С payment_origin
        TestVariant4(workingMerchantId, workingAmount, workingCurrency, workingDescription, workingSalt, workingLanguage, workingSignature, receiveSecretKey, payoutSecretKey);
        
        // Тест 5: Алфавитный порядок
        TestVariant5(workingMerchantId, workingAmount, workingCurrency, workingDescription, workingSalt, workingLanguage, workingSignature, receiveSecretKey, payoutSecretKey);
        
        // Тест 6: Исправленный payout ключ
        TestVariant6(workingMerchantId, workingAmount, workingCurrency, workingDescription, workingSalt, workingLanguage, workingSignature, receiveSecretKey);
        
        Debug.Log("=== 🔚 АНАЛИЗ ЗАВЕРШЕН ===");
    }
    
    private void TestVariant1(string merchantId, string amount, string currency, string description, string salt, string language, string targetSignature, string receiveKey, string payoutKey)
    {
        Debug.Log("🔬 [ТЕСТ 1] payment.php;merchant_id;amount;currency;description;salt;language");
        
        string dataToSign = $"payment.php;{merchantId};{amount};{currency};{description};{salt};{language}";
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveKey);
        string sig2 = ComputeMD5Hash(dataToSign + payoutKey);
        
        Debug.Log($"   Строка: {dataToSign}");
        Debug.Log($"   Receive: {sig1} {(sig1 == targetSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"   Payout:  {sig2} {(sig2 == targetSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log("");
    }
    
    private void TestVariant2(string merchantId, string amount, string currency, string description, string salt, string language, string targetSignature, string receiveKey, string payoutKey)
    {
        Debug.Log("🔬 [ТЕСТ 2] merchant_id;amount;currency;description;salt;language (без payment.php)");
        
        string dataToSign = $"{merchantId};{amount};{currency};{description};{salt};{language}";
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveKey);
        string sig2 = ComputeMD5Hash(dataToSign + payoutKey);
        
        Debug.Log($"   Строка: {dataToSign}");
        Debug.Log($"   Receive: {sig1} {(sig1 == targetSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"   Payout:  {sig2} {(sig2 == targetSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log("");
    }
    
    private void TestVariant3(string merchantId, string amount, string currency, string description, string salt, string language, string targetSignature, string receiveKey, string payoutKey)
    {
        Debug.Log("🔬 [ТЕСТ 3] merchant_id;amount;currency;salt;description;language (другой порядок)");
        
        string dataToSign = $"{merchantId};{amount};{currency};{salt};{description};{language}";
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveKey);
        string sig2 = ComputeMD5Hash(dataToSign + payoutKey);
        
        Debug.Log($"   Строка: {dataToSign}");
        Debug.Log($"   Receive: {sig1} {(sig1 == targetSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"   Payout:  {sig2} {(sig2 == targetSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log("");
    }
    
    private void TestVariant4(string merchantId, string amount, string currency, string description, string salt, string language, string targetSignature, string receiveKey, string payoutKey)
    {
        Debug.Log("🔬 [ТЕСТ 4] payment.php;merchant_id;amount;currency;description;salt;language;merchant_cabinet");
        
        string dataToSign = $"payment.php;{merchantId};{amount};{currency};{description};{salt};{language};merchant_cabinet";
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveKey);
        string sig2 = ComputeMD5Hash(dataToSign + payoutKey);
        
        Debug.Log($"   Строка: {dataToSign}");
        Debug.Log($"   Receive: {sig1} {(sig1 == targetSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"   Payout:  {sig2} {(sig2 == targetSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log("");
    }
    
    private void TestVariant5(string merchantId, string amount, string currency, string description, string salt, string language, string targetSignature, string receiveKey, string payoutKey)
    {
        Debug.Log("🔬 [ТЕСТ 5] Алфавитный порядок: amount;currency;description;language;merchant_id;salt");
        
        string dataToSign = $"{amount};{currency};{description};{language};{merchantId};{salt}";
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveKey);
        string sig2 = ComputeMD5Hash(dataToSign + payoutKey);
        
        Debug.Log($"   Строка: {dataToSign}");
        Debug.Log($"   Receive: {sig1} {(sig1 == targetSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"   Payout:  {sig2} {(sig2 == targetSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log("");
    }
    
    private void TestVariant6(string merchantId, string amount, string currency, string description, string salt, string language, string targetSignature, string receiveKey)
    {
        Debug.Log("🔬 [ТЕСТ 6] С исправленным payout ключом (3 L): lvA1DXTL8ILLLj0P");
        
        string correctedPayoutKey = "lvA1DXTL8ILLLj0P";
        string dataToSign = $"payment.php;{merchantId};{amount};{currency};{description};{salt};{language}";
        
        string sig1 = ComputeMD5Hash(dataToSign + receiveKey);
        string sig2 = ComputeMD5Hash(dataToSign + correctedPayoutKey);
        
        Debug.Log($"   Строка: {dataToSign}");
        Debug.Log($"   Receive: {sig1} {(sig1 == targetSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log($"   Исправл.: {sig2} {(sig2 == targetSignature ? "✅ СОВПАДЕНИЕ!" : "❌")}");
        Debug.Log("");
    }
    
    [ContextMenu("🎲 Тест нашей текущей подписи")]
    public void TestCurrentSignature()
    {
        Debug.Log("=== 🎲 ТЕСТ НАШЕЙ ТЕКУЩЕЙ ПОДПИСИ ===");
        
        string merchantId = "552170";
        string amount = "1000";
        string currency = "UZS";
        string description = "Test Payment";
        string salt = "4567b562755d47f2"; // Из последнего лога
        string language = "ru";
        string receiveKey = "wUQ18x3bzP86MUzn";
        
        string dataToSign = $"payment.php;{merchantId};{amount};{currency};{description};{salt};{language}";
        string ourSignature = ComputeMD5Hash(dataToSign + receiveKey);
        
        Debug.Log($"Наша строка: {dataToSign}");
        Debug.Log($"Наша подпись: {ourSignature}");
        Debug.Log($"Ожидаемая: 22190143504e05e488bd9ee2d6d202a0 (из логов)");
        Debug.Log($"Совпадает: {ourSignature == "22190143504e05e488bd9ee2d6d202a0"}");
    }
    
    [ContextMenu("🔧 Генерировать правильную тестовую ссылку")]
    public void GenerateCorrectTestUrl()
    {
        Debug.Log("=== 🔧 ГЕНЕРАЦИЯ ПРАВИЛЬНОЙ ТЕСТОВОЙ ССЫЛКИ ===");
        
        // После того как найдем правильный алгоритм, обновим этот метод
        Debug.Log("❌ Сначала нужно найти правильный алгоритм подписи");
        Debug.Log("Запустите: 🚀 Запустить полный анализ подписи");
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