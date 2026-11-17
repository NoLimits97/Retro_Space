using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

// GameManager v6 — v4 + само-возвращающиеся пулы на очередях:
// любой SetActive(false) сам кладёт объект в очередь (OnDisable + флаг InQueue);
// спавнер — чистый O(1) Dequeue без проверок и без проход-сканов.
// GC-оркестрация и прочий функционал — без изменений.
public class GameManager : Gui
{
    [Header("Префабы объектов")]
    [SerializeField] GameObject[] meteorPrefabs;
    [SerializeField] GameObject windPrefab;
    [SerializeField] GameObject[] dustPrefab;
    public float dustSpawnInterval = 5f;
    private float dustTimer = 0f;

    [Header("Настройки спавна метеоритов")]
    [SerializeField] float meteorSpawnInterval = 1.5f;
    [SerializeField] float spawnX = 10f;
    [SerializeField] float spawnYMin = -4.5f;
    [SerializeField] float spawnYMax = 4.5f;

    [Header("Продвинутый спавн")]
    [SerializeField] private float minSpawnInterval = 0.5f;
    [SerializeField] private float difficultyScale = 0.02f;
    [SerializeField] private float meteorSpacing = 1.5f;

    [Header("Настройки спавна ветра")]
    [SerializeField] float windSpawnInterval = 0.2f;

    [Header("Настройки памяти пула")]
    [SerializeField] private int maxMeteorsInMemory = 30;
    [SerializeField] private int maxWindInMemory = 10;
    [SerializeField] private int maxDustInMemory = 10;
    private Pool meteorPool;
    private Pool windPool;
    private Pool dustPool;

    [Header("Глобальный стиль интерфейса")]
    public GUISkin globalSkin;

    [Header("Элементы для бесшовного старта")]
    [SerializeField] GameObject menuHolder;
    [SerializeField] GameObject playerRocket;
    [SerializeField] GameObject gameplayUIHolder;
    [SerializeField] GameObject settingsHolder;
    [SerializeField] GameObject HolderAchieviements;
    [SerializeField] GameObject mainMenuPanel;

    [SerializeField] private UnityEngine.UI.Text timerText;
    [SerializeField] private UnityEngine.UI.Text bestTimeText;
    [SerializeField] private UnityEngine.UI.Button playButton;

    private bool isGameActive = false;
    private bool isPaused = false;
    private bool isWaitingForConfirm = false;
    private bool isInSubMenu = false;

    [Header("Настройки Паузы")]
    [SerializeField] GameObject pauseMenuHolder;
    [SerializeField] private UnityEngine.UI.Button firstPauseMenuButton;

    float meteorTimer = 0f;
    float windTimer = 0f;
    float gametimer = 0f;
    float bestTime = 0f;
    private float lastShownTime = -1f;
    public static string savedHubNodeID = "";

    // =========================================================================
    // ПУЛ: объект сам возвращается в очередь при любом SetActive(false)
    // =========================================================================
    public sealed class PoolItem : MonoBehaviour
    {
        [System.NonSerialized] public Queue<PoolItem> Home;
        [System.NonSerialized] public bool InQueue;

        // OnDisable срабатывает на любой переход active->inactive:
        // мувер погасил метеор — он сам уехал в очередь. Ровно один раз (флаг).
        private void OnDisable()
        {
            if (Home != null && !InQueue)
            {
                InQueue = true;
                Home.Enqueue(this);
            }
        }
    }

    private sealed class Pool
    {
        public readonly Queue<PoolItem> Free;
        public readonly PoolItem[] All;   // только для разового прогрева

        public Pool(int capacity)
        {
            Free = new Queue<PoolItem>(capacity);
            All = new PoolItem[capacity];
        }

        public void Register(PoolItem item, int index)
        {
            item.Home = Free;
            All[index] = item;
            // в очередь здесь не суём: начальный SetActive(false) сам
            // пришлёт объект через OnDisable
        }
    }

    // Чистый O(1): первый в очереди гарантированно неактивен.
    private GameObject TakeFromPool(Pool p)
    {
        Queue<PoolItem> q = p.Free;
        while (q.Count > 0)
        {
            PoolItem it = q.Dequeue();
            if (it == null) continue;   // объект убит извне — пропускаем
            it.InQueue = false;
            return it.gameObject;
        }
        return null; // всё реально летит — память не выделяется, как и раньше
    }

    private void WarmupPool(Pool p)
    {
        for (int i = 0; i < p.All.Length; i++)
        {
            if (p.All[i] == null) continue;
            p.All[i].gameObject.SetActive(true);
            p.All[i].gameObject.SetActive(false);
        }
    }

    // =========================================================================
    void Start()
    {
        PreWarm(16); // замена HeapPreWarmer: надуть кучу до геймплея

        meteorPool = new Pool(maxMeteorsInMemory);
        if (meteorPrefabs != null && meteorPrefabs.Length > 0)
        {
            for (int i = 0; i < maxMeteorsInMemory; i++)
            {
                GameObject obj = Instantiate(meteorPrefabs[i % meteorPrefabs.Length]);
                PoolItem item = obj.AddComponent<PoolItem>();
                meteorPool.Register(item, i);
                obj.SetActive(false); // OnDisable сам положит в очередь
            }
        }

        windPool = new Pool(maxWindInMemory);
        if (windPrefab != null)
        {
            for (int i = 0; i < maxWindInMemory; i++)
            {
                GameObject obj = Instantiate(windPrefab);
                PoolItem item = obj.AddComponent<PoolItem>();
                windPool.Register(item, i);
                obj.SetActive(false);
            }
        }

        dustPool = new Pool(maxDustInMemory);
        if (dustPrefab != null && dustPrefab.Length > 0)
        {
            for (int i = 0; i < maxDustInMemory; i++)
            {
                GameObject obj = Instantiate(dustPrefab[i % dustPrefab.Length]);
                PoolItem item = obj.AddComponent<PoolItem>();
                dustPool.Register(item, i);
                obj.SetActive(false);
            }
        }

        if (globalSkin != null && globalSkin.font != null)
        {
            globalSkin.font.RequestCharactersInTexture(
                "Подтвердите выход в корабль: Круг - Да, Любая другая - Отмена", 24, FontStyle.Normal);
        }

        if (menuHolder != null) menuHolder.SetActive(false);
        if (playerRocket != null) playerRocket.SetActive(false);
        StartCoroutine(StartupWarmup());

        bestTime = PlayerPrefs.GetFloat("BestFlightTime", 0f);
        if (bestTimeText != null)
        {
            bestTimeText.text = bestTime.ToString("F1") + " сек";
        }
    }

    void Update()
    {
        windTimer += Time.deltaTime;
        if (windTimer >= windSpawnInterval)
        {
            SpawnWind();
            windTimer = 0f;
        }

        if (isGameActive && !isPaused)
        {
            meteorTimer += Time.deltaTime;
            gametimer += Time.deltaTime;

            float shown = Mathf.Floor(gametimer * 10f) / 10f;
            if (shown != lastShownTime)
            {
                lastShownTime = shown;
                if (timerText != null)
                {
                    timerText.text = "Time: " + shown.ToString("F1") + " сек";
                }
            }

            float currentSpawnInterval = Mathf.Max(minSpawnInterval, meteorSpawnInterval - (gametimer * difficultyScale));
            if (meteorTimer >= currentSpawnInterval)
            {
                MasterSpawnObstacle();
                meteorTimer = 0f;
            }

            if (Input.GetButtonDown("ButtonY"))
            {
                TogglePause();
            }
            return;
        }

        if (isWaitingForConfirm)
        {
            if (Input.anyKeyDown)
            {
                if (Input.GetButtonDown("ButtonB"))
                {
                    ReturnToShipHub();
                }
                else
                {
                    isWaitingForConfirm = false;
                    if (menuHolder != null) menuHolder.SetActive(true);
                    ResetFocusForVita();
                }
            }
            return;
        }

        if (isInSubMenu)
        {
            if (Input.GetButtonDown("ButtonB"))
            {
                if (settingsHolder != null && settingsHolder.activeSelf)
                {
                    CloseSettings();
                }
                else if (HolderAchieviements != null && HolderAchieviements.activeSelf)
                {
                    CloseAchieviements();
                }
            }
            return;
        }

        if (isGameActive && isPaused)
        {
            if (Input.GetButtonDown("ButtonY") || Input.GetButtonDown("ButtonB"))
            {
                TogglePause();
            }
        }
        else if (!isGameActive)
        {
            if (Input.GetButtonDown("ButtonB"))
            {
                isWaitingForConfirm = true;
                if (menuHolder != null) menuHolder.SetActive(false);
            }
        }
    }

    // =========================================================================
    // БЛОК СПАВНА
    // =========================================================================
    private void SpawnSingle(Pool pool, float yPosition, float zPosition = 0f)
    {
        GameObject obj = TakeFromPool(pool);
        if (obj != null)
        {
            obj.transform.position = new Vector3(spawnX, yPosition, zPosition);
            obj.transform.rotation = Quaternion.identity;
            obj.SetActive(true);
        }
    }

    private void SpawnDuet(Pool pool)
    {
        float y1 = Random.Range(spawnYMin, spawnYMax);
        float y2 = Random.Range(spawnYMin, spawnYMax);
        if (Mathf.Abs(y1 - y2) < meteorSpacing)
            y2 = (y1 > 0) ? y1 - meteorSpacing : y1 + meteorSpacing;
        SpawnSingle(pool, y1);
        SpawnSingle(pool, y2);
    }

    private void SpawnWall(Pool pool)
    {
        float safeZoneY = Random.Range(spawnYMin + 1.5f, spawnYMax - 1.5f);
        float safeSize = 2.2f;
        for (float currentY = spawnYMin; currentY <= spawnYMax; currentY += meteorSpacing)
        {
            if (Mathf.Abs(currentY - safeZoneY) < safeSize) continue;
            SpawnSingle(pool, currentY);
        }
    }

    private void PatternMeteorAndDustLine()
    {
        float randomY = Random.Range(spawnYMin, spawnYMax);
        SpawnSingle(meteorPool, randomY);
        float originalSpawnX = spawnX;
        spawnX += 2.5f;
        SpawnSingle(dustPool, randomY);
        spawnX = originalSpawnX;
    }

    private void PatternVerticalMeteorDuet()
    {
        float randomY = Random.Range(spawnYMin + 1.0f, spawnYMax - 1.0f);
        float halfSpacing = meteorSpacing / 2f;
        SpawnSingle(meteorPool, randomY + halfSpacing);
        SpawnSingle(meteorPool, randomY - halfSpacing);
    }

    private void MasterSpawnObstacle()
    {
        int spawnPattern = Random.Range(0, 3);
        switch (spawnPattern)
        {
            case 0:
                SpawnSingle(meteorPool, Random.Range(spawnYMin, spawnYMax));
                break;
            case 1:
                PatternMeteorAndDustLine();
                break;
            case 2:
                PatternVerticalMeteorDuet();
                break;
        }
    }

    void SpawnWind()
    {
        float randomY = Random.Range(spawnYMin, spawnYMax);
        SpawnSingle(windPool, randomY, 1f);
    }

    // =========================================================================
    // ПРОГРЕВ
    // =========================================================================
    private IEnumerator StartupWarmup()
    {
        GuiWarmup.Prepare(globalSkin,
            globalSkin != null ? globalSkin.font : null,
            new int[] { 24 },
            new string[] { "Подтвердите выход в корабль: Круг - Да, Любая другая - Отмена" });
        GuiWarmup.Start(3);

        if (playerRocket != null)
        {
            playerRocket.transform.position = new Vector3(-5f, 0f, 0f);
            playerRocket.SetActive(true);
        }

        WarmupPool(meteorPool);
        WarmupPool(windPool);
        WarmupPool(dustPool);

        yield return null;
        yield return null;

        if (playerRocket != null) playerRocket.SetActive(false);
        if (menuHolder != null) menuHolder.SetActive(true);
        PlannedGC(); // мусор загрузки собирается в хитч прогрева (было в HeapPreWarmer)
    }

    // =========================================================================
    // БЛОК ИНТЕРФЕЙСА И СОСТОЯНИЙ
    // =========================================================================
    public void PlayGame()
    {
        isGameActive = true;
        isPaused = false;
        isInSubMenu = false;
        gametimer = 0f;
        lastShownTime = -1f;

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (playerRocket != null) playerRocket.SetActive(true);
        if (gameplayUIHolder != null) gameplayUIHolder.SetActive(true);

        PlayerController controller = playerRocket.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.ResetPlayerState();
        }
        if (playerRocket != null) playerRocket.transform.position = new Vector3(-5f, 0f, 0f);

        PlannedGC();             // hitch тонет в нажатии кнопки
        VitaGcPlugin.Disable();  // полёт без автосборок
    }

    public void OpenSettings()
    {
        if (isWaitingForConfirm) return;
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsHolder != null)
        {
            settingsHolder.SetActive(true);
            isInSubMenu = true;
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            UnityEngine.UI.Slider settingsSlider = settingsHolder.GetComponentInChildren<UnityEngine.UI.Slider>();
            if (settingsSlider != null)
            {
                settingsSlider.Select();
            }
        }
    }

    public void OpenAchieviements()
    {
        if (isWaitingForConfirm) return;
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (HolderAchieviements != null)
        {
            isInSubMenu = true;
            HolderAchieviements.SetActive(true);
            UnityEngine.UI.Button firstButton = HolderAchieviements.GetComponentInChildren<UnityEngine.UI.Button>();
            if (EventSystem.current != null && firstButton != null)
            {
                firstButton.Select();
            }
        }
    }

    public void GameOver()
    {
        VitaGcPlugin.Enable(); // меню результатов снова с авто-GC
        isGameActive = false;
        if (playerRocket != null) playerRocket.SetActive(false);
        if (gameplayUIHolder != null) gameplayUIHolder.SetActive(false);
        if (menuHolder != null) menuHolder.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        if (gametimer > bestTime)
        {
            bestTime = gametimer;
            PlayerPrefs.SetFloat("BestFlightTime", bestTime);
            PlayerPrefs.Save();
            if (bestTimeText != null)
            {
                bestTimeText.text = bestTime.ToString("F1") + " сек";
            }
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            if (playButton != null)
            {
                playButton.Select();
            }
        }
        PlannedGC(); // собираем мусор полёта на экране результатов
    }

    public void CloseSettings()
    {
        if (settingsHolder != null) settingsHolder.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        isInSubMenu = false;
        ResetFocusForVita();
    }

    public void CloseAchieviements()
    {
        if (HolderAchieviements != null) HolderAchieviements.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        isInSubMenu = false;
        ResetFocusForVita();
    }

    private void ResetFocusForVita()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            if (playButton != null)
            {
                playButton.Select();
            }
        }
    }

    // Плановая сборка там, где hitch невидим. Двойной проход — дочистить финализаторы.
    private void PlannedGC()
    {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    // Замена HeapPreWarmer.PreWarm: надуть кучу, чтобы аллокации
    // не будили Boehm расширением во время полёта.
    private static void PreWarm(int mb)
    {
        var chunks = new List<byte[]>(mb);
        for (int i = 0; i < mb; i++)
            chunks.Add(new byte[1024 * 1024]);
        chunks.Clear();
        chunks = null;
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        if (isPaused)
        {
            Time.timeScale = 0f;
            if (pauseMenuHolder != null) pauseMenuHolder.SetActive(true);
            if (EventSystem.current != null && firstPauseMenuButton != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                firstPauseMenuButton.Select();
            }
            if (AudioManager.instance != null)
            {
                AudioManager.instance.PauseMusic();
            }
            PlannedGC(); // пауза = сейф-момент: мусор полёта под ширмой паузы
        }
        else
        {
            Time.timeScale = 1f;
            if (pauseMenuHolder != null) pauseMenuHolder.SetActive(false);
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            if (AudioManager.instance != null)
            {
                AudioManager.instance.ResumeMusic();
            }
        }
    }

    public void ExitToMainMenu()
    {
        isGameActive = false;
        isPaused = false;
        isInSubMenu = false;
        Time.timeScale = 1f;
        gametimer = 0f;
        meteorTimer = 0f;
        windTimer = 0f;
        lastShownTime = -1f;

        if (timerText != null)
        {
            timerText.text = "Время: 0.0 сек";
        }

        if (EventSystem.current != null && playButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            playButton.Select();
        }

        if (menuHolder != null) menuHolder.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (playerRocket != null) playerRocket.SetActive(false);
        if (gameplayUIHolder != null) gameplayUIHolder.SetActive(false);
        if (pauseMenuHolder != null) pauseMenuHolder.SetActive(false);

        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayRandomMusic();
        }

        VitaGcPlugin.Enable(); // меню снова с авто-GC
        PlannedGC();
    }

    // =========================================================================
    // ОТРИСОВКА ЧЕРЕЗ ЯДРО Gui
    // =========================================================================
    protected override bool ShouldDraw()
    {
        return isWaitingForConfirm;
    }

    protected override void Draw()
    {
        if (globalSkin != null)
        {
            GUI.skin = globalSkin;
        }
        GUIStyle myLabelStyle = LabelStyle(24, globalSkin != null ? globalSkin.font : null);
        Rect positionRect = GuiCore.R(30, 232, 900, 80);
        GUI.Label(positionRect, "Подтвердите выход в корабль: Круг - Да, Любая другая - Отмена", myLabelStyle);
    }

    void ReturnToShipHub()
    {
        isWaitingForConfirm = false;
        isGameActive = false;
        isPaused = false;
        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(savedHubNodeID))
        {
            savedHubNodeID = "Gramophone_Zone";
        }
        Debug.Log("Улетаем на корабль к точке: " + savedHubNodeID);

        VitaGcPlugin.Enable(); // хаб снова с авто-GC
        PlannedGC();
        UnityEngine.SceneManagement.SceneManager.LoadScene("Hub_work");
    }
}