using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DressmakerAccess
{
    [BepInPlugin("lilian.dressmakeraccess", "Dressmaker Access", "0.9.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Plugin Instance;

        internal static ConfigEntry<bool> AssistDefaultApplied;
        internal static ConfigEntry<bool> SteeringTone;
        internal static ConfigEntry<bool> TestCommands;
        internal static ConfigEntry<bool> MuteSpeech;
        internal static ConfigEntry<bool> NoSteam;
        internal static ConfigEntry<string> SaveFolder;

        private float _startTime;
        private bool _greeted;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            AssistDefaultApplied = Config.Bind("Internal", "AssistDefaultApplied", false,
                "Set once the mod has switched the game's sewing assist on for the first time.");
            SteeringTone = Config.Bind("Sewing", "SteeringTone", true,
                "Play a tone while sewing that tells you which side the seam line is on.");
            TestCommands = Config.Bind("Debug", "TestCommands", false,
                "Read test commands from DressmakerAccess_cmd.txt next to the game exe.");
            MuteSpeech = Config.Bind("Debug", "MuteSpeech", false,
                "Log speech instead of sending it to NVDA (for silent self-tests).");
            NoSteam = Config.Bind("Debug", "NoSteam", false,
                "Skip the game's Steam start-up (self-test copy only, so tests don't add playtime).");
            SaveFolder = Config.Bind("Debug", "SaveFolder", "",
                "Self-test copy only: keep saves in this folder instead of the real save folder.");

            // A blind player cannot see that the window lost focus; a frozen game is invisible.
            Application.runInBackground = true;

            new Harmony("lilian.dressmakeraccess").PatchAll();
            _startTime = Time.realtimeSinceStartup;
            Log.LogInfo("Dressmaker Access loaded.");
        }

        private void Update()
        {
            try
            {
                if (!_greeted && Time.realtimeSinceStartup - _startTime > 2f)
                {
                    _greeted = true;
                    Speech.Say("Dressmaker Access loaded. Press H at any time to hear what the keys do.");
                }

                // Unity's own UI navigation would also act on arrows/Enter, moving and pressing
                // a hidden selection of its own (it wandered onto Export and opened file windows).
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.sendNavigationEvents)
                    es.sendNavigationEvents = false;

                VirtualInput.Tick();
                Speech.Tick();
                if (TestCommands.Value)
                    Commands.Poll();

                Keys.Handle();

                Dialogue.Tick();
                TextWatch.Tick();
                Sewing.Tick();
                Measuring.Tick();
                Cutting.Tick();
                Rooms.Tick();
            }
            catch (Exception e)
            {
                Log.LogError("Update error: " + e);
            }
        }
    }
}
