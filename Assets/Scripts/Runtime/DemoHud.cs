using System;
using UnityEngine;

namespace DontForgetMe
{
    public sealed class DemoHud : MonoBehaviour
    {
        private static readonly Color Paper = new Color(0.93f, 0.91f, 0.85f);
        private static readonly Color Muted = new Color(0.72f, 0.74f, 0.73f);
        private static readonly Color Amber = new Color(0.89f, 0.72f, 0.42f);

        private Font font;
        private Font serif;
        private GUIStyle body, title, titleMid, titleEnd, tagline, small, smallCentre, smallRight, label, button, caption, prompt, chapter, keycap, panel, outline;
        // Interface scale relative to a 1280 x 800 layout. Fonts are rasterised at the scaled
        // size instead of stretching the whole interface, so text stays sharp on dense displays.
        private float k = 1f;
        private float styledFor;
        private Texture2D white, fadeDown, fadeUp, fadeRight, round, ring, glow, diamond, buttonIdle, buttonHover;
        private bool showNotes;
        private float loadedAt;
        private OpeningPhase lastPhase;
        private string chapterText = "";
        private float chapterAt = -100f;

        private void Awake()
        {
            loadedAt = Time.realtimeSinceStartup;
            font = Art.Get<Font>("font/sans");
            serif = Art.Get<Font>("font/serif");
            white = Fill(1, 1, (x, y) => Color.white);
            fadeDown = Fill(1, 64, (x, y) => new Color(1, 1, 1, Mathf.Pow(y / 63f, 1.6f)));
            fadeUp = Fill(1, 64, (x, y) => new Color(1, 1, 1, Mathf.Pow(1f - y / 63f, 1.6f)));
            fadeRight = Fill(64, 1, (x, y) => new Color(1, 1, 1, Mathf.Pow(1f - x / 63f, 1.4f)));
            round = Fill(32, 32, (x, y) => new Color(1, 1, 1, Rounded(x, y, 32, 7f, 0f)));
            ring = Fill(32, 32, (x, y) => new Color(1, 1, 1, Rounded(x, y, 32, 7f, 0f) - Rounded(x, y, 32, 7f, 1.4f) * 0.999f));
            glow = Fill(64, 64, (x, y) =>
            {
                float d = new Vector2(x / 63f - 0.5f, y / 63f - 0.5f).magnitude * 2f;
                return new Color(1, 1, 1, Mathf.Clamp01(Mathf.Pow(d, 2.2f)));
            });
            diamond = Fill(32, 32, (x, y) =>
            {
                float d = Mathf.Abs(x - 15.5f) + Mathf.Abs(y - 15.5f);
                return new Color(1, 1, 1, Mathf.Clamp01(13f - d) * (d > 7.5f ? 1f : 0.25f));
            });
        }

        private static Texture2D Fill(int width, int height, Func<int, int, Color> shade)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, shade(x, y));
            texture.Apply();
            return texture;
        }

        // Coverage of a rounded square, optionally shrunk by "inset" pixels.
        private static float Rounded(int x, int y, int size, float radius, float inset)
        {
            float half = size / 2f - inset;
            float r = Mathf.Max(0.5f, radius - inset);
            Vector2 p = new Vector2(Mathf.Abs(x + 0.5f - size / 2f), Mathf.Abs(y + 0.5f - size / 2f));
            Vector2 q = p - new Vector2(half - r, half - r);
            float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
            return Mathf.Clamp01(0.5f - distance);
        }

        private int Points(float size)
        {
            return Mathf.Max(1, Mathf.RoundToInt(size * k));
        }

        private Rect Scaled(Rect rect)
        {
            return new Rect(rect.x * k, rect.y * k, rect.width * k, rect.height * k);
        }

        private void InitializeStyles()
        {
            if (body != null && Mathf.Abs(styledFor - k) < 0.005f) return;
            styledFor = k;
            body = new GUIStyle(GUI.skin.label) { font = font, fontSize = Points(18), wordWrap = true, richText = true };
            body.normal.textColor = Paper;
            title = new GUIStyle(body) { font = serif, fontSize = Points(64), wordWrap = false };
            titleMid = new GUIStyle(title) { fontSize = Points(44), alignment = TextAnchor.MiddleCenter };
            titleEnd = new GUIStyle(title) { fontSize = Points(48) };
            tagline = new GUIStyle(body) { fontSize = Points(22) };
            small = new GUIStyle(body) { fontSize = Points(14) };
            small.normal.textColor = Muted;
            smallCentre = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            smallRight = new GUIStyle(small) { alignment = TextAnchor.MiddleRight };
            panel = new GUIStyle { border = new RectOffset(10, 10, 10, 10) };
            panel.normal.background = round;
            outline = new GUIStyle { border = new RectOffset(10, 10, 10, 10) };
            outline.normal.background = ring;
            label = new GUIStyle(body) { fontSize = Points(12), wordWrap = false };
            label.normal.textColor = Amber;
            caption = new GUIStyle(body) { fontSize = Points(21), alignment = TextAnchor.LowerCenter };
            prompt = new GUIStyle(body) { fontSize = Points(19), alignment = TextAnchor.MiddleLeft, wordWrap = false };
            chapter = new GUIStyle(body) { font = serif, fontSize = Points(34), alignment = TextAnchor.MiddleCenter, wordWrap = false };
            keycap = new GUIStyle(body) { fontSize = Points(15), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            if (buttonIdle == null)
            {
                buttonIdle = Tinted(round, new Color(1f, 1f, 1f, 0.08f));
                buttonHover = Tinted(round, new Color(0.89f, 0.72f, 0.42f, 0.9f));
            }
            button = new GUIStyle(GUI.skin.button)
            {
                font = font, fontSize = Points(17), alignment = TextAnchor.MiddleCenter, border = new RectOffset(10, 10, 10, 10),
                padding = new RectOffset(Points(18), Points(18), Points(8), Points(8))
            };
            button.normal.background = buttonIdle;
            button.hover.background = buttonHover;
            button.active.background = buttonHover;
            button.focused.background = buttonIdle;
            button.normal.textColor = Paper;
            button.hover.textColor = new Color(0.08f, 0.07f, 0.06f);
            button.active.textColor = new Color(0.08f, 0.07f, 0.06f);
            button.focused.textColor = Paper;
        }

        private static Texture2D Tinted(Texture2D source, Color tint)
        {
            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color[] pixels = source.GetPixels();
            for (int i = 0; i < pixels.Length; i++) pixels[i] *= tint;
            copy.SetPixels(pixels);
            copy.Apply();
            return copy;
        }

        private void Draw(Texture2D texture, Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(Scaled(rect), texture);
            GUI.color = previous;
        }

        // Nine-sliced, so rounded corners keep their shape at any size.
        private void Sliced(GUIStyle style, Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(Scaled(rect), GUIContent.none, style);
            GUI.color = previous;
        }

        private bool Button(Rect rect, string text)
        {
            return GUI.Button(Scaled(rect), text, button);
        }

        private void Text(Rect rect, string text, GUIStyle style, float alpha = 1f, bool shadow = true)
        {
            rect = Scaled(rect);
            float offset = Mathf.Max(1f, 1.5f * k);
            Color previous = GUI.color;
            if (shadow)
            {
                Color ink = style.normal.textColor;
                style.normal.textColor = Color.black;
                GUI.color = new Color(1, 1, 1, alpha * 0.75f);
                GUI.Label(new Rect(rect.x + offset, rect.y + offset, rect.width, rect.height), StripColour(text), style);
                style.normal.textColor = ink;
            }
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        private static string StripColour(string text)
        {
            if (text.IndexOf("<color", StringComparison.Ordinal) < 0) return text;
            return System.Text.RegularExpressions.Regex.Replace(text, "</?color[^>]*>", "");
        }

        private void OnGUI()
        {
            float scale = k = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            InitializeStyles();
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            DemoGame game = DemoGame.Instance;
            if (game == null) return;

            if (!game.HasStarted) Intro(width, height, game);
            else
            {
                bool ended = game.EndedAt >= 0 && Time.time > game.EndedAt + (game.IsFailed ? 0.8f : DemoGame.EndPanelDelay);
                Play(width, height, scale, game, ended);
                if (ended) EndPanel(width, height, game, !game.IsFailed);
                else if (game.IsPaused) PausePanel(width, height, game);
            }

            float opening = 1f - Mathf.Clamp01((Time.realtimeSinceStartup - loadedAt - 0.4f) / 1.6f);
            if (opening > 0) Draw(white, new Rect(0, 0, width, height), new Color(0, 0, 0, opening));
        }

        private void Play(float width, float height, float scale, DemoGame game, bool ended)
        {
            if (game.State.Phase != lastPhase || chapterAt < -50f)
            {
                if (chapterAt < -50f) { chapterText = "第一段 · 还没走完的普通一天"; chapterAt = Time.time; }
                else if (game.State.Phase == OpeningPhase.Debris) { chapterText = "第二段 · 穿过后巷"; chapterAt = Time.time; }
                lastPhase = game.State.Phase;
            }

            // Hurt feedback: a red edge that flares on impact and lingers when health is low.
            float hurt = Mathf.Clamp01(1f - (Time.time - game.Player.LastHitTime) / 0.9f) * 0.55f
                + Mathf.Clamp01((45f - game.Player.Health) / 45f) * 0.3f;
            if (hurt > 0.01f) Draw(glow, new Rect(-width * 0.1f, -height * 0.1f, width * 1.2f, height * 1.2f), new Color(0.55f, 0.04f, 0.03f, hurt));
            if (ended) return;

            Draw(fadeDown, new Rect(0, 0, width, 150), new Color(0, 0, 0, 0.5f));
            Draw(fadeUp, new Rect(0, height - 230, width, 230), new Color(0, 0, 0, 0.62f));

            Draw(white, new Rect(40, 36, 2, 58), new Color(Amber.r, Amber.g, Amber.b, 0.9f));
            Text(new Rect(54, 32, 300, 18), "当 前 目 标", label);
            Text(new Rect(54, 51, 520, 60), game.Objective, body);

            float age = Time.time - chapterAt;
            if (age < 6f)
            {
                float alpha = Mathf.Clamp01(age / 1.2f) * Mathf.Clamp01((6f - age) / 1.5f);
                Text(new Rect(0, height * 0.24f, width, 50), chapterText, chapter, alpha);
                Draw(white, new Rect(width / 2 - 60, height * 0.24f + 56, 120, 1), new Color(Paper.r, Paper.g, Paper.b, alpha * 0.7f));
            }

            Bar(40, height - 92, "生命", game.Player.Health / 100f, new Color(0.82f, 0.36f, 0.30f));
            Bar(40, height - 70, "体力", game.Player.Stamina / 100f, new Color(0.88f, 0.84f, 0.72f));
            Bar(40, height - 48, "精力", game.Player.Energy / 100f, new Color(0.55f, 0.66f, 0.74f));

            if (game.State.Phase == OpeningPhase.Repair)
            {
                float progress = game.State.RepairProgress;
                bool stalled = game.RepairStalled;
                // While someone has hold of him the bar stops and says so.
                float pulse = stalled ? 0.6f + 0.4f * Mathf.Sin(Time.time * 9f) : 1f;
                Text(new Rect(width / 2 - 200, 34, 400, 22), stalled ? "老周被缠住了 · 门停了" : "老周正在砸开锈死的链子、校正轨道", smallCentre, pulse);
                Draw(white, new Rect(width / 2 - 150, 62, 300, 3), new Color(1, 1, 1, 0.18f));
                Draw(white, new Rect(width / 2 - 150, 62, 300 * progress, 3), stalled ? new Color(0.82f, 0.36f, 0.30f, pulse) : Amber);
            }

            bool live = !game.IsPaused && !game.IsFailed && game.State.Phase != OpeningPhase.Complete;
            if (live)
            {
                // During the fight the player is meant to be facing the alley, not a marker behind them.
                if (game.State.Phase != OpeningPhase.Repair)
                    DrawMarker(game.ObjectivePosition + Vector3.up * 2.25f, width, height, scale, game);
                if (game.Nearest != null)
                {
                    Vector2 size = prompt.CalcSize(new GUIContent(game.Nearest.Prompt)) / k;
                    float total = size.x + 46;
                    float x = width / 2 - total / 2, y = height - 232;
                    Sliced(panel, new Rect(x - 14, y - 6, total + 28, 42), new Color(0, 0, 0, 0.45f));
                    Sliced(outline, new Rect(x, y, 30, 30), Amber);
                    Text(new Rect(x, y, 30, 30), "E", keycap, 1f, false);
                    Text(new Rect(x + 42, y - 1, size.x + 10, 32), game.Nearest.Prompt, prompt);
                }
            }

            string spoken = game.VisibleCaption;
            if (!string.IsNullOrEmpty(spoken))
                Text(new Rect(width / 2 - 470, height - 190, 940, 128), Speakers(spoken), caption);

            float hint = live ? Mathf.Clamp01((28f - (Time.time - game.StartedAt)) / 3f) : 1f;
            if (hint > 0)
                Text(new Rect(width - 660, height - 36, 620, 22),
                    "WASD 移动    鼠标 视角    Shift 奔跑    E 互动    F 推开    Esc 暂停", smallRight, hint * 0.85f);
        }

        // Colours the speaker's name at the start of each spoken line.
        private static string Speakers(string text)
        {
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf('：');
                if (colon <= 0 || colon > 4) continue;
                string who = lines[i].Substring(0, colon);
                string colour = who == "她" ? "#EDB9A2" : who == "老周" ? "#E6C27A" : "#B9C6CB";
                lines[i] = "<color=" + colour + ">" + who + "</color>　" + lines[i].Substring(colon + 1);
            }
            return string.Join("\n", lines);
        }

        private void Bar(float x, float y, string name, float value, Color color)
        {
            Text(new Rect(x, y - 7, 40, 18), name, small);
            Draw(white, new Rect(x + 40, y, 150, 3), new Color(1, 1, 1, 0.16f));
            Draw(white, new Rect(x + 40, y, 150 * Mathf.Clamp01(value), 3), color);
        }

        private void Intro(float width, float height, DemoGame game)
        {
            Draw(white, new Rect(0, 0, width, height), new Color(0.02f, 0.025f, 0.03f, 0.42f));
            Draw(fadeRight, new Rect(0, 0, width * 0.8f, height), new Color(0, 0, 0, 0.72f));
            float x = 110, y = height / 2 - 210;
            Text(new Rect(x, y, 300, 18), "开 场 试 玩", label);
            Text(new Rect(x - 3, y + 22, 800, 90), "还有一个人", title);
            Draw(white, new Rect(x, y + 122, 72, 2), Amber);
            Text(new Rect(x, y + 142, 620, 30), "死亡不是生命的结束，遗忘才是。", tagline);
            Text(new Rect(x, y + 194, 560, 110),
                "一段尚未走完的普通日常，和一条需要一起走出去的后巷。\n先照顾狗，认识修理铺的人；骚乱发生后找到同伴，再合作撤离。\n没有强制死亡、失忆或异化，也不要求击杀异常者。", small);
            Text(new Rect(x, y + 300, 620, 24), "WASD 移动    鼠标 视角    Shift 奔跑    E 互动    F 推开    Esc 暂停", small);
            if (Button(new Rect(x, y + 352, 250, 48), "开始这段旅程")) game.Begin();
        }

        private void PausePanel(float width, float height, DemoGame game)
        {
            Draw(white, new Rect(0, 0, width, height), new Color(0.02f, 0.025f, 0.03f, 0.55f));
            float x = width / 2 - 190, y = height / 2 - 170;
            Text(new Rect(x, y, 380, 60), "稍作停留", titleMid);
            Draw(white, new Rect(width / 2 - 30, y + 70, 60, 2), Amber);
            Text(new Rect(x, y + 86, 380, 46), "当前为暂停菜单，不是世界中的睡眠。\n游戏时间与行动已经暂停。", smallCentre);
            if (Button(new Rect(x, y + 154, 380, 44), "继续")) game.TogglePause();
            if (Button(new Rect(x, y + 208, 380, 44), "重新开始")) game.Restart();
            if (Button(new Rect(x, y + 262, 380, 44), "退出游戏（编辑器中请停止 Play）")) game.Quit();
        }

        private void EndPanel(float width, float height, DemoGame game, bool success)
        {
            float alpha = Mathf.Clamp01((Time.time - game.EndedAt - (success ? DemoGame.EndPanelDelay : 0.8f)) / 1.2f);
            Draw(white, new Rect(0, 0, width, height), new Color(0.02f, 0.025f, 0.03f, 0.62f * alpha));
            float x = width / 2 - 330, y = height / 2 - 220;
            Text(new Rect(x, y, 660, 70), success ? "先别走散。" : "这次没能走出后巷", titleEnd, alpha);
            Draw(white, new Rect(x, y + 82, 72, 2), new Color(Amber.r, Amber.g, Amber.b, alpha));
            string text = success
                ? "她、狗、老周，还有街坊，都过来了。\n\n老周准备往服务站方向走。他没有承诺那里安全，只说至少有能遮雨的地方。你们决定先与他同行。\n\n开场 Demo 到此结束。没有人被剧情强制遗忘。"
                : "别站着让他们围住：F 只推得开面前够得着的人，推完就换位置。\n街坊和她也在挡。先把缠住老周的人弄开，门才开得动；不需要打倒谁。";
            Text(new Rect(x, y + 104, 660, 170), text, body, alpha);
            if (success)
                Text(new Rect(x, y + 268, 660, 40),
                    game.State.GaveWater ? "这段路上，你还停下来给狗倒了水。" : "有些小事没有发生，故事也不因此惩罚你。", small, alpha);
            if (alpha < 0.6f) return;
            if (Button(new Rect(x, y + 322, 250, 46), "重新走一遍")) game.Restart();
            if (Button(new Rect(x + 266, y + 322, 250, 46), "查看制作说明")) showNotes = !showNotes;
            if (showNotes)
                Text(new Rect(x, y + 384, 660, 60),
                    "此版验证开场操作、合作撤离与人物联系。睡眠、遗忘异化和引魂尚未接入剧情；记忆核心规则另有自动化测试。", small);
        }

        private void DrawMarker(Vector3 world, float width, float height, float scale, DemoGame game)
        {
            Vector3 screen = game.View.WorldToScreenPoint(world);
            if (screen.z <= 0)
            {
                Text(new Rect(width / 2 - 150, 84, 300, 24), "目标在身后，转身看看", smallCentre);
                return;
            }
            float x = Mathf.Clamp(screen.x / scale, 80, width - 80);
            float y = Mathf.Clamp((Screen.height - screen.y) / scale, 150, height - 260);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
            Draw(diamond, new Rect(x - 9, y - 9, 18, 18), new Color(Amber.r, Amber.g, Amber.b, pulse));
            Text(new Rect(x - 60, y + 10, 120, 20),
                Mathf.RoundToInt(Vector3.Distance(game.Player.transform.position, world)) + " 米", smallCentre);
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in new[] { white, fadeDown, fadeUp, fadeRight, round, ring, glow, diamond, buttonIdle, buttonHover })
                if (texture != null) Destroy(texture);
        }
    }
}
