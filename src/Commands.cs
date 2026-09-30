using System;
using System.IO;
using Framework;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// Self-test channel (only when Debug.TestCommands is on in the config): the mod
    /// reads DressmakerAccess_cmd.txt next to the exe and runs one command per poll,
    /// logging "[cmd] x". Lets a test drive the game with no keyboard focus at all.
    /// </summary>
    internal static class Commands
    {
        private static string _path;
        private static float _next;

        internal static void Poll()
        {
            if (Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 0.35f;
            if (_path == null)
                _path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "DressmakerAccess_cmd.txt");
            if (!File.Exists(_path))
                return;
            string[] lines;
            try { lines = File.ReadAllLines(_path); }
            catch { return; }
            if (lines.Length == 0)
                return;
            string cmd = lines[0].Trim();
            try { File.WriteAllLines(_path, lines.Length > 1 ? lines[1..] : Array.Empty<string>()); }
            catch { return; }
            if (cmd.Length == 0)
                return;
            Plugin.Log.LogInfo("[cmd] " + cmd);
            try { Run(cmd); }
            catch (Exception e) { Plugin.Log.LogError("[cmd] failed: " + e); }
        }

        private static void Tree(Transform t, int depth, int max)
        {
            string rect = "";
            if (t is RectTransform rt)
            {
                Vector3[] c = new Vector3[4];
                rt.GetWorldCorners(c);
                rect = $" corners=({c[0].x:0},{c[0].y:0})-({c[2].x:0},{c[2].y:0})";
            }
            var comps = string.Join(",", System.Linq.Enumerable.Select(t.GetComponents<Component>(), x => x.GetType().Name));
            Plugin.Log.LogInfo($"[tree] {new string(' ', depth * 2)}{t.name} active={t.gameObject.activeSelf}{rect} [{comps}]");
            if (depth >= max) return;
            foreach (Transform ch in t) Tree(ch, depth + 1, max);
        }

        private static void Run(string cmd)
        {
            string[] a = cmd.Split(' ');
            switch (a[0])
            {
                case "next": case "prev": case "enter":
                    Keys.Do(a[0]); break;
                case "key":
                    Keys.Do(a[1]); break;
                case "status": Rooms.Status(); break;
                case "help": Screens.Help(); break;
                case "optdown": Dialogue.MoveOption(1); break;
                case "optup": Dialogue.MoveOption(-1); break;
                case "styles": Sketch.SayStyles(); break;
                case "screen": TextWatch.ReadScreen(); break;
                case "dump": UINav.Dump(); break;
                case "unlockrack": UnlockRackPatch.On = true; break;
                case "dumpall": UINav.DumpAll(); break;
                case "progress": Sewing.SayProgress(); break;
                case "assist": Sewing.ToggleAssist(); break;
                case "space":
                    // Story boxes read the real Space key; emulate their handler.
                    Keys.Do("enter"); break;
                case "hold": VirtualInput.HoldKey(a[1]); break;
                case "release": VirtualInput.ReleaseKey(a[1]); break;
                case "click": VirtualInput.Click(); break;
                case "select":
                    // select <text>: focus and click the first item whose label contains text
                    string want = cmd.Substring(7).ToLowerInvariant();
                    var all = UINav.Gather();
                    all.Sort((x, y) => (y.Label.ToLowerInvariant().StartsWith(want) ? 1 : 0) - (x.Label.ToLowerInvariant().StartsWith(want) ? 1 : 0));
                    foreach (var it in all)
                        if (it.Label.ToLowerInvariant().Contains(want))
                        {
                            Plugin.Log.LogInfo("[cmd] selecting " + it.Label);
                            UINav.ActivateItem(it);
                            return;
                        }
                    Plugin.Log.LogInfo("[cmd] no item matching " + want);
                    break;
                case "shelf": ShopAccess.CmdShelf(a[1]); break;
                case "focus": UINav.FocusMatching(cmd.Substring(6)); break;
                case "adjust":
                    int times = int.Parse(a[1]);
                    for (int i = 0; i < System.Math.Abs(times); i++) UINav.AdjustCurrent(System.Math.Sign(times));
                    break;
                case "cutkey":
                    // cutkey <x|left|right|up|down|q|e|f|c|back> [shift]: press a cutting-table key
                    Cutting.CmdKey(a[1], a.Length > 2); break;
                case "details": Keys.RoomDetails(); break;
                case "line": Measuring.CmdLine(int.Parse(a[1])); break;
                case "boxes":
                    foreach (TextBox tb in UnityEngine.Object.FindObjectsByType<TextBox>(FindObjectsSortMode.None))
                        Plugin.Log.LogInfo($"[boxes] {UINav.Path(tb.gameObject)} activeAndEnabled={tb.isActiveAndEnabled}");
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    Plugin.Log.LogInfo($"[boxes] eventsystem selected={(es != null && es.currentSelectedGameObject != null ? UINav.Path(es.currentSelectedGameObject) : "none")} nav={(es != null && es.sendNavigationEvents)}");
                    break;
                case "tape": Measuring.CmdTape(int.Parse(a[1])); break;
                case "gear": Measuring.CmdGear(float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture)); break;
                case "tree":
                    var root = GameObject.Find(a[1]);
                    if (root == null) { foreach (var t0 in Resources.FindObjectsOfTypeAll<Transform>()) if (t0.name == a[1] && t0.gameObject.scene.IsValid()) { root = t0.gameObject; break; } }
                    if (root == null) { Plugin.Log.LogInfo("[tree] not found"); break; }
                    Tree(root.transform, 0, a.Length > 2 ? int.Parse(a[2]) : 2);
                    break;
                case "shot":
                    string shot = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "shot.png");
                    ScreenCapture.CaptureScreenshot(shot);
                    Plugin.Log.LogInfo("[cmd] screenshot -> " + shot);
                    break;
                case "timescale": Time.timeScale = float.Parse(a[1]); break;
                case "back": Back.Backspace(); break;
                case "escape": if (!Back.ClosePopup()) SingletonBehaviour<GameManager>.Instance.ShowHideOptions(); break;
                case "sale":
                    foreach (SellDressSummary sd in Resources.FindObjectsOfTypeAll<SellDressSummary>())
                        if (sd.gameObject.scene.IsValid())
                        {
                            for (Transform up = sd.transform; up != null; up = up.parent) up.gameObject.SetActive(true);
                            sd.ShowSaleSummary(new Dress.SaleDataItem { Materials = 90, Labour = 30, QualityBonusPercent = 5, PrestigeBonusPercent = 0, SellPrice = 126 }, null, null);
                            break;
                        }
                    break;
                case "pause": SingletonBehaviour<GameManager>.Instance.ShowHideOptions(); break;
                case "palette":
                    foreach (ColorPicker cp in Resources.FindObjectsOfTypeAll<ColorPicker>())
                    {
                        if (!cp.gameObject.scene.IsValid()) continue;
                        int n = 0;
                        foreach (Color c in cp.Colours)
                        {
                            Color.RGBToHSV(c, out float h, out float s, out float v);
                            Plugin.Log.LogInfo($"[palette] {++n} #{ColorUtility.ToHtmlStringRGB(c)} h={h * 360:0} s={s:0.00} v={v:0.00} -> {Sketch.ColourName(c)}");
                        }
                    }
                    break;
                case "knight": SingletonBehaviour<KnighthoodScene>.Instance.ShowKnighthoodEnvelope(); break;
                case "gossip":
                    // gossip [index]: show one of the game's newspaper pages
                    var reports = ScriptableEnum.GetValueList<GossipReportDefinition>();
                    int gi = a.Length > 1 ? int.Parse(a[1]) : 0;
                    Plugin.Log.LogInfo("[cmd] gossip reports: " + string.Join(", ", System.Linq.Enumerable.Select(reports, r => r.name)));
                    SingletonBehaviour<FrontDesk>.Instance.RequestGossipReport(reports[gi]);
                    break;
                default: Plugin.Log.LogInfo("[cmd] unknown"); break;
            }
        }
    }
}
