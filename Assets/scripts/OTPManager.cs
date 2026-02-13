using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;

/// <summary>
/// Manager for working with OTP activation codes for AR
/// Automatically activates AR after entering a 6-digit code
/// </summary>
public class OTPManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject otpInputPanel;
    [SerializeField] private TMP_InputField otpInputField;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button closeButton;
    
    [Header("Events")]
    public System.Action<float, int, string> OnOTPActivated; // amount, quantity, currency
    public System.Action<string> OnOTPError;
    
    [Header("AR Integration")]
    [SerializeField] private ARPaymentController arPaymentController;
    
    private string currentOTPCode;
    private bool isProcessing = false;
    private DatabaseReference otpRef;
    [SerializeField] private string firebaseDatabaseUrl = "https://comeback-2a6b2-default-rtdb.firebaseio.com";
    private bool isFirebaseReady = false;
    
    // Singleton pattern
    public static OTPManager Instance { get; private set; }
    
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
    
    void Start()
    {
        InitializeUI();
        StartCoroutine(InitializeFirebaseCoroutine());
        HideOTPPanel();
    }
    
    IEnumerator InitializeFirebaseCoroutine()
    {
        Debug.Log("[OTPManager] 🔥 Starting Firebase initialization...");
        
        // Check Firebase dependencies
        var task = FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => task.IsCompleted);
        
        DependencyStatus status;
        try
        {
            status = task.Result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[OTPManager] ❌ Critical dependency check error: {e.Message}");
            isFirebaseReady = false;
            yield break;
        }
        
        if (status != DependencyStatus.Available)
        {
            Debug.LogError($"[OTPManager] ❌ Firebase dependencies unavailable: {status}");
            isFirebaseReady = false;
            yield break;
        }
        
        Debug.Log("[OTPManager] ✅ Firebase зависимости доступны");
        
        // Initialize Firebase App
        FirebaseApp app = null;
        try
        {
            app = FirebaseApp.DefaultInstance;
        }
        catch (Exception e)
        {
            Debug.LogError($"[OTPManager] ❌ Error getting Firebase App: {e.Message}");
            isFirebaseReady = false;
            yield break;
        }
        
        if (app == null)
        {
            Debug.LogError("[OTPManager] ❌ FirebaseApp.DefaultInstance = null");
            isFirebaseReady = false;
            yield break;
        }
        
        Debug.Log($"[OTPManager] 🔥 Firebase App: {app.Name}");
        
        // Initialize Firebase Database
        FirebaseDatabase database = null;
        try
        {
            database = FirebaseDatabase.GetInstance(app, firebaseDatabaseUrl);
            Debug.Log($"[OTPManager] ✅ Using explicit URL: {firebaseDatabaseUrl}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OTPManager] ⚠️ Fallback to DefaultInstance: {e.Message}");
            try
            {
                database = FirebaseDatabase.DefaultInstance;
            }
            catch (Exception e2)
            {
                Debug.LogError($"[OTPManager] ❌ Error getting DefaultInstance: {e2.Message}");
                isFirebaseReady = false;
                yield break;
            }
        }
        
        if (database == null)
        {
            Debug.LogError("[OTPManager] ❌ FirebaseDatabase = null");
            isFirebaseReady = false;
            yield break;
        }
        
        // Get database reference
        try
        {
            otpRef = database.GetReference("activation_codes");
            isFirebaseReady = true;
            Debug.Log("[OTPManager] 🎉 Firebase успешно инициализирован!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[OTPManager] ❌ Error getting database reference: {e.Message}");
            isFirebaseReady = false;
            yield break;
        }
    }
    
    void InitializeUI()
    {
        if (otpInputField != null)
        {
            otpInputField.onValueChanged.AddListener(OnOTPInputChanged);
            otpInputField.characterLimit = 6;
            otpInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideOTPPanel);
        }
    }
    
    void OnOTPInputChanged(string value)
    {
        // Limit input to numbers only
        string numericValue = "";
        foreach (char c in value)
        {
            if (char.IsDigit(c))
            {
                numericValue += c;
            }
        }
        
        if (numericValue != value)
        {
            otpInputField.text = numericValue;
        }
        
        currentOTPCode = numericValue;
        
        // Automatic activation after entering 6 digits
        if (numericValue.Length == 6 && !isProcessing)
        {
            StartCoroutine(VerifyOTPCode(numericValue));
        }
    }
    
    /// <summary>
    /// Shows the OTP code input panel
    /// </summary>
    public void ShowOTPPanel()
    {
        if (otpInputPanel != null)
        {
            otpInputPanel.SetActive(true);
            if (otpInputField != null)
            {
                otpInputField.text = "";
                otpInputField.Select();
            }
            UpdateStatus("Enter 6-digit OTP code", Color.black);
        }
    }
    
    /// <summary>
    /// Hides the OTP code input panel
    /// </summary>
    public void HideOTPPanel()
    {
        if (otpInputPanel != null)
        {
            otpInputPanel.SetActive(false);
        }
    }
    
    
    /// <summary>
    /// Verifies OTP code through Firebase
    /// </summary>
    private IEnumerator VerifyOTPCode(string otpCode)
    {
        isProcessing = true;
        UpdateStatus("Verifying OTP code...", Color.blue);
        
        // Wait for Firebase initialization if needed
        if (!isFirebaseReady || otpRef == null)
        {
            UpdateStatus("Connecting to server...", Color.blue);
            Debug.Log("[OTPManager] ⏳ Waiting for Firebase initialization...");
            
            float startTime = Time.time;
            const float timeoutSeconds = 10f; // Увеличиваем timeout
            
            while (!isFirebaseReady && Time.time - startTime < timeoutSeconds)
            {
                Debug.Log($"[OTPManager] ⏳ Waiting for Firebase... {Time.time - startTime:F1}s");
                yield return new WaitForSeconds(0.5f);
            }
            
            // Try to reinitialize Firebase if failed
            if (!isFirebaseReady)
            {
                Debug.LogWarning("[OTPManager] ⚠️ Firebase not initialized, attempting to reinitialize...");
                StartCoroutine(InitializeFirebaseCoroutine());
                
                // Wait a bit more
                startTime = Time.time;
                while (!isFirebaseReady && Time.time - startTime < 5f)
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }
        
        if (!isFirebaseReady || otpRef == null)
        {
            string errorMsg = $"Firebase not initialized. isFirebaseReady: {isFirebaseReady}, otpRef: {(otpRef == null ? "null" : "not null")}";
            Debug.LogError($"[OTPManager] ❌ {errorMsg}");
            UpdateStatus($"Connection error. Please try again", Color.red);
            isProcessing = false;
            yield break;
        }
        
        // Search for OTP code in Firebase
        var otpQuery = otpRef.OrderByChild("code").EqualTo(otpCode);
        
        var task = otpQuery.GetValueAsync();
        yield return new WaitUntil(() => task.IsCompleted);
        
        if (task.Exception != null)
        {
            Debug.LogError($"[OTPManager] Firebase error: {task.Exception.Message}");
            UpdateStatus("Server connection error", Color.red);
            isProcessing = false;
            yield break;
        }
        
        DataSnapshot snapshot = task.Result;
        
        if (snapshot.Exists)
        {
            // OTP code found, check status
            foreach (var child in snapshot.Children)
            {
                var otpData = child.Value as Dictionary<string, object>;
                if (otpData != null)
                {
                    string status = otpData.ContainsKey("status") ? otpData["status"].ToString() : "";
                    string amount = otpData.ContainsKey("amount") ? otpData["amount"].ToString() : "0";
                    string quantity = otpData.ContainsKey("quantity") ? otpData["quantity"].ToString() : "0";
                    string currency = otpData.ContainsKey("currency") ? otpData["currency"].ToString() : "UZS";
                    
                    if (status == "active")
                    {
                        // Activate OTP code
                        StartCoroutine(ActivateOTPCode(child.Key, otpData));
                        yield break;
                    }
                    else if (status == "used")
                    {
                        UpdateStatus("OTP code already used", Color.red);
                        isProcessing = false;
                        yield break;
                    }
                    else if (status == "expired")
                    {
                        UpdateStatus("OTP code expired", Color.red);
                        isProcessing = false;
                        yield break;
                    }
                }
            }
            
            UpdateStatus("OTP code not found or inactive", Color.red);
        }
        else
        {
            UpdateStatus("OTP code not found", Color.red);
        }
        
        isProcessing = false;
    }
    
    /// <summary>
    /// Activates OTP code in Firebase
    /// </summary>
    private IEnumerator ActivateOTPCode(string firebaseKey, Dictionary<string, object> otpData)
    {
        // Update status to "used"
        var updates = new Dictionary<string, object>
        {
            ["status"] = "used",
            ["used_at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["device_id"] = SystemInfo.deviceUniqueIdentifier
        };
        
        var updateTask = otpRef.Child(firebaseKey).UpdateChildrenAsync(updates);
        yield return new WaitUntil(() => updateTask.IsCompleted);
        
        if (updateTask.Exception != null)
        {
            Debug.LogError($"[OTPManager] OTP update error: {updateTask.Exception.Message}");
            UpdateStatus("OTP activation error", Color.red);
            isProcessing = false;
            yield break;
        }
        
        try
        {
            // Parse data
            float amount = 0f;
            int quantity = 0;
            string currency = "UZS";
            
            if (otpData.ContainsKey("amount") && float.TryParse(otpData["amount"].ToString(), out float parsedAmount))
            {
                amount = parsedAmount;
            }
            
            if (otpData.ContainsKey("quantity") && int.TryParse(otpData["quantity"].ToString(), out int parsedQuantity))
            {
                quantity = parsedQuantity;
            }
            
            if (otpData.ContainsKey("currency"))
            {
                currency = otpData["currency"].ToString();
            }
            
            // OTP code successfully activated (each OTP = 1 ticket)
            UpdateStatus($"OTP activated! {quantity} minutes of AR access available", Color.green);
            
            // Notify subscribers
            OnOTPActivated?.Invoke(amount, quantity, currency);
            
            // Activate subscription through FreedomPayManager (like successful payment)
            if (FreedomPayManager.Instance != null)
            {
                // Activate subscription for the number of minutes from OTP
                FreedomPayManager.Instance.ActivateSubscriptionWithOTP(quantity);
                Debug.Log($"[OTPManager] Subscription activated for {quantity} minutes via OTP");
            }
            else
            {
                Debug.LogWarning("[OTPManager] FreedomPayManager not found!");
            }
            
            // Hide panel after 2 seconds
            StartCoroutine(HidePanelAfterDelay(2f));
        }
        catch (Exception e)
        {
            Debug.LogError($"[OTPManager] Activation error: {e.Message}");
            UpdateStatus("OTP activation error", Color.red);
        }
        
        isProcessing = false;
    }
    
    private IEnumerator HidePanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideOTPPanel();
    }
    
    private void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        
        Debug.Log($"[OTPManager] {message}");
    }
    
    /// <summary>
    /// Checks if OTP code is active
    /// </summary>
    public bool IsOTPActive()
    {
        return !string.IsNullOrEmpty(currentOTPCode) && currentOTPCode.Length == 6;
    }
    
    /// <summary>
    /// Checks Firebase status
    /// </summary>
    public bool IsFirebaseReady()
    {
        return isFirebaseReady && otpRef != null;
    }
    
    /// <summary>
    /// Gets current OTP code
    /// </summary>
    public string GetCurrentOTPCode()
    {
        return currentOTPCode;
    }
    
    /// <summary>
    /// Force Firebase reinitialization
    /// </summary>
    public void ReinitializeFirebase()
    {
        Debug.Log("[OTPManager] 🔄 Forced Firebase reinitialization...");
        isFirebaseReady = false;
        otpRef = null;
        StartCoroutine(InitializeFirebaseCoroutine());
    }
    
    /// <summary>
    /// Clears current OTP code
    /// </summary>
    public void ClearOTPCode()
    {
        currentOTPCode = "";
        if (otpInputField != null)
        {
            otpInputField.text = "";
        }
    }
    
    void OnDestroy()
    {
        if (otpInputField != null)
        {
            otpInputField.onValueChanged.RemoveListener(OnOTPInputChanged);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HideOTPPanel);
        }
    }
}
