using System.Collections.Generic;
using System.Linq;
using Framework;
using HarmonyLib;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// The sidebar's own Filter (your fabrics / your accessories). It is easy to set in the shop
    /// thinking it filters the shelf, and it then stays on in every room. For fabric the game
    /// only shows a fabric that has EVERY checked style, so a few styles can empty the list
    /// silently. Say when that happens, and put what is on into the Filter button's label.
    /// </summary>
    internal static class SidebarFilter
    {
        private static readonly AccessTools.FieldRef<SidebarInventory, FabricFilterData> FabricFilters =
            AccessTools.FieldRefAccess<SidebarInventory, FabricFilterData>("_currentFilters");
        private static readonly AccessTools.FieldRef<SidebarInventory, AccessoryFilterData> AccessoryFilters =
            AccessTools.FieldRefAccess<SidebarInventory, AccessoryFilterData>("_currentAccessoryFilters");
        private static readonly AccessTools.FieldRef<SidebarInventory, List<InventoryItem>> FabricItems =
            AccessTools.FieldRefAccess<SidebarInventory, List<InventoryItem>>("_fabricItems");
        private static readonly AccessTools.FieldRef<SidebarInventory, Transform> AccessoryParent =
            AccessTools.FieldRefAccess<SidebarInventory, Transform>("accessoryParent");

        private static SidebarInventory Inv
        {
            get { try { return SingletonBehaviour<SidebarInventory>.Instance; } catch { return null; } }
        }

        private static string Join(IEnumerable<string> items)
        {
            var l = items.Where(s => !string.IsNullOrEmpty(s)).OrderBy(s => s).ToList();
            if (l.Count <= 1) return l.FirstOrDefault() ?? "";
            return string.Join(", ", l.Take(l.Count - 1)) + " and " + l[l.Count - 1];
        }

        /// <summary>What the fabric filter is set to, or null when it is off.</summary>
        internal static string FabricSummary(SidebarInventory inv)
        {
            FabricFilterData f = FabricFilters(inv);
            var parts = new List<string>();
            if (f.fabricTypes != null && f.fabricTypes.Count > 0)
                parts.Add("type " + Join(f.fabricTypes.Where(t => t != null).Select(t => t.PrettyName)));
            if (f.colors != null && f.colors.Count > 0)
                parts.Add("colour " + Join(f.colors.Select(c => c.ToString())));
            if (f.tags != null && f.tags.Count > 0)
                parts.Add("styles " + Join(f.tags.Where(t => t != null).Select(t => t.PrettyName))
                          + (f.tags.Count > 1 ? ", a fabric must have all of them" : ""));
            return parts.Count == 0 ? null : string.Join("; ", parts);
        }

        internal static string AccessorySummary(SidebarInventory inv)
        {
            AccessoryFilterData f = AccessoryFilters(inv);
            var parts = new List<string>();
            if (f.accessoryTypes != null && f.accessoryTypes.Count > 0)
                parts.Add("type " + Join(f.accessoryTypes.Where(t => t != null).Select(t => t.PrettyName)));
            if (f.tags != null && f.tags.Count > 0)
                parts.Add("styles " + Join(f.tags.Where(t => t != null).Select(t => t.PrettyName)));
            return parts.Count == 0 ? null : string.Join("; ", parts);
        }

        private static int OwnedFabrics => PlayerProgress.Current?.ownedFabricPieces?.Count ?? 0;
        private static int OwnedAccessories => PlayerProgress.Current?.ownedAccessories?.Count ?? 0;

        /// <summary>"Showing 2 of your 5 fabrics", plus a warning when the filter hides them all.</summary>
        internal static string Shown(SidebarInventory inv, SidebarInventory.Tab tab, bool always)
        {
            if (tab == SidebarInventory.Tab.Fabric)
            {
                int shown = FabricItems(inv)?.Count ?? 0, owned = OwnedFabrics;
                string on = FabricSummary(inv);
                if (owned == 0)
                    return always ? "You don't own any fabric yet; buy some in the Fabric Closet." : null;
                if (shown == 0 && on != null)
                    return $"No fabrics showing: the sidebar filter hides all {owned} of yours. It is set to {on}. "
                         + "Tab to Filter and choose Reset to see them all.";
                if (on != null && (always || shown < owned))
                    return $"Showing {shown} of your {owned} fabrics, filtered by {on}.";
                return always ? $"{shown} fabrics." : null;
            }
            if (tab == SidebarInventory.Tab.Accessory)
            {
                Transform p = AccessoryParent(inv);
                int shown = p != null ? p.childCount : 0, owned = OwnedAccessories;
                string on = AccessorySummary(inv);
                if (owned == 0)
                    return always ? "You don't own any accessories yet." : null;
                if (shown == 0 && on != null)
                    return $"No accessories showing: the sidebar filter hides all {owned} of yours. It is set to {on}. "
                         + "Tab to Filter and choose Reset to see them all.";
                if (on != null && (always || shown < owned))
                    return $"Showing {shown} of your {owned} accessories, filtered by {on}.";
                return always ? $"{shown} accessories." : null;
            }
            return null;
        }

        /// <summary>The two Filter buttons in the shop look the same; say which is which.</summary>
        internal static string Label(GameObject go)
        {
            if (go.name != "FilterButton" && go.name != "Filter")
                return null;
            SidebarInventory inv = go.GetComponentInParent<SidebarInventory>();
            if (inv != null)
            {
                bool fabric = inv.CurrentTab == SidebarInventory.Tab.Fabric;
                string on = fabric ? FabricSummary(inv) : AccessorySummary(inv);
                return (fabric ? "Filter your own fabrics" : "Filter your own accessories")
                     + (on != null ? ", on: " + on : ", off");
            }
            Shop shop;
            try { shop = SingletonBehaviour<Shop>.Instance; } catch { shop = null; }
            if (shop != null && shop.filterButton != null && go == shop.filterButton.gameObject)
                return "Filter the shop shelf";
            return null;
        }

        [HarmonyPatch(typeof(SidebarInventory), "OnFiltersApplied")]
        private static class FabricAppliedPatch
        {
            private static void Postfix(SidebarInventory __instance)
                => Speech.Queue(Shown(__instance, SidebarInventory.Tab.Fabric, true));
        }

        [HarmonyPatch(typeof(SidebarInventory), "OnAccessoryFiltersApplied")]
        private static class AccessoryAppliedPatch
        {
            private static void Postfix(SidebarInventory __instance)
                => Speech.Queue(Shown(__instance, SidebarInventory.Tab.Accessory, true));
        }
    }
}
