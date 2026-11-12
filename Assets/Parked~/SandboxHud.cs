using UnityEngine;

namespace DontForgetMe
{
    public sealed class SandboxHud : MonoBehaviour
    {
        private Font font;
        private Texture2D pixel;
        private GUIStyle body;
        private GUIStyle small;
        private GUIStyle title;
        private GUIStyle button;

        private void Awake()
        {
            font = DemoHud.CreateFont();
            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }

        private void OnGUI()
        {
            SandboxGame game = SandboxGame.Instance;
            if (game == null) return;
            if (body == null)
            {
                body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true };
                body.normal.textColor = new Color(0.93f, 0.93f, 0.88f);
                small = new GUIStyle(body) { fontSize = 15 };
                title = new GUIStyle(body) { fontSize = 25, fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17 };
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            Panel(new Rect(20, 20, 430, 185));
            GUI.Label(new Rect(36, 30, 400, 36), "开放测试场 · 独立于剧情", title);
            Vector3 p = game.Player.transform.position;
            GUI.Label(new Rect(36, 72, 400, 120),
                "300 × 300 米 · " + (game.IsNight ? "夜间" : "白天") + " · 敌人" + (game.EnemiesEnabled ? "开启" : "关闭")
                + "\n坐标 X " + p.x.ToString("0") + " / Z " + p.z.ToString("0")
                + " · 朝向 " + game.Player.Heading.ToString("0") + "°"
                + "\n生命 " + game.Player.Health.ToString("0") + "   体力 " + game.Player.Stamina.ToString("0")
                + "   精力 " + game.Player.Energy.ToString("0")
                + "\n精力消耗 " + game.Player.EnergyDrainPerSecond.ToString("0.00") + "/秒"
                + "\n手持：" + (game.Equipment.Weapon.HasValue ? SandboxItems.Name(game.Equipment.Weapon.Value) : "无"), small);

            DrawMap(game, new Rect(width - 230, 20, 210, 210));
            Panel(new Rect(20, 215, 430, 92));
            GUI.Label(new Rect(36, 222, 400, 80),
                "记忆观察员 (5,0) · " + game.Observer.Stage
                + "\n你 → 他：" + game.Observer.State.PlayerMemory.ToString("0.000")
                + " · 转变 " + (game.Observer.State.Progress * 100).ToString("0") + "%"
                + "\nM 调节记忆 / 开始缓慢遗忘 / 重置", small);
            Panel(new Rect(20, height - 108, width - 40, 88));
            GUI.Label(new Rect(36, height - 99, width - 72, 76),
                "WASD 移动 / 鼠标视角 / Shift 冲刺 / E 互动 / 左键武器攻击 / F 推开\n"
                + "G 丢武器 / B 丢背包 / I 背包 / M 实时遗忘观察 / Esc 暂停与测试控制\n"
                + "营地 (0,0) · 训练区 (26,10) · 废墟 (57,57) · 西侧树林 · 南侧岩石与台阶", small);
            if (!game.IsPaused && !game.IsFailed)
            {
                if (game.Nearest != null)
                    GUI.Label(new Rect(width / 2 - 330, height - 170, 660, 40), "[E] " + game.Nearest.Prompt, body);
                DrawWorldLabels(game, width, height, scale);
            }
            if (game.VisibleCaption.Length > 0)
            {
                Panel(new Rect(width / 2 - 360, height - 248, 720, 65));
                GUI.Label(new Rect(width / 2 - 345, height - 240, 690, 54), game.VisibleCaption, body);
            }
            if (game.IsFailed) FailurePanel(game, width, height);
            else if (game.InventoryOpen) Inventory(game, width, height);
            else if (game.IsPaused) Pause(game, width, height);
            else if (game.ObservationOpen) Observation(game, width);
        }

        private void Observation(SandboxGame game, float width)
        {
            SandboxObserver observer = game.Observer;
            SandboxForgettingState state = observer.State;
            float x = width - 480;
            const float y = 245;
            Panel(new Rect(x, y, 460, 430));
            GUI.Label(new Rect(x + 16, y + 10, 428, 32), "遗忘实验 · 世界继续运行", title);
            GUI.Label(new Rect(x + 16, y + 48, 428, 28), observer.Stage, body);
            GUI.Label(new Rect(x + 16, y + 82, 428, 24),
                "你对他的记忆：" + state.PlayerMemory.ToString("0.000"), small);
            float memory = GUI.HorizontalSlider(new Rect(x + 20, y + 112, 416, 20), state.PlayerMemory, 0, 1);
            if (!Mathf.Approximately(memory, state.PlayerMemory)) observer.SetPlayerMemory(memory);
            if (GUI.Button(new Rect(x + 16, y + 140, 132, 32), "归零", button)) observer.SetPlayerMemory(0);
            if (GUI.Button(new Rect(x + 160, y + 140, 132, 32), "濒忘 0.12", button)) observer.SetPlayerMemory(0.12f);
            if (GUI.Button(new Rect(x + 304, y + 140, 140, 32), "记得 1.0", button)) observer.SetPlayerMemory(1);
            bool witness = GUI.Toggle(new Rect(x + 16, y + 184, 428, 26),
                state.WitnessMemory > 0, "另一观察者记得他（保护，不转变）", body);
            if (witness != (state.WitnessMemory > 0)) observer.SetWitnessMemory(witness ? 1 : 0);
            state.AutoDecay = GUI.Toggle(new Rect(x + 16, y + 220, 428, 26),
                state.AutoDecay, "缓慢遗忘：每秒 -0.01（仅你 → 他）", body);
            GUI.Label(new Rect(x + 16, y + 260, 428, 64),
                "记忆归零且无人保护后，12 秒逐渐变色、扭曲姿态，再成为可攻击目标。"
                + (game.EnemiesEnabled ? "敌人攻击已开启。" : "敌人攻击关闭：可安全观察。"), small);
            if (GUI.Button(new Rect(x + 16, y + 330, 208, 36), "重置实验（恢复友方）", button))
                observer.ResetObservation();
            if (GUI.Button(new Rect(x + 236, y + 330, 208, 36), "敌人攻击：" + (game.EnemiesEnabled ? "开" : "关"), button))
                game.ToggleEnemies();
            if (GUI.Button(new Rect(x + 16, y + 382, 428, 32), "关闭面板（M / Esc）", button))
                game.ToggleObservation();
        }

        private void DrawWorldLabels(SandboxGame game, float width, float height, float scale)
        {
            foreach (SandboxTarget target in game.Targets)
            {
                float distance = Vector3.Distance(target.transform.position, game.Player.transform.position);
                if (distance > 35) continue;
                Vector3 world = target.transform.position + Vector3.up * 2.3f;
                Vector3 point = game.View.WorldToScreenPoint(world);
                if (point.z <= 0) continue;
                if (Physics.Linecast(game.View.transform.position, world, ~(1 << 8), QueryTriggerInteraction.Ignore)) continue;
                float x = point.x / scale;
                float y = (Screen.height - point.y) / scale;
                if (x < 0 || x > width || y < 0 || y > height - 250) continue;
                Color previous = GUI.color;
                GUI.color = target.IsEnemy ? new Color(1, 0.55f, 0.4f) : Color.white;
                GUI.Label(new Rect(x - 70, y - 10, 160, 50), target.Label + "\n"
                    + (target.Health.IsDown ? "已击倒" : target.Health.Value.ToString("0") + " / 100"), small);
                GUI.color = previous;
            }
        }

        private void DrawMap(SandboxGame game, Rect rect)
        {
            Panel(rect);
            GUI.Label(new Rect(rect.x + 8, rect.y + 4, 190, 24), "区域地图 · 上北 / 右东", small);
            Rect map = new Rect(rect.x + 10, rect.y + 34, rect.width - 20, rect.height - 44);
            Fill(new Rect(map.center.x - 1, map.y, 2, map.height), new Color(0.5f, 0.48f, 0.38f));
            Fill(new Rect(map.x, map.center.y - 1, map.width, 2), new Color(0.5f, 0.48f, 0.38f));
            MapDot(map, new Vector3(-80, 0, 55), "林", new Color(0.4f, 0.7f, 0.35f));
            MapDot(map, new Vector3(57, 0, 57), "墟", Color.gray);
            MapDot(map, new Vector3(26, 0, 10), "战", new Color(1, 0.5f, 0.3f));
            MapDot(map, Vector3.zero, "营", new Color(0.9f, 0.8f, 0.4f));
            Vector3 p = game.Player.transform.position;
            float x = map.x + (p.x + 150) / 300 * map.width;
            float y = map.y + (150 - p.z) / 300 * map.height;
            Fill(new Rect(x - 3, y - 3, 6, 6), Color.cyan);
        }

        private void MapDot(Rect map, Vector3 position, string label, Color color)
        {
            float x = map.x + (position.x + 150) / 300 * map.width;
            float y = map.y + (150 - position.z) / 300 * map.height;
            Fill(new Rect(x - 3, y - 3, 6, 6), color);
            GUI.Label(new Rect(x + 4, y - 10, 26, 25), label, small);
        }

        private void Inventory(SandboxGame game, float width, float height)
        {
            float x = width / 2 - 390;
            float y = height / 2 - 290;
            Panel(new Rect(x, y, 780, 580));
            GUI.Label(new Rect(x + 22, y + 15, 720, 38), "背包与记忆 · 世界已暂停", title);
            SandboxBackpack bag = game.Equipment.Backpack;
            GUI.Label(new Rect(x + 22, y + 60, 720, 30), bag == null ? "未穿戴背包；先在营地拾取。"
                : "背包容量 " + bag.Contents.Count + " / " + bag.Capacity + "（每件物品一格；内容随背包保留）", body);
            if (GUI.Button(new Rect(x + 22, y + 98, 225, 36), "手中武器收入背包", button)) game.StoreWeapon();
            if (GUI.Button(new Rect(x + 264, y + 98, 225, 36), "丢弃手中武器", button)) game.DropWeapon();
            if (GUI.Button(new Rect(x + 506, y + 98, 250, 36), "丢弃整个背包", button)) { game.DropBackpack(); return; }
            if (bag != null)
            {
                for (int i = 0; i < bag.Contents.Count; i++)
                {
                    float row = y + 150 + i * 38;
                    GUI.Label(new Rect(x + 26, row, 350, 30), (i + 1) + ". " + SandboxItems.Name(bag.Contents[i]), body);
                    if (GUI.Button(new Rect(x + 390, row, 170, 32),
                        SandboxItems.IsWeapon(bag.Contents[i]) ? "拿到手中" : "使用补给", button))
                    { game.UseSlot(i); return; }
                    if (GUI.Button(new Rect(x + 575, row, 180, 32), "放到地上", button))
                    { game.DropSlot(i); return; }
                }
            }
            GUI.Label(new Rect(x + 22, y + 390, 735, 28), "定向记忆（0 表示该方向无有效联系；不模拟玩家本人的记忆）", small);
            for (int i = 0; i < game.Companions.Count; i++)
            {
                ActorMotor actor = game.Companions[i];
                GUI.Label(new Rect(x + 22, y + 425 + i * 25, 735, 26),
                    actor.DisplayName + "：你 → 对方 " + game.Memories.Strength("player", actor.Identity).ToString("0.00")
                    + " / 对方 → 你 " + game.Memories.Strength(actor.Identity, "player").ToString("0.00")
                    + (actor.FollowTarget == null ? " · 等候" : " · 跟随"), small);
            }
            if (GUI.Button(new Rect(x + 22, y + 525, 735, 38), "关闭（I / Esc）", button)) game.Resume();
        }

        private void Pause(SandboxGame game, float width, float height)
        {
            float x = width / 2 - 300;
            float y = height / 2 - 282;
            Panel(new Rect(x, y, 600, 564));
            GUI.Label(new Rect(x + 22, y + 16, 556, 36), "测试控制 · 世界已暂停", title);
            if (Control(x, y + 65, "继续")) game.Resume();
            if (Control(x, y + 117, "敌人攻击：" + (game.EnemiesEnabled ? "开启 → 关闭" : "关闭 → 开启"))) game.ToggleEnemies();
            if (Control(x, y + 169, "重置所有战斗目标（装备不变）")) game.ResetTargets();
            if (Control(x, y + 221, "切换昼夜")) game.SetNight(!game.IsNight);
            if (Control(x, y + 273, "回营地 / 恢复状态 / 召回同伴（装备不变）")) game.ReturnToCamp();
            if (Control(x, y + 325, "重新生成测试场（清空本次测试物品与记忆）")) game.LoadScene("Sandbox");
            if (Control(x, y + 377, "返回原剧情开场")) game.LoadScene("Opening");
            GUI.Label(new Rect(x + 22, y + 440, 556, 100),
                "敌人默认关闭；攻击仅对木桩、异常者和完成转变的观察员生效，不伤害友方。\nM 可实时调节遗忘实验；没有存档或自动昼夜推进。", small);
        }

        private bool Control(float x, float y, string label)
        {
            return GUI.Button(new Rect(x + 22, y, 556, 42), label, button);
        }

        private void FailurePanel(SandboxGame game, float width, float height)
        {
            float x = width / 2 - 300;
            float y = height / 2 - 140;
            Panel(new Rect(x, y, 600, 280));
            GUI.Label(new Rect(x + 22, y + 20, 556, 45), "测试角色已倒下", title);
            GUI.Label(new Rect(x + 22, y + 77, 556, 60), "生命耗尽。装备和背包内容没有丢失。\n回营地会关闭敌人，恢复状态并召回同伴。", body);
            if (Control(x, y + 155, "恢复并回到营地")) game.ReturnToCamp();
            if (Control(x, y + 210, "返回原剧情")) game.LoadScene("Opening");
        }

        private void Panel(Rect rect) { Fill(rect, new Color(0.06f, 0.08f, 0.085f, 0.96f)); }

        private void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (font != null) Destroy(font);
            if (pixel != null) Destroy(pixel);
        }
    }
}
