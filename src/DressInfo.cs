using System.Collections.Generic;
using System.Linq;
using System.Text;
using Framework;
using HarmonyLib;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// Words for what the game only draws.
    ///  - Style variations: many bodices, skirts, sleeves and collars come in 2 to 4 versions that
    ///    the game shows only as dots and a changed sketch. Described here from the game's own
    ///    sketches (I looked at every one): most bodices' second version adds a waistband.
    ///  - Finished dresses in the sketchbook: the page shows a 3D dress; the mod lists it as lines
    ///    you can arrow through (client, summary, score, then each part with its fabric, colours
    ///    and accessories), built from the saved dress.
    ///  - The friendship book: choosing a person reads their page.
    /// </summary>
    internal static class DressInfo
    {
        private const string Band = "adds a waistband";
        private static readonly string[] Waistband = { "no waistband", Band };

        // Keyed by the game's internal name for the part (not the shown name, which can be translated).
        private static readonly Dictionary<string, string[]> Variants = new Dictionary<string, string[]>
        {
            // Bodices
            { "Apron Bodice", new[] { "a wide front panel with narrow straps", "a narrower front panel with wider straps" } },
            { "Bustier Bodice", new[] { "strapless", "with thin shoulder straps", "with straps tied in bows at the shoulders" } },
            { "Double Breasted Bodice", new[] { "the right front is the wide piece that crosses over", "mirrored: the left front crosses over" } },
            { "Elevated Dirndl", new[] { "a plain top panel", "the top panel is split by a centre front seam" } },
            { "Elevated Scoop Bodice", Waistband },
            { "Elevated Scoop Ruffle Bodice", Waistband },
            { "Elevated Stays", new[] { "a plain top panel", "the top panel is split by a centre front seam" } },
            { "Flared Bugonia Bodice", Waistband },
            { "Halter Neck Bodice", new[] {
                "a band round the neck, and a straight seam under the bust",
                "tied in a bow behind the neck, and a straight seam under the bust",
                "a band round the neck, and a gathered V-shaped seam under the bust",
                "tied in a bow behind the neck, and a gathered V-shaped seam under the bust" } },
            { "Kaftan Wrap Bodice", new[] { "wraps across one way", "mirrored: wraps across the other way" } },
            { "Katarina Bodice", new[] { "a curved, sweetheart-like neckline", "a straight, deep V neckline" } },
            { "Kimono Bodice", new[] { "a straight band under the bust", "a curved seam under the bust" } },
            { "Land Girl Bodice", new[] { "a plain yoke at the top", "the yoke is split by a centre front seam" } },
            { "One Shoulder Bodice", new[] { "the strap over one shoulder", "mirrored in the sketch, with the strap over the other shoulder; it uses the same pattern pieces as style 1" } },
            { "Princess Bodice", Waistband },
            { "Princess Heart Bodice", Waistband },
            { "Princess Scoop Bodice", Waistband },
            { "Princess Scoop Ruffle Bodice", Waistband },
            { "Princess V Bodice", Waistband },
            { "Princess V Elevated Bodice", Waistband },
            { "Princess V Elevated Ruffle Bodice", Waistband },
            { "Princess V Ruffle Bodice", Waistband },
            { "Ruched Sweetheart Bodice", new[] { "the gathers sweep across one way", "mirrored: the gathers sweep across the other way" } },
            { "Ruffle Bodice Straight", Waistband },
            { "Single Keyhole Bodice", Waistband },
            { "Split Crew Neck Bodice", Waistband },
            { "Square Bodice", Waistband },
            { "Strapless Bodice", new[] { "plain", Band, "adds a folded band along the top edge", "adds a band along the top edge and a waistband" } },
            { "Sweetheart Bodice Elevated", Waistband },
            { "Wrap Bodice", new[] { "wraps across one way", "mirrored: wraps across the other way" } },
            // Collars
            { "Double Ruffle Collar", new[] { "with a big bow at the front", "without the bow" } },
            { "Fable Collar", new[] { "with a bow and long ribbons at the front", "without the bow" } },
            { "Mandarin Collar", new[] { "a narrow opening at the front", "a wider opening at the front" } },
            // Skirts
            { "Apron Skirt", new[] { "two gathered ruffles at the hem", "an apron-shaped panel over the front, with one ruffle at the hem" } },
            { "Bow Ballgown Skirt", new[] { "a big bow with long tails at one hip", "mirrored: the bow at the other hip" } },
            { "Circle Skirt", new[] { "long", "short, about knee length" } },
            { "Coat Dress Skirt", new[] { "long", "shorter" } },
            { "Flare Skirt", new[] { "long", "shorter" } },
            { "Kaftan Skirt", new[] { "the front opening to one side", "mirrored: the opening to the other side" } },
            { "Koi Skirt", new[] { "a cascade of ruffles down one side", "mirrored: the cascade down the other side" } },
            { "Mermaid Skirt", new[] { "the flare grows out of the skirt's own panels", "a separate flounce, joined by a seam around the knees" } },
            { "Mother of Pearl Skirt", new[] { "long", "short" } },
            { "Poinsetta Skirt", new[] { "pointed petal panels near the hem", "adds a band across the middle, and a second row edging the petal points" } },
            { "Scalloped Skirt", new[] { "scallops cut into the hem of the panels", "a separate row of scalloped pieces at the hem" } },
            { "Straight Gathered Tiered Skirt", new[] { "long, in three tiers", "shorter, in three tiers" } },
            { "Tulip Hip Skirt", new[] { "plain", "adds bows with long tails on both hips" } },
            { "Wavecrest Skirt", new[] { "tiered ruffles down one side", "mirrored: the ruffles down the other side" } },
            { "Wrap Skirt", new[] { "wraps across, opening to one side, no tie", "the same with a bow tie at the waist", "wraps the other way, with a bow tie", "wraps the other way, no tie" } },
            // Sleeves
            { "Bell Sleeve", new[] { "long", "shorter, about elbow length" } },
            { "Short Puffed Sleeve", new[] { "with a cuff band", "no cuff" } },
            { "Short Sleeve", new[] { "with a cuff band", "no cuff" } },
        };

        /// <summary>"Style 2 of 2: adds a waistband", or "" when the part has only one version.</summary>
        internal static string Variant(GarmentComponent gc, int index, bool withNumber = true)
        {
            if (gc == null || gc.variations == null || gc.variations.Count < 2)
                return "";
            index = Mathf.Clamp(index, 0, gc.variations.Count - 1);
            string note = Variants.TryGetValue(gc.name, out string[] n) && index < n.Length ? n[index] : null;
            string num = withNumber ? $"style {index + 1} of {gc.variations.Count}" : "";
            if (note == null) return num;
            return withNumber ? num + ": " + note : note;
        }

        internal static string Part(GarmentComponent gc, int variant)
        {
            if (gc == null) return null;
            string v = Variant(gc, variant);
            return gc.PrettyName + (v.Length > 0 ? ", " + v : "");
        }

        // ---------- a finished dress, line by line ----------

        internal static List<string> DressLines(DressState d)
        {
            var lines = new List<string>();
            if (d == null || d.bodiceDefinition == null)
                return lines;
            var groups = new List<(string what, GarmentComponent gc, int variant)>
            {
                ("Bodice", d.bodiceDefinition, d.bodiceVariant),
                ("Skirt", d.skirtDefinition, d.skirtVariant),
                ("Collar", d.collarDefinition, d.collarVariant),
                ("Sleeves", d.sleeveDefinition, d.sleeveVariant),
            };
            int i = 0;
            foreach (var g in groups)
            {
                if (g.gc == null)
                    continue;
                int count = g.gc.GetVariant(g.variant).panels.Count;
                var fabrics = new List<Fabric>();
                for (int k = 0; k < count && i < (d.panelStates?.Count ?? 0); k++, i++)
                    if (d.panelStates[i].fabric != null && !fabrics.Contains(d.panelStates[i].fabric))
                        fabrics.Add(d.panelStates[i].fabric);
                if (count == 0)
                    continue; // "no sleeves" / "no collar" parts have no pieces
                var sb = new StringBuilder(g.what).Append(": ").Append(Part(g.gc, g.variant));
                if (fabrics.Count > 0)
                    sb.Append(". In ").Append(string.Join(" and ", fabrics.Select(FabricWords)));
                lines.Add(sb.Append('.').ToString());
            }
            if (d.sleeveDefinition == null || d.sleeveDefinition.GetVariant(d.sleeveVariant).panels.Count == 0)
                lines.Add("No sleeves.");
            if (d.collarDefinition == null || d.collarDefinition.GetVariant(d.collarVariant).panels.Count == 0)
                lines.Add("No collar.");

            var acc = (d.accessoryStates ?? new List<AccessoryItemState>()).Where(a => a.placed && a.definition != null).ToList();
            if (acc.Count == 0)
                lines.Add("No accessories.");
            else
            {
                var parts = acc.GroupBy(a => a.definition).Select(gr =>
                {
                    float metres = gr.Sum(a => a.PathLength());
                    return metres > 0.01f
                        ? $"{gr.Key.PrettyName}, {metres:0.#} metres"
                        : (gr.Count() > 1 ? $"{gr.Count()} {gr.Key.PrettyName}" : gr.Key.PrettyName);
                });
                lines.Add("Accessories: " + string.Join("; ", parts) + ".");
            }
            return lines;
        }

        private static string FabricWords(Fabric f)
        {
            string colours = f.colors != null && f.colors.Count > 0
                ? " (" + string.Join(", ", f.colors.Select(c => c.ToString().ToLowerInvariant())) + ")" : "";
            return f.PrettyName + colours;
        }

        // ---------- the sketchbook's finished-dress page as list items ----------

        private static readonly AccessTools.FieldRef<Sketchbook, int> OldIndex =
            AccessTools.FieldRefAccess<Sketchbook, int>("_oldQuestIndex");
        private static readonly AccessTools.FieldRef<Sketchbook, GameObject> OldHolder =
            AccessTools.FieldRefAccess<Sketchbook, GameObject>("oldDressViewHolder");
        private static readonly AccessTools.FieldRef<Sketchbook, ComissionScoreDisplay> ScoreDisplay =
            AccessTools.FieldRefAccess<Sketchbook, ComissionScoreDisplay>("comissionScoreDisplay");
        private static readonly AccessTools.FieldRef<Sketchbook, GameObject> ScoreHolder =
            AccessTools.FieldRefAccess<Sketchbook, GameObject>("oldQuestScoreDisplay");

        internal static List<string> OldDressPage(Sketchbook b)
        {
            var lines = new List<string>();
            PlayerProgress pp = PlayerProgress.Current;
            int i = OldIndex(b);
            if (pp == null || i < 0 || i >= pp.completedQuests.Count)
                return lines;
            QuestState q = pp.completedQuests[i];
            QuestDefinition def = q.questDefinition;
            bool rack = def == QuestDefinition.RepeatingQuest;
            lines.Add($"{SketchPages.Kind(q)}{(rack ? "" : ": " + Speech.Clean(def.PrettyName))}. Finished dress {i + 1} of {pp.completedQuests.Count}.");
            if (!rack && def.questGiverCharacter != null)
                lines.Add("Client: " + def.questGiverCharacter.PrettyName + ".");
            if (q.dressRecipient != null && (def.questGiverCharacter == null || q.dressRecipient != def.questGiverCharacter))
                lines.Add("Given to " + q.dressRecipient.PrettyName + ".");
            if (!rack)
            {
                string summary = Speech.Clean(SingletonBehaviour<GameManager>.Instance.GetQuestSummary(def));
                if (!string.IsNullOrEmpty(summary) && !summary.StartsWith("Could not find"))
                    lines.Add(summary);
            }
            GameObject sh = ScoreHolder(b);
            ComissionScoreDisplay sd = ScoreDisplay(b);
            if (sh != null && sh.activeInHierarchy && sd != null)
            {
                string score = UINav.TextOf(sd.gameObject);
                if (!string.IsNullOrEmpty(score))
                {
                    // "Quality, 97/75, Elegant, 61/65, Avoid: Black" -> "Quality 97 of 75, Elegant 61 of 65, Avoid: Black"
                    string[] p = score.Split(new[] { ", " }, System.StringSplitOptions.RemoveEmptyEntries);
                    var parts = new List<string>();
                    for (int k = 0; k < p.Length; k++)
                    {
                        string[] nums = k + 1 < p.Length ? p[k + 1].Split('/') : new string[0];
                        if (nums.Length == 2) { parts.Add($"{p[k]} {nums[0]} of {nums[1]}"); k++; }
                        else parts.Add(p[k]);
                    }
                    lines.Add("Score: " + string.Join(", ", parts) + ".");
                }
            }
            lines.AddRange(DressLines(q.dress));
            return lines;
        }

        /// <summary>UINav source: one item per line of the finished-dress page, first in the list.</summary>
        internal static List<UINav.Item> PageItems()
        {
            var items = new List<UINav.Item>();
            Sketchbook b = Sketch.Book;
            if (b == null || SketchPages.SectionName(b) != "OldDresses")
                return items;
            GameObject holder = OldHolder(b);
            if (holder == null || !holder.activeInHierarchy)
                return items;
            List<string> lines = OldDressPage(b);
            for (int k = 0; k < lines.Count; k++)
            {
                string line = lines[k];
                items.Add(new UINav.Item
                {
                    Go = holder,
                    // Off the top of the screen, one row apart, so they come first and in order.
                    Screen = new Vector2(10f, Screen.height * 3f - k * 40f),
                    Label = line,
                    OnActivate = () => Speech.Say(line),
                });
            }
            return items;
        }

        // ---------- the friendship book ----------

        private static readonly AccessTools.FieldRef<Sketchbook, CharacterDefinition> LastPerson =
            AccessTools.FieldRefAccess<Sketchbook, CharacterDefinition>("_lastSelectedCharacter");
        private static readonly AccessTools.FieldRef<Sketchbook, TMPro.TextMeshProUGUI> BioText =
            AccessTools.FieldRefAccess<Sketchbook, TMPro.TextMeshProUGUI>("personBioText");

        private static bool Known(CharacterDefinition c)
            => PlayerProgress.Current.GetCharacterRelationship(c) > 0f
               || PlayerProgress.Current.activeCommissions.Any(q => q.questDefinition.questGiverCharacter == c);

        internal static string Stars(CharacterDefinition c, int of)
        {
            int n = Mathf.FloorToInt(PlayerProgress.Current.GetCharacterRelationship(c));
            return $"friendship {n} of {of} stars";
        }

        /// <summary>A person's name in the friendship book's list.</summary>
        internal static string PersonLabel(ContactListPersonItem p)
        {
            if (p.forCharacter == null) return null;
            Sketchbook b = Sketch.Book;
            bool sel = b != null && LastPerson(b) == p.forCharacter;
            string name = Known(p.forCharacter) ? p.forCharacter.PrettyName : "Someone you haven't met yet";
            return $"{name}, {Stars(p.forCharacter, p.stars.Count)}{(sel ? ", selected" : "")}";
        }

        private static string PersonPage(Sketchbook b, CharacterDefinition c)
        {
            if (!Known(c))
                return "Someone you haven't met yet.";
            var sb = new StringBuilder(c.PrettyName).Append(", ").Append(Stars(c, 5)).Append(". ");
            // The page's text: a measurements line, "Personal style:", then one line per style,
            // shown only once you have that many stars.
            string bio = BioText(b) != null ? BioText(b).text : "";
            var known = new List<string>();
            int unknown = 0;
            foreach (string raw in bio.Split('\n'))
            {
                if (raw.Contains("emptyStar")) { unknown++; continue; }
                string line = Speech.Clean(raw);
                if (line.Length == 0) continue;
                if (raw.Contains("starOutline")) known.Add(line);
                else if (!line.EndsWith(":"))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(line, @"([\d.]+) - ([\d.]+) - ([\d.]+)");
                    if (m.Success) line = line.Substring(0, m.Index) + $"bust {m.Groups[1]}, waist {m.Groups[2]}, hips {m.Groups[3]} centimetres";
                    sb.Append(line).Append(". ");
                }
            }
            if (known.Count > 0 || unknown > 0)
            {
                sb.Append("Personal style: ").Append(known.Count > 0 ? string.Join("; ", known) : "nothing known yet").Append(". ");
                if (unknown > 0)
                    sb.Append(unknown).Append(unknown == 1 ? " more to learn" : " more to learn").Append(", one for each new star. ");
            }
            int made = PlayerProgress.Current.completedQuests.Count(q => q.questDefinition.questGiverCharacter == c);
            sb.Append(made == 0 ? "No dresses made for them yet."
                : $"{made} {(made == 1 ? "dress" : "dresses")} made for them: Tab to {(made == 1 ? "it" : "them")}, Enter opens one.");
            return Speech.Clean(sb.ToString());
        }

        private static System.Collections.IEnumerator SayPersonLater(CharacterDefinition c)
        {
            yield return new WaitForSecondsRealtime(0.3f);
            Sketchbook b = Sketch.Book;
            if (b != null && LastPerson(b) == c)
                Speech.Queue(PersonPage(b, c));
        }

        [HarmonyPatch(typeof(Sketchbook), "ShowPersonDetail")]
        private static class PersonPatch
        {
            private static void Postfix(CharacterDefinition character)
            {
                if (character != null && Plugin.Instance != null)
                    Plugin.Instance.StartCoroutine(SayPersonLater(character));
            }
        }
    }
}
