# Настройка Firebase в Unity

## Проблема
Unity показывает ошибку: `Failed to get FirebaseDatabase instance: Specify DatabaseURL within FirebaseApp or from your GetInstance() call.`

## Решение

### 1. Проверьте наличие Firebase SDK
1. Откройте Unity Editor
2. Перейдите в `Window > Package Manager`
3. Убедитесь, что установлены пакеты:
   - Firebase Database
   - Firebase Core

### 2. Проверьте google-services.json
1. Файл `google-services.json` должен быть в корне папки Assets
2. Убедитесь, что он содержит правильные данные:
   ```json
   {
     "project_info": {
       "firebase_url": "https://comeback-2a6b2-default-rtdb.firebaseio.com",
       "project_id": "comeback-2a6b2"
     }
   }
   ```

### 3. Настройка Build Settings
1. Перейдите в `File > Build Settings`
2. Выберите платформу Android
3. Убедитесь, что Package Name соответствует тому, что указан в google-services.json: `com.Lemala.ComeBack`

### 4. Настройка Player Settings
1. Откройте `Edit > Project Settings > Player`
2. В разделе Android Settings:
   - **Package Name**: `com.Lemala.ComeBack`
   - **Minimum API Level**: Android 5.1 (API level 22) или выше
   - **Target API Level**: Используйте последнюю версию

### 5. Проверка SubscriptionManager
1. Найдите GameObject с компонентом `SubscriptionManager` в сцене
2. Убедитесь, что компонент активен
3. В консоли Unity должны появиться сообщения:
   ```
   [SubscriptionManager] Инициализация Firebase...
   [SubscriptionManager] Firebase успешно инициализирован!
   [SubscriptionManager] Loading subscription settings from Firebase...
   ```

### 6. Отладка
Если проблема не решена:

1. **Очистите кэш Unity**:
   - Закройте Unity
   - Удалите папки `Library` и `Temp` в проекте
   - Откройте Unity заново

2. **Переимпортируйте google-services.json**:
   - Удалите файл из Assets
   - Перетащите его обратно
   - Unity должен автоматически обработать файл

3. **Проверьте консоль Unity**:
   - Ищите ошибки, связанные с Firebase
   - Обратите внимание на сообщения от SubscriptionManager

### 7. Тестирование
1. Запустите игру в Unity Editor
2. В консоли должны появиться сообщения:
   ```
   [SubscriptionManager] Firebase App инициализирован: [DEFAULT]
   [SubscriptionManager] ✅ Настройки подписки загружены успешно
   [ARPaymentController] ✅ Значения ПРИНУДИТЕЛЬНО обновлены: Новая цена: 5000 UZS
   ```

### 8. Альтернативный способ инициализации
Если проблема сохраняется, можно попробовать ручную инициализацию:

```csharp
FirebaseApp.DefaultInstance.SetEditorDatabaseUrl("https://comeback-2a6b2-default-rtdb.firebaseio.com");
```

## Контакты
Если проблема не решается, проверьте:
1. Интернет-соединение
2. Правильность настроек Firebase проекта
3. Соответствие Package Name в Unity и Firebase Console
