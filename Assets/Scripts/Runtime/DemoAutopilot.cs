using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DontForgetMe
{
    // Development aid: plays the whole opening without a person at the keyboard, saving a
    // picture at each beat and a log of anything that failed to happen in time. Start a build
    // with "-dfmAutopilot [-dfmShots <folder>]", or use the editor menu under Dont Forget Me.
    public sealed class DemoAutopilot : MonoBehaviour
    {
        public static bool Finished { get; private set; }
        public static int Problems { get; private set; }

        // Survive the scene reload that "restart" performs, so the walkthrough can check it too.
        private static readonly List<string> log = new List<string>();
        private static bool restarted;
        private DemoGame game;
        private ThirdPersonPlayer player;
        private string folder;
        private Vector3? waypoint;
        private bool sprint;
        private float frames, slowest, elapsed;
        private int shot;
        private static bool shadowsSqueezed;
        private bool steadinessSkipped;

        private void Start()
        {
            Application.runInBackground = true;
            // Without a display there is no frame pacing, and thousands of frames a second is not
            // what anyone plays at. Step the game at a fixed, ordinary rate instead.
            if (Application.isBatchMode) Time.captureFramerate = 45;
            game = DemoGame.Instance;
            player = game.Player;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-dfmShots");
            folder = at >= 0 && at + 1 < args.Length ? args[at + 1] : Environment.GetEnvironmentVariable("DFM_SHOTS");
            if (string.IsNullOrEmpty(folder)) folder = Path.Combine(Application.persistentDataPath, "Autopilot");
            Directory.CreateDirectory(folder);
            Application.logMessageReceived += Heard;
            player.ScriptedMove = Steer;
            if (restarted)
            {
                StartCoroutine(AfterRestart());
                return;
            }
            Finished = false;
            Problems = 0;
            shadowsSqueezed = false;
            log.Clear();
            StartCoroutine(Run());
        }

        // The pipeline says so when the local lights in view need more shadow maps than the atlas
        // holds. It then rescales them from frame to frame, which shows as shadows jumping on walls.
        private void Heard(string message, string stack, LogType type)
        {
            if (shadowsSqueezed || !message.StartsWith("Reduced additional punctual light shadows")) return;
            shadowsSqueezed = true;
            Problem("too many shadow-casting local lights in view: " + message);
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= Heard;
        }

        private Vector3 Steer()
        {
            player.ScriptedSprint = sprint;
            if (!waypoint.HasValue) return Vector3.zero;
            Vector3 to = waypoint.Value - player.transform.position;
            to.y = 0;
            return to.magnitude < 0.15f ? Vector3.zero : to.normalized;
        }

        private void Update()
        {
            if (game == null || !game.HasStarted || game.IsPaused) return;
            frames++;
            elapsed += Time.unscaledDeltaTime;
            if (frames > 30) slowest = Mathf.Max(slowest, Time.unscaledDeltaTime);
            // Keep the camera loosely behind the direction of travel, as a player would.
            if (player.PlanarSpeed > 0.5f && game.State.Phase != OpeningPhase.Repair)
                player.Yaw = Mathf.LerpAngle(player.Yaw, player.Heading, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
        }

        private void Note(string message)
        {
            string line = Time.time.ToString("F1") + "s  " + message;
            log.Add(line);
            Debug.Log("[Autopilot] " + line);
        }

        private void Problem(string message)
        {
            Problems++;
            Note("PROBLEM: " + message);
        }

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0;
            return value;
        }

        private IEnumerator Walk(Vector3 target, float stopWithin, float timeout, bool run = false)
        {
            waypoint = target;
            sprint = run;
            float until = Time.time + timeout;
            while (Time.time < until)
            {
                if (Flat(target - player.transform.position).magnitude <= stopWithin) break;
                yield return null;
            }
            if (Time.time >= until) Problem("could not reach " + target + ", stopped at " + player.transform.position);
            waypoint = null;
            sprint = false;
            yield return new WaitForSeconds(0.35f);
        }

        private IEnumerator Use(string promptPart, float timeout, Func<bool> done)
        {
            float until = Time.time + timeout;
            while (Time.time < until && !done())
            {
                if (game.Nearest != null && game.Nearest.Prompt.Contains(promptPart)) game.Interact();
                yield return new WaitForSeconds(0.25f);
            }
            if (!done()) Problem("interaction '" + promptPart + "' did not complete; nearest is " +
                (game.Nearest != null ? game.Nearest.Prompt : "nothing"));
        }

        // "settle" is how many frames to let the camera and anti-aliasing converge first; a
        // picture of something in mid-movement passes a small number.
        private IEnumerator Shot(string name, float yaw = float.NaN, float pitch = float.NaN, int settle = 24)
        {
            if (!float.IsNaN(yaw)) player.Yaw = yaw;
            if (!float.IsNaN(pitch)) player.Pitch = pitch;
            for (int i = 0; i < settle; i++) yield return null;
            string path = Path.Combine(folder, (shot++).ToString("00") + "_" + name + ".png");
            if (Application.isBatchMode)
            {
                Texture2D image = Offscreen();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Destroy(image);
            }
            else
            {
                ScreenCapture.CaptureScreenshot(path);
                yield return null;
                yield return null;
            }
            Note("shot " + Path.GetFileName(path));
        }

        private Texture2D Offscreen()
        {
            Camera view = game.View;
            RenderTexture target = RenderTexture.GetTemporary(1600, 1000, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture before = view.targetTexture;
            view.targetTexture = target;
            for (int i = 0; i < 6; i++) view.Render();
            RenderTexture.active = target;
            Texture2D image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            view.targetTexture = before;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(target);
            return image;
        }

        // Holds the camera still and compares consecutive frames in coarse blocks. Film grain and
        // sub-pixel jitter average out inside a block; a shadow that jumps or two surfaces fighting
        // for the same depth do not. People and the interface are hidden for the half second it
        // takes, so that only the scenery is compared. The block that changed most is reported
        // with its position, bottom-left being 0% 0%.
        private IEnumerator Steady(string name, float yaw, float pitch)
        {
            const int cols = 32, rows = 20, frames = 8;
            // Moving shadows of the people who are hidden reach about 5%; surfaces fighting over the
            // same depth measured 20% and more.
            const float limit = 0.1f;
            // Without a display the pictures are rendered offscreen, and that path is not steady
            // from frame to frame by itself (successive renders differ in how blurred they are),
            // so a comparison there says nothing about what a player sees.
            if (Application.isBatchMode)
            {
                if (!steadinessSkipped) Note("steadiness checks skipped: they need a real display");
                steadinessSkipped = true;
                yield break;
            }
            player.Yaw = yaw;
            player.Pitch = pitch;
            Camera view = game.View;
            Vector3 at = view.transform.position;
            int still = 0;
            for (float until = Time.time + 5f; still < 12 && Time.time < until;)
            {
                yield return null;
                still = (view.transform.position - at).sqrMagnitude < 1e-7f ? still + 1 : 0;
                at = view.transform.position;
            }
            DemoHud hud = game.GetComponent<DemoHud>();
            int mask = view.cullingMask;
            view.cullingMask = mask & ~(1 << 8);
            hud.enabled = false;
            // Long enough for temporal anti-aliasing to forget the people it was just drawing.
            for (int i = 0; i < 20; i++) yield return null;
            float[] low = new float[cols * rows], high = new float[cols * rows];
            byte[] first = null, last = null;
            for (int f = 0; f < frames; f++)
            {
                yield return new WaitForEndOfFrame();
                Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
                Color32[] pixels = image.GetPixels32();
                int width = image.width, height = image.height;
                if (f == 0) first = image.EncodeToPNG();
                if (f == frames - 1) last = image.EncodeToPNG();
                Destroy(image);
                for (int b = 0; b < low.Length; b++)
                {
                    int x0 = b % cols * width / cols, x1 = (b % cols + 1) * width / cols;
                    int y0 = b / cols * height / rows, y1 = (b / cols + 1) * height / rows;
                    long sum = 0;
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                        {
                            Color32 c = pixels[y * width + x];
                            sum += c.r * 2 + c.g * 5 + c.b;
                        }
                    float mean = sum / (8f * 255f * (x1 - x0) * (y1 - y0));
                    low[b] = f == 0 ? mean : Mathf.Min(low[b], mean);
                    high[b] = f == 0 ? mean : Mathf.Max(high[b], mean);
                }
            }
            view.cullingMask = mask;
            hud.enabled = true;
            int worst = 0, unsteady = 0;
            for (int b = 0; b < low.Length; b++)
            {
                if (high[b] - low[b] > high[worst] - low[worst]) worst = b;
                if (high[b] - low[b] > 0.02f) unsteady++;
            }
            float moved = high[worst] - low[worst];
            string result = string.Format("steadiness {0}: over {1} frames the most changed block moved {2:P1} of full brightness (at {3}% {4}%), {5} of {6} blocks moved more than 2%",
                name, frames, moved, (worst % cols * 100 + 50) / cols, (worst / cols * 100 + 50) / rows, unsteady, low.Length);
            if (moved <= limit)
            {
                Note(result);
                yield break;
            }
            // Keep the first and last frame, so whatever is flickering can be looked at.
            string stem = Path.Combine(folder, "unsteady_" + name.Replace(", ", "_").Replace(' ', '_'));
            File.WriteAllBytes(stem + "_first.png", first);
            File.WriteAllBytes(stem + "_last.png", last);
            Problem("the scenery flickers. " + result);
        }

        private IEnumerator Take(Piece piece)
        {
            Vector3 at = piece.Root.transform.position;
            yield return Walk(new Vector3(at.x, 0, at.z - 1.25f), 0.35f, 10);
            yield return Use(piece.Root.GetComponent<Interactable>().Prompt, 5, () => piece.Claimed);
        }

        private IEnumerator SetDown(Piece piece)
        {
            float until = Time.time + 25f;
            while (Time.time < until && (!piece.Moved || player.Busy)) yield return null;
            if (!piece.Moved) Problem(piece.Id + " was never set down");
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(3.5f);
            yield return Shot("title");
            game.Begin();
            yield return new WaitForSeconds(2.5f);
            yield return Shot("street_start", 0, 10);
            yield return Shot("street_back", 150, 6);
            yield return Shot("street_west", -62, 4);
            yield return Steady("street, west shop fronts", -62, 4);

            yield return Walk(new Vector3(1.2f, 0, 5.2f), 0.3f, 12);
            yield return Use("给狗倒水", 4, () => game.State.GaveWater);
            yield return new WaitForSeconds(5f);
            yield return Shot("dog_water", 60, 18);

            yield return Walk(new Vector3(-3.4f, 0, 11.6f), 0.4f, 15);
            yield return Shot("workshop_wide", -48, 8);
            yield return Walk(new Vector3(-6.4f, 0, 10.9f), 0.4f, 10);
            yield return Shot("general_store", -90, 8);
            yield return Steady("general store and repair shop", -90, 8);
            yield return Walk(new Vector3(-5.9f, 0, 13.2f), 0.3f, 10);
            yield return Shot("workshop_close", -35, 12);
            yield return Shot("zhou", -100, 6);
            yield return Use("修理铺的人", 4, () => game.State.Phase != OpeningPhase.Street);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("startled", -116, 6, 2);
            yield return new WaitForSeconds(2.4f);
            yield return Shot("shop_closing", -116, 6, 2);
            yield return Shot("riot_begins", 30, 8);

            float patience = Time.time + 25f;
            while (Time.time < patience && game.Woman.HasDestination) yield return null;
            yield return Walk(game.Woman.transform.position + new Vector3(-0.9f, 0, -1.3f), 0.4f, 20, true);
            yield return Shot("reunion", 20, 8);
            yield return Use("确认她和狗", 12, () => game.State.Phase != OpeningPhase.Reunion);
            yield return new WaitForSeconds(1.5f);

            yield return Walk(new Vector3(0.6f, 0, 25.5f), 0.4f, 15);
            yield return Shot("alley_mouth", 0, 2);
            yield return Steady("alley mouth from the street", 0, 2);
            yield return Walk(new Vector3(0, 0, 31.5f), 0.4f, 15);
            if (game.State.Phase != OpeningPhase.Debris) Problem("entering the alley did not start the debris phase");
            yield return Shot("alley_in", 0, 4);
            yield return Walk(new Vector3(0, 0, 38.5f), 0.4f, 15);
            yield return Shot("alley_mid", 0, 0);
            // Looking back toward the street: the pile behind is being carried about and would count as movement.
            yield return Steady("alley, west wall", -110, 2);
            yield return Steady("alley, east wall", 110, 2);
            yield return Walk(new Vector3(0.2f, 0, 42.4f), 0.3f, 12);
            yield return Shot("gate_and_pile", 2, 8);

            // The heavy pieces, front to back. The neighbours have to clear what lies in front first.
            int carried = 0;
            foreach (Piece piece in game.Pile)
            {
                if (!piece.Heavy) continue;
                patience = Time.time + 70f;
                float grace = Time.time + 16f;
                while (Time.time < patience && !game.CanMove(piece))
                {
                    // A neighbour slow to clear what lies in front: take it ourselves, as a player would.
                    Piece blocking = null;
                    if (Time.time > grace && !player.Busy && piece.After != null)
                        foreach (Piece first in piece.After)
                            if (game.CanMove(first)) blocking = first;
                    if (blocking != null)
                    {
                        Note("carrying " + blocking.Id + " for the neighbours");
                        yield return Take(blocking);
                        yield return SetDown(blocking);
                    }
                    yield return null;
                }
                if (!game.CanMove(piece))
                {
                    Problem("could not get at " + piece.Id + "; the pieces in front of it were never cleared");
                    continue;
                }
                if (carried == 0)
                {
                    yield return Walk(new Vector3(0, 0, 43.6f), 0.4f, 10);
                    yield return Shot("neighbours_clearing", 0, 10);
                }
                yield return Take(piece);
                if (carried == 0)
                {
                    yield return new WaitForSeconds(1.5f);
                    yield return Shot("carrying", float.NaN, 14, 2);
                }
                if (carried == 3)
                {
                    yield return new WaitForSeconds(1.3f);
                    yield return Shot("wheeling_truck", float.NaN, 14, 2);
                }
                yield return SetDown(piece);
                carried++;
            }
            if (game.State.ClearedDebrisCount != game.State.DebrisRequired)
                Problem("pile not cleared: " + game.State.ClearedDebrisCount + " of " + game.State.DebrisRequired);
            yield return Walk(new Vector3(0, 0, 43.4f), 0.4f, 10);
            yield return Shot("alley_look_back", 180, 4);
            yield return Shot("pile_cleared", 2, 8);

            patience = Time.time + 20f;
            while (Time.time < patience && Flat(game.Zhou.transform.position - DemoGame.ZhouWork).magnitude > 0.8f) yield return null;
            yield return Walk(new Vector3(0.0f, 0, 46.2f), 0.35f, 12);
            yield return Shot("gate_close_up", 12, 6);
            yield return Use("帮老周", 6, () => game.State.Phase != OpeningPhase.Debris);
            yield return new WaitForSeconds(2.5f);
            yield return Shot("repair", 30, 10);
            yield return Shot("threat_far", 180, 2);

            // Hold the line: go for whoever is on old Zhou or closest, shove when they are in reach.
            // Halfway through, stand back for a few seconds, the way a player who loses track of
            // the crowd would, to see what happens to the gate work when nobody covers for it.
            bool pushed = false, crowded = false, stalled = false, idled = false;
            float idleUntil = -1f;
            Vector3 home = new Vector3(0.1f, 0, 46.1f), aside = new Vector3(-0.7f, 0, 46.6f);
            float began = Time.time;
            patience = Time.time + 200f;
            while (game.State.Phase == OpeningPhase.Repair && Time.time < patience && !game.IsFailed)
            {
                player.Yaw = Mathf.LerpAngle(player.Yaw, 180, 0.08f);
                if (!idled && game.State.RepairProgress > 0.5f)
                {
                    idled = true;
                    idleUntil = Time.time + 12f;
                    Note("standing back at " + game.State.RepairProgress.ToString("P0"));
                }
                Hostile urgent = game.Siege.Nearest(game.Zhou.transform.position, 2.4f, true);
                if (urgent == null) urgent = game.Siege.Nearest(player.transform.position, 3.2f, true);
                // Standing back means stepping out of the lane altogether, until the gate work stops or it starts to hurt.
                if (Time.time < idleUntil && !stalled && player.Health > 55f)
                    waypoint = Flat(aside - player.transform.position).magnitude > 0.4f ? aside : (Vector3?)null;
                else if (urgent != null)
                {
                    Vector3 to = Flat(urgent.transform.position - player.transform.position);
                    waypoint = to.magnitude > 1.25f ? urgent.transform.position - to.normalized : (Vector3?)null;
                    if (to.magnitude < 1.45f && player.TryPush() && !pushed)
                    {
                        pushed = true;
                        waypoint = null;
                        yield return new WaitForSeconds(0.17f);
                        yield return Shot("push", float.NaN, 8, 1);
                        yield return new WaitForSeconds(0.3f);
                        yield return Shot("reeling", float.NaN, 8, 1);
                    }
                }
                else waypoint = Flat(home - player.transform.position).magnitude > 0.5f ? home : (Vector3?)null;
                if (!stalled && game.RepairStalled && game.State.RepairProgress > 0.05f)
                {
                    stalled = true;
                    Note("gate work interrupted at " + game.State.RepairProgress.ToString("P0") + ", health " + player.Health.ToString("F0"));
                    yield return Shot("zhou_grabbed", 0, 10, 2);
                }
                if (!crowded && game.Siege.Hostiles.Count >= 6 && game.State.RepairProgress > 0.72f)
                {
                    crowded = true;
                    waypoint = null;
                    yield return Shot("crowd", 180, 4, 3);
                    yield return Shot("cowering", -40, 10, 3);
                }
                yield return null;
            }
            waypoint = null;
            if (game.State.Phase != OpeningPhase.Regroup) Problem("gate was never opened; phase is " + game.State.Phase + ", progress " + game.State.RepairProgress);
            if (!stalled) Note("the gate work was never interrupted in this run");
            Note("gate opened after " + (Time.time - began).ToString("F0") + " s of holding, with " + game.Siege.Hostiles.Count + " in the alley, "
                + player.ShovesLanded + " shoves landed, health " + player.Health.ToString("F0"));
            yield return Shot("gate_open", 0, 8, 3);
            yield return Walk(new Vector3(0.2f, 0, 47.2f), 0.5f, 8, true);
            yield return Walk(new Vector3(0.1f, 0, 50.8f), 0.5f, 8, true);
            yield return Shot("holding_the_door", 180, 8, 2);
            yield return Walk(new Vector3(0.4f, 0, 53.2f), 0.5f, 10, true);

            // Stand clear of the gateway, as the objective says. Only someone who has got well into
            // the yard keeps the gate open, and has to be shoved back toward it.
            patience = Time.time + 60f;
            bool closingShot = false;
            Vector3 clear = new Vector3(0.4f, 0, 53.2f);
            while (!game.GateShut && Time.time < patience && !game.IsFailed)
            {
                player.Yaw = Mathf.LerpAngle(player.Yaw, 180, 0.08f);
                Hostile intruder = null;
                foreach (Hostile hostile in game.Siege.Hostiles)
                    if (hostile.transform.position.z > 50.6f
                        && (intruder == null || hostile.transform.position.z > intruder.transform.position.z)) intruder = hostile;
                if (intruder != null)
                {
                    Vector3 to = Flat(intruder.transform.position - player.transform.position);
                    waypoint = to.magnitude > 1.25f || to.z > 0 ? intruder.transform.position + Vector3.forward : (Vector3?)null;
                    if (to.magnitude < 1.45f && to.z < 0) player.TryPush();
                }
                else waypoint = Flat(clear - player.transform.position).magnitude > 0.5f ? clear : (Vector3?)null;
                if (!closingShot && Mathf.Abs(game.Gate.transform.position.x) < 1.6f && Mathf.Abs(game.Gate.transform.position.x) > 0.3f)
                {
                    closingShot = true;
                    yield return Shot("gate_closing", 180, 8, 1);
                }
                yield return null;
            }
            waypoint = null;
            if (!game.GateShut) Problem("the gate never shut: " + Where());
            foreach (Hostile hostile in game.Siege.Hostiles)
                if (game.GateShut && hostile.transform.position.z > 48.6f) Problem(hostile.name + " was shut in on our side of the gate");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("gate_shut", 180, 6);
            yield return Walk(game.Zhou.transform.position + new Vector3(0.9f, 0, -1.1f), 0.6f, 15);
            patience = Time.time + 25f;
            while (Time.time < patience && !game.EveryonePresent()) yield return null;
            if (!game.EveryonePresent()) Problem("not everyone came through the gate: " + Where());
            yield return new WaitForSeconds(1.5f);
            yield return Shot("courtyard", 0, 8);
            yield return Shot("courtyard_back", 178, 4);
            yield return Steady("courtyard, east wall", 90, 4);
            yield return Steady("courtyard, west wall", -90, 4);
            yield return Use("确认大家", 10, () => game.State.Phase == OpeningPhase.Complete);
            yield return new WaitForSeconds(3f);
            yield return Shot("together", 15, 10);
            yield return new WaitForSeconds(DemoGame.EndPanelDelay);
            yield return Shot("end_card");

            Note(Application.isBatchMode
                ? "no display: stepped at a fixed 45 frames per second, so frame rate was not measured; health " + player.Health.ToString("F0")
                : string.Format("{0}x{1}, average {2:F1} fps, slowest frame {3:F0} ms, health {4:F0}",
                    Screen.width, Screen.height, elapsed > 0 ? frames / elapsed : 0, slowest * 1000f, player.Health));
            restarted = true;
            game.Restart();
        }

        // "Restart" reloads the scene; a fresh game must come back waiting on its title screen.
        private IEnumerator AfterRestart()
        {
            shot = 90;
            yield return new WaitForSecondsRealtime(3f);
            if (game.HasStarted || game.State.Phase != OpeningPhase.Street || game.Player == null || game.Zhou == null
                || game.Siege.Hostiles.Count != 0)
                Problem("restart did not return to a fresh title screen");
            yield return Shot("after_restart");
            Note("problems " + Problems);
            File.WriteAllLines(Path.Combine(folder, "autopilot.log"), log);
            restarted = false;
            Finished = true;
            if (!Application.isEditor) Application.Quit(Problems == 0 ? 0 : 1);
        }

        private string Where()
        {
            string text = "player " + player.transform.position + "  ";
            foreach (ActorMotor actor in new[] { game.Woman, game.Dog, game.Zhou, game.ResidentA, game.ResidentB })
                text += actor.Identity + " " + actor.transform.position + "  ";
            foreach (Hostile hostile in game.Siege.Hostiles)
                text += hostile.name + " " + hostile.State + " z" + hostile.transform.position.z.ToString("F1") + "  ";
            return text;
        }
    }
}
