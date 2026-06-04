using System;
using System.Collections.Generic;
using Dalamud.Game.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// Reads the player's currently VISIBLE gear. Icon/name/rarity come from the
    /// equipped item (glamour-aware) via the inventory; the DYES come from the
    /// rendered <c>Character.DrawData</c>, which reflects what's actually on screen
    /// (a dye applied to a glamour / glamour-plate isn't on the item's own Stains).
    /// Must run on the framework thread; the returned list is plain data.
    /// </summary>
    public static class GearReader
    {
        /// <summary>
        /// Reads the player's currently visible gear, or <c>null</c> when the read can't be
        /// trusted (no rendered character / missing data) — distinct from an empty list, which
        /// means "logged out, nothing equipped". The caller treats null as "hold the last
        /// published state" so a transient blip (zoning, cutscene) never publishes a misleading
        /// inventory-only read in which an invisible-glamoured slot momentarily reappears.
        /// </summary>
        public static unsafe IReadOnlyList<GearSlotData>? ReadVisibleGear()
        {
            var result = new List<GearSlotData>(GlamSlots.All.Count);

            try
            {
                // Logged out (title / character select) → clear the bus definitively. The
                // equipped-items container can linger in memory after the local player is torn
                // down on logout, so gate on login state, not inventory emptiness — otherwise
                // the chara==null "hold" below would keep the last character's gear resident.
                if (!Plugin.ClientState.IsLoggedIn) return result; // empty → clear

                var items = Plugin.GameInventory.GetInventoryItems(GameInventoryType.EquippedItems);
                if (items.Length == 0) return result; // nothing equipped → clear bus

                var itemSheet = Plugin.DataManager.GetExcelSheet<Item>();
                if (itemSheet == null) return null; // data not ready → hold

                var stainSheet = Plugin.DataManager.GetExcelSheet<Stain>();

                var player = Plugin.ObjectTable.LocalPlayer;
                var chara = player != null ? (Character*)player.Address : null;

                // No rendered character → we can't read dyes or tell which slots are hidden.
                // Hold the last published state rather than fall back to a misleading
                // inventory-only read (which would un-hide invisible glamours).
                if (chara == null) return null;

                foreach (var slot in GlamSlots.All)
                {
                    // Bonus cosmetics (facewear glasses, fashion accessory) live on the
                    // character object, not the EquippedItems container — resolved separately.
                    if (slot.Key == "FACEWEAR") { TryAddBonus(result, slot, chara, BonusKind.Facewear); continue; }
                    if (slot.Key == "FASHION")  { TryAddBonus(result, slot, chara, BonusKind.Fashion);  continue; }

                    if (slot.EquipIndex >= items.Length) continue;

                    var inv = items[slot.EquipIndex];

                    // Visible item = the glamour override when present, else the real item.
                    // Use BaseItemId, not ItemId: equipped HQ/collectible items carry an id
                    // offset (+1,000,000 / +500,000) that has no row in the Item sheet, which
                    // would make every HQ slot resolve to nothing. Glamour ids are already
                    // base, and NQ/HQ share an appearance, so BaseItemId is also the right key.
                    var visibleId = inv.GlamourId != 0 ? inv.GlamourId : inv.BaseItemId;
                    if (visibleId == 0) continue; // empty slot

                    // Rendered model + dyes from the draw data — the game's already-collapsed
                    // result: glamour/plate appearance when glamoured, raw colour when not.
                    var (model, stain0, stain1) = ReadDrawModel(chara, slot.Key);

                    // Model id 0 renders nothing on screen — an empty/hidden slot, an empty
                    // off-hand, or an "invisible" glamour (e.g. The Emperor's New …). That isn't
                    // visible gear, so skip it — exactly the same outcome as an empty off-hand.
                    if (model == 0) continue;

                    var row = itemSheet.GetRow(visibleId);
                    if (row.RowId == 0) continue;

                    var name = row.Name.ExtractText();

                    var (color0, dyeName0) = ResolveStain(stainSheet, stain0);
                    var (color1, dyeName1) = ResolveStain(stainSheet, stain1);

                    // Informational labels (from the visible item, like its name/icon).
                    var category = row.ItemUICategory.ValueNullable?.Name.ExtractText() ?? string.Empty;
                    var tags = BuildTags(row.IsUnique);
                    var levels = BuildLevels(row.LevelEquip, row.LevelItem.RowId);

                    result.Add(new GearSlotData(
                        slot,
                        visibleId,
                        row.Icon,
                        name,
                        row.Rarity,
                        stain0,
                        stain1,
                        color0,
                        color1,
                        dyeName0,
                        dyeName1,
                        category,
                        tags,
                        levels));
                }

                if (result.Count == 0 && items.Length > 0)
                    Plugin.Logger?.Debug($"Gear read: {items.Length} equipped items, 0 resolved.");
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Gear read failed: {ex.Message}");
                return null; // unreliable read → hold the last published state
            }

            return result;
        }

        /// <summary>Reads the rendered primary model id and the two dye-channel ids for a slot
        /// from the draw data. A model id of 0 means the slot draws nothing (empty or invisible).</summary>
        private static unsafe (ushort Model, byte Stain0, byte Stain1) ReadDrawModel(Character* chara, string slotKey)
        {
            switch (slotKey)
            {
                case "MAINHAND":
                {
                    var m = chara->DrawData.Weapon(DrawDataContainer.WeaponSlot.MainHand).ModelId;
                    return (m.Id, m.Stain0, m.Stain1);
                }
                case "OFFHAND":
                {
                    var o = chara->DrawData.Weapon(DrawDataContainer.WeaponSlot.OffHand).ModelId;
                    return (o.Id, o.Stain0, o.Stain1);
                }
                default:
                {
                    if (!TryEquipSlot(slotKey, out var es)) return (0, 0, 0);
                    var e = chara->DrawData.Equipment(es);
                    return (e.Id, e.Stain0, e.Stain1);
                }
            }
        }

        private static bool TryEquipSlot(string key, out DrawDataContainer.EquipmentSlot slot)
        {
            slot = key switch
            {
                "HEAD"   => DrawDataContainer.EquipmentSlot.Head,
                "BODY"   => DrawDataContainer.EquipmentSlot.Body,
                "HANDS"  => DrawDataContainer.EquipmentSlot.Hands,
                "LEGS"   => DrawDataContainer.EquipmentSlot.Legs,
                "FEET"   => DrawDataContainer.EquipmentSlot.Feet,
                "EARS"   => DrawDataContainer.EquipmentSlot.Ears,
                "NECK"   => DrawDataContainer.EquipmentSlot.Neck,
                "WRISTS" => DrawDataContainer.EquipmentSlot.Wrists,
                "RINGR"  => DrawDataContainer.EquipmentSlot.RFinger,
                "RINGL"  => DrawDataContainer.EquipmentSlot.LFinger,
                _        => (DrawDataContainer.EquipmentSlot)0xFF,
            };
            return (byte)slot <= 9;
        }

        // Resolved here (on the framework thread) so the publish worker never touches sheets.
        // Returns the BGR-packed colour and the dye name; (0, "") when undyed.
        private static (uint Color, string Name) ResolveStain(Lumina.Excel.ExcelSheet<Stain>? stainSheet, byte stainId)
        {
            if (stainId == 0 || stainSheet == null) return (0u, string.Empty);
            var row = stainSheet.GetRow(stainId);
            return row.RowId == 0 ? (0u, string.Empty) : (row.Color, row.Name.ExtractText());
        }

        // Only "Unique" carries signal — nearly every item is untradable, so that tag is just noise.
        private static string BuildTags(bool unique) => unique ? "Unique" : string.Empty;

        private enum BonusKind { Facewear, Fashion }

        // Facewear (Dawntrail glasses) and the active fashion accessory (ornament) are
        // "bonus" cosmetics: their ids live on the character object and resolve against
        // their own sheets, not the Item sheet. They carry only an icon + name — no
        // rarity tier, dyes, or levels — so those fields are left empty/neutral.
        private static unsafe void TryAddBonus(List<GearSlotData> result, GlamSlot slot,
            Character* chara, BonusKind kind)
        {
            if (chara == null) return;

            uint id, iconId;
            string name, category;

            if (kind == BonusKind.Facewear)
            {
                id = chara->DrawData.GlassesIds[0];
                if (id == 0) return;
                var sheet = Plugin.DataManager.GetExcelSheet<Glasses>();
                if (sheet == null) return;
                var row = sheet.GetRow(id);
                if (row.RowId == 0) return;
                name = row.Name.ExtractText();
                iconId = (uint)row.Icon;
                category = "Facewear";
            }
            else
            {
                id = chara->OrnamentData.OrnamentId;
                if (id == 0) return;
                var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Ornament>();
                if (sheet == null) return;
                var row = sheet.GetRow(id);
                if (row.RowId == 0) return;
                name = row.Singular.ExtractText();
                iconId = (uint)row.Icon;
                category = "Fashion Accessory";
            }

            if (string.IsNullOrEmpty(name)) return;

            result.Add(new GearSlotData(
                slot, id, iconId, name, 1,
                0, 0, 0u, 0u, string.Empty, string.Empty,
                category, string.Empty, string.Empty));
        }

        // "Lv. {equip} · Ilvl {item}" — each part is shown only when it carries signal (> 1).
        // An equip level of 0/1 (no real requirement) or an item level of 1 (cosmetic/glamour
        // pieces) is a default that means nothing, so it's suppressed; "" when neither applies.
        private static string BuildLevels(byte equipLevel, uint itemLevel)
        {
            var parts = new List<string>(2);
            if (equipLevel > 1) parts.Add($"Lv. {equipLevel}");
            if (itemLevel > 1) parts.Add($"Ilvl {itemLevel}");
            return string.Join(" · ", parts);
        }
    }
}
