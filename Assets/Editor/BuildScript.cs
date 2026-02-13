using UnityEditor;
class BuildScript {
    static void BuildIOS() {
        // Настройки сборки
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        
        string[] scenes = { "Assets/Scenes/SampleScene.unity" }; // ЗАМЕНИТЕ на путь к вашей главной сцене!
        
        BuildPipeline.BuildPlayer(scenes, "ios", BuildTarget.iOS, BuildOptions.None);
    }
}