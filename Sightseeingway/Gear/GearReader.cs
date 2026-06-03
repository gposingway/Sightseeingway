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
                    var visibleId = inv.GlamourId != 0 ? inv.GlamourId : inv.ItemId;
                    if (visibleId == 0) continue; // empty slot

                    var row = itemSheet.GetRow(visibleId);
                    if (row.RowId == 0) continue;

                    var stains = inv.Stains;
                    byte stain0 = stains.Length > 0 ? stains[0] : (byte)0;
                    byte stain1 = stains.Length > 1 ? stains[1] : (byte)0;

                    var name = row.Name.ExtractText();

                    result.Add(new GearSlotData(
                        slot,
                        visibleId,
                        row.Icon,
                        name,
                        row.Rarity,
                        stain0,
                        stain1,
                        ResolveStainColor(stainSheet, stain0),
                        ResolveStainColor(stainSheet, stain1)));
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Gear read failed: {ex.Message}");
            }

            return result;
        }

        // Resolved here (on the framework thread) so the publish worker never touches sheets.
        private static uint ResolveStainColor(Lumina.Excel.ExcelSheet<Stain>? stainSheet, byte stainId)
        {
            if (stainId == 0 || stainSheet == null) return 0;
            var row = stainSheet.GetRow(stainId);
            return row.RowId == 0 ? 0u : row.Color;
        }
    }
}
