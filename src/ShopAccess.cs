using System.Collections.Generic;
using System.Linq;
using System.Text;
using Framework;
using HarmonyLib;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// The fabric shop. The bolts and haberdashery on the shelves are 3D objects,
    /// so they're added to the Tab list by hand, in shelf order. Enter takes one
    /// down (the game's own select), which opens the buy panel: a normal set of
    /// buttons (more/less, Buy) the navigator already reads.
    /// </summary>
    internal static class ShopAccess
    {
        private static readonly System.Reflection.MethodInfo SelectMethod = AccessTools.Method(typeof(Shop), "Select");
        private static readonly AccessTools.FieldRef<Shop, ShopInteractable> Selected =
            AccessTools.FieldRefAccess<Shop, ShopInteractable>("_selected");
        private static readonly AccessTools.FieldRef<ShopItemInfo, int> Length =
            AccessTools.FieldRefAccess<ShopItemInfo, int>("_length");

        private static Shop ShopOrNull
        {
            get
            {
                var gm = Rooms.GameManagerOrNull();
                if (gm == null || gm.CurrentScene != GameManager.Scene.Store)
                    return null;
                try { return SingletonBehaviour<Shop>.Instance; } catch { return null; }
            }
        }

        private static int _index = -1;
        internal static bool ShelfFocused;

        private static List<ShopInteractable> Shelf()
        {
            return Object.FindObjectsByType<ShopInteractable>(FindObjectsSortMode.None)
                .Where(i => i.isActiveAndEnabled && (ShopOrNull == null || ShopOrNull.mode == Shop.Mode.Fabric ? i.Fabric != null : i.Accessory != null))
                .OrderBy(i => i.transform.parent != null ? Mathf.Round(i.transform.parent.position.x * 100f) : 0f)
                .ThenBy(i => i.HomePosition.x).ToList(); // home spot: a taken-down item moves
        }

        /// <summary>Shelf keys. Returns true if the key was used.</summary>
        internal static bool HandleKeys(bool shift)
        {
            Shop shop = ShopOrNull;
            if (shop == null || Dialogue.Active)
                return false;
            int move = 0;
            if (Input.GetKeyDown(KeyCode.DownArrow)) move = 1;
            else if (Input.GetKeyDown(KeyCode.UpArrow)) move = -1;
            else if (Input.GetKeyDown(KeyCode.PageDown)) move = 10;
            else if (Input.GetKeyDown(KeyCode.PageUp)) move = -10;
            else if (Input.GetKeyDown(KeyCode.Home)) move = int.MinValue;
            else if (Input.GetKeyDown(KeyCode.End)) move = int.MaxValue;
            if (move != 0)
            {
                Step(move);
                return true;
            }
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++)
            {
                if (!Input.GetKeyDown(k)) continue;
                if (!shiftHeld && (k == KeyCode.H || k == KeyCode.M || k == KeyCode.R)) return false;
                Letter((char)('a' + (k - KeyCode.A)));
                return true;
            }
            return false;
        }

        private static void Step(int move)
        {
            List<ShopInteractable> shelf = Shelf();
            if (shelf.Count == 0) { Speech.Say("The shelf is empty. Check the filter."); return; }
            if (move == int.MinValue) _index = 0;
            else if (move == int.MaxValue) _index = shelf.Count - 1;
            else _index = Mathf.Clamp((_index < 0 ? (move > 0 ? -1 : 0) : _index) + move, 0, shelf.Count - 1);
            Announce(shelf);
        }

        private static void Letter(char letter)
        {
            List<ShopInteractable> shelf = Shelf();
            for (int i = 1; i <= shelf.Count; i++)
            {
                int j = ((_index < 0 ? -1 : _index) + i) % shelf.Count;
                if (NameOf(shelf[j]).ToLowerInvariant().StartsWith(letter.ToString()))
                {
                    _index = j;
                    Announce(shelf);
                    return;
                }
            }
            Speech.Say("Nothing starting with " + char.ToUpper(letter));
        }

        private static void Announce(List<ShopInteractable> shelf)
        {
            ShelfFocused = true;
            Speech.Say($"{Describe(shelf[_index], brief: true)}. {_index + 1} of {shelf.Count}");
        }

        /// <summary>Test harness: a number steps, a word jumps to the next name starting with it.</summary>
        internal static void CmdShelf(string arg)
        {
            if (int.TryParse(arg, out int n)) { Step(n); return; }
            List<ShopInteractable> shelf = Shelf();
            for (int i = 1; i <= shelf.Count; i++)
            {
                int j = ((_index < 0 ? -1 : _index) + i) % shelf.Count;
                if (NameOf(shelf[j]).ToLowerInvariant().StartsWith(arg.ToLowerInvariant())) { _index = j; Announce(shelf); return; }
            }
        }

        internal static bool TakeDownCurrent()
        {
            Shop shop = ShopOrNull;
            if (shop == null || !ShelfFocused)
                return false;
            List<ShopInteractable> shelf = Shelf();
            if (_index < 0 || _index >= shelf.Count)
                return false;
            SelectMethod.Invoke(shop, new object[] { shelf[_index] });
            return true;
        }

        private static string NameOf(ShopInteractable si)
            => si.Fabric != null ? si.Fabric.PrettyName : si.Accessory != null ? si.Accessory.PrettyName : "";

        /// <summary>Styles, with the ones this client asked for first.</summary>
        internal static string Styles(List<TagWeight> weights, int others)
        {
            if (weights == null || weights.Count == 0) return "";
            var wanted = new HashSet<ItemTag>(PlayerProgress.Current?.CurrentQuestDefinition?.GetQuestTags() ?? new List<ItemTag>());
            var ordered = weights.Where(t => t.tag != null && t.weight != 0).OrderByDescending(t => t.weight).ToList();
            var take = ordered.Where(t => wanted.Contains(t.tag)).Concat(ordered.Where(t => !wanted.Contains(t.tag)).Take(others));
            return string.Join(", ", take.Select(t => $"{t.tag.PrettyName} {Mathf.RoundToInt(t.weight)}"));
        }

        internal static string Describe(ShopInteractable si, bool brief)
        {
            var sb = new StringBuilder();
            if (si.Fabric != null)
            {
                Fabric f = si.Fabric;
                sb.Append(f.PrettyName);
                if (f.fabricType != null && !f.PrettyName.ToLowerInvariant().Contains(f.fabricType.PrettyName.ToLowerInvariant()))
                    sb.Append(", ").Append(f.fabricType.PrettyName);
                string look = DressInfo.FabricLook(f);
                if (look.Length > 0) sb.Append(". ").Append(look);
                sb.Append($", {f.cost} gold a metre");
                string styles = Styles(f.tagWeights, brief ? 3 : 20);
                if (styles.Length > 0) sb.Append(". ").Append(styles);
                if (!brief)
                {
                    if (f.colors != null && f.colors.Count > 0)
                        sb.Append(". Colours: ").Append(string.Join(", ", f.colors.Select(c => c.ToLocalized())));
                    FabricPiece owned = PlayerProgress.Current?.ownedFabricPieces?.FirstOrDefault(p => p.fabric == f);
                    sb.Append(owned != null ? $". You own {owned.length:0.##} metres" : ". You own none");
                }
            }
            else if (si.Accessory != null)
            {
                AccessoryDefinition a = si.Accessory;
                sb.Append(string.IsNullOrEmpty(a.PrettyName) ? a.name : a.PrettyName);
                if (a.accessoryType != null) sb.Append(", ").Append(a.accessoryType.PrettyName);
                sb.Append(a.prefab is AccessoryItemPath ? $", {a.cost} gold a metre" : $", {a.cost} gold each");
                string astyles = Styles(a.tagWeights, brief ? 3 : 20);
                if (astyles.Length > 0) sb.Append(". ").Append(astyles);
            }
            return sb.ToString();
        }

        /// <summary>Names for the buy panel's controls.</summary>
        internal static string Label(GameObject go)
        {
            ShopItemInfo info = go.GetComponentInParent<ShopItemInfo>();
            if (info == null)
                return null;
            var fabric = Traverse.Create(info).Field("_fabric").GetValue<Fabric>();
            var acc = Traverse.Create(info).Field("_accessory").GetValue<AccessoryDefinition>();
            int len = Length(info);
            bool byLength = fabric != null || (acc != null && acc.prefab is AccessoryItemPath);
            string unit = byLength ? (len == 1 ? "metre" : "metres") : "";
            int cost = fabric != null ? fabric.cost * len : acc != null ? acc.cost * len : 0;
            if (go.name == "Count")
                return $"{(fabric != null ? "Metres to buy" : "How many to buy")}: {len}, edit box";
            if (go.GetComponent<UnityEngine.UI.Slider>() != null)
                return null; // default slider text is fine once named below
            if (go.name == "Buy")
                return $"Buy {len} {unit} for {cost} gold. You have {PlayerProgress.Current?.gold ?? 0}";
            return null;
        }

        [HarmonyPatch(typeof(Shop), "Select")]
        private static class SelectPatch
        {
            private static void Postfix(Shop __instance, ShopInteractable interactable)
            {
                if (Selected(__instance) == interactable)
                    Speech.Say("Taken down: " + Describe(interactable, brief: false) +
                               ". Tab to the buy panel to choose how much, then Buy.");
                else
                    Speech.Say("Put back.");
            }
        }

        [HarmonyPatch(typeof(ShopItemInfo), nameof(ShopItemInfo.AddLength))]
        private static class LengthPatch
        {
            private static void Postfix(ShopItemInfo __instance) => SayAmount(__instance);
        }

        private static void SayAmount(ShopItemInfo info)
        {
            var btn = Traverse.Create(info).Field("buyButtonText").GetValue<TMPro.TMP_Text>();
            Speech.Say($"{Length(info)}. {(btn != null ? Speech.Clean(btn.text) : "")}");
        }

        [HarmonyPatch(typeof(ShopItemInfo), nameof(ShopItemInfo.Buy))]
        private static class BuyPatch
        {
            private static int _goldBefore;
            private static void Prefix() => _goldBefore = PlayerProgress.Current?.gold ?? 0;
            private static void Postfix(ShopItemInfo __instance)
            {
                int gold = PlayerProgress.Current?.gold ?? 0;
                if (gold < _goldBefore)
                    Speech.Queue($"Bought. You have {gold} gold left.");
            }
        }
    }
}
