using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Пример настройки AR сцены с оптимизированными компонентами
/// Демонстрирует правильную настройку для устранения мерцания
/// </summary>
public class ARSceneSetupExample : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARObjectManager arObjectManager;
    [SerializeField] private ARPerformanceManager performanceManager;
    [SerializeField] private VideoSpawner videoSpawner;
    
    [Header("UI Components")]
    [SerializeField] private GameObject performancePanel;
    [SerializeField] private Text performanceText;
    [SerializeField] private Slider qualitySlider;
    [SerializeField] private Button optimizeButton;
    [SerializeField] private Button highQualityButton;
    [SerializeField] private Button lowQualityButton;
    
    [Header("Scene Objects")]
    [SerializeField] private GameObject arSessionOrigin;
    [SerializeField] private GameObject arSession;
    [SerializeField] private Camera arCamera;
    
    void Start()
    {
        SetupARScene();
        SetupUI();
        LogSceneInfo();
    }
    
    void SetupARScene()
    {
        Debug.Log("[ARSceneSetupExample] 🚀 Настройка AR сцены...");
        
        // Автопоиск компонентов если не назначены
        if (arObjectManager == null)
        {
            arObjectManager = FindObjectOfType<ARObjectManager>();
            if (arObjectManager == null)
            {
                Debug.LogWarning("[ARSceneSetupExample] ⚠️ ARObjectManager не найден, создаем...");
                GameObject managerObj = new GameObject("AR Object Manager");
                arObjectManager = managerObj.AddComponent<ARObjectManager>();
            }
        }
        
        if (performanceManager == null)
        {
            performanceManager = FindObjectOfType<ARPerformanceManager>();
            if (performanceManager == null)
            {
                Debug.LogWarning("[ARSceneSetupExample] ⚠️ ARPerformanceManager не найден, создаем...");
                GameObject perfObj = new GameObject("AR Performance Manager");
                performanceManager = perfObj.AddComponent<ARPerformanceManager>();
            }
        }
        
        if (videoSpawner == null)
        {
            videoSpawner = FindObjectOfType<VideoSpawner>();
            if (videoSpawner == null)
            {
                Debug.LogWarning("[ARSceneSetupExample] ⚠️ VideoSpawner не найден, создаем...");
                GameObject spawnerObj = new GameObject("Video Spawner");
                videoSpawner = spawnerObj.AddComponent<VideoSpawner>();
            }
        }
        
        // Автопоиск AR объектов
        if (arSessionOrigin == null)
        {
            arSessionOrigin = GameObject.Find("AR Session Origin");
        }
        
        if (arSession == null)
        {
            arSession = GameObject.Find("AR Session");
        }
        
        if (arCamera == null)
        {
            arCamera = FindObjectOfType<Camera>();
        }
        
        // Настраиваем компоненты
        ConfigureARComponents();
        
        Debug.Log("[ARSceneSetupExample] ✅ AR сцена настроена");
    }
    
    void ConfigureARComponents()
    {
        // Настраиваем ARObjectManager
        if (arObjectManager != null)
        {
            // Оптимальные настройки для предотвращения мерцания
            arObjectManager.maxActiveObjects = 8;
            arObjectManager.updateInterval = 0.5f;
            arObjectManager.enableSmoothTransitions = true;
            arObjectManager.enableLOD = true;
            
            Debug.Log("[ARSceneSetupExample] ✅ ARObjectManager настроен");
        }
        
        // Настраиваем ARPerformanceManager
        if (performanceManager != null)
        {
            // Настройки для мобильных устройств
            performanceManager.enablePerformanceMonitoring = true;
            performanceManager.enableAutoOptimization = true;
            performanceManager.enableThermalProtection = true;
            performanceManager.targetFPS = 60;
            performanceManager.minFPS = 30;
            
            Debug.Log("[ARSceneSetupExample] ✅ ARPerformanceManager настроен");
        }
        
        // Настраиваем VideoSpawner
        if (videoSpawner != null)
        {
            // Ограничиваем одновременные загрузки для стабильности
            videoSpawner.maxConcurrentLoads = 3;
            videoSpawner.loadDelay = 0.2f;
            
            Debug.Log("[ARSceneSetupExample] ✅ VideoSpawner настроен");
        }
    }
    
    void SetupUI()
    {
        Debug.Log("[ARSceneSetupExample] 🎨 Настройка UI...");
        
        // Создаем панель производительности если не существует
        if (performancePanel == null)
        {
            CreatePerformancePanel();
        }
        
        // Настраиваем кнопки
        if (optimizeButton != null)
        {
            optimizeButton.onClick.AddListener(OnOptimizeButtonClick);
        }
        
        if (highQualityButton != null)
        {
            highQualityButton.onClick.AddListener(OnHighQualityButtonClick);
        }
        
        if (lowQualityButton != null)
        {
            lowQualityButton.onClick.AddListener(OnLowQualityButtonClick);
        }
        
        Debug.Log("[ARSceneSetupExample] ✅ UI настроен");
    }
    
    void CreatePerformancePanel()
    {
        // Создаем Canvas если не существует
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Performance Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        
        // Создаем панель
        GameObject panelObj = new GameObject("Performance Panel");
        panelObj.transform.SetParent(canvas.transform, false);
        
        // Добавляем Image для фона
        UnityEngine.UI.Image panelImage = panelObj.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);
        
        // Позиционируем панель
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0.7f);
        panelRect.anchorMax = new Vector2(0.4f, 1);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        // Создаем текст производительности
        GameObject textObj = new GameObject("Performance Text");
        textObj.transform.SetParent(panelObj.transform, false);
        
        performanceText = textObj.AddComponent<Text>();
        performanceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        performanceText.fontSize = 14;
        performanceText.color = Color.white;
        performanceText.text = "Производительность AR\nЗагрузка...";
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.1f, 0.1f);
        textRect.anchorMax = new Vector2(0.9f, 0.9f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Создаем слайдер качества
        GameObject sliderObj = new GameObject("Quality Slider");
        sliderObj.transform.SetParent(panelObj.transform, false);
        
        qualitySlider = sliderObj.AddComponent<Slider>();
        qualitySlider.minValue = 0;
        qualitySlider.maxValue = 3;
        qualitySlider.value = 1;
        
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.1f, 0.05f);
        sliderRect.anchorMax = new Vector2(0.9f, 0.15f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;
        
        // Создаем кнопки
        CreateButton("Optimize", new Vector2(0.1f, 0.2f), new Vector2(0.4f, 0.3f), OnOptimizeButtonClick);
        CreateButton("High Quality", new Vector2(0.5f, 0.2f), new Vector2(0.9f, 0.3f), OnHighQualityButtonClick);
        CreateButton("Low Quality", new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.45f), OnLowQualityButtonClick);
        
        performancePanel = panelObj;
        
        Debug.Log("[ARSceneSetupExample] ✅ Панель производительности создана");
    }
    
    void CreateButton(string text, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObj = new GameObject(text + " Button");
        buttonObj.transform.SetParent(performancePanel.transform, false);
        
        Button button = buttonObj.AddComponent<Button>();
        UnityEngine.UI.Image buttonImage = buttonObj.AddComponent<UnityEngine.UI.Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        // Создаем текст кнопки
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        Text buttonText = textObj.AddComponent<Text>();
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = 12;
        buttonText.color = Color.white;
        buttonText.text = text;
        buttonText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Позиционируем кнопку
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorMin;
        buttonRect.anchorMax = anchorMax;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        
        // Добавляем обработчик
        button.onClick.AddListener(onClick);
    }
    
    void OnOptimizeButtonClick()
    {
        if (performanceManager != null)
        {
            performanceManager.ForceQualityLevel(1);
            Debug.Log("[ARSceneSetupExample] 🔄 Оптимизация производительности активирована");
        }
    }
    
    void OnHighQualityButtonClick()
    {
        if (performanceManager != null)
        {
            performanceManager.ForceQualityLevel(0);
            Debug.Log("[ARSceneSetupExample] 🎯 Высокое качество активировано");
        }
    }
    
    void OnLowQualityButtonClick()
    {
        if (performanceManager != null)
        {
            performanceManager.ForceQualityLevel(3);
            Debug.Log("[ARSceneSetupExample] 🔋 Низкое качество активировано для экономии батареи");
        }
    }
    
    void LogSceneInfo()
    {
        Debug.Log("=== ИНФОРМАЦИЯ О AR СЦЕНЕ ===");
        Debug.Log($"AR Session Origin: {(arSessionOrigin != null ? "найден" : "НЕ НАЙДЕН")}");
        Debug.Log($"AR Session: {(arSession != null ? "найден" : "НЕ НАЙДЕН")}");
        Debug.Log($"AR Camera: {(arCamera != null ? "найдена" : "НЕ НАЙДЕНА")}");
        Debug.Log($"AR Object Manager: {(arObjectManager != null ? "найден" : "НЕ НАЙДЕН")}");
        Debug.Log($"AR Performance Manager: {(performanceManager != null ? "найден" : "НЕ НАЙДЕН")}");
        Debug.Log($"Video Spawner: {(videoSpawner != null ? "найден" : "НЕ НАЙДЕН")}");
        Debug.Log($"Performance Panel: {(performancePanel != null ? "создана" : "НЕ СОЗДАНА")}");
        Debug.Log("=== КОНЕЦ ИНФОРМАЦИИ ===");
    }
    
    void Update()
    {
        // Обновляем UI каждые 0.5 секунды
        if (Time.frameCount % 30 == 0) // При 60 FPS = каждые 0.5 секунды
        {
            UpdatePerformanceUI();
        }
    }
    
    void UpdatePerformanceUI()
    {
        if (performanceText == null || performanceManager == null) return;
        
        string info = $"🚀 Производительность AR\n\n";
        info += $"FPS: {performanceManager.GetCurrentFPS():F1}\n";
        info += $"Средний FPS: {performanceManager.GetAverageFPS():F1}\n";
        info += $"Качество: {performanceManager.GetCurrentQualityLevel() + 1}/4\n";
        info += $"Тепловой стресс: {performanceManager.GetThermalStress():F2}\n";
        
        if (arObjectManager != null)
        {
            info += $"Активных объектов: {arObjectManager.GetActiveObjectCount()}\n";
            info += $"Размер пула: {arObjectManager.GetPoolSize()}";
        }
        
        performanceText.text = info;
    }
    
    // Методы для отладки
    [ContextMenu("Log Scene Info")]
    void LogSceneInfoFromMenu()
    {
        LogSceneInfo();
    }
    
    [ContextMenu("Force Optimize")]
    void ForceOptimizeFromMenu()
    {
        OnOptimizeButtonClick();
    }
    
    [ContextMenu("Create Test Objects")]
    void CreateTestObjects()
    {
        if (arObjectManager != null)
        {
            // Создаем тестовые объекты для демонстрации
            for (int i = 0; i < 3; i++)
            {
                ARObjectManager.GeoObject testObj = new ARObjectManager.GeoObject
                {
                    id = $"test_{i}",
                    x = Random.Range(-10f, 10f),
                    y = Random.Range(-10f, 10f),
                    objectType = "video",
                    objectURL = "https://sample-videos.com/zip/10/mp4/SampleVideo_1280x720_1mb.mp4"
                };
                
                var arObj = arObjectManager.CreateARObject(testObj);
            }
            
            Debug.Log("[ARSceneSetupExample] 🧪 Тестовые объекты созданы");
        }
    }
}
