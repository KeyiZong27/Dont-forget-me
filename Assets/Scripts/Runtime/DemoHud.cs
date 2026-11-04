using System;
using UnityEngine;

namespace DontForgetMe
{
    public sealed class DemoHud : MonoBehaviour
    {
        private Font font;
        private GUIStyle body;
        private GUIStyle title;
        private GUIStyle small;
        private GUIStyle statValue;
        private GUIStyle button;
        private GUIStyle caption;
        private GUIStyle warning;
        private Texture2D panel;
        private Texture2D meterTexture;
        private bool hasStarted;
        private bool showNotes;

        private void Awake()
        {
            font = CreateFont();
            panel = new Texture2D(1, 1);
            panel.SetPixel(0, 0, new Color(0.07f, 0.085f, 0.09f, 0.92f));
            panel.Apply();
            meterTexture = new Texture2D(1, 1);
            meterTexture.SetPixel(0, 0, Color.white);
            meterTexture.Apply();
        }

        internal static Font CreateFont()
        {
            string[] installed = Font.GetOSInstalledFontNames();
            string[] preferred = { "PingFang SC", "Microsoft YaHei", "Noto Sans CJK SC", "Heiti SC", "SimHei", "Arial Unicode MS" };
            foreach (string candidate in preferred)
            {
                if (Array.Exists(installed, item => item == candidate))
                {
                    return Font.CreateDynamicFontFromOSFont(candidate, 20);
                }
            }
            Debug.LogError("No supported CJK system font was found. Install Noto Sans CJK SC to display Chinese UI.");
            return Font.CreateDynamicFontFromOSFont("Arial", 20);
        }

        private void InitializeStyles()
        {
            if (body != null) return;
            body = new GUIStyle(GUI.skin.label)
            {
                font = font, fontSize = 19, wordWrap = true, richText = false
            };
            body.normal.textColor = new Color(0.91f, 0.9f, 0.84f);
            title = new GUIStyle(body) { fontSize = 32, fontStyle = FontStyle.Bold };
            small = new GUIStyle(body) { fontSize = 15 };
            small.normal.textColor = new Color(0.86f, 0.88f, 0.88f);
            statValue = new GUIStyle(small)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 14
            };
            statValue.normal.textColor = Color.white;
            caption = new GUIStyle(body) { fontSize = 21, alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button)
            {
                font = font, fontSize = 18, padding = new RectOffset(14, 14, 10, 10)
            };
            warning = new GUIStyle(caption) { fontSize = 18, fontStyle = FontStyle.Bold };
            warning.normal.textColor = new Color(1f, 0.91f, 0.82f);
        }

        private void OnGUI()
        {
            InitializeStyles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            DemoGame game = DemoGame.Instance;
            if (game == null) return;

            if (!hasStarted)
            {
                Intro(width, height, game);
                return;
            }

            Panel(new Rect(24, 24, 440, 145));
            GUI.Label(new Rect(44, 37, 400, 30),
                game.State.Phase == OpeningPhase.Complete
                    ? "第二章 · 雨停之前"
                    : "第一章 · 还有一个人", body);
            GUI.Label(new Rect(44, 75, 400, 80), game.Objective, body);
            Rect statusPanel = new Rect(width - 310, 24, 286, 152);
            Panel(statusPanel);
            GUI.Label(new Rect(statusPanel.x + 18, statusPanel.y + 8, 250, 22), "状态", small);
            DrawMeter(new Rect(statusPanel.x + 18, statusPanel.y + 34, 250, 34),
                "生命", game.Player.Health, 100, new Color(0.76f, 0.30f, 0.25f),
                Mathf.CeilToInt(game.Player.Health).ToString());
            DrawMeter(new Rect(statusPanel.x + 18, statusPanel.y + 72, 250, 34),
                "体力", game.Player.Stamina, 100, new Color(0.84f, 0.68f, 0.31f),
                Mathf.CeilToInt(game.Player.Stamina).ToString());
            DrawMeter(new Rect(statusPanel.x + 18, statusPanel.y + 110, 250, 34),
                "精力", game.Player.Energy, 100, new Color(0.39f, 0.68f, 0.69f),
                Mathf.CeilToInt(game.Player.Energy) + "  ·  -" +
                    game.Player.EnergyDrainPerSecond.ToString("0.00") + "/秒");

            if (game.State.Phase == OpeningPhase.Repair)
            {
                Rect progressPanel = new Rect(width / 2 - 180, 24, 360, 76);
                Panel(progressPanel);
                DrawMeter(new Rect(progressPanel.x + 18, progressPanel.y + 12, 324, 48),
                    "老周正在修门", game.State.RepairProgress, 1, new Color(0.77f, 0.62f, 0.36f),
                    Mathf.RoundToInt(game.State.RepairProgress * 100) + "%");
            }

            if (game.IsThreatActive && !game.IsPaused && !game.IsFailed)
            {
                string threatStatus;
                string threatAction;
                if (game.IsThreatWarning)
                {
                    threatStatus = "入口处发现异常者 · " + Mathf.CeilToInt(game.ThreatApproachSeconds)
                        + " 秒后靠近";
                    threatAction = "转身确认位置；准备按 F 推开，或按住 Shift 跑开。";
                }
                else if (game.ThreatDistance <= 3f && game.CanPushThreat)
                {
                    threatStatus = "威胁就在身边 · " + game.ThreatDistance.ToString("0.0") + " 米";
                    threatAction = "按 F 推开（需 12 体力），或按住 Shift 奔跑拉开距离。";
                }
                else if (game.ThreatDistance <= 3f)
                {
                    threatStatus = "威胁就在身边 · " + game.ThreatDistance.ToString("0.0") + " 米";
                    threatAction = "体力不足以推开；按住 Shift 奔跑拉开距离！";
                }
                else
                {
                    threatStatus = "异常者正在接近 · " + game.ThreatDistance.ToString("0.0") + " 米";
                    threatAction = "靠近 3 米内按 F 推开，或按住 Shift 奔跑拉开距离。";
                }

                Rect warningPanel = new Rect(width / 2 - 350, 184, 700, 76);
                Color previousColor = GUI.color;
                GUI.color = new Color(0.85f, 0.22f, 0.12f, 1f);
                Panel(warningPanel);
                GUI.color = previousColor;
                GUI.Label(new Rect(warningPanel.x + 16, warningPanel.y + 7,
                    warningPanel.width - 32, 27), threatStatus, warning);
                GUI.Label(new Rect(warningPanel.x + 16, warningPanel.y + 38,
                    warningPanel.width - 32, 27), threatAction, small);
            }

            if (!game.IsPaused && !game.IsFailed && !game.IsComplete)
            {
                DrawMarker(game.ObjectivePosition + Vector3.up * 2.2f, width, height, scale, game);
                if (game.Nearest != null)
                {
                    Panel(new Rect(width / 2 - 260, height - 220, 520, 48));
                    GUI.Label(new Rect(width / 2 - 246, height - 210, 492, 35),
                        "[ E ]  " + game.Nearest.Prompt, caption);
                }
            }

            if (!string.IsNullOrEmpty(game.VisibleCaption))
            {
                Panel(new Rect(width / 2 - 440, height - 160, 880, 105));
                GUI.Label(new Rect(width / 2 - 420, height - 150, 840, 88), game.VisibleCaption, caption);
            }
            Rect controlsPanel = new Rect(width / 2 - 420, height - 46, 840, 34);
            Panel(controlsPanel);
            GUI.Label(new Rect(controlsPanel.x + 14, controlsPanel.y + 5, controlsPanel.width - 28, 24),
                "WASD 移动   鼠标 视角   Shift 奔跑   E 互动   F 近距离推开   Esc 暂停", small);

            if (game.IsFailed)
                EndPanel(width, height, game, false);
            else if (game.IsComplete)
                EndPanel(width, height, game, true);
            else if (game.IsPaused)
                PausePanel(width, height, game);
        }

        private void Intro(float width, float height, DemoGame game)
        {
            Panel(new Rect(width / 2 - 350, height / 2 - 230, 700, 460));
            GUI.Label(new Rect(width / 2 - 315, height / 2 - 200, 630, 55), "还有一个人", title);
            GUI.Label(new Rect(width / 2 - 315, height / 2 - 140, 630, 85),
                "死亡不是生命的结束，遗忘才是。\n\n一段尚未走完的普通日常、一条要一起走出去的后巷，和雨停之前的一夜。", body);
            GUI.Label(new Rect(width / 2 - 315, height / 2 - 25, 630, 100),
                "这是短篇 3D 灰盒，不是最终美术。\n第一章：照顾狗、认识老周，骚乱后合作撤离。\n第二章：两通电话、窄路上的碎玻璃、服务区过夜与清晨分路。\n没有强制死亡、失忆或异化，也不要求击杀异常者。", small);
            GUI.Label(new Rect(width / 2 - 315, height / 2 + 85, 630, 45),
                "WASD 移动 · 鼠标转向 · Shift 奔跑 · E 互动 · F 近距离推开", small);
            if (GUI.Button(new Rect(width / 2 - 305, height / 2 + 150, 290, 48), "开始这段旅程", button))
            {
                hasStarted = true;
                game.Begin();
            }
            if (GUI.Button(new Rect(width / 2 + 15, height / 2 + 150, 290, 48), "进入独立开放测试场", button))
                UnityEngine.SceneManagement.SceneManager.LoadScene("Sandbox");
        }

        private void PausePanel(float width, float height, DemoGame game)
        {
            Panel(new Rect(width / 2 - 260, height / 2 - 175, 520, 350));
            GUI.Label(new Rect(width / 2 - 225, height / 2 - 150, 450, 45), "稍作停留", title);
            GUI.Label(new Rect(width / 2 - 225, height / 2 - 85, 450, 55),
                "当前为暂停菜单，不是世界中的睡眠。\n游戏时间与行动已经暂停。", small);
            if (GUI.Button(new Rect(width / 2 - 215, height / 2 - 5, 430, 42), "继续", button)) game.TogglePause();
            if (GUI.Button(new Rect(width / 2 - 215, height / 2 + 50, 430, 42), "重新开始", button)) game.Restart();
            if (GUI.Button(new Rect(width / 2 - 215, height / 2 + 105, 430, 42), "退出游戏（编辑器中请停止 Play）", button)) game.Quit();
        }

        private void EndPanel(float width, float height, DemoGame game, bool success)
        {
            Panel(new Rect(width / 2 - 360, height / 2 - 225, 720, 450));
            GUI.Label(new Rect(width / 2 - 325, height / 2 - 200, 650, 50),
                success ? "分路，不是散伙" : "这次没能走出后巷", title);
            string text = success
                ? "两通电话、一夜休整，和一个清晨的约定。\n\n老周往柳河中转站去等妻女；你们先去找可用的车，再去北街接林禾的母亲。地址抄在了路线册里。\n\n第一、二章 Demo 到此结束。分开以后，他仍带着和你们的有效联系；没有人被剧情强制遗忘。"
                : "失败原因：生命值耗尽。异常者在 1.35 米内会每 1.7 秒造成 12 点伤害。\n\n它从入口处出现，头顶有红色标记。靠近 3 米内且有至少 12 点体力时按 F 推开；也可以按住 Shift 奔跑拉开距离。目标是撑过修门时间，不需要击败对方。";
            GUI.Label(new Rect(width / 2 - 325, height / 2 - 130, 650, 160), text, body);
            if (success)
                GUI.Label(new Rect(width / 2 - 325, height / 2 + 40, 650, 45), SmallKindness(game), small);
            if (GUI.Button(new Rect(width / 2 - 325, height / 2 + 105, 310, 45), "重新走一遍", button)) game.Restart();
            if (GUI.Button(new Rect(width / 2 + 15, height / 2 + 105, 310, 45), "查看制作说明", button)) showNotes = !showNotes;
            if (showNotes)
                GUI.Label(new Rect(width / 2 - 325, height / 2 + 164, 650, 60),
                    "此版验证开场合作撤离与第二章的电话、照料、过夜与分路。睡眠会恢复精力并少量衰减联系，但不断开任何关系；遗忘异化与引魂尚未接入。", small);
        }

        private static string SmallKindness(DemoGame game)
        {
            bool water = game.State.GaveWater;
            bool shared = game.Journey.SharedWater;
            if (water && shared) return "这一路上，你给狗倒过水，也把自己的水递给了老周。";
            if (shared) return "服务区里，你把自己的水递给了老周。";
            if (water) return "这段路上，你还停下来给狗倒了水。";
            return "有些小事没有发生，故事也不因此惩罚你。";
        }

        private void DrawMarker(Vector3 world, float width, float height, float scale, DemoGame game)
        {
            Vector3 screen = game.View.WorldToScreenPoint(world);
            if (screen.z <= 0)
            {
                GUI.Label(new Rect(width / 2 - 120, 110, 240, 35), "目标在身后，转身看看", caption);
                return;
            }
            float x = Mathf.Clamp(screen.x / scale, 80, width - 80);
            float y = Mathf.Clamp((Screen.height - screen.y) / scale, 185, height - 245);
            GUI.Label(new Rect(x - 70, y - 15, 140, 35),
                Mathf.RoundToInt(Vector3.Distance(game.Player.transform.position, world)) + " 米", caption);
        }

        private void Panel(Rect rect)
        {
            GUI.DrawTexture(rect, panel);
        }

        private void DrawMeter(Rect rect, string label, float value, float maxValue,
            Color fillColor, string valueLabel)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width / 2, 20), label, small);
            GUI.Label(new Rect(rect.x + rect.width / 2, rect.y, rect.width / 2, 20), valueLabel, statValue);
            Rect track = new Rect(rect.x, rect.y + 23, rect.width, 7);
            Color previousColor = GUI.color;
            GUI.color = new Color(0.72f, 0.75f, 0.75f, 1f);
            GUI.DrawTexture(track, meterTexture);
            GUI.color = previousColor;
            float normalized = maxValue > 0 ? Mathf.Clamp01(value / maxValue) : 0;
            if (normalized <= 0) return;
            Rect fill = new Rect(track.x, track.y, track.width * normalized, track.height);
            GUI.color = fillColor;
            GUI.DrawTexture(fill, meterTexture);
            GUI.color = previousColor;
        }

        private void OnDestroy()
        {
            if (panel != null) Destroy(panel);
            if (meterTexture != null) Destroy(meterTexture);
            if (font != null) Destroy(font);
        }
    }
}
