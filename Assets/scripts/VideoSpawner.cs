using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using ARLocation;
using UnityEngine.Video;
using UnityEngine.UI;

/// <summary>
/// Оптимизированный VideoSpawner для работы с ARObjectManager
/// Устраняет мерцание и улучшает производительность
/// </summary>
public class VideoSpawner : MonoBehaviour
{
    [Header("AR Object Manager")]
    [SerializeField] private ARObjectManager arObjectManager;
    
    [Header("Firebase Settings")]
    [SerializeField] private string databaseUrl = "https://comeback-2a6b2-default-rtdb.firebaseio.com/";
    
    [Header("Performance Settings")]
    [SerializeField] public int maxConcurrentLoads = 3; // Максимум одновременных загрузок
    [SerializeField] public float loadDelay = 0.2f; // Задержка между загрузками
    
    [Header("Fallback Settings")]
    [SerializeField] private GameObject videoPrefab; // Fallback prefab если ARObjectManager не найден
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Text loadingIndicator;
    
    // Приватные переменные
    private DatabaseReference databaseReference;
    private Queue<ARObjectManager.GeoObject> loadQueue = new Queue<ARObjectManager.GeoObject>();
    private Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>();
    private Coroutine loadCoroutine;
    private bool isInitialized = false;
    
    // Fallback для старых версий
    private List<GameObject> objectPool = new List<GameObject>();
    private int poolSize = 20;
    
    void Start()
    {
        InitializeVideoSpawner();
    }
    
    void InitializeVideoSpawner()
    {
        // Автопоиск ARObjectManager
        if (arObjectManager == null)
        {
            arObjectManager = FindObjectOfType<ARObjectManager>();
            if (arObjectManager != null)
            {
                Debug.Log("[VideoSpawner] ✅ ARObjectManager найден автоматически");
            }
            else
            {
                Debug.LogWarning("[VideoSpawner] ⚠️ ARObjectManager не найден, используем fallback режим");
            }
        }
        
        // Автопоиск камеры
        if (cameraTransform == null)
        {
            Camera arCamera = FindObjectOfType<Camera>();
            if (arCamera != null)
            {
                cameraTransform = arCamera.transform;
                Debug.Log("[VideoSpawner] ✅ Камера найдена автоматически");
            }
        }
        
        // Инициализируем Firebase
        InitializeFirebase();
        
        // Создаем fallback пул если нужно
        if (arObjectManager == null)
        {
            InitializeObjectPool();
        }
    }
    
    void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                Debug.Log("[VideoSpawner] ✅ Firebase успешно инициализирован");
                FirebaseDatabase database = FirebaseDatabase.GetInstance(databaseUrl);
                databaseReference = database.RootReference;
                
                isInitialized = true;
                SubscribeToDatabaseChanges();
            }
            else
            {
                Debug.LogError("[VideoSpawner] ❌ Ошибка инициализации Firebase");
            }
        });
    }
    
    void InitializeObjectPool()
    {
        if (videoPrefab == null)
        {
            Debug.LogWarning("[VideoSpawner] ⚠️ VideoPrefab не назначен для fallback режима");
            return;
        }
        
        for (int i = 0; i < poolSize; i++)
        {
            GameObject videoObject = Instantiate(videoPrefab);
            videoObject.SetActive(false);
            objectPool.Add(videoObject);
        }
        
        Debug.Log($"[VideoSpawner] ✅ Fallback пул создан: {poolSize} объектов");
    }
    
    void SubscribeToDatabaseChanges()
    {
        if (databaseReference == null) return;
        
        databaseReference.Child("objects").ValueChanged += HandleValueChanged;
        Debug.Log("[VideoSpawner] ✅ Подписка на изменения Firebase установлена");
    }
    
    void HandleValueChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError($"[VideoSpawner] ❌ Ошибка Firebase: {args.DatabaseError.Message}");
            return;
        }
        
        if (!isInitialized) return;
        
        // Очищаем старую очередь
        loadQueue.Clear();
        
        // Обрабатываем новые данные
        foreach (DataSnapshot childSnapshot in args.Snapshot.Children)
        {
            string objectId = childSnapshot.Key;
            IDictionary data = (IDictionary)childSnapshot.Value;
            
            if (!data.Contains("x") || !data.Contains("y") || !data.Contains("objectType") || !data.Contains("objectURL"))
            {
                Debug.LogWarning($"[VideoSpawner] ⚠️ Объект {objectId} имеет неполные данные");
                continue;
            }
            
            ARObjectManager.GeoObject geoObject = new ARObjectManager.GeoObject
            {
                id = objectId,
                x = float.Parse(data["x"].ToString()),
                y = float.Parse(data["y"].ToString()),
                objectType = data["objectType"].ToString(),
                objectURL = data["objectURL"].ToString()
            };
            
            // Проверяем что объект еще не создан
            if (!spawnedObjects.ContainsKey(objectId))
            {
                loadQueue.Enqueue(geoObject);
            }
        }
        
        // Запускаем загрузку объектов
        if (loadQueue.Count > 0)
        {
            if (loadCoroutine != null)
                StopCoroutine(loadCoroutine);
            loadCoroutine = StartCoroutine(ProcessLoadQueue());
        }
        
        Debug.Log($"[VideoSpawner] 📊 Обработано {loadQueue.Count} новых объектов");
    }
    
    IEnumerator ProcessLoadQueue()
    {
        int loadedCount = 0;
        
        while (loadQueue.Count > 0)
        {
            if (loadedCount >= maxConcurrentLoads)
            {
                // Ждем завершения загрузки
                yield return new WaitForSeconds(loadDelay);
                loadedCount = 0;
            }
            
            ARObjectManager.GeoObject geoObject = loadQueue.Dequeue();
            
            if (arObjectManager != null)
            {
                // Используем новый ARObjectManager
                var arObj = arObjectManager.CreateARObject(geoObject);
                spawnedObjects[geoObject.id] = null; // Помечаем как созданный
            }
            else
            {
                // Fallback режим
                PlaceObjectFallback(geoObject);
            }
            
            loadedCount++;
            
            // Небольшая задержка между загрузками
            yield return new WaitForSeconds(loadDelay);
        }
        
        Debug.Log("[VideoSpawner] ✅ Очередь загрузки обработана");
    }
    
    void PlaceObjectFallback(ARObjectManager.GeoObject obj)
    {
        GameObject videoObject = GetPooledObject();
        if (videoObject == null)
        {
            Debug.LogWarning("[VideoSpawner] ⚠️ Fallback пул исчерпан");
            return;
        }
        
        if (loadingIndicator != null)
            loadingIndicator.gameObject.SetActive(true);
        
        // Создаем плоскость для видео
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.parent = videoObject.transform;
        
        float scaleFactor = 0.2f;
        plane.transform.localScale = new Vector3(scaleFactor * (16f / 9f), 1f, scaleFactor);
        plane.transform.localPosition = new Vector3(0, 0.5f, 0);
        
        // Настраиваем VideoPlayer
        VideoPlayer videoPlayer = plane.AddComponent<VideoPlayer>();
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = obj.objectURL;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
        
        // Настраиваем материал
        Renderer planeRenderer = plane.GetComponent<Renderer>();
        Material chromaKeyMaterial = new Material(Shader.Find("Custom/ChromaKeyShader"));
        chromaKeyMaterial.SetColor("_ChromaKeyColor", new Color(0f / 255f, 154f / 255f, 61f / 255f, 1));
        chromaKeyMaterial.SetFloat("_Threshold", 0.1f);
        planeRenderer.material = chromaKeyMaterial;
        
        videoPlayer.targetMaterialRenderer = planeRenderer;
        videoPlayer.targetMaterialProperty = "_MainTex";
        
        // Настраиваем геолокацию
        Location location = new Location
        {
            Latitude = obj.x,
            Longitude = obj.y,
            Altitude = 0,
            AltitudeMode = AltitudeMode.GroundRelative
        };
        
        PlaceAtLocation placeAtLocation = videoObject.GetComponent<PlaceAtLocation>() ?? videoObject.AddComponent<PlaceAtLocation>();
        placeAtLocation.Location = location;
        
        // Настраиваем поворот к камере
        if (cameraTransform != null)
        {
            LookAtCamera lookAtCamera = plane.GetComponent<LookAtCamera>() ?? plane.AddComponent<LookAtCamera>();
            lookAtCamera.cameraTransform = cameraTransform;
        }
        
        // Добавляем в созданные объекты
        spawnedObjects[obj.id] = videoObject;
        
        // Подготавливаем видео
        videoPlayer.prepareCompleted += (VideoPlayer vp) =>
        {
            if (loadingIndicator != null)
                loadingIndicator.gameObject.SetActive(false);
            
            vp.Pause(); // Останавливаем до активации
            Debug.Log($"[VideoSpawner] ✅ Fallback видео подготовлено: {obj.id}");
        };
        
        videoPlayer.errorReceived += (VideoPlayer vp, string errorMsg) =>
        {
            Debug.LogError($"[VideoSpawner] ❌ Ошибка fallback видео {obj.id}: {errorMsg}");
            if (loadingIndicator != null)
                loadingIndicator.gameObject.SetActive(false);
        };
        
        videoPlayer.Prepare();
    }
    
    GameObject GetPooledObject()
    {
        foreach (GameObject obj in objectPool)
        {
            if (!obj.activeInHierarchy)
            {
                obj.SetActive(true);
                return obj;
            }
        }
        return null;
    }
    
    void Update()
    {
        // Проверяем что компонент включен
        if (!enabled) return;
        
        // Если используем ARObjectManager, не нужно управлять объектами здесь
        if (arObjectManager != null) return;
        
        // Fallback управление объектами
        ManageActiveVideosFallback();
    }
    
    void ManageActiveVideosFallback()
    {
        if (cameraTransform == null) return;
        
        // Проверяем что камера активна
        if (!cameraTransform.gameObject.activeInHierarchy)
        {
            // Останавливаем все видео если камера неактивна
            foreach (var entry in spawnedObjects)
            {
                if (entry.Value != null)
                {
                    VideoPlayer videoPlayer = entry.Value.GetComponentInChildren<VideoPlayer>();
                    if (videoPlayer != null && videoPlayer.isPlaying)
                    {
                        videoPlayer.Pause();
                    }
                    entry.Value.SetActive(false);
                }
            }
            return;
        }
        
        // Управляем видимостью объектов на основе расстояния
        foreach (var entry in spawnedObjects)
        {
            GameObject videoObject = entry.Value;
            if (videoObject == null) continue;
            
            float distance = Vector3.Distance(cameraTransform.position, videoObject.transform.position);
            VideoPlayer videoPlayer = videoObject.GetComponentInChildren<VideoPlayer>();
            
            if (videoPlayer == null) continue;
            
            if (distance < 250f) // Увеличено до 250м для соответствия ARObjectManager
            {
                if (!videoObject.activeInHierarchy)
                {
                    videoObject.SetActive(true);
                }
                
                if (!videoPlayer.isPlaying && videoPlayer.isPrepared)
                {
                    videoPlayer.Play();
                }
            }
            else
            {
                if (videoPlayer.isPlaying)
                {
                    videoPlayer.Pause();
                }
                videoObject.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Очищает все созданные объекты
    /// </summary>
    public void ClearAllObjects()
    {
        if (arObjectManager != null)
        {
            arObjectManager.ClearAllObjects();
        }
        else
        {
            // Fallback очистка
            foreach (var entry in spawnedObjects)
            {
                if (entry.Value != null)
                {
                    DestroyImmediate(entry.Value);
                }
            }
            spawnedObjects.Clear();
        }
        
        Debug.Log("[VideoSpawner] ✅ Все объекты очищены");
    }
    
    /// <summary>
    /// Получает количество активных объектов
    /// </summary>
    public int GetActiveObjectCount()
    {
        if (arObjectManager != null)
        {
            return arObjectManager.GetActiveObjectCount();
        }
        
        int count = 0;
        foreach (var entry in spawnedObjects)
        {
            if (entry.Value != null && entry.Value.activeInHierarchy)
            {
                count++;
            }
        }
        return count;
    }
    
    void OnDestroy()
    {
        if (loadCoroutine != null)
        {
            StopCoroutine(loadCoroutine);
        }
        
        ClearAllObjects();
    }
    
    // Методы для отладки
    [ContextMenu("Log Performance Info")]
    void LogPerformanceInfo()
    {
        Debug.Log($"[VideoSpawner] 📊 Информация о производительности:");
        Debug.Log($"  Использует ARObjectManager: {arObjectManager != null}");
        Debug.Log($"  Активных объектов: {GetActiveObjectCount()}");
        Debug.Log($"  Размер fallback пула: {objectPool.Count}");
        Debug.Log($"  Очередь загрузки: {loadQueue.Count}");
        Debug.Log($"  Firebase инициализирован: {isInitialized}");
    }
    
    [ContextMenu("Force Process Queue")]
    void ForceProcessQueue()
    {
        if (loadQueue.Count > 0)
        {
            if (loadCoroutine != null)
                StopCoroutine(loadCoroutine);
            loadCoroutine = StartCoroutine(ProcessLoadQueue());
            Debug.Log("[VideoSpawner] 🔄 Принудительная обработка очереди");
        }
        else
        {
            Debug.Log("[VideoSpawner] ℹ️ Очередь загрузки пуста");
        }
    }
}
