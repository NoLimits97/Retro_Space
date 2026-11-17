using UnityEngine;

public static class VitaGcPlugin
{
    private static int state = 0; // 0 = не пробовали, 1 = работает, -1 = недоступен

    // __Internal означает, что функция слинкована в главный eboot.bin
    [System.Runtime.InteropServices.DllImport("__Internal",
        CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern void VG_SetGcDisabled(int on);

    [System.Runtime.InteropServices.DllImport("__Internal",
        CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern int VG_IsGcDisabled();

    public static bool NativeActive { get { return state == 1; } }

    public static void Disable()
    {
        if (Application.platform != RuntimePlatform.PSP2) { state = -1; return; }
        if (state == -1) return;
        try
        {
            VG_SetGcDisabled(1);
            state = 1;
            Debug.Log("[GC] Native GC_disable() called successfully!");
        }
        catch (System.DllNotFoundException) { state = -1; Debug.LogError("[GC] __Internal DllNotFound"); }
        catch (System.EntryPointNotFoundException) { state = -1; Debug.LogError("[GC] VG_SetGcDisabled EntryPointNotFound"); }
    }

    public static void Enable()
    {
        if (state != 1) return;
        try { VG_SetGcDisabled(0); } catch { }
    }
}