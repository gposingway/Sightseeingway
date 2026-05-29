using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System;

namespace Sightseeingway
{
    /// <summary>
    /// Wrappers around live game state (weather, Eorzea time).
    /// </summary>
    public static class Client
    {
        public static unsafe string GetCurrentWeatherName()
        {
            var weatherManager = WeatherManager.Instance();
            if (weatherManager == null) return "Unknown Weather";

            var currentWeatherId = weatherManager->GetCurrentWeather();
            var weatherSheet = Plugin.DataManager?.GetExcelSheet<Weather>();
            if (weatherSheet == null) return "Unknown Weather";

            var currentWeatherRow = weatherSheet.GetRow(currentWeatherId);
            var currentWeatherName = currentWeatherRow.Name.ExtractText();
            return string.IsNullOrEmpty(currentWeatherName) ? "Unknown Weather" : currentWeatherName;
        }

        /// <summary>
        /// The map's most specific live place name where the player stands —
        /// the landmark sub-area (e.g. "Summerford Farms"), else the parent area
        /// (e.g. "Summerford"), else null when in unnamed ground. Used to preview
        /// the filename; the snapshot path resolves the same data in StateCapture.
        /// </summary>
        public static unsafe string? GetCurrentLandmarkName()
        {
            var info = FFXIVClientStructs.FFXIV.Client.Game.UI.TerritoryInfo.Instance();
            if (info == null) return null;

            var placeNames = Plugin.DataManager?.GetExcelSheet<PlaceName>();
            if (placeNames == null) return null;

            string? Resolve(uint id)
            {
                if (id == 0) return null;
                var row = placeNames.GetRow(id);
                if (row.RowId == 0) return null;
                var name = row.Name.ExtractText();
                return string.IsNullOrEmpty(name) ? null : name;
            }

            return Resolve(info->SubAreaPlaceNameId) ?? Resolve(info->AreaPlaceNameId);
        }

        public static DateTime GetCurrentEorzeaDateTime()
        {
            try
            {
                if (Plugin.ObjectTable.LocalPlayer == null) return DateTime.MinValue;

                // 175 real seconds per Eorzean hour, so 1 real second = 3600/175 Eorzean seconds.
                const double EorzeaMultiplier = 3600.0 / 175.0;
                var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var eorzeaTimestamp = unixTimestamp * EorzeaMultiplier;

                return DateTimeOffset.FromUnixTimeSeconds((long)eorzeaTimestamp).DateTime;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
