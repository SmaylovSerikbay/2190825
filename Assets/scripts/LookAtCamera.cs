using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    public Transform cameraTransform; // Ссылка на камеру

    void Update()
    {
        if (cameraTransform != null)
        {
            // Поворачиваем объект так, чтобы он смотрел на камеру
            transform.LookAt(cameraTransform.position);

            // Поворот плоскости, чтобы она была видна камерой
            transform.Rotate(90, 0, 0);
        }
    }
}
