using System.Collections;
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;

public class ObjectSpawner : MonoBehaviour
{
    private DatabaseReference databaseReference;

    // Класс для хранения данных о геообъекте
    [System.Serializable]
    public class GeoObject
    {
        public float x;            // Координата X
        public float y;            // Координата Y
        public string objectType;  // Тип объекта (видео, изображение и т. д.)
        public string objectURL;   // URL объекта
    }

    void Start()
    {
        // Инициализация Firebase и получение ссылки на базу данных
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            databaseReference = FirebaseDatabase.DefaultInstance.RootReference;

            // Получаем данные из Firebase
            GetDataFromFirebase();
        });
    }

    // Метод для получения данных из Firebase
    void GetDataFromFirebase()
    {
        databaseReference.Child("objects").GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                Debug.LogError("Ошибка при получении данных");
            }
            else if (task.IsCompleted)
            {
                DataSnapshot snapshot = task.Result;

                // Проходим по всем дочерним элементам (объектам) в Firebase
                foreach (DataSnapshot childSnapshot in snapshot.Children)
                {
                    // Преобразуем данные в словарь
                    IDictionary data = (IDictionary)childSnapshot.Value;

                    // Создаем объект GeoObject из полученных данных
                    GeoObject geoObject = new GeoObject
                    {
                        x = float.Parse(data["x"].ToString()),  // Преобразование координаты X
                        y = float.Parse(data["y"].ToString()),  // Преобразование координаты Y
                        objectType = data["objectType"].ToString(), // Получение типа объекта
                        objectURL = data["objectURL"].ToString()  // Получение URL объекта
                    };

                    // Отображаем объект на сцене Unity
                    PlaceObject(geoObject);
                }
            }
        });
    }

    // Метод для размещения объекта на сцене
    void PlaceObject(GeoObject obj)
    {
        Vector3 position = new Vector3(obj.x, 0, obj.y); // Создаем вектор позиции

        // Размещаем объект в зависимости от его типа
        if (obj.objectType == "video")
        {
            // Код для размещения видео (в дальнейшем)
        }
        else if (obj.objectType == "image")
        {
            // Код для размещения изображения (в дальнейшем)
        }
        else if (obj.objectType == "3Dmodel")
        {
            // Код для размещения 3D модели (в дальнейшем)
        }
    }
}
