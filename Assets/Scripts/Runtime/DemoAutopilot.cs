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

        private void Start()
        {
            Application.runInBackground = true;
            game = DemoGame.Instance;
            player = game.Player;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-dfmShots");
            folder = at >= 0 && at + 1 < args.Length ? args[at + 1] : Environment.GetEnvironmentVariable("DFM_SHOTS");
            if (string.IsNullOrEmpty(folder)) folder = Path.Combine(Application.persistentDataPath, "Autopilot");
            Directory.CreateDirectory(folder);
            player.ScriptedMove = Steer;
            if (restarted)
            {
                StartCoroutine(AfterRestart());
                return;
            }
            Finished = false;
            Problems = 0;
            log.Clear();
            StartCoroutine(Run());
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
            if (player.PlanarSpeed > 0.5f)
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

        private IEnumerator Walk(Vector3 target, float stopWithin, float timeout, bool run = false)
        {
            waypoint = target;
            sprint = run;
            float until = Time.time + timeout;
            while (Time.time < until)
            {
                Vector3 to = target - player.transform.position;
                to.y = 0;
                if (to.magnitude <= stopWithin) break;
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

        private IEnumerator Shot(string name, float yaw = float.NaN, float pitch = float.NaN)
        {
            if (!float.IsNaN(yaw)) player.Yaw = yaw;
            if (!float.IsNaN(pitch)) player.Pitch = pitch;
            // Let the camera settle and temporal anti-aliasing converge on the new view.
            for (int i = 0; i < 24; i++) yield return null;
            string path = Path.Combine(folder, (shot++).ToString("00") + "_" + name + ".png");
            if (Application.isBatchMode)
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

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(3.5f);
            yield return Shot("title");
            game.Begin();
            yield return new WaitForSeconds(2.5f);
            yield return Shot("street_start", 0, 10);
            yield return Shot("street_back", 150, 6);
            yield return Shot("street_west", -62, 4);

            yield return Walk(new Vector3(1.2f, 0, 5.2f), 0.3f, 12);
            yield return Use("给狗倒水", 4, () => game.State.GaveWater);
            yield return new WaitForSeconds(5f);
            yield return Shot("dog_water", 60, 18);

            yield return Walk(new Vector3(-3.4f, 0, 11.6f), 0.4f, 15);
            yield return Shot("workshop_wide", -48, 8);
            yield return Walk(new Vector3(-5.9f, 0, 13.2f), 0.3f, 10);
            yield return Shot("workshop_close", -35, 12);
            yield return Shot("zhou", -100, 6);
            yield return Use("修理铺的人", 4, () => game.State.Phase != OpeningPhase.Street);
            yield return new WaitForSeconds(3.2f);
            yield return Shot("riot_begins", 30, 8);

            float patience = Time.time + 25f;
            while (Time.time < patience && game.Woman.HasDestination) yield return null;
            yield return Walk(game.Woman.transform.position + new Vector3(-0.9f, 0, -1.3f), 0.4f, 20, true);
            yield return Shot("reunion", 20, 8);
            yield return Use("确认她和狗", 12, () => game.State.Phase != OpeningPhase.Reunion);
            yield return new WaitForSeconds(1.5f);

            yield return Walk(new Vector3(0.6f, 0, 25.5f), 0.4f, 15);
            yield return Shot("alley_mouth", 0, 2);
            yield return Walk(new Vector3(0, 0, 31.5f), 0.4f, 15);
            if (game.State.Phase != OpeningPhase.Debris) Problem("entering the alley did not start the debris phase");
            yield return Shot("alley_in", 0, 4);
            yield return Walk(new Vector3(0, 0, 38.5f), 0.4f, 15);
            yield return Shot("alley_mid", 0, 0);
            yield return Shot("alley_look_back", 180, 4);

            yield return Walk(new Vector3(-0.9f, 0, 42.7f), 0.3f, 12);
            yield return Shot("gate_and_debris", 5, 10);
            yield return Use("搬开挡路", 4, () => game.State.ClearedDebrisCount >= 1);
            yield return new WaitForSeconds(1.2f);
            yield return Walk(new Vector3(0.9f, 0, 42.7f), 0.3f, 12);
            yield return Use("搬开挡路", 4, () => game.State.ClearedDebrisCount >= 2);
            yield return new WaitForSeconds(1.2f);

            yield return Walk(new Vector3(0.3f, 0, 45.2f), 0.3f, 12);
            patience = Time.time + 20f;
            while (Time.time < patience && Vector3.Distance(game.Zhou.transform.position, new Vector3(-0.8f, game.Zhou.transform.position.y, 46)) > 0.9f)
                yield return null;
            yield return Use("开始修理", 6, () => game.State.Phase != OpeningPhase.Debris);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("repair", -20, 8);
            yield return Shot("threat_far", 180, 2);

            bool pictured = false;
            patience = Time.time + 40f;
            while (game.State.Phase == OpeningPhase.Repair && Time.time < patience && !game.IsFailed)
            {
                player.Yaw = Mathf.LerpAngle(player.Yaw, 180, 0.1f);
                if (player.TryPush() && !pictured)
                {
                    pictured = true;
                    yield return Shot("push", 180, 6);
                }
                yield return null;
            }
            if (game.State.Phase != OpeningPhase.Regroup) Problem("repair did not finish; phase is " + game.State.Phase);
            player.TryPush();
            yield return Shot("gate_open", 0, 8);
            yield return Walk(new Vector3(0, 0, 46.6f), 0.4f, 8, true);
            yield return Walk(new Vector3(0, 0, 50.8f), 0.4f, 8, true);
            yield return Walk(new Vector3(-0.6f, 0, 55.8f), 0.5f, 10, true);
            patience = Time.time + 25f;
            while (Time.time < patience && !game.EveryonePresent()) yield return null;
            if (!game.EveryonePresent()) Problem("not everyone came through the gate: " + Where());
            yield return new WaitForSeconds(1.5f);
            yield return Shot("courtyard", 0, 8);
            yield return Shot("courtyard_back", 170, 6);
            yield return Use("确认大家", 10, () => game.State.Phase == OpeningPhase.Complete);
            yield return new WaitForSeconds(3f);
            yield return Shot("together", 15, 10);
            yield return new WaitForSeconds(DemoGame.EndPanelDelay);
            yield return Shot("end_card");

            Note(string.Format("{0}x{1}, average {2:F1} fps, slowest frame {3:F0} ms, health {4:F0}",
                Screen.width, Screen.height, elapsed > 0 ? frames / elapsed : 0, slowest * 1000f, player.Health));
            restarted = true;
            game.Restart();
        }

        // "Restart" reloads the scene; a fresh game must come back waiting on its title screen.
        private IEnumerator AfterRestart()
        {
            shot = 90;
            yield return new WaitForSecondsRealtime(3f);
            if (game.HasStarted || game.State.Phase != OpeningPhase.Street || game.Player == null || game.Zhou == null)
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
            string text = "";
            foreach (ActorMotor actor in new[] { game.Woman, game.Dog, game.Zhou, game.ResidentA, game.ResidentB })
                text += actor.Identity + " " + actor.transform.position + "  ";
            return text;
        }
    }
}
