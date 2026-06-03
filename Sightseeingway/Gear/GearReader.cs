using System;
using System.Collections.Generic;
using Dalamud.Game.Inventory;
using Lumina.Excel.Sheets;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// Reads the player's currently VISIBLE gear from the EquippedItems container,
    /// honoring glamour overrides. Must run on the framework thread — it touches
    /// the live inventory; the returned list is plain data and thread-safe to pass on.
    /// </summary>
    public static class GearReader
    {
        public static IReadOnlyList<GearSlotData> ReadVisibleGear()
        {
            var result = new List<GearSlotData>(GlamSlots.All.Count);

            try
            {
                var items = Plugin.GameInventory.GetInventoryItems(GameInventoryType.EquippedItems);
                if (items.Length == 0) return result;

                var itemSheet = Plugin.DataManager.GetExcelSheet<Item>();
                if (itemSheet == null) return result;

                var stainSheet = Plugin.DataManager.GetExcelSheet<Stain>();

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

                    var stains = inv.Stains;
                    byte stain0 = stains.Length > 0 ? stains[0] : (byte)0;
                    byte stain1 = stains.Length > 1 ? stains[1] : (byte)0;

                    var name = row.Name.ExtractText();

                    var (color0, dyeName0) = ResolveStain(stainSheet, stain0);
                    var (color1, dyeName1) = ResolveStain(stainSheet, stain1);

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
                        dyeName1));
                }

                // Diagnostic: equipped items present but nothing resolved usually means an
                // id-offset / sheet-lookup issue worth seeing under /sway debug.
                if (result.Count == 0 && items.Length > 0)
                    Plugin.Logger?.Debug($"Gear read: {items.Length} equipped items, 0 resolved.");
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Gear read failed: {ex.Message}");
            }

            return result;
        }

        // Resolved here (on the framework thread) so the publish worker never touches sheets.
        // Returns the BGR-packed colour and the dye name; (0, "") when undyed.
        private static (uint Color, string Name) ResolveStain(Lumina.Excel.ExcelSheet<Stain>? stainSheet, byte stainId)
        {
            if (stainId == 0 || stainSheet == null) return (0u, string.Empty);
            var row = stainSheet.GetRow(stainId);
            return row.RowId == 0 ? (0u, string.Empty) : (row.Color, row.Name.ExtractText());
        }
    }
}
