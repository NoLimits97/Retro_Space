using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Text;
using System.Threading;

// VitaLagProfiler v6 — ноль влияния на фреймтайм в геймплее:
// - главный тред: только запись struct'ов в ring-буферы (без строк, IO, P/Invoke);
// - автосборка и запись файла — в фоновом потоке, преаллоцированными буферами;
// - синхронных записей из главного треда нет вообще.
public class VitaLagProfiler : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private float spikeThresholdMs = 18f;
    [SerializeField] private float warmupSeconds = 5f;
    [SerializeField] private float autoDumpInterval = 30f;
    [SerializeField] private string logPath = "ux0:data/lag_log.txt";
    [SerializeField] private bool showOverlay = false;

    private struct Spike { public float t; public float ms; public long heapKb; }
    private struct HeapSample { public float t; public long kb; }

    private const int CAP = 512;
    private const int HEAP_CAP = 256;

    private readonly Spike[] buf = new Spike[CAP];
    private readonly HeapSample[] heapBuf = new HeapSample[HEAP_CAP];

    private volatile int count = 0;        // пишет главный тред
    private volatile int heapCount = 0;
    private volatile int written = 0;      // пишет поток-писатель
    private volatile float lastDumpAt = -10f;

    private float maxMs = 0f;
    private long frames = 0;
    private float heapTimer = 0f;
    private long lastHeapKb = 0;
    private string curScene = "";
    private float overlayTimer = 0f;
    private string overlayText = "";
    private static VitaLagProfiler inst;

    // очередь тегов для потока-писателя
    private readonly object qLock = new object();
    private readonly System.Collections.Generic.Queue<string> queue = new System.Collections.Generic.Queue<string>();
    private Thread writer;
    private volatile bool quitRequested = false;
    private StreamWriter sw;
    private readonly StringBuilder sb = new StringBuilder(1 << 16); // преаллок
    private readonly char[] charBuf = new char[1 << 16];            // преаллок

    private const string GcPluginName = "vita_gc";
    [System.Runtime.InteropServices.DllImport(GcPluginName,
        CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern int VG_IsGcDisabled();

    private static int SafeGcProbe()
    {
        try { return VG_IsGcDisabled(); }
        catch (System.DllNotFoundException) { return -1; }
        catch (System.EntryPointNotFoundException) { return -2; }
        catch (System.Exception) { return -3; }
    }

    void Awake()
    {
        if (inst != null) { Destroy(gameObject); return; }
        inst = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        curScene = SceneManager.GetActiveScene().name;
        writer = new Thread(WriterLoop);
        writer.IsBackground = true;
        writer.Priority = System.Threading.ThreadPriority.BelowNormal;
        writer.Start();
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; Enqueue("disable"); }
    void OnApplicationPause(bool p) { if (p) Enqueue("pause"); }
    void OnApplicationQuit()
    {
        Enqueue("quit");
        quitRequested = true;
        if (writer != null) writer.Join(1000);
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        Enqueue("scene:" + curScene + "->" + s.name);
        curScene = s.name;
    }

    private void Enqueue(string tag) { lock (qLock) { queue.Enqueue(tag); } }
    private string Dequeue() { lock (qLock) { return queue.Count > 0 ? queue.Dequeue() : null; } }

    void Update()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        frames++;
        if (ms > maxMs) maxMs = ms;

        float t = Time.realtimeSinceStartup;

        // трасса кучи 1 Гц — без аллокаций
        heapTimer += Time.unscaledDeltaTime;
        if (heapTimer >= 1f)
        {
            heapTimer = 0f;
            lastHeapKb = System.GC.GetTotalMemory(false) / 1024;
            heapBuf[heapCount % HEAP_CAP] = new HeapSample { t = t, kb = lastHeapKb };
            heapCount++;
        }

        // спайк — только struct в буфер, без P/Invoke и строк
        if (t > warmupSeconds && t - lastDumpAt > 0.5f && ms > spikeThresholdMs)
        {
            buf[count % CAP] = new Spike { t = t, ms = ms, heapKb = lastHeapKb };
            count++;
            if (count - written >= CAP) Enqueue("overflow");
        }

        if (showOverlay)
        {
            overlayTimer += Time.unscaledDeltaTime;
            if (overlayTimer > 0.5f)
            {
                overlayTimer = 0f;
                overlayText = "spikes=" + count + " max=" + maxMs.ToString("F1") + "ms heap=" + lastHeapKb + "KB";
            }
        }

        if (Input.GetKeyDown(KeyCode.F9)) Enqueue("manual");
    }

#if VA_DEBUG_IMGUI
    void OnGUI()
    {
        if (!showOverlay) return;
        GUI.Label(new Rect(10, 60, 800, 40), overlayText);
    }
#endif

    // ================= фоновый поток-писатель =================
    private void WriterLoop()
    {
        try
        {
            sw = new StreamWriter(new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read));
            sw.Write("=== session start scene=");
            sw.Write(curScene);
            sw.Write(" probe=");
            sw.Write(SafeGcProbe());
            sw.Write('\n');
            sw.Flush();
        }
        catch { return; }

        float autoTimer = 0f;
        while (true)
        {
            Thread.Sleep(200);
            autoTimer += 0.2f;
            if (autoTimer >= autoDumpInterval) { autoTimer = 0f; Enqueue("auto"); }

            string tag;
            while ((tag = Dequeue()) != null)
            {
                WriteDump(tag);
                lastDumpAt = Time.realtimeSinceStartup;
            }

            if (quitRequested)
            {
                while ((tag = Dequeue()) != null) WriteDump(tag);
                break;
            }
        }

        try { sw.Flush(); sw.Close(); } catch { }
    }

    private int heapWritten = 0;

    private void WriteDump(string tag)
    {
        int cNow = count;
        int hNow = heapCount;
        if (cNow <= written && hNow <= heapWritten) return;

        sb.Length = 0;
        sb.Append("=== ").Append(tag)
          .Append(" scene=").Append(curScene)
          .Append(" frames=").Append(frames)
          .Append(" spikes=").Append(cNow - written)
          .Append(" max=").Append(maxMs.ToString("F1")).Append("ms\n");

        sb.Append("--- heap trace (KB) ---\n");
        for (int i = heapWritten; i < hNow; i++)
        {
            HeapSample h = heapBuf[i % HEAP_CAP];
            sb.Append("h t=").Append(h.t.ToString("F0")).Append(" kb=").Append(h.kb);
            if (i > 0)
            {
                HeapSample p = heapBuf[(i - 1) % HEAP_CAP];
                long d = h.kb - p.kb;
                if (d < -4) sb.Append("  <<<< DROP ").Append(d);
            }
            sb.Append('\n');
        }

        sb.Append("--- spikes ---\n");
        for (int i = written; i < cNow; i++)
        {
            Spike s = buf[i % CAP];
            sb.Append("t=").Append(s.t.ToString("F2"))
              .Append(" ms=").Append(s.ms.ToString("F1"))
              .Append(" heap=").Append(s.heapKb);
            if (i > 0)
            {
                Spike p = buf[(i - 1) % CAP];
                sb.Append(" dt=").Append((s.t - p.t).ToString("F1"));
            }
            sb.Append('\n');
        }

        // в файл без ToString() всего StringBuilder — преаллоком char[]
        int total = sb.Length;
        int pos = 0;
        while (pos < total)
        {
            int n = total - pos > charBuf.Length ? charBuf.Length : total - pos;
            sb.CopyTo(pos, charBuf, 0, n);
            sw.Write(charBuf, 0, n);
            pos += n;
        }
        sw.Flush();

        written = cNow;
        heapWritten = hNow;
    }
}