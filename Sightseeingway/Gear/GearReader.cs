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
        public static unsafe IReadOnlyList<GearSlotData> ReadVisibleGear()
        {
            var result = new List<GearSlotData>(GlamSlots.All.Count);

            try
            {
                var items = Plugin.GameInventory.GetInventoryItems(GameInventoryType.EquippedItems);
                if (items.Length == 0) return result;

                var itemSheet = Plugin.DataManager.GetExcelSheet<Item>();
                if (itemSheet == null) return result;

                var stainSheet = Plugin.DataManager.GetExcelSheet<Stain>();

                var player = Plugin.ObjectTable.LocalPlayer;
                var chara = player != null ? (Character*)player.Address : null;

                foreach (var slot in GlamSlots.All)
                {
                    if (slot.EquipIndex >= items.Length) continue;

                    var inv = items[slot.EquipIndex];

                    // Visible item = the glamour override when present, else the real item.
                    // Use BaseItemId, not ItemId: equipped HQ/collectible items carry an id
                    // offset (+1,000,000 / +500,000) that has no row in the Item sheet, which
                    // would make every HQ slot resolve to nothing. Glamour ids are already
                    // base, and NQ/HQ share an appearance, so BaseItemId is also the right key.
                    var visibleId = inv.GlamourId != 0 ? inv.GlamourId : inv.BaseItemId;
                    if (visibleId == 0) continue; // empty slot

                    var row = itemSheet.GetRow(visibleId);
                    if (row.RowId == 0) continue;

                    // Rendered (visible) dyes from the draw data — the game's already-collapsed
                    // result: glamour/plate colour when glamoured, raw colour when not.
                    byte stain0 = 0, stain1 = 0;
                    if (chara != null) (stain0, stain1) = ReadDrawStains(chara, slot.Key);

                    var name = row.Name.ExtractText();

                    var (color0, dyeName0) = ResolveStain(stainSheet, stain0);
                    var (color1, dyeName1) = ResolveStain(stainSheet, stain1);

                    // Informational labels (from the visible item, like its name/icon).
                    var category = row.ItemUICategory.ValueNullable?.Name.ExtractText() ?? string.Empty;
                    var tags = BuildTags(row.IsUnique, row.IsUntradable);
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
            }

            return result;
        }

        /// <summary>Reads the two rendered dye-channel ids for a slot from the draw data.</summary>
        private static unsafe (byte, byte) ReadDrawStains(Character* chara, string slotKey)
        {
            switch (slotKey)
            {
                case "MAINHAND":
                {
                    var m = chara->DrawData.Weapon(DrawDataContainer.WeaponSlot.MainHand).ModelId;
                    return (m.Stain0, m.Stain1);
                }
                case "OFFHAND":
                {
                    var o = chara->DrawData.Weapon(DrawDataContainer.WeaponSlot.OffHand).ModelId;
                    return (o.Stain0, o.Stain1);
                }
                default:
                {
                    if (!TryEquipSlot(slotKey, out var es)) return (0, 0);
                    var e = chara->DrawData.Equipment(es);
                    return (e.Stain0, e.Stain1);
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

        // "Unique · Untradable" when both apply, a single word when one does, "" when neither.
        private static string BuildTags(bool unique, bool untradable)
        {
            if (unique && untradable) return "Unique · Untradable";
            if (unique) return "Unique";
            if (untradable) return "Untradable";
            return string.Empty;
        }

        // "Lv. {equip} · Ilvl {item}" — drops the equip part for items with no level requirement.
        private static string BuildLevels(byte equipLevel, uint itemLevel)
            => equipLevel > 0 ? $"Lv. {equipLevel} · Ilvl {itemLevel}" : $"Ilvl {itemLevel}";
    }
}
