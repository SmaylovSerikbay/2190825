using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class ARPhotoController : MonoBehaviour
{
    public Camera arCamera;         // AR камера
    public Button photoButton;      // Кнопка фото
    public Image flashEffect;       // UI-элемент для вспышки

    private string filePath;

    void Start()
    {
        // Назначаем функцию для кнопки "Фото"
        photoButton.onClick.AddListener(TakePhoto);
        flashEffect.gameObject.SetActive(false); // Отключаем вспышку по умолчанию
    }

    void TakePhoto()
    {
        StartCoroutine(CaptureAndSavePhoto());
    }

    IEnumerator CaptureAndSavePhoto()
    {
        // Временно скрываем кнопку фото
        photoButton.gameObject.SetActive(false);

        yield return new WaitForEndOfFrame();  // Ждем окончания рендеринга

        // Создаем текстуру для скриншота
        Texture2D screenImage = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenImage.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); // Захват экрана
        screenImage.Apply();

        // Сохраняем изображение как PNG
        byte[] imageBytes = screenImage.EncodeToPNG();
        filePath = Path.Combine(Application.persistentDataPath, "AR_Screenshot.png");
        File.WriteAllBytes(filePath, imageBytes);
        Debug.Log($"Скриншот сохранен: {filePath}");

        Destroy(screenImage);  // Очищаем текстуру из памяти

        // Показать эффект вспышки
        StartCoroutine(FlashEffectCoroutine());

        // Сохраняем фото в галерею устройства с помощью Native Gallery
        NativeGallery.SaveImageToGallery(filePath, "AR Photos", "AR_Screenshot_{0}.png");

        Debug.Log("Фото добавлено в галерею.");

        // Восстанавливаем видимость кнопки
        photoButton.gameObject.SetActive(true);
    }

    IEnumerator FlashEffectCoroutine()
    {
        // Включаем вспышку
        flashEffect.gameObject.SetActive(true);
        flashEffect.color = new Color(1, 1, 1, 1); // Полностью белый

        // Плавное уменьшение прозрачности (эффект исчезающей вспышки)
        for (float alpha = 1; alpha >= 0; alpha -= Time.deltaTime * 2)
        {
            flashEffect.color = new Color(1, 1, 1, alpha);
            yield return null;
        }

        // Отключаем вспышку
        flashEffect.gameObject.SetActive(false);
    }
}
