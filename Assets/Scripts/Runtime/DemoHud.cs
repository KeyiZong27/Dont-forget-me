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
        private GUIStyle button;
        private GUIStyle caption;
        private Texture2D panel;
        private bool hasStarted;
        private bool showNotes;

        private void Awake()
        {
            string[] installed = Font.GetOSInstalledFontNames();
            string[] preferred = { "PingFang SC", "Microsoft YaHei", "Noto Sans CJK SC", "Heiti SC", "SimHei", "Arial Unicode MS" };
            foreach (string candidate in preferred)
            {
                if (Array.Exists(installed, item => item == candidate))
                {
                    font = Font.CreateDynamicFontFromOSFont(candidate, 20);
                    break;
                }
            }
            if (font == null)
            {
                Debug.LogError("No supported CJK system font was found. Install Noto Sans CJK SC to display Chinese UI.");
                font = Font.CreateDynamicFontFromOSFont("Arial", 20);
            }
            panel = new Texture2D(1, 1);
            panel.SetPixel(0, 0, new Color(0.07f, 0.085f, 0.09f, 0.92f));
            panel.Apply();
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
            small.normal.textColor = new Color(0.70f, 0.74f, 0.73f);
            caption = new GUIStyle(body) { fontSize = 21, alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button)
            {
                font = font, fontSize = 18, padding = new RectOffset(14, 14, 10, 10)
            };
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
            GUI.Label(new Rect(44, 37, 400, 30), "还有一个人 · 开场灰盒", body);
            GUI.Label(new Rect(44, 75, 400, 80), game.Objective, body);
            Panel(new Rect(width - 230, 24, 206, 132));
            GUI.Label(new Rect(width - 212, 36, 180, 25), "生命  " + Mathf.CeilToInt(game.Player.Health), small);
            GUI.Label(new Rect(width - 212, 70, 180, 25), "体力  " + Mathf.CeilToInt(game.Player.Stamina), small);
            GUI.Label(new Rect(width - 212, 104, 180, 25), "精力  " + Mathf.CeilToInt(game.Player.Energy), small);

            if (game.State.Phase == OpeningPhase.Repair)
            {
                Panel(new Rect(width / 2 - 180, 24, 360, 65));
                GUI.Label(new Rect(width / 2 - 164, 40, 328, 35),
                    "老周正在修门  " + Mathf.RoundToInt(game.State.RepairProgress * 100) + "%", body);
            }

            if (!game.IsPaused && !game.IsFailed && game.State.Phase != OpeningPhase.Complete)
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
            GUI.Label(new Rect(25, height - 33, 950, 25),
                "WASD 移动   鼠标视角   Shift 奔跑   E 互动   F 推开   Esc 暂停 / 释放鼠标", small);

            if (game.IsFailed)
                EndPanel(width, height, game, false);
            else if (game.State.Phase == OpeningPhase.Complete)
                EndPanel(width, height, game, true);
            else if (game.IsPaused)
                PausePanel(width, height, game);
        }

        private void Intro(float width, float height, DemoGame game)
        {
            Panel(new Rect(width / 2 - 350, height / 2 - 230, 700, 460));
            GUI.Label(new Rect(width / 2 - 315, height / 2 - 200, 630, 55), "还有一个人", title);
            GUI.Label(new Rect(width / 2 - 315, height / 2 - 140, 630, 85),
                "死亡不是生命的结束，遗忘才是。\n\n一段尚未走完的普通日常，和一条需要一起走出去的后巷。", body);
            GUI.Label(new Rect(width / 2 - 315, height / 2 - 25, 630, 100),
                "这是短篇 3D 灰盒，不是最终美术。\n先照顾狗，认识修理铺的人；骚乱发生后找到同伴，再合作撤离。\n没有强制死亡、失忆或异化，也不要求击杀异常者。", small);
            GUI.Label(new Rect(width / 2 - 315, height / 2 + 85, 630, 45),
                "WASD 移动 · 鼠标转向 · E 互动 · F 推开 · Esc 暂停", small);
            if (GUI.Button(new Rect(width / 2 - 145, height / 2 + 150, 290, 48), "开始这段旅程", button))
            {
                hasStarted = true;
                game.Begin();
            }
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
                success ? "先别走散。" : "这次没能走出后巷", title);
            string text = success
                ? "她、狗、老周，还有街坊，都过来了。\n\n老周准备往服务站方向走。他没有承诺那里安全，只说至少有能遮雨的地方。你们决定先与他同行。\n\n开场 Demo 到此结束。没有人被剧情强制遗忘。"
                : "可以推开对方、绕行，也可以提前站到门边等待通过。\n目标是争取修门时间，不需要正面打倒对方。";
            GUI.Label(new Rect(width / 2 - 325, height / 2 - 130, 650, 160), text, body);
            if (success)
                GUI.Label(new Rect(width / 2 - 325, height / 2 + 40, 650, 45),
                    game.State.GaveWater ? "这段路上，你还停下来给狗倒了水。" : "有些小事没有发生，故事也不因此惩罚你。", small);
            if (GUI.Button(new Rect(width / 2 - 325, height / 2 + 105, 310, 45), "重新走一遍", button)) game.Restart();
            if (GUI.Button(new Rect(width / 2 + 15, height / 2 + 105, 310, 45), "查看制作说明", button)) showNotes = !showNotes;
            if (showNotes)
                GUI.Label(new Rect(width / 2 - 325, height / 2 + 164, 650, 60),
                    "此版验证开场操作、合作撤离与人物联系。睡眠、遗忘异化和引魂尚未接入剧情；记忆核心规则另有自动化测试。", small);
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

        private void OnDestroy()
        {
            if (panel != null) Destroy(panel);
            if (font != null) Destroy(font);
        }
    }
}
