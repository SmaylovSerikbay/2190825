using UnityEngine;

public class GPSStageObject : MonoBehaviour
{
    public float latitude;
    public float longitude;

    public void Initialize(float lat, float lon)
    {
        latitude = lat;
        longitude = lon;
        // Здесь можно добавить логику для преобразования GPS координат в местные координаты Unity
        // Например, используя метод ConvertGPSCoordinatesToUnity
    }
}
