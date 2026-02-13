using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using ARLocation;

public class FirebaseLocationLoader : MonoBehaviour
{
    private DatabaseReference reference;

    public GameObject targetObject;  // Объект, который нужно перемещать

    void Start()
    {
        Debug.Log("Инициализация Firebase...");

        // Инициализация Firebase
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && task.Result == DependencyStatus.Available)
            {
                Debug.Log("Firebase успешно инициализирован.");
                FirebaseApp app = FirebaseApp.DefaultInstance;
                reference = FirebaseDatabase.DefaultInstance.RootReference;
                LoadLocationData();  // Загружаем данные о координатах
            }
            else
            {
                Debug.LogError("Ошибка инициализации Firebase: " + task.Exception);
            }
        });
    }

    void LoadLocationData()
    {
        Debug.Log("Запрашиваем данные у Firebase по пути 'objects'...");

        // Запрашиваем данные из Firebase
        FirebaseDatabase.DefaultInstance.GetReference("objects")
        .GetValueAsync().ContinueWithOnMainThread(task =>
        {
            Debug.Log("Запрос данных у Firebase отправлен.");

            if (task.IsFaulted)
            {
                Debug.LogError("Ошибка при загрузке данных из Firebase: " + task.Exception);
            }
            else if (task.IsCompleted)
            {
                Debug.Log("Данные успешно загружены из Firebase.");
                DataSnapshot snapshot = task.Result;

                if (snapshot.Exists && snapshot.HasChildren)
                {
                    Debug.Log($"Объектов найдено: {snapshot.ChildrenCount}");

                    foreach (DataSnapshot obj in snapshot.Children)
                    {
                        Debug.Log($"Обрабатывается объект: {obj.Key}");

                        // Проверяем наличие полей x и y
                        if (obj.HasChild("x") && obj.HasChild("y"))
                        {
                            string objectType = obj.Child("objectType").Value.ToString();
                            double latitude = double.Parse(obj.Child("x").Value.ToString());
                            double longitude = double.Parse(obj.Child("y").Value.ToString());

                            Debug.Log($"Тип объекта: {objectType}, Широта: {latitude}, Долгота: {longitude}");

                            // Обновляем позицию объекта на основе координат
                            UpdateObjectPosition(latitude, longitude);
                        }
                        else
                        {
                            Debug.LogWarning($"Объект {obj.Key} не содержит полей x и y.");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("Снимок данных не существует или пуст.");
                }
            }
            else
            {
                Debug.LogWarning("Запрос данных из Firebase не завершён.");
            }
            Debug.Log("Запрос к Firebase завершен.");
        });
    }

    void UpdateObjectPosition(double latitude, double longitude)
    {
        Debug.Log($"Попытка обновления позиции объекта. Широта: {latitude}, Долгота: {longitude}");

        // Пример обновления позиции объекта в Unity по координатам
        Vector3 newPosition = ARLocationManager.Instance.GetWorldPositionForLocation(
            new ARLocation.Location { Latitude = latitude, Longitude = longitude, Altitude = 0 }, true);

        Debug.Log($"Вычисленная позиция объекта в мире: {newPosition}");

        if (targetObject != null)
        {
            targetObject.transform.position = newPosition;  // Перемещаем целевой объект
            Debug.Log($"Объект {targetObject.name} перемещен на новую позицию: {newPosition}");
        }
        else
        {
            Debug.LogError("Целевой объект не задан. Пожалуйста, назначьте объект в инспекторе.");
        }
    }
}
