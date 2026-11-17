// File: Assets/VitaUI/VitaUIBootstrap.cs — пример меню по шаблону спеки
using UnityEngine;

namespace VitaUI
{
    public class VitaUIBootstrap : MonoBehaviour
    {
        public UIStyle style;
        public Font font;

        void Awake()
        {
            GameObject go = new GameObject("UIRoot");
            UIRoot root = go.AddComponent<UIRoot>();
            root.style = style;
            root.font = font;
            // UIRoot.Awake отработает синхронно при AddComponent,
            // UIRoot.Start (→ Run → OnBind всех) — позже в этом же кадре. Порядок ок.

            MainMenu main = new MainMenu("Main");
            SettingsScreen settings = new SettingsScreen("Settings");
            AchScreen ach = new AchScreen("Ach");
            SoundSettings sound = new SoundSettings("Sound", settings);
            UITabs tabs = new UITabs(main, settings, ach);

            main.Wire(tabs, settings, ach);
            settings.Wire(tabs, main, sound);
            ach.Wire(tabs, main);
        }
    }

    class MainMenu : UIScreen
    {
        UITabs tabs; UIScreen settings, ach;
        public MainMenu(string n) : base(n) { }
        public void Wire(UITabs t, UIScreen s, UIScreen a) { tabs = t; settings = s; ach = a; }

        protected override void OnBind()
        {
            UIBox root = panel.AddVBox(UIAnchor.Center, 0, -20, 14);
            root.AddTitle("Retro Space", 48);
            UIBox menu = root.AddVBox(10);
            menu.AddButton("Play", () => UI.Toast("Play (demo)"));
            menu.AddButton("Settings", () => tabs.SwitchTo(settings));
            menu.AddButton("Achievements", () => tabs.SwitchTo(ach));
        }
        //protected override void OnBack2() { } // заглушка — см. ниже
        public override bool OnBack() { return true; } // main: здесь будет конфирм выхода
    }

    class SettingsScreen : UIScreen
    {
        UITabs tabs; UIScreen main, sound;
        public SettingsScreen(string n) : base(n) { }
        public void Wire(UITabs t, UIScreen m, UIScreen s) { tabs = t; main = m; sound = s; }

        protected override void OnBind()
        {
            UIBox root = panel.AddVBox(UIAnchor.Center, 0, 0, 14);
            root.AddTitle("Settings", 48);
            UIBox list = root.AddVBox(10);
            list.AddButton("Sound", () => UI.GoSub(this, sound));
        }
        public override bool OnBack() { tabs.SwitchTo(main); return true; }
    }

    class SoundSettings : UIScreen
    {
        UISlider vol;
        public SoundSettings(string n, UIScreen parent) : base(n, parent) { }
        protected override void OnBind()
        {
            UIBox root = panel.AddVBox(UIAnchor.Center, 0, 0, 14);
            root.AddTitle("Sound", 48);
            UIBox row = root.AddHBox(12, Align.Center);
            vol = row.AddSlider(200, 0.7f, v => UI.Toast("volume " + v.ToString("0.00")));
            root.AddButton("Reset to Defaults", () =>
            {
                vol.value = 0.5f;
                if (vol.onChange != null) vol.onChange(0.5f);
            }, new Vector2(220, 60));
        }
        // OnBack НЕ пишем: дефолт через parent
    }

    class AchScreen : UIScreen
    {
        UITabs tabs; UIScreen main;
        UIDynText best;
        public AchScreen(string n) : base(n) { }
        public void Wire(UITabs t, UIScreen m) { tabs = t; main = m; }

        protected override void OnBind()
        {
            UIBox root = panel.AddVBox(UIAnchor.Center, 0, 0, 14);
            root.AddTitle("Achievements", 48);
            best = root.AddDynText(400, 40, 24);
        }
        protected override void OnShow() { best.SetText("Best: 12.4 sec"); }
        public override bool OnBack() { tabs.SwitchTo(main); return true; }
    }
}
