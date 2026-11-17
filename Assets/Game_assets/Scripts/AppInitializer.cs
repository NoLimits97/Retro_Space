using UnityEngine;
using System.IO;

// AppInitializer — платформенная инициализация окружения (точка входа).
// Vita  : создаёт ux0:/data/DecodedAudio (нативный путь для fopen плагина).
// Editor: создаёт <корень проекта>/DecodedAudio — ВНЕ Assets, чтобы Unity
//         не импортировал брошенные MP3 как ассеты.
// PC-билд: создаёт папку рядом с игрой (<папка игры>/DecodedAudio).
public static class AppInitializer
{
    // Единый флаг платформы для всех скриптов
    public static bool IsVita { get { return Application.platform == RuntimePlatform.PSP2; } }

    // Путь выставляется при старте; читается AudioManager'ом и другими.
    public static string AudioPath { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeAtStartup()
    {
        AudioPath = ResolveAudioPath();
        try
        {
            if (!Directory.Exists(AudioPath))
            {
                Directory.CreateDirectory(AudioPath);
                Debug.Log("[AppInitializer] Папка создана: " + AudioPath);
            }
            else
            {
                Debug.Log("[AppInitializer] Папка уже существует: " + AudioPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[AppInitializer] Ошибка инициализации папки: " + e.Message);
        }
    }

    private static string ResolveAudioPath()
    {
        if (IsVita)
        {
            // Слеш после двоеточия (ux0:/data/...) критичен для нативных C-функций fopen
            return "ux0:/data/DecodedAudio";
        }
#if UNITY_EDITOR
        // Корень проекта, НАД Assets — иначе Unity импортирует MP3 как ассеты
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, "DecodedAudio");
#else
        // PC-билд: рядом с exe (<игра>_Data лежит в той же папке)
        string gameDir = Path.GetDirectoryName(Application.dataPath);
        return Path.Combine(gameDir, "DecodedAudio");
#endif
    }
}