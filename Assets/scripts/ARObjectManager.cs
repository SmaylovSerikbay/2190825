using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using ARLocation;

/// <summary>
/// Оптимизированный менеджер AR объектов для предотвращения мерцания и улучшения производительности
/// </summary>
public class ARObjectManager : MonoBehaviour
{
    [Header("Performance Settings")]
    [SerializeField] public int maxActiveObjects = 8; // Максимум активных объектов
    [SerializeField] public float updateInterval = 0.5f; // Интервал обновления (вместо каждого кадра)
    [SerializeField] private float activationDistance = 250f; // Дистанция активации (увеличено до 250м)
    [SerializeField] private float deactivationDistance = 275f; // Дистанция деактивации с гистерезисом (250 + 25)
    
    [Header("Smooth Transitions")]
    [SerializeField] private float fadeInDuration = 0.3f; // Длительность появления
    [SerializeField] private float fadeOutDuration = 0.2f; // Длительность исчезновения
    [SerializeField] public bool enableSmoothTransitions = true; // Включить плавные переходы
    
    [Header("LOD Settings")]
    [SerializeField] public bool enableLOD = true; // Включить систему LOD
    [SerializeField] private float[] lodDistances = { 50f, 100f, 200f }; // Дистанции для разных уровней детализации (адаптировано под 250м радиус)
    [SerializeField] private int[] lodMaxObjects = { 8, 6, 4 }; // Максимум объектов для каждого LOD уровня (менее агрессивно)
    
    [Header("Object Pooling")]
    [SerializeField] private int initialPoolSize = 15; // Начальный размер пула
    [SerializeField] private bool enableDynamicPooling = true; // Динамическое расширение пула
    
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private GameObject videoPrefab;
    [SerializeField] private UnityEngine.UI.Text loadingIndicator;
    
    // Приватные переменные
    private Dictionary<string, ARObject> managedObjects = new Dictionary<string, ARObject>();
    private Queue<ARObject> objectPool = new Queue<ARObject>();
    private List<ARObject> activeObjects = new List<ARObject>();
    private Coroutine updateCoroutine;
    
    // Класс для управления AR объектом
    [System.Serializable]
    public class ARObject
    {
        public string id;
        public GameObject gameObject;
        public VideoPlayer videoPlayer;
        public CanvasGroup canvasGroup;
        public PlaceAtLocation placeAtLocation;
        public LookAtCamera lookAtCamera;
        public float lastUpdateTime;
        public int currentLOD;
        public bool isTransitioning;
        public Vector3 targetPosition;
        
        public ARObject(string id, GameObject obj)
        {
            this.id = id;
            this.gameObject = obj;
            this.videoPlayer = obj.GetComponentInChildren<VideoPlayer>();
            this.canvasGroup = obj.GetComponent<CanvasGroup>();
            this.placeAtLocation = obj.GetComponent<PlaceAtLocation>();
            this.lookAtCamera = obj.GetComponentInChildren<LookAtCamera>();
            this.lastUpdateTime = Time.time;
            this.currentLOD = 0;
            this.isTransitioning = false;
        }
    }
    
    // Класс для геолокации объекта
    [System.Serializable]
    public class GeoObject
    {
        public string id;
        public float x;
        public float y;
        public string objectType;
        public string objectURL;
    }
    
    void Start()
    {
        InitializeManager();
        
        // ИСПРАВЛЕНО: Загружаем объекты из кэша если он актуален
        if (IsCacheValid())
        {
            LoadObjectsFromCache();
            Debug.Log("[ARObjectManager] 📱 Объекты загружены из кэша");
            
            // ИСПРАВЛЕНО: Принудительно активируем все объекты из кэша
            StartCoroutine(ActivateCachedObjectsAfterDelay());
            
            // ИСПРАВЛЕНО: Дополнительно переинициализируем видео для устранения зависания
            StartCoroutine(ReinitializeCachedVideosAfterDelay());
        }
        else
        {
            Debug.Log("[ARObjectManager] 📱 Кэш не валиден или отсутствует");
        }
        
        // ИСПРАВЛЕНО: Запускаем автоматическое сохранение в кэш
        StartCoroutine(AutoSaveCache());
    }
    
    void InitializeManager()
    {
        // Автопоиск камеры если не привязана
        if (cameraTransform == null)
        {
            Camera arCamera = FindObjectOfType<Camera>();
            if (arCamera != null)
            {
                cameraTransform = arCamera.transform;
                Debug.Log("[ARObjectManager] ✅ Камера найдена автоматически");
            }
        }
        
        // Создаем пул объектов
        InitializeObjectPool();
        
        // Запускаем оптимизированное обновление
        if (updateCoroutine != null)
            StopCoroutine(updateCoroutine);
        updateCoroutine = StartCoroutine(OptimizedUpdate());
        
        Debug.Log("[ARObjectManager] ✅ Менеджер инициализирован");
    }
    
    void InitializeObjectPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePooledObject();
        }
        Debug.Log($"[ARObjectManager] ✅ Пул объектов создан: {initialPoolSize} объектов");
    }
    
    void CreatePooledObject()
    {
        if (videoPrefab == null)
        {
            Debug.LogError("[ARObjectManager] ❌ VideoPrefab не назначен!");
            return;
        }
        
        GameObject obj = Instantiate(videoPrefab);
        obj.SetActive(false);
        
        // Добавляем CanvasGroup для плавных переходов
        CanvasGroup canvasGroup = obj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = obj.AddComponent<CanvasGroup>();
        }
        
        // Создаем ARObject
        ARObject arObj = new ARObject("pooled", obj);
        objectPool.Enqueue(arObj);
    }
    
    ARObject GetPooledObject()
    {
        if (objectPool.Count > 0)
        {
            return objectPool.Dequeue();
        }
        
        // Динамически создаем новый объект если пул пуст
        if (enableDynamicPooling)
        {
            CreatePooledObject();
            if (objectPool.Count > 0)
            {
                return objectPool.Dequeue();
            }
        }
        
        return null;
    }
    
    void ReturnToPool(ARObject arObj)
    {
        if (arObj == null || arObj.gameObject == null) return;
        
        // Останавливаем видео
        if (arObj.videoPlayer != null && arObj.videoPlayer.isPlaying)
        {
            arObj.videoPlayer.Stop();
        }
        
        // Скрываем объект
        arObj.gameObject.SetActive(false);
        
        // Возвращаем в пул
        objectPool.Enqueue(arObj);
        
        // Убираем из активных
        activeObjects.Remove(arObj);
    }
    
    /// <summary>
    /// Создает новый AR объект
    /// </summary>
    public ARObject CreateARObject(GeoObject geoData)
    {
        if (managedObjects.ContainsKey(geoData.id))
        {
            Debug.Log($"[ARObjectManager] ⚠️ Объект {geoData.id} уже существует");
            return null;
        }
        
        ARObject arObj = GetPooledObject();
        if (arObj == null)
        {
            Debug.LogWarning("[ARObjectManager] ⚠️ Не удалось получить объект из пула");
            return null;
        }
        
        // Настраиваем объект
        SetupARObject(arObj, geoData);
        
        // Добавляем в управляемые
        managedObjects[geoData.id] = arObj;
        
        // ИСПРАВЛЕНО: Сразу добавляем в активные объекты
        if (!activeObjects.Contains(arObj))
        {
            activeObjects.Add(arObj);
            Debug.Log($"[ARObjectManager] ✅ Объект {geoData.id} добавлен в активные (всего: {activeObjects.Count})");
        }
        
        // ИСПРАВЛЕНО: Автоматически сохраняем в кэш при создании
        SaveObjectsToCache();
        
        Debug.Log($"[ARObjectManager] ✅ Создан AR объект: {geoData.id}");
        
        return arObj;
    }
    
    void SetupARObject(ARObject arObj, GeoObject geoData)
    {
        GameObject obj = arObj.gameObject;
        
        // Обновляем ID
        arObj.id = geoData.id;
        
        // Создаем плоскость для видео
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.parent = obj.transform;
        
        float scaleFactor = 0.2f;
        plane.transform.localScale = new Vector3(scaleFactor * (16f / 9f), 1f, scaleFactor);
        plane.transform.localPosition = new Vector3(0, 0.5f, 0);
        
        // Настраиваем VideoPlayer
        if (arObj.videoPlayer == null)
        {
            arObj.videoPlayer = plane.AddComponent<VideoPlayer>();
        }
        
        arObj.videoPlayer.source = VideoSource.Url;
        arObj.videoPlayer.url = geoData.objectURL;
        arObj.videoPlayer.isLooping = true;
        arObj.videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
        
        // ВОССТАНОВЛЕНО: Правильные настройки для отображения видео
        arObj.videoPlayer.playOnAwake = true; // Запускаем автоматически
        arObj.videoPlayer.waitForFirstFrame = true; // Ждем первый кадр
        
        // Настраиваем материал
        Renderer planeRenderer = plane.GetComponent<Renderer>();
        Material chromaKeyMaterial = new Material(Shader.Find("Custom/ChromaKeyShader"));
        chromaKeyMaterial.SetColor("_ChromaKeyColor", new Color(0f / 255f, 154f / 255f, 61f / 255f, 1));
        chromaKeyMaterial.SetFloat("_Threshold", 0.1f);
        planeRenderer.material = chromaKeyMaterial;
        
        arObj.videoPlayer.targetMaterialRenderer = planeRenderer;
        arObj.videoPlayer.targetMaterialProperty = "_MainTex";
        
        // Настраиваем геолокацию
        if (arObj.placeAtLocation == null)
        {
            arObj.placeAtLocation = obj.GetComponent<PlaceAtLocation>();
            if (arObj.placeAtLocation == null)
            {
                arObj.placeAtLocation = obj.AddComponent<PlaceAtLocation>();
            }
        }
        
        Location location = new Location
        {
            Latitude = (double)geoData.x,
            Longitude = (double)geoData.y,
            Altitude = 0,
            AltitudeMode = AltitudeMode.GroundRelative
        };
        arObj.placeAtLocation.Location = location;
        
        // Настраиваем поворот к камере
        if (arObj.lookAtCamera == null)
        {
            arObj.lookAtCamera = plane.GetComponent<LookAtCamera>();
            if (arObj.lookAtCamera == null)
            {
                arObj.lookAtCamera = plane.AddComponent<LookAtCamera>();
            }
        }
        
        if (cameraTransform != null)
        {
            arObj.lookAtCamera.cameraTransform = cameraTransform;
        }
        
        // Настраиваем CanvasGroup для плавных переходов
        if (arObj.canvasGroup == null)
        {
            arObj.canvasGroup = obj.GetComponent<CanvasGroup>();
            if (arObj.canvasGroup == null)
            {
                arObj.canvasGroup = obj.AddComponent<CanvasGroup>();
            }
        }
        
        // Показываем индикатор загрузки
        if (loadingIndicator != null)
        {
            loadingIndicator.gameObject.SetActive(true);
        }
        
        // Подготавливаем видео
        arObj.videoPlayer.prepareCompleted += (VideoPlayer vp) =>
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.gameObject.SetActive(false);
            }
            
            // Добавляем объект в активные СРАЗУ после подготовки
            if (!activeObjects.Contains(arObj))
            {
                activeObjects.Add(arObj);
                Debug.Log($"[ARObjectManager] ✅ Объект {geoData.id} добавлен в активные (всего: {activeObjects.Count})");
            }
            
                    // ВОССТАНОВЛЕНО: Простая проверка зацикливания
        if (vp.isPrepared)
        {
            // Проверяем зацикливание
            if (!vp.isLooping)
            {
                vp.isLooping = true;
                Debug.Log($"[ARObjectManager] 🔄 Включено зацикливание для: {geoData.id}");
            }
            
            // Видео уже запустится автоматически (playOnAwake = true)
            Debug.Log($"[ARObjectManager] 🎥 Видео готово к воспроизведению: {geoData.id}");
        }
            
            // Плавно показываем объект
            if (enableSmoothTransitions)
            {
                StartCoroutine(FadeInObject(arObj));
            }
            else
            {
                obj.SetActive(true);
            }
            
            Debug.Log($"[ARObjectManager] ✅ Видео подготовлено: {geoData.id}");
        };
        
        // ИСПРАВЛЕНО: Обработчик завершения видео для принудительного зацикливания
        arObj.videoPlayer.loopPointReached += (VideoPlayer vp) =>
        {
            Debug.Log($"[ARObjectManager] 🔄 Видео завершено, перезапуск: {geoData.id}");
            if (vp.isPrepared)
            {
                vp.Play();
            }
        };
        
        arObj.videoPlayer.errorReceived += (VideoPlayer vp, string errorMsg) =>
        {
            Debug.LogError($"[ARObjectManager] ❌ Ошибка видео {geoData.id}: {errorMsg}");
            if (loadingIndicator != null)
            {
                loadingIndicator.gameObject.SetActive(false);
            }
            
            // Возвращаем объект в пул при ошибке
            ReturnToPool(arObj);
        };
        
        arObj.videoPlayer.Prepare();
    }
    
    /// <summary>
    /// Плавное появление объекта
    /// </summary>
    IEnumerator FadeInObject(ARObject arObj)
    {
        if (arObj.canvasGroup == null) yield break;
        
        arObj.isTransitioning = true;
        arObj.gameObject.SetActive(true);
        activeObjects.Add(arObj);
        
        // Начинаем с прозрачности 0
        arObj.canvasGroup.alpha = 0f;
        
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeInDuration;
            arObj.canvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);
            yield return null;
        }
        
        arObj.canvasGroup.alpha = 1f;
        arObj.isTransitioning = false;
    }
    
    /// <summary>
    /// Плавное исчезновение объекта
    /// </summary>
    IEnumerator FadeOutObject(ARObject arObj)
    {
        if (arObj.canvasGroup == null) yield break;
        
        arObj.isTransitioning = true;
        
        float elapsed = 0f;
        float startAlpha = arObj.canvasGroup.alpha;
        
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeOutDuration;
            arObj.canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);
            yield return null;
        }
        
        arObj.canvasGroup.alpha = 0f;
        arObj.isTransitioning = false;
        
        // Возвращаем в пул
        ReturnToPool(arObj);
    }
    
    /// <summary>
    /// Оптимизированное обновление вместо Update()
    /// </summary>
    IEnumerator OptimizedUpdate()
    {
        while (true)
        {
            if (enabled && cameraTransform != null)
            {
                UpdateObjectVisibility();
            }
            
            yield return new WaitForSeconds(updateInterval);
        }
    }
    
    void UpdateObjectVisibility()
    {
        if (activeObjects.Count == 0) return;
        
        Vector3 cameraPos = cameraTransform.position;
        int currentLOD = GetCurrentLODLevel();
        int maxObjectsForLOD = GetMaxObjectsForLOD(currentLOD);
        
        // Сортируем объекты по расстоянию
        activeObjects.Sort((a, b) => 
        {
            float distA = Vector3.Distance(cameraPos, a.gameObject.transform.position);
            float distB = Vector3.Distance(cameraPos, b.gameObject.transform.position);
            return distA.CompareTo(distB);
        });
        
        // Обрабатываем все объекты, но с разными приоритетами
        for (int i = 0; i < activeObjects.Count; i++)
        {
            ARObject arObj = activeObjects[i];
            if (arObj == null || arObj.gameObject == null) continue;
            
            float distance = Vector3.Distance(cameraPos, arObj.gameObject.transform.position);
            
            // ИСПРАВЛЕНО: Объекты активируются только по расстоянию, не по индексу
            bool shouldBeActive = distance < activationDistance;
            bool isCurrentlyActive = arObj.gameObject.activeInHierarchy && arObj.canvasGroup.alpha > 0.1f;
            
            // Применяем LOD ограничения только для дальних объектов
            bool isHighPriority = i < maxObjectsForLOD;
            bool isLowPriority = !isHighPriority && distance > lodDistances[Mathf.Min(currentLOD, lodDistances.Length - 1)];
            
            if (shouldBeActive && !isCurrentlyActive && !arObj.isTransitioning)
            {
                // Активируем объект если он в пределах дистанции
                if (enableSmoothTransitions)
                {
                    StartCoroutine(FadeInObject(arObj));
                }
                else
                {
                    arObj.gameObject.SetActive(true);
                                    // ВОССТАНОВЛЕНО: Простая активация без принудительного запуска видео
                if (arObj.videoPlayer != null && !arObj.videoPlayer.isPrepared)
                {
                    // Только если видео не готово, переподготавливаем
                    arObj.videoPlayer.Prepare();
                    Debug.Log($"[ARObjectManager] 🔄 Видео переподготавливается: {arObj.id}");
                }
                }
                
                Debug.Log($"[ARObjectManager] ✅ Объект {arObj.id} активирован (расстояние: {distance:F1}м, приоритет: {(isHighPriority ? "высокий" : "низкий")})");
            }
            else if (!shouldBeActive && isCurrentlyActive && !arObj.isTransitioning)
            {
                // Деактивируем объект только если он слишком далеко
                if (enableSmoothTransitions)
                {
                    StartCoroutine(FadeOutObject(arObj));
                }
                else
                {
                    // ИСПРАВЛЕНО: Приостановка видео при деактивации
                    if (arObj.videoPlayer != null && arObj.videoPlayer.isPlaying)
                    {
                        arObj.videoPlayer.Pause();
                        Debug.Log($"[ARObjectManager] ⏸️ Видео приостановлено при деактивации: {arObj.id}");
                    }
                    ReturnToPool(arObj);
                }
                
                Debug.Log($"[ARObjectManager] ❌ Объект {arObj.id} деактивирован (расстояние: {distance:F1}м)");
            }
            
            // Обновляем LOD уровень
            if (enableLOD)
            {
                UpdateObjectLOD(arObj, distance);
            }
        }
    }
    
    int GetCurrentLODLevel()
    {
        if (!enableLOD) return 0;
        
        // ИСПРАВЛЕНО: Более мягкая LOD система
        // Определяем LOD уровень на основе FPS или других метрик
        float currentFPS = 1f / Time.deltaTime;
        
        // Более мягкие пороги для предотвращения агрессивного снижения качества
        if (currentFPS < 15f) return 2; // Низкий LOD только при критически плохой производительности
        if (currentFPS < 25f) return 1; // Средний LOD при плохой производительности
        return 0; // Высокий LOD при нормальной производительности
    }
    
    int GetMaxObjectsForLOD(int lodLevel)
    {
        if (lodLevel < lodMaxObjects.Length)
        {
            return Mathf.Min(lodMaxObjects[lodLevel], maxActiveObjects);
        }
        return maxActiveObjects;
    }
    
    void UpdateObjectLOD(ARObject arObj, float distance)
    {
        if (arObj.videoPlayer == null) return;
        
        // Определяем LOD уровень на основе расстояния
        int newLOD = 0;
        for (int i = 0; i < lodDistances.Length; i++)
        {
            if (distance > lodDistances[i])
            {
                newLOD = i + 1;
            }
        }
        
        if (newLOD != arObj.currentLOD)
        {
            arObj.currentLOD = newLOD;
            
            // Применяем настройки LOD
            switch (newLOD)
            {
                case 0: // Высокое качество
                    arObj.videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
                    break;
                case 1: // Среднее качество
                    arObj.videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
                    break;
                case 2: // Низкое качество
                    arObj.videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
                    break;
            }
        }
    }
    
    /// <summary>
    /// Очищает все объекты
    /// </summary>
    public void ClearAllObjects()
    {
        foreach (var arObj in activeObjects.ToArray())
        {
            if (arObj != null)
            {
                ReturnToPool(arObj);
            }
        }
        
        managedObjects.Clear();
        Debug.Log("[ARObjectManager] ✅ Все объекты очищены");
    }
    
    /// <summary>
    /// Получает количество активных объектов
    /// </summary>
    public int GetActiveObjectCount()
    {
        int count = activeObjects.Count;
        Debug.Log($"[ARObjectManager] 📊 GetActiveObjectCount: {count} активных объектов");
        
        // Расширенная диагностика
        if (count == 0 && managedObjects.Count > 0)
        {
            Debug.LogWarning($"[ARObjectManager] ⚠️ Нет активных объектов, но {managedObjects.Count} управляемых объектов");
            foreach (var entry in managedObjects)
            {
                if (entry.Value != null && entry.Value.gameObject != null)
                {
                    bool inActiveList = activeObjects.Contains(entry.Value);
                    bool isActiveInHierarchy = entry.Value.gameObject.activeInHierarchy;
                    bool hasVideoPlayer = entry.Value.videoPlayer != null;
                    bool isVideoPrepared = hasVideoPlayer && entry.Value.videoPlayer.isPrepared;
                    
                    Debug.LogWarning($"  - {entry.Key}: активен={isActiveInHierarchy}, в активных={inActiveList}, видео готово={isVideoPrepared}");
                    
                    // Проверяем расстояние до камеры
                    if (cameraTransform != null)
                    {
                        float distance = Vector3.Distance(cameraTransform.position, entry.Value.gameObject.transform.position);
                        Debug.LogWarning($"    Расстояние до камеры: {distance:F1}м (порог активации: {activationDistance}m)");
                    }
                }
            }
        }
        
        // Дополнительная информация о состоянии системы
        Debug.Log($"[ARObjectManager] 📊 Состояние системы:");
        Debug.Log($"  - LOD включен: {enableLOD}");
        Debug.Log($"  - Плавные переходы: {enableSmoothTransitions}");
        Debug.Log($"  - Максимум активных объектов: {maxActiveObjects}");
        Debug.Log($"  - Дистанция активации: {activationDistance}m");
        Debug.Log($"  - Размер пула: {objectPool.Count}");
        
        return count;
    }
    
    /// <summary>
    /// Получает общее количество объектов в пуле
    /// </summary>
    public int GetPoolSize()
    {
        return objectPool.Count + activeObjects.Count;
    }
    
    void OnDestroy()
    {
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
        
        // ИСПРАВЛЕНО: Сохраняем объекты в кэш перед уничтожением
        SaveObjectsToCache();
        ClearAllObjects();
    }
    
    // Методы для отладки
    [ContextMenu("Log Performance Info")]
    void LogPerformanceInfo()
    {
        Debug.Log($"[ARObjectManager] 📊 Информация о производительности:");
        Debug.Log($"  Активных объектов: {GetActiveObjectCount()}");
        Debug.Log($"  Размер пула: {GetPoolSize()}");
        Debug.Log($"  FPS: {1f / Time.deltaTime:F1}");
        Debug.Log($"  Интервал обновления: {updateInterval}s");
    }
    
    [ContextMenu("Force Update Objects")]
    void ForceUpdateObjects()
    {
        UpdateObjectVisibility();
        Debug.Log("[ARObjectManager] 🔄 Принудительное обновление объектов");
    }
    
    [ContextMenu("Force Activate All Objects")]
    public void ForceActivateAllObjects()
    {
        Debug.Log($"[ARObjectManager] 🚀 Принудительная активация всех объектов...");
        Debug.Log($"  Управляемых объектов: {managedObjects.Count}");
        Debug.Log($"  Активных объектов: {activeObjects.Count}");
        
        int activatedCount = 0;
        
        foreach (var entry in managedObjects)
        {
            if (entry.Value != null && entry.Value.gameObject != null)
            {
                // Добавляем в активные если еще не добавлен
                if (!activeObjects.Contains(entry.Value))
                {
                    activeObjects.Add(entry.Value);
                    Debug.Log($"  ✅ Добавлен в активные: {entry.Key}");
                }
                
                // Активируем GameObject
                if (!entry.Value.gameObject.activeInHierarchy)
                {
                    entry.Value.gameObject.SetActive(true);
                    activatedCount++;
                    Debug.Log($"  ✅ Активирован GameObject: {entry.Key}");
                }
                
                // ИСПРАВЛЕНО: Принудительная переинициализация видео для устранения зависания
                if (entry.Value.videoPlayer != null)
                {
                    // Проверяем, не зависло ли видео
                    if (entry.Value.videoPlayer.isPrepared && !entry.Value.videoPlayer.isPlaying)
                    {
                        // Видео готово но не воспроизводится - возможно зависло
                        Debug.Log($"  🔄 Видео зависло, переинициализируем: {entry.Key}");
                        entry.Value.videoPlayer.Stop();
                        entry.Value.videoPlayer.Prepare();
                    }
                    else if (!entry.Value.videoPlayer.isPrepared)
                    {
                        Debug.Log($"  🔄 Переподготовка видео: {entry.Key}");
                        entry.Value.videoPlayer.Prepare();
                    }
                    else
                    {
                        Debug.Log($"  🎥 Видео работает нормально: {entry.Key}");
                    }
                }
                
                // ИСПРАВЛЕНО: Проверяем позицию объектов из кэша
                if (entry.Value.placeAtLocation != null)
                {
                    // Позиция уже установлена при создании объекта
                    Debug.Log($"[ARObjectManager] 📍 Позиция объекта проверена: {entry.Key}");
                }
            }
        }
        
        Debug.Log($"[ARObjectManager] 🎯 Принудительная активация завершена!");
        Debug.Log($"  - Активировано GameObject'ов: {activatedCount}");
        Debug.Log($"  - Всего активных объектов: {activeObjects.Count}");
        Debug.Log($"  - Управляемых объектов: {managedObjects.Count}");
    }
    
    /// <summary>
    /// Принудительно перезапускает все видео
    /// </summary>
    [ContextMenu("Restart All Videos")]
    public void RestartAllVideos()
    {
        Debug.Log($"[ARObjectManager] 🔄 Перезапуск всех видео...");
        int restartedCount = 0;
        
        foreach (var entry in managedObjects)
        {
            if (entry.Value != null && entry.Value.videoPlayer != null)
            {
                if (entry.Value.videoPlayer.isPrepared)
                {
                    entry.Value.videoPlayer.Stop();
                    entry.Value.videoPlayer.Play();
                    restartedCount++;
                    Debug.Log($"  🔄 Видео перезапущено: {entry.Key}");
                }
                else
                {
                    entry.Value.videoPlayer.Prepare();
                    Debug.Log($"  🔄 Видео переподготавливается: {entry.Key}");
                }
            }
        }
        
        Debug.Log($"[ARObjectManager] 🎯 Перезапуск видео завершен! Перезапущено: {restartedCount}");
    }
    
    /// <summary>
    /// Принудительно переинициализирует все видео из кэша для устранения зависания
    /// </summary>
    [ContextMenu("Reinitialize Cached Videos")]
    public void ReinitializeCachedVideos()
    {
        Debug.Log($"[ARObjectManager] 🔄 Переинициализация всех видео из кэша...");
        int reinitializedCount = 0;
        
        foreach (var entry in managedObjects)
        {
            if (entry.Value != null && entry.Value.videoPlayer != null)
            {
                // Принудительно переинициализируем видео
                entry.Value.videoPlayer.Stop();
                entry.Value.videoPlayer.url = entry.Value.videoPlayer.url; // Обновляем URL
                entry.Value.videoPlayer.Prepare();
                reinitializedCount++;
                Debug.Log($"  🔄 Видео переинициализировано: {entry.Key}");
            }
        }
        
        Debug.Log($"[ARObjectManager] 🎯 Переинициализация видео завершена! Обработано: {reinitializedCount}");
    }
    
    /// <summary>
    /// Сохраняет объекты в кэш для быстрой загрузки
    /// </summary>
    private void SaveObjectsToCache()
    {
        try
        {
            var cacheData = new List<ARObjectCacheItem>();
            foreach (var entry in managedObjects)
            {
                if (entry.Value != null)
                {
                    // Сохраняем полные данные объекта для восстановления
                    var objData = new ARObjectCacheItem
                    {
                        id = entry.Key,
                        x = (float)(entry.Value.placeAtLocation?.Location?.Latitude ?? 0.0),
                        y = (float)(entry.Value.placeAtLocation?.Location?.Longitude ?? 0.0),
                        objectURL = entry.Value.videoPlayer?.url ?? "",
                        timestamp = System.DateTime.Now.Ticks
                    };
                    cacheData.Add(objData);
                }
            }
            
            string jsonData = JsonUtility.ToJson(new ARObjectsCacheData { objects = cacheData });
            PlayerPrefs.SetString("ARObjectsCache", jsonData);
            PlayerPrefs.SetString("ARObjectsCacheTimestamp", System.DateTime.Now.Ticks.ToString());
            PlayerPrefs.Save();
            
            Debug.Log($"[ARObjectManager] 💾 Кэш сохранен: {cacheData.Count} объектов");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ARObjectManager] ❌ Ошибка сохранения кэша: {e.Message}");
        }
    }
    
    /// <summary>
    /// Загружает объекты из кэша
    /// </summary>
    private void LoadObjectsFromCache()
    {
        try
        {
            if (!PlayerPrefs.HasKey("ARObjectsCache")) return;
            
            string jsonData = PlayerPrefs.GetString("ARObjectsCache");
            var cacheData = JsonUtility.FromJson<ARObjectsCacheData>(jsonData);
            
            if (cacheData != null && cacheData.objects != null)
            {
                Debug.Log($"[ARObjectManager] 📱 Загружаем {cacheData.objects.Count} объектов из кэша...");
                
                foreach (var objData in cacheData.objects)
                {
                    // Создаем GeoObject из кэша с полными данными
                    var geoData = new GeoObject
                    {
                        id = objData.id,
                        x = objData.x, // Используем сохраненные координаты
                        y = objData.y, // Используем сохраненные координаты
                        objectURL = objData.objectURL // Используем сохраненный URL
                    };
                    
                    // Создаем объект из кэша и размещаем по координатам
                    var arObj = CreateARObject(geoData);
                    if (arObj != null && arObj.gameObject != null)
                    {
                        // Размещаем объект по сохраненным координатам
                        Vector3 worldPosition = new Vector3(geoData.x, 0, geoData.y);
                        arObj.gameObject.transform.position = worldPosition;
                        
                        // ИСПРАВЛЕНО: Обновляем Location в PlaceAtLocation
                        if (arObj.placeAtLocation != null)
                        {
                            Location location = new Location
                            {
                                Latitude = (double)geoData.x,
                                Longitude = (double)geoData.y,
                                Altitude = 0,
                                AltitudeMode = AltitudeMode.GroundRelative
                            };
                            arObj.placeAtLocation.Location = location;
                        }
                        
                        Debug.Log($"[ARObjectManager] 📍 Объект из кэша размещен: {geoData.id} в позиции {worldPosition}");
                    }
                }
                
                Debug.Log($"[ARObjectManager] ✅ Кэш загружен: {cacheData.objects.Count} объектов");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ARObjectManager] ❌ Ошибка загрузки кэша: {e.Message}");
        }
    }
    
    /// <summary>
    /// Проверяет актуальность кэша
    /// </summary>
    private bool IsCacheValid()
    {
        if (!PlayerPrefs.HasKey("ARObjectsCacheTimestamp")) return false;
        
        try
        {
            long timestamp = long.Parse(PlayerPrefs.GetString("ARObjectsCacheTimestamp"));
            var cacheTime = System.DateTime.FromBinary(timestamp);
            var timeSinceCache = System.DateTime.Now - cacheTime;
            
            // Кэш действителен 1 час
            return timeSinceCache.TotalHours < 1;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Очищает устаревший кэш
    /// </summary>
    [ContextMenu("Clear Cache")]
    public void ClearCache()
    {
        PlayerPrefs.DeleteKey("ARObjectsCache");
        PlayerPrefs.DeleteKey("ARObjectsCacheTimestamp");
        PlayerPrefs.Save();
        Debug.Log("[ARObjectManager] 🗑️ Кэш очищен");
    }
    
    /// <summary>
    /// Принудительно загружает объекты из кэша
    /// </summary>
    [ContextMenu("Load From Cache")]
    public void LoadFromCache()
    {
        if (IsCacheValid())
        {
            LoadObjectsFromCache();
        }
        else
        {
            Debug.Log("[ARObjectManager] ⚠️ Кэш недействителен или отсутствует");
        }
    }
    
    /// <summary>
    /// Принудительно сохраняет объекты в кэш
    /// </summary>
    [ContextMenu("Save To Cache")]
    public void SaveToCache()
    {
        SaveObjectsToCache();
    }
    
    /// <summary>
    /// Автоматическое сохранение в кэш каждые 30 секунд
    /// </summary>
    IEnumerator AutoSaveCache()
    {
        while (true)
        {
            yield return new WaitForSeconds(30f);
            
            if (managedObjects.Count > 0)
            {
                SaveObjectsToCache();
                Debug.Log($"[ARObjectManager] 💾 Автосохранение в кэш: {managedObjects.Count} объектов");
            }
        }
    }
    
    /// <summary>
    /// Принудительная активация объектов из кэша с задержкой
    /// </summary>
    private IEnumerator ActivateCachedObjectsAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        
        Debug.Log("[ARObjectManager] 🔄 Принудительная активация объектов из кэша...");
        
        foreach (var entry in managedObjects)
        {
            if (entry.Value != null && entry.Value.gameObject != null)
            {
                // Активируем GameObject
                entry.Value.gameObject.SetActive(true);
                
                // Добавляем в активные объекты
                if (!activeObjects.Contains(entry.Value))
                {
                    activeObjects.Add(entry.Value);
                }
                
                // ИСПРАВЛЕНО: Принудительная переинициализация видео для объектов из кэша
                if (entry.Value.videoPlayer != null)
                {
                    // Принудительно переинициализируем видео для устранения зависания
                    entry.Value.videoPlayer.Stop();
                    entry.Value.videoPlayer.url = entry.Value.videoPlayer.url; // Обновляем URL
                    entry.Value.videoPlayer.Prepare();
                    Debug.Log($"[ARObjectManager] 🔄 Видео из кэша переинициализируется: {entry.Key}");
                }
                
                // ИСПРАВЛЕНО: Проверяем и исправляем позицию объекта
                if (entry.Value.placeAtLocation != null)
                {
                    // Позиция уже установлена при загрузке из кэша
                    Debug.Log($"[ARObjectManager] 📍 Позиция объекта из кэша проверена: {entry.Key}");
                }
            }
        }
        
        Debug.Log($"[ARObjectManager] ✅ Активация объектов из кэша завершена: {activeObjects.Count} активных");
    }
    
    /// <summary>
    /// Переинициализация видео из кэша с задержкой для устранения зависания
    /// </summary>
    private IEnumerator ReinitializeCachedVideosAfterDelay()
    {
        yield return new WaitForSeconds(3f); // Ждем 3 секунды после активации
        
        Debug.Log("[ARObjectManager] 🔄 Переинициализация видео из кэша для устранения зависания...");
        ReinitializeCachedVideos();
    }
}

[System.Serializable]
public class ARObjectsCacheData
{
    public List<ARObjectCacheItem> objects;
}

[System.Serializable]
public class ARObjectCacheItem
{
    public string id;
    public float x; // ИСПРАВЛЕНО: изменено с double на float
    public float y; // ИСПРАВЛЕНО: изменено с double на float
    public string objectURL;
    public long timestamp;
}
