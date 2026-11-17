using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading; // без Tasks/async — совместимо с .NET 3.5
using UnityEngine;
using UnityEngine.SceneManagement;

// AudioManager — PS Vita: аппаратный MP3 (vita_audio), PC: Nyquist-декодер (nyquist_wrapper).
// PC: стриминг без аллокаций + декод на отдельном ядре. Без TPL, .NET 3.5.
// + панель имени трека (5 c, мерцание) + смена трека по ButtonX с кулдауном 2 c.
public class AudioManager : Gui
{
    public static AudioManager instance;

    [Header("Плейлист (PC fallback)")]
    [SerializeField] private AudioClip[] backgroundPlaylist;

    [Header("Эффекты")]
    [SerializeField] private AudioClip gameOverSound;

    [Header("Настройки")]
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("Vita: путь к MP3")]
    [SerializeField] private string customExternalAudioPath = "ux0:/data/DecodedAudio";

    [SerializeField] private bool enableGui = false;

    private float resumeGuardTime = 0f;
    private float pauseStuckTime = 0f;
    private bool initStarted = false;

    // ==================== 1. Нативный плагин VITA ====================
#if UNITY_VITA
    private const string VitaPluginName = "vita_audio";
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_Init();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private static extern int VA_PlayTrack(string path);
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern void VA_StopTrack();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern void VA_SetPause(int paused);
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern void VA_SetVolume(float volume);
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern void VA_SetFadeTime(float seconds);
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_GetState();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_GetSampleRate();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_GetChannels();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_GetLastError();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern void VA_SetLoop(int loop);
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_SetCore(int core);
    // Диагностика Vita
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_DbgAlive();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_DbgPolls();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_DbgCmds();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_DbgPlays();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_DbgOpens();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_DbgInitCount();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_DbgThreadStatus();
    [DllImport(VitaPluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int VA_DbgDecRet();
#endif

    // ==================== 2. Nyquist для PC ====================
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR
    private const string NyquistPluginName = "nyquist_wrapper";
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NYQ_Init();
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int NYQ_PlayTrack(string path);
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void NYQ_StopTrack();
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void NYQ_SetPause(int paused);
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void NYQ_SetVolume(float volume);
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void NYQ_SetFadeTime(float seconds);
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NYQ_GetState();
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NYQ_GetSampleRate();
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NYQ_GetChannels();
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NYQ_GetLastError();
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void NYQ_SetLoop(int loop);
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NYQ_SetCore(int core);
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern UIntPtr NYQ_GetTotalSamples();
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void NYQ_GetPCMData(float[] outBuffer, UIntPtr samples);
    [DllImport(NyquistPluginName, CallingConvention = CallingConvention.Cdecl)]
    private static extern float NYQ_GetCurrentVolume();

    // Win32: пиннинг декод-потока к отдельному ядру
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")]
    private static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);
#endif
#endif

    // ==================== 3. Состояние ====================
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private int currentTrackIndex = -1;
    private float targetVolume = 0.75f;
    private Coroutine fadeCoroutine;
    private List<string> externalMp3 = new List<string>();
    private int externalIndex = -1;
    private string currentTrackName = "-";
    private bool pluginReady = false;
    private bool pluginWorkable = false;
    private bool isPaused = false;
    private bool isLaunching = false;
    private bool stoppedForHub = false;
    private int failCount = 0;
    private float lastPlayCmdTime = 0f;
    private float timeToNextFallback = 0f;
    private bool isDecodingMp3 = false;

    public enum PlayMode { None, UnityFallback, NativeHardware, PcNyquist }
    private PlayMode mode = PlayMode.None;

    private const int ST_STOP = 0;
    private const int ST_FADE_IN = 1;
    private const int ST_PLAY = 2;
    private const int ST_FADE_OUT = 3;
    private const int ST_PAUSE = 4;

    private string excText = "";

    // ---- Панель имени трека + скип ----
    private const float TRACK_NAME_TIME = 3.5f;   // сколько держим панель
    private const float SKIP_COOLDOWN = 2.5f;     // кулдаун ButtonX
    private float trackNameShowUntil = 0f;
    private float skipCooldownUntil = 0f;
    private GUIStyle trackStyle;
    private int trackStyleW = -1;
    private static Texture2D panelTex;

    public string CurrentTrackName { get { return currentTrackName; } }
    public float CurrentVolume { get { return targetVolume; } }

    private bool TrackNameVisible { get { return Time.realtimeSinceStartup < trackNameShowUntil; } }

    // Структура для передачи данных из фонового потока (PC)
    private struct DecodedPcm
    {
        public float[] buffer;
        public int sampleCount;
        public int channels;
        public int sampleRate;
        public string name;
        public bool isValid;
        public int idx; // индекс трека для удаления при ошибке
    }

    // PC: потоковое декодирование (без Task/async — .NET 3.5)
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR
    private Thread decodeThread;
    private volatile bool decodeReady;
    private DecodedPcm decodeResult;
    private int decodeGen; // защита от «поздно приехавшего» результата

    // Читалка для AudioClip.Create(stream). Никаких аллокаций в аудио-потоке!
    private sealed class ClipStreamReader
    {
        public float[] buffer;
        public int total;
        private int pos;
        public void Read(float[] data)
        {
            int left = total - pos;
            int copy = data.Length < left ? data.Length : left;
            if (copy > 0) { Array.Copy(buffer, pos, data, 0, copy); pos += copy; }
            for (int i = copy; i < data.Length; i++) data[i] = 0f;
        }
    }

    // Два вечных буфера: пока один играет, во второй декодируется следующий трек.
    // GC не получает ничего — массивы никогда не становятся мусором.
    private static float[][] pcmBufs = new float[2][];
    private int lastClipBuf = 0;
    private string pendingPath;
    private int pendingIdx;
#endif

    // ============ Дебаг кеш ============
    private const float DBG_POLL_INTERVAL = 0.12f;
    private float dbgNextPoll = 0f;
    private int d_initErr = -777, d_st = -1, d_rate = 0, d_ch = 0, d_err = 0;
    private int d_initCnt = -1, d_alive = -1, d_polls = -1, d_cmds = -1, d_plays = -1, d_opens = -1;
    private int d_thrStat = -9, d_decRet = 0, d_ver = -1, d_thrId = 0, d_core = -1;

    private void RefreshDbg()
    {
#if UNITY_VITA
        d_initErr = SafeGet(() => VA_GetLastError(), -777, "LastError");
        d_st = SafeGet(() => VA_GetState(), -1, "GetState");
        d_rate = SafeGet(() => VA_GetSampleRate(), 0, "GetSampleRate");
        d_ch = SafeGet(() => VA_GetChannels(), 0, "Channels");
        d_err = d_initErr;
        d_initCnt = SafeGet(() => VA_DbgInitCount(), -1, "InitCount");
        d_alive = SafeGet(() => VA_DbgAlive(), -1, "Alive");
        d_polls = SafeGet(() => VA_DbgPolls(), -1, "Polls");
        d_cmds = SafeGet(() => VA_DbgCmds(), -1, "Cmds");
        d_plays = SafeGet(() => VA_DbgPlays(), -1, "Plays");
        d_opens = SafeGet(() => VA_DbgOpens(), -1, "Opens");
        d_thrStat = SafeGet(() => VA_DbgThreadStatus(), -9, "ThreadStatus");
        d_decRet = SafeGet(() => VA_DbgDecRet(), 0, "DecRet");
#endif
    }

    // ==================== 4. Жизненный цикл ====================
    private void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        musicSource = gameObject.AddComponent<AudioSource>();
        sfxSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = false;
        targetVolume = PlayerPrefs.GetFloat("GameVolume", 0.75f);
        musicSource.volume = targetVolume;
        sfxSource.volume = targetVolume;
    }

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        BindVolumeSlider();
        if (s.name == "Hub_work")
        {
            StopMusic();
            stoppedForHub = true;
        }
        else if (stoppedForHub)
        {
            stoppedForHub = false;
            failCount = 0;
            PlayRandomMusic();
        }
    }

    private void OnApplicationPause(bool p) { if (p) PlayerPrefs.Save(); }
    private void OnApplicationQuit() { PlayerPrefs.Save(); }

    private void BindVolumeSlider()
    {
        UnityEngine.UI.Slider[] all = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Slider>();
        for (int i = 0; i < all.Length; i++)
        {
            UnityEngine.UI.Slider sl = all[i];
            if (sl == null) continue;
            if (!sl.gameObject.scene.IsValid()) continue;
            if (sl.gameObject.tag != "VolumeSlider") continue;
            sl.onValueChanged.RemoveAllListeners();
            sl.onValueChanged.AddListener(ChangeMasterVolume);
            sl.value = targetVolume;
        }
    }

    private void Start()
    {
        BindVolumeSlider();
        if (initStarted) return;
        initStarted = true;
#if UNITY_VITA
        StartCoroutine(VitaInitSequence());
#else
        StartCoroutine(PcInitSequence());
#endif
    }

    // ==================== 5. Инициализация VITA ====================
#if UNITY_VITA
    private IEnumerator VitaInitSequence()
    {
        excText = "Initializing...";
        yield return new WaitForSeconds(0.3f);
        int res = -100;
        try { res = VA_Init(); }
        catch (Exception e) { excText = "VA_Init EXC: " + e.Message; pluginReady = false; }
        pluginReady = (res == 0);
        if (!pluginReady)
        {
            excText = "VA_Init returned: " + res + " (0x" + res.ToString("X8") + ")";
            yield return new WaitForSeconds(0.5f);
            mode = PlayMode.UnityFallback;
            PlayRandomMusic();
            yield break;
        }
        try
        {
            VA_SetFadeTime(fadeDuration);
            VA_SetVolume(targetVolume);
            VA_SetLoop(1);
        }
        catch (Exception e) { excText = "VA_Set EXC: " + e.Message; }
        yield return new WaitForSecondsRealtime(1.0f);
        int alive = SafeGet(() => VA_DbgAlive(), -1, "Alive");
        int polls = SafeGet(() => VA_DbgPolls(), -1, "Polls");
        if (alive < 1 || polls < 1)
        {
            excText = "Thread DEAD (alive=" + alive + " polls=" + polls + "). Fallback.";
            pluginReady = false;
            pluginWorkable = false;
            mode = PlayMode.UnityFallback;
            PlayRandomMusic();
            yield break;
        }
        pluginWorkable = true;
        dbgNextPoll = 0f;
        excText = "Thread OK. Scan...";
        yield return null;
        ScanExternalMusic();
        if (externalMp3.Count > 0)
        {
            mode = PlayMode.NativeHardware;
            excText = "MP3 found: " + externalMp3.Count;
            PlayRandomMusic();
        }
        else
        {
            mode = PlayMode.UnityFallback;
            excText = "No MP3 found, fallback.";
            PlayRandomMusic();
        }
    }
#endif

    // ==================== 6. Инициализация PC (Nyquist) ====================
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR
    private IEnumerator PcInitSequence()
    {
        excText = "PC Init: Nyquist...";
        yield return new WaitForSeconds(0.1f);
        int res = -100;
        try { res = NYQ_Init(); }
        catch (Exception e)
        {
            excText = "NYQ_Init EXC: " + e.GetType().Name + ": " + e.Message;
            Debug.LogError("[AudioManager] " + excText);
            pluginReady = false;
        }
        Debug.Log("[AudioManager] NYQ_Init result: " + res);
        pluginReady = (res == 0);
        if (!pluginReady)
        {
            Debug.LogError("[AudioManager] Nyquist not ready -> Unity fallback. " + excText);
            mode = PlayMode.UnityFallback;
            PlayRandomMusic();
            yield break;
        }
        try
        {
            NYQ_SetFadeTime(fadeDuration);
            NYQ_SetVolume(targetVolume);
            NYQ_SetLoop(0);
        }
        catch (Exception e) { excText = "NYQ_Set EXC: " + e.Message; }
        ScanExternalMusic();
        if (externalMp3.Count > 0)
        {
            mode = PlayMode.PcNyquist;
            excText = "Nyquist ready: " + externalMp3.Count + " tracks";
            Debug.Log("[AudioManager] PC Mode: Nyquist decoder active");
            PlayRandomMusic();
        }
        else if (backgroundPlaylist != null && backgroundPlaylist.Length > 0)
        {
            mode = PlayMode.UnityFallback;
            excText = "No MP3 folder, using Unity fallback";
            Debug.Log("[AudioManager] PC Mode: Unity fallback (no MP3 files)");
            PlayRandomMusic();
        }
        else
        {
            mode = PlayMode.None;
            excText = "No audio available";
            Debug.LogWarning("[AudioManager] No audio sources available!");
        }
    }
#endif

    // ==================== 7. Скан музыки ====================
    private string GetScanPath()
    {
#if UNITY_VITA
        return string.IsNullOrEmpty(customExternalAudioPath) ? AppInitializer.AudioPath : customExternalAudioPath;
#else
#if UNITY_EDITOR
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DecodedAudio");
#else
        return Path.Combine(Application.dataPath, "../DecodedAudio");
#endif
#endif
    }

    private void ScanExternalMusic()
    {
        externalMp3.Clear();
        string path = GetScanPath();
        try
        {
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path);
                foreach (var f in files)
                {
                    if (f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                        externalMp3.Add(f);
#if !UNITY_VITA
                    else if (f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                        externalMp3.Add(f);
#endif
                }
                Debug.Log("[AudioManager] MP3 count in " + path + ": " + externalMp3.Count);
            }
            else
            {
                Debug.LogWarning("[AudioManager] Directory not found: " + path);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[AudioManager] Scan Fail: " + e.Message);
        }
    }

    // ==================== 8. Update Loop ====================
    private void Update()
    {
        // Смена трека (ButtonX) с кулдауном 2 c — работает и в меню, и в геймплее.
        // Не срабатывает в хабе и на паузе.
        if (!stoppedForHub && !isPaused && !isDecodingMp3 &&
            Time.realtimeSinceStartup >= skipCooldownUntil &&
            Input.GetButtonDown("ButtonX"))
        {
            skipCooldownUntil = Time.realtimeSinceStartup + SKIP_COOLDOWN;
            SkipToNextTrack();
        }

#if UNITY_VITA
        if (mode == PlayMode.NativeHardware && pluginWorkable)
        {
            // Опрос натива Vita ~8 раз/сек
            if (Time.realtimeSinceStartup >= dbgNextPoll)
            {
                dbgNextPoll = Time.realtimeSinceStartup + DBG_POLL_INTERVAL;
                RefreshDbg();
            }
            if (d_st == ST_PLAY || d_st == ST_FADE_IN) failCount = 0;
            // Детектор десинхрона паузы
            if (d_st == ST_PAUSE && !isPaused && !stoppedForHub)
            {
                pauseStuckTime += Time.deltaTime;
                if (pauseStuckTime > 1.5f)
                {
                    pauseStuckTime = 0f;
                    SafeCall(() => VA_SetPause(0), "VA_SetPause(0) desync");
                }
            }
            else
            {
                pauseStuckTime = 0f;
            }
            // Guard после Resume
            if (resumeGuardTime > 0f)
            {
                resumeGuardTime -= Time.deltaTime;
                if (d_st == ST_STOP)
                {
                    resumeGuardTime = 0f;
                    isLaunching = false;
                    PlayRandomMusic();
                }
            }
            // Авто-следующий трек + самопочинка
            if (d_st == ST_STOP && !isPaused && !stoppedForHub)
            {
                float elapsed = Time.time - lastPlayCmdTime;
                if (elapsed > 3f)
                {
                    failCount++;
                    if (failCount >= 3)
                    {
                        failCount = 0;
                        isLaunching = false;
                        PlayRandomMusic();
                    }
                    else
                    {
                        isLaunching = false;
                        PlayRandomMusic();
                    }
                }
            }
        }
#endif

        // PC: старт декода (только когда не идёт фейд — буферы не пересекаются) + опрос результата
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR
        if (mode == PlayMode.PcNyquist)
        {
            if (pendingPath != null && fadeCoroutine == null && (decodeThread == null || !decodeThread.IsAlive))
            {
                string p = pendingPath;
                int i2 = pendingIdx;
                pendingPath = null;
                StartDecode(p, i2);
            }
            PollDecodeResult();
        }
#endif

        // Авто-следующий трек для Unity fallback И PC Nyquist (когда клип доиграл)
        if (mode == PlayMode.UnityFallback || mode == PlayMode.PcNyquist)
        {
            if (musicSource != null && !musicSource.isPlaying && fadeCoroutine == null && !isPaused && !stoppedForHub && !isDecodingMp3)
            {
                if (timeToNextFallback <= 0f)
                {
                    PlayRandomMusic();
                    timeToNextFallback = 3f;
                }
            }
            if (timeToNextFallback > 0f) timeToNextFallback -= Time.deltaTime;
        }
    }

    // ==================== 9. Public API ====================
    public void PlayRandomMusic()
    {
        if (isPaused) ResumeMusic();
#if UNITY_VITA
        if (mode == PlayMode.NativeHardware && externalMp3.Count > 0)
            PlayNativeTrack();
#endif
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR
        if (mode == PlayMode.PcNyquist && externalMp3.Count > 0)
            PlayPcTrackAsync();
#endif
        if (mode == PlayMode.UnityFallback)
            PlayRandomFallbackMusic();
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
#if UNITY_VITA
        if (mode == PlayMode.NativeHardware)
        {
            StopNativePlayback();
            mode = PlayMode.UnityFallback;
        }
#endif
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR
        if (mode == PlayMode.PcNyquist)
        {
            // сбрасываем идущее декодирование, чтобы результат не воскресил режим
            decodeGen++;
            isDecodingMp3 = false;
            pendingPath = null;
            mode = PlayMode.UnityFallback;
        }
#endif
        ShowTrackName(clip.name);
        PlayClipUnityPath(clip);
    }

    public void PlayNextTrack()
    {
        SkipToNextTrack();
    }

    public void StopMusic()
    {
        isLaunching = false;
        resumeGuardTime = 0f;
        // отменяем фейд, если он идёт
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        // сбрасываем декодирование, чтобы поздно приехавший результат не воскресил музыку
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR
        decodeGen++;
        isDecodingMp3 = false;
        pendingPath = null;
#endif
#if UNITY_VITA
        if (mode == PlayMode.NativeHardware)
        {
            SafeCall(() => VA_StopTrack(), "VA_StopTrack");
            currentTrackName = "-";
        }
#endif
        if (mode == PlayMode.UnityFallback || mode == PlayMode.PcNyquist)
        {
            musicSource.Stop();
        }
    }

    public void PauseMusic()
    {
        isPaused = true;
        resumeGuardTime = 0f;
#if UNITY_VITA
        if (mode == PlayMode.NativeHardware)
            SafeCall(() => VA_SetPause(1), "VA_SetPause(1)");
#endif
        if (mode == PlayMode.UnityFallback || mode == PlayMode.PcNyquist)
            musicSource.Pause();
    }

    public void ResumeMusic()
    {
        isPaused = false;
#if UNITY_VITA
        if (mode == PlayMode.NativeHardware)
        {
            SafeCall(() => VA_SetPause(0), "VA_SetPause(0)");
            resumeGuardTime = 1.0f;
        }
#endif
        if (mode == PlayMode.UnityFallback || mode == PlayMode.PcNyquist)
        {
            musicSource.UnPause();
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null) sfxSource.PlayOneShot(clip);
    }

    public void ChangeMasterVolume(float volume)
    {
        targetVolume = Mathf.Clamp01(volume);
        musicSource.volume = targetVolume;
        sfxSource.volume = targetVolume;
#if UNITY_VITA
        if (mode == PlayMode.NativeHardware)
            SafeCall(() => VA_SetVolume(targetVolume), "VA_SetVolume");
#endif
        PlayerPrefs.SetFloat("GameVolume", targetVolume);
    }

    public void PlayGameOver() { PlaySFX(gameOverSound); }

#if UNITY_VITA
    public void SetAudioCore(int core)
    {
        if (pluginWorkable) SafeCall(() => VA_SetCore(core), "VA_SetCore");
    }
#endif

    public void SetEnableGui(bool on)
    {
        enableGui = on;
    }

    // ==================== 10. Internal tracks (Vita) ====================
#if UNITY_VITA
    private void PlayNativeTrack()
    {
        if (externalMp3.Count == 0) return;
        int idx;
        do { idx = UnityEngine.Random.Range(0, externalMp3.Count); }
        while (idx == externalIndex && externalMp3.Count > 1);
        externalIndex = idx;
        currentTrackName = Path.GetFileName(externalMp3[idx]);
        isLaunching = true;
        lastPlayCmdTime = Time.time;
        try
        {
            int ret = VA_PlayTrack(externalMp3[idx]);
            if (ret != 0) Debug.LogError("[AudioManager] VA_PlayTrack returned: " + ret);
            else ShowTrackName(currentTrackName);
        }
        catch (Exception e)
        {
            Debug.LogError("[AudioManager] VA_PlayTrack EXC: " + e.Message);
            mode = PlayMode.UnityFallback;
            PlayRandomFallbackMusic();
        }
    }
#endif

    // ==================== 11. PC Nyquist: стриминг без аллокаций, декод на отдельном ядре ====================
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR

    // Пиннуем декод-поток к последнему ядру: не мешает главному потоку и аудио-потоку Unity
    private static void PinDecodeThreadToCore()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            int cores = System.Environment.ProcessorCount;
            if (cores > 1)
            {
                int core = cores - 1;
                UIntPtr mask = (UIntPtr)(1uL << core);
                SetThreadAffinityMask(GetCurrentThread(), mask);
            }
        }
        catch { }
#endif
    }

    private void PlayPcTrackAsync()
    {
        if (externalMp3.Count == 0 || isDecodingMp3) return;
        int idx;
        do { idx = UnityEngine.Random.Range(0, externalMp3.Count); }
        while (idx == externalIndex && externalMp3.Count > 1);
        externalIndex = idx;
        pendingPath = externalMp3[idx];
        pendingIdx = idx;
        
        isDecodingMp3 = true;
        decodeReady = false;
    }

    private void StartDecode(string path, int idx)
    {
        int gen = ++decodeGen;
        int writeBuf = 1 - lastClipBuf; // пишем в буфер, который не держит ни один клип
        decodeThread = new Thread(() =>
        {
            PinDecodeThreadToCore();
            DecodedPcm r = DecodeMp3Sync(path, idx, writeBuf);
            if (gen == decodeGen) { decodeResult = r; decodeReady = true; }
        });
        decodeThread.IsBackground = true;
        decodeThread.Start();
    }

    private void PollDecodeResult()
    {
        if (!isDecodingMp3 || !decodeReady) return;
        decodeReady = false;
        isDecodingMp3 = false;
        DecodedPcm pcm = decodeResult;

        // Битый файл — выкидываем из плейлиста и пробуем следующий
        if (!pcm.isValid)
        {
            if (pcm.idx >= 0 && pcm.idx < externalMp3.Count)
                externalMp3.RemoveAt(pcm.idx);
            if (externalMp3.Count > 0)
                PlayRandomMusic();
            else
            {
                mode = PlayMode.UnityFallback;
                PlayRandomFallbackMusic();
            }
            return;
        }

        // Успех и всё ещё можно играть — создаём клип НА ГЛАВНОМ ПОТОКЕ.
        // stream = true: Unity сам дочитывает чанками на аудио-потоке,
        // копирования 60 МБ в главном потоке больше нет.
        if (!stoppedForHub && mode == PlayMode.PcNyquist)
        {
            lastClipBuf = 1 - lastClipBuf;
            ClipStreamReader reader = new ClipStreamReader();
            reader.buffer = pcm.buffer;
            reader.total = pcm.sampleCount;
            AudioClip clip = AudioClip.Create(
                pcm.name,
                pcm.sampleCount / pcm.channels,
                pcm.channels,
                pcm.sampleRate,
                true,
                reader.Read);
            ShowTrackName(pcm.name);
            PlayClipUnityPath(clip);
        }
        // иначе: результат приехал поздно (ушли в хаб / сменился режим) —
        // просто выбрасываем, плейлист НЕ трогаем
    }

    private DecodedPcm DecodeMp3Sync(string filePath, int trackIndex, int bufIdx)
    {
        DecodedPcm result = default(DecodedPcm);
        result.idx = trackIndex;
        try
        {
            int loadResult = NYQ_PlayTrack(filePath);
            if (loadResult != 0)
            {
                Debug.LogError("[Nyquist] Failed to load file: " + filePath + " (error " + loadResult + ")");
                return result;
            }

            int sampleRate = NYQ_GetSampleRate();
            int channels = NYQ_GetChannels();
            UIntPtr samplesCount = NYQ_GetTotalSamples();
            ulong samples = samplesCount.ToUInt64();

            if (sampleRate == 0 || channels == 0 || samples == 0)
            {
                Debug.LogError("[Nyquist] Invalid audio params: rate=" + sampleRate + ", ch=" + channels + ", samples=" + samples);
                NYQ_StopTrack();
                return result;
            }

            // Многоразовый буфер: растёт только при встрече с более длинным треком.
            // Массив статический — GC никогда его не видит.
            int need = (int)samples;
            float[] buf = pcmBufs[bufIdx];
            if (buf == null || buf.Length < need) { buf = new float[need]; pcmBufs[bufIdx] = buf; }
            NYQ_GetPCMData(buf, samplesCount);
            NYQ_StopTrack();

            result.buffer = buf;
            result.sampleCount = need;
            result.channels = channels;
            result.sampleRate = sampleRate;
            result.name = Path.GetFileNameWithoutExtension(filePath);
            result.isValid = true;
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError("[Nyquist] Decode exception: " + e.GetType().Name + " - " + e.Message);
            try { NYQ_StopTrack(); }
            catch { }
            return result;
        }
    }
#endif

    // ==================== 12. Смена трека по ButtonX ====================
    private void SkipToNextTrack()
    {
#if UNITY_VITA
        if (mode == PlayMode.NativeHardware && externalMp3.Count > 0)
        {
            PlayNativeTrack();
            return;
        }
#endif
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR
        if (mode == PlayMode.PcNyquist && externalMp3.Count > 0)
        {
            PlayPcTrackAsync();
            return;
        }
#endif
        if (mode == PlayMode.UnityFallback)
            PlayRandomFallbackMusic();
    }

    // ==================== 13. Панель имени трека ====================
    private void ShowTrackName(string name)
    {
        currentTrackName = name;
        trackNameShowUntil = Time.realtimeSinceStartup + TRACK_NAME_TIME;
        // люфт 2 c считаем от реальной смены трека, а не от нажатия
        skipCooldownUntil = Time.realtimeSinceStartup + SKIP_COOLDOWN;
    }

    private void DrawTrackNamePanel()
    {
        if (trackStyle == null || trackStyleW != Screen.width)
        {
            trackStyle = new GUIStyle(GUI.skin.label);
            trackStyle.fontSize = GuiCore.FS(18);
            trackStyle.alignment = TextAnchor.MiddleLeft;
            trackStyle.wordWrap = false;
            trackStyleW = Screen.width;
        }
        if (panelTex == null)
        {
            panelTex = new Texture2D(1, 1);
            panelTex.SetPixel(0, 0, Color.white);
            panelTex.Apply();
        }

        float now = Time.realtimeSinceStartup;
        float remain = trackNameShowUntil - now;
        // фейд в последние полсекунды
        float fade = remain < 0.5f ? remain / 0.5f : 1f;
        // лёгкое мерцание (~1.4 Гц, амплитуда 22%)
        float blink = 0.78f + 0.22f * Mathf.Sin(now * 9f);
        float a = blink * fade;

        // Слева внизу, подрезан справа (было 700) — координаты 960x544, GuiCore масштабирует
        Rect r = GuiCore.RR(16, 478, 620, 54);

        // Подложка едва заметная (было 0.55)
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.25f * a);
        GUI.DrawTexture(r, panelTex);
        GUI.color = old;

        Rect tr = r;
        tr.x += GuiCore.FS(12);
        tr.width -= GuiCore.FS(12);
        trackStyle.normal.textColor = new Color(1f, 1f, 1f, a);
        string label = currentTrackName;
        if (label.Length > 72) label = label.Substring(0, 69) + "...";
        GUI.Label(tr, label, trackStyle);
    }

    // ==================== 14. Fallback + фейды ====================
    private void PlayRandomFallbackMusic()
    {
        if (backgroundPlaylist == null || backgroundPlaylist.Length == 0) return;
        if (backgroundPlaylist.Length == 1)
        {
            if (backgroundPlaylist[0] != null)
            {
                ShowTrackName(backgroundPlaylist[0].name);
                PlayClipUnityPath(backgroundPlaylist[0]);
            }
            return;
        }
        int newIdx;
        do { newIdx = UnityEngine.Random.Range(0, backgroundPlaylist.Length); }
        while (newIdx == currentTrackIndex);
        currentTrackIndex = newIdx;
        AudioClip clip = backgroundPlaylist[currentTrackIndex];
        if (clip == null) return;
        ShowTrackName(clip.name);
        PlayClipUnityPath(clip);
    }

    private void PlayClipUnityPath(AudioClip clip)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeTrackRoutine(clip));
    }

    private IEnumerator FadeTrackRoutine(AudioClip newClip)
    {
        float startVolume = musicSource.volume;
        if (musicSource.isPlaying)
        {
            while (musicSource.volume > 0f)
            {
                musicSource.volume -= startVolume * (Time.deltaTime / fadeDuration);
                yield return null;
            }
            AudioClip old = musicSource.clip;
            musicSource.Stop();
            musicSource.volume = 0f; // с нуля — чтобы фейд-ин реально работал
            if (old != null && old != newClip)
                UnityEngine.Object.Destroy(old);
            musicSource.clip = newClip;
        }
        else
        {
            AudioClip old = musicSource.clip;
            if (old != null && old != newClip)
                UnityEngine.Object.Destroy(old);
            musicSource.clip = newClip;
            musicSource.volume = 0f;
        }
        musicSource.Play();
        while (musicSource.volume < targetVolume)
        {
            musicSource.volume += targetVolume * (Time.deltaTime / fadeDuration);
            yield return null;
        }
        musicSource.volume = targetVolume;
        fadeCoroutine = null;
    }

#if UNITY_VITA
    private void StopNativePlayback()
    {
        SafeCall(() => VA_StopTrack(), "StopNativePlayback");
    }
#endif

    // ==================== 15. Safe wrappers ====================
    private T SafeGet<T>(Func<T> func, T fallback, string name = "")
    {
        try { return func(); }
        catch (Exception e)
        {
            Debug.LogError("[VA] " + name + " EXC: " + e.GetType().Name + " | " + e.Message);
            return fallback;
        }
    }

    private void SafeCall(Action action, string name = "")
    {
        try { action(); }
        catch (Exception e)
        {
            Debug.LogError("[VA] " + name + " EXC: " + e.GetType().Name + " | " + e.Message);
        }
    }

    // ==================== 16. Монитор + панель ====================
    protected override bool ShouldDraw()
    {
        // Панель имени трека рисуется всегда (меню и геймплей), монитор — по enableGui
        return enableGui || TrackNameVisible;
    }

    private GUIStyle dbgStyle;
    private int dbgStyleW = -1;

    protected override void Draw()
    {
        if (TrackNameVisible) DrawTrackNamePanel();
        if (!enableGui) return;

        if (dbgStyle == null || dbgStyleW != Screen.width)
        {
            dbgStyle = new GUIStyle(GUI.skin.label);
            dbgStyle.fontSize = GuiCore.FS(18);
            dbgStyle.normal.textColor = Color.white;
            dbgStyleW = Screen.width;
        }
        float cy = 30f;
        GUI.Box(GuiCore.RR(480, 0, 480, 544), "Audio Manager Monitor");
        DrawLine("Play Mode: <color=yellow>" + GetModeName() + "</color>", dbgStyle, GuiCore.RR(490, cy, 460, 30));
        cy += 30f;

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR
        if (mode == PlayMode.PcNyquist)
        {
            DrawLine("PC Nyquist Decoder (stream)", dbgStyle, GuiCore.RR(490, cy, 460, 30));
            cy += 30f;
            DrawLine("Track: " + currentTrackName, dbgStyle, GuiCore.RR(490, cy, 460, 30));
            cy += 30f;
            DrawLine("Files: " + externalMp3.Count, dbgStyle, GuiCore.RR(490, cy, 460, 30));
            cy += 30f;
            DrawLine("Decoding: " + isDecodingMp3, dbgStyle, GuiCore.RR(490, cy, 460, 30));
            return;
        }
#endif

#if !UNITY_VITA
        if (mode == PlayMode.UnityFallback)
        {
            DrawLine("PC Unity Fallback Mode", dbgStyle, GuiCore.RR(490, cy, 460, 30));
            cy += 30f;
            DrawLine("Track: " + currentTrackName, dbgStyle, GuiCore.RR(490, cy, 460, 30));
            return;
        }
#endif

#if UNITY_VITA
        DrawLine("Plugin Ready: " + pluginReady + " | Workable: " + pluginWorkable, dbgStyle, GuiCore.RR(490, cy, 460, 30));
        cy += 30f;
        DrawLine("VA Init Error: <color=orange>" + d_initErr + " (0x" + d_initErr.ToString("X8") + ")</color>", dbgStyle, GuiCore.RR(490, cy, 460, 30));
        cy += 30f;
        if (!pluginReady)
        {
            DrawLine("<color=red>" + excText + "</color>", dbgStyle, GuiCore.RR(490, cy, 460, 30));
            cy += 30f;
            DrawLine("Fallback: playing via AudioSource", dbgStyle, GuiCore.RR(490, cy, 460, 30));
            return;
        }
        DrawLine("State: <color=cyan>" + GetStateName(d_st) + "</color>(" + d_st + ")", dbgStyle, GuiCore.RR(490, cy, 460, 30));
        cy += 30f;
        DrawLine("Track: " + currentTrackName, dbgStyle, GuiCore.RR(490, cy, 460, 30));
        cy += 30f;
        DrawLine("Rate/Ch: " + d_rate + " / " + d_ch + " | MP3s: " + externalMp3.Count, dbgStyle, GuiCore.RR(490, cy, 460, 30));
        cy += 30f;
        DrawLine("Vol: " + targetVolume.ToString("0.00") + " | Paused: " + isPaused, dbgStyle, GuiCore.RR(490, cy, 460, 30));
        cy += 30f;
        string errCol = d_err == 0 ? "green" : "red";
        DrawLine("LastError: <color=" + errCol + ">" + d_err + " (0x" + d_err.ToString("X8") + ")</color>", dbgStyle, GuiCore.RR(490, cy, 460, 30));
        cy += 30f;
        DrawLine("ThrStat: " + d_thrStat + " | DecRet: " + d_decRet + " (0x" + d_decRet.ToString("X8") + ")", dbgStyle, GuiCore.RR(490, cy, 460, 30));
        cy += 30f;
        DrawLine("Init: " + d_initCnt + " | Alive: " + d_alive + " | Polls: " + d_polls + " | Cmds: " + d_cmds, dbgStyle, GuiCore.RR(490, cy, 460, 30));
        cy += 30f;
        DrawLine("Plays: " + d_plays + " | Opens: " + d_opens, dbgStyle, GuiCore.RR(490, cy, 460, 30));
#endif
    }

    private string GetModeName()
    {
        switch (mode)
        {
            case PlayMode.None: return "None";
            case PlayMode.UnityFallback: return "Unity Fallback";
            case PlayMode.NativeHardware: return "Vita Native";
            case PlayMode.PcNyquist: return "PC Nyquist";
            default: return "?";
        }
    }

    private string GetStateName(int s)
    {
        switch (s)
        {
            case ST_STOP: return "STOP";
            case ST_FADE_IN: return "FADE_IN";
            case ST_PLAY: return "PLAY";
            case ST_FADE_OUT: return "FADE_OUT";
            case ST_PAUSE: return "PAUSE";
        }
        return "UNKNOWN(" + s + ")";
    }

    private void DrawLine(string text, GUIStyle style, Rect r)
    {
        GUI.Label(r, text, style);
    }
}