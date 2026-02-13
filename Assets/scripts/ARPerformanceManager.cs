using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Менеджер производительности AR для автоматической оптимизации
/// Предотвращает перегрев и зависания на мобильных устройствах
/// </summary>
public class ARPerformanceManager : MonoBehaviour
{
    [Header("Performance Monitoring")]
    [SerializeField] public bool enablePerformanceMonitoring = true;
    [SerializeField] private float monitoringInterval = 1.0f; // Интервал мониторинга в секундах
    [SerializeField] public int targetFPS = 60; // Целевой FPS
    [SerializeField] public int minFPS = 30; // Минимальный допустимый FPS
    
    [Header("Auto-Optimization")]
    [SerializeField] public bool enableAutoOptimization = true;
    [SerializeField] private bool enableDynamicQuality = true;
    [SerializeField] public bool enableThermalProtection = true;
    
    [Header("Quality Levels")]
    [SerializeField] private int maxActiveObjects = 8; // Максимум активных объектов
    [SerializeField] private float[] qualityLevels = { 1.0f, 0.8f, 0.6f, 0.4f }; // Уровни качества
    [SerializeField] private int[] maxObjectsPerLevel = { 8, 6, 4, 2 }; // Максимум объектов для каждого уровня
    
    [Header("Thermal Protection")]
    [SerializeField] private float thermalThreshold = 0.9f; // Порог тепловой защиты (менее агрессивно)
    [SerializeField] private float cooldownTime = 3.0f; // Время охлаждения (быстрее восстановление)
    [SerializeField] private bool enableFrameRateLimiting = true;
    
    [Header("UI References")]
    [SerializeField] private Text performanceText;
    [SerializeField] private Slider qualitySlider;
    [SerializeField] private GameObject performancePanel;
    
    // Приватные переменные
    private Coroutine monitoringCoroutine;
    private int currentQualityLevel = 0;
    private float lastFrameTime;
    private float currentFPS;
    private float averageFPS;
    private List<float> fpsHistory = new List<float>();
    private int frameCount = 0;
    private float lastMonitoringTime;
    
    // Компоненты для оптимизации
    private ARObjectManager arObjectManager;
    private VideoSpawner videoSpawner;
    private Camera arCamera;
    
    // Тепловая защита
    private float thermalStress = 0f;
    private float lastThermalCheck = 0f;
    private bool isInCooldown = false;
    
    void Start()
    {
        InitializePerformanceManager();
    }
    
    void InitializePerformanceManager()
    {
        // Автопоиск компонентов
        arObjectManager = FindObjectOfType<ARObjectManager>();
        videoSpawner = FindObjectOfType<VideoSpawner>();
        arCamera = FindObjectOfType<Camera>();
        
        // Настраиваем UI
        SetupPerformanceUI();
        
        // Запускаем мониторинг
        if (enablePerformanceMonitoring)
        {
            StartPerformanceMonitoring();
        }
        
        // Применяем начальные настройки
        ApplyQualitySettings();
        
        Debug.Log("[ARPerformanceManager] ✅ Менеджер производительности инициализирован");
    }
    
    void SetupPerformanceUI()
    {
        if (performancePanel != null)
        {
            performancePanel.SetActive(true);
        }
        
        if (qualitySlider != null)
        {
            qualitySlider.minValue = 0;
            qualitySlider.maxValue = qualityLevels.Length - 1;
            qualitySlider.value = currentQualityLevel;
            qualitySlider.onValueChanged.AddListener(OnQualitySliderChanged);
        }
    }
    
    void StartPerformanceMonitoring()
    {
        if (monitoringCoroutine != null)
        {
            StopCoroutine(monitoringCoroutine);
        }
        
        monitoringCoroutine = StartCoroutine(PerformanceMonitoring());
        Debug.Log("[ARPerformanceManager] 📊 Мониторинг производительности запущен");
    }
    
    IEnumerator PerformanceMonitoring()
    {
        while (enablePerformanceMonitoring)
        {
            // Обновляем метрики
            UpdatePerformanceMetrics();
            
            // Проверяем тепловую защиту
            if (enableThermalProtection)
            {
                CheckThermalProtection();
            }
            
            // Автоматическая оптимизация
            if (enableAutoOptimization)
            {
                AutoOptimizePerformance();
            }
            
            // Обновляем UI
            UpdatePerformanceUI();
            
            yield return new WaitForSeconds(monitoringInterval);
        }
    }
    
    void UpdatePerformanceMetrics()
    {
        // Вычисляем текущий FPS
        float deltaTime = Time.time - lastFrameTime;
        if (deltaTime > 0)
        {
            currentFPS = 1f / deltaTime;
        }
        
        lastFrameTime = Time.time;
        
        // Добавляем в историю
        fpsHistory.Add(currentFPS);
        if (fpsHistory.Count > 10)
        {
            fpsHistory.RemoveAt(0);
        }
        
        // Вычисляем средний FPS
        float sum = 0f;
        foreach (float fps in fpsHistory)
        {
            sum += fps;
        }
        averageFPS = sum / fpsHistory.Count;
        
        // Обновляем счетчик кадров
        frameCount++;
    }
    
    void CheckThermalProtection()
    {
        if (Time.time - lastThermalCheck < 1f) return;
        
        lastThermalCheck = Time.time;
        
        // Более мягкая модель теплового стресса на основе FPS
        if (currentFPS < minFPS)
        {
            thermalStress += 0.05f; // Медленнее накапливаем стресс
        }
        else if (currentFPS > targetFPS * 0.7f) // Более мягкий порог восстановления
        {
            thermalStress = Mathf.Max(0f, thermalStress - 0.08f); // Быстрее восстанавливаемся
        }
        
        // Проверяем порог
        if (thermalStress > thermalThreshold && !isInCooldown)
        {
            Debug.LogWarning($"[ARPerformanceManager] ⚠️ Тепловая защита активирована! Стресс: {thermalStress:F2}");
            ActivateThermalProtection();
        }
        
        // Проверяем время охлаждения
        if (isInCooldown && Time.time - lastThermalCheck > cooldownTime)
        {
            isInCooldown = false;
            thermalStress = Mathf.Max(0f, thermalStress - 0.3f);
            Debug.Log("[ARPerformanceManager] ✅ Охлаждение завершено");
        }
    }
    
    void ActivateThermalProtection()
    {
        isInCooldown = true;
        
        // Снижаем качество
        if (currentQualityLevel < qualityLevels.Length - 1)
        {
            SetQualityLevel(currentQualityLevel + 1);
        }
        
        // Ограничиваем FPS
        if (enableFrameRateLimiting)
        {
            Application.targetFrameRate = 30;
        }
        
        // Останавливаем тяжелые процессы
        if (arObjectManager != null)
        {
            // Временно отключаем некоторые объекты
            StartCoroutine(TemporaryObjectReduction());
        }
        
        Debug.Log("[ARPerformanceManager] 🔥 Тепловая защита: качество снижено, FPS ограничен");
    }
    
    IEnumerator TemporaryObjectReduction()
    {
        if (arObjectManager == null) yield break;
        
        // Временно уменьшаем количество активных объектов
        int originalMax = arObjectManager.maxActiveObjects;
        arObjectManager.maxActiveObjects = Mathf.Max(2, originalMax / 2);
        
        yield return new WaitForSeconds(cooldownTime);
        
        // Восстанавливаем настройки
        arObjectManager.maxActiveObjects = originalMax;
        Debug.Log("[ARPerformanceManager] ✅ Настройки объектов восстановлены");
    }
    
    void AutoOptimizePerformance()
    {
        // Автоматическая настройка качества на основе FPS
        if (averageFPS < minFPS && currentQualityLevel < qualityLevels.Length - 1)
        {
            // Снижаем качество
            SetQualityLevel(currentQualityLevel + 1);
            Debug.Log($"[ARPerformanceManager] 🔄 Автооптимизация: качество снижено до уровня {currentQualityLevel}");
        }
        else if (averageFPS > targetFPS * 0.9f && currentQualityLevel > 0 && thermalStress < thermalThreshold * 0.5f)
        {
            // Повышаем качество
            SetQualityLevel(currentQualityLevel - 1);
            Debug.Log($"[ARPerformanceManager] 🔄 Автооптимизация: качество повышено до уровня {currentQualityLevel}");
        }
    }
    
    void SetQualityLevel(int level)
    {
        if (level < 0 || level >= qualityLevels.Length) return;
        
        currentQualityLevel = level;
        ApplyQualitySettings();
        
        // Обновляем UI
        if (qualitySlider != null)
        {
            qualitySlider.value = currentQualityLevel;
        }
        
        Debug.Log($"[ARPerformanceManager] 🎯 Уровень качества установлен: {level}");
    }
    
    void ApplyQualitySettings()
    {
        if (currentQualityLevel >= qualityLevels.Length) return;
        
        float qualityMultiplier = qualityLevels[currentQualityLevel];
        int maxObjects = maxObjectsPerLevel[currentQualityLevel];
        
        // Применяем настройки к ARObjectManager
        if (arObjectManager != null)
        {
            arObjectManager.maxActiveObjects = maxObjects;
        }
        
        // Применяем настройки к камере
        if (arCamera != null)
        {
            // Настраиваем качество рендеринга
            if (arCamera.allowHDR)
            {
                arCamera.allowHDR = currentQualityLevel < 2;
            }
            
            if (arCamera.allowMSAA)
            {
                arCamera.allowMSAA = currentQualityLevel < 1;
            }
        }
        
        // Применяем глобальные настройки Unity
        QualitySettings.shadowDistance = 50f * qualityMultiplier;
        QualitySettings.shadowResolution = currentQualityLevel < 2 ? ShadowResolution.High : ShadowResolution.Medium;
        QualitySettings.antiAliasing = currentQualityLevel < 1 ? 4 : (currentQualityLevel < 2 ? 2 : 0);
        
        Debug.Log($"[ARPerformanceManager] ⚙️ Применены настройки качества: уровень {currentQualityLevel}");
    }
    
    void UpdatePerformanceUI()
    {
        if (performanceText != null)
        {
            string info = $"FPS: {currentFPS:F1}\n";
            info += $"Средний FPS: {averageFPS:F1}\n";
            info += $"Качество: {currentQualityLevel + 1}/{qualityLevels.Length}\n";
            info += $"Тепловой стресс: {thermalStress:F2}\n";
            info += $"Активных объектов: {(arObjectManager != null ? arObjectManager.GetActiveObjectCount() : 0)}";
            
            performanceText.text = info;
        }
    }
    
    void OnQualitySliderChanged(float value)
    {
        int newLevel = Mathf.RoundToInt(value);
        if (newLevel != currentQualityLevel)
        {
            SetQualityLevel(newLevel);
        }
    }
    
    /// <summary>
    /// Принудительно устанавливает уровень качества
    /// </summary>
    public void ForceQualityLevel(int level)
    {
        SetQualityLevel(level);
    }
    
    /// <summary>
    /// Получает текущий уровень качества
    /// </summary>
    public int GetCurrentQualityLevel()
    {
        return currentQualityLevel;
    }
    
    /// <summary>
    /// Получает текущий FPS
    /// </summary>
    public float GetCurrentFPS()
    {
        return currentFPS;
    }
    
    /// <summary>
    /// Получает средний FPS
    /// </summary>
    public float GetAverageFPS()
    {
        return averageFPS;
    }
    
    /// <summary>
    /// Получает уровень теплового стресса
    /// </summary>
    public float GetThermalStress()
    {
        return thermalStress;
    }
    
    /// <summary>
    /// Сбрасывает тепловой стресс
    /// </summary>
    public void ResetThermalStress()
    {
        thermalStress = 0f;
        isInCooldown = false;
        Debug.Log("[ARPerformanceManager] 🔄 Тепловой стресс сброшен");
    }
    
    /// <summary>
    /// Включает/выключает мониторинг производительности
    /// </summary>
    public void SetPerformanceMonitoring(bool enabled)
    {
        enablePerformanceMonitoring = enabled;
        
        if (enabled && monitoringCoroutine == null)
        {
            StartPerformanceMonitoring();
        }
        else if (!enabled && monitoringCoroutine != null)
        {
            StopCoroutine(monitoringCoroutine);
            monitoringCoroutine = null;
        }
        
        Debug.Log($"[ARPerformanceManager] 📊 Мониторинг производительности: {(enabled ? "включен" : "выключен")}");
    }
    
    void OnDestroy()
    {
        if (monitoringCoroutine != null)
        {
            StopCoroutine(monitoringCoroutine);
        }
        
        // Восстанавливаем настройки Unity
        QualitySettings.shadowDistance = 50f;
        QualitySettings.shadowResolution = ShadowResolution.High;
        QualitySettings.antiAliasing = 4;
        
        Debug.Log("[ARPerformanceManager] ✅ Настройки Unity восстановлены");
    }
    
    // Методы для отладки
    [ContextMenu("Log Performance Info")]
    void LogPerformanceInfo()
    {
        Debug.Log($"[ARPerformanceManager] 📊 Информация о производительности:");
        Debug.Log($"  Текущий FPS: {currentFPS:F1}");
        Debug.Log($"  Средний FPS: {averageFPS:F1}");
        Debug.Log($"  Уровень качества: {currentQualityLevel + 1}/{qualityLevels.Length}");
        Debug.Log($"  Тепловой стресс: {thermalStress:F2}");
        Debug.Log($"  В режиме охлаждения: {isInCooldown}");
        Debug.Log($"  Активных объектов: {(arObjectManager != null ? arObjectManager.GetActiveObjectCount() : 0)}");
    }
    
    [ContextMenu("Force High Quality")]
    void ForceHighQuality()
    {
        SetQualityLevel(0);
        Debug.Log("[ARPerformanceManager] 🔄 Принудительно установлено высокое качество");
    }
    
    [ContextMenu("Force Low Quality")]
    void ForceLowQuality()
    {
        SetQualityLevel(qualityLevels.Length - 1);
        Debug.Log("[ARPerformanceManager] 🔄 Принудительно установлено низкое качество");
    }
    
    [ContextMenu("Reset Thermal Protection")]
    void ResetThermalProtection()
    {
        ResetThermalStress();
        Application.targetFrameRate = -1; // Снимаем ограничение FPS
        Debug.Log("[ARPerformanceManager] 🔄 Тепловая защита сброшена");
    }
}
