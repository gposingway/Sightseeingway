using Dalamud.Game.ClientState.Conditions;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using Sightseeingway.Metadata;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sightseeingway.Metadata
{
    /// <summary>
    /// Reads live game state and produces an immutable <see cref="StateSnapshot"/>.
    ///
    /// Must be invoked from the framework thread — it touches Dalamud services
    /// that are not safe to read from arbitrary threads. Once the snapshot is
    /// returned, every value it holds is captured by-value and can be read
    /// freely from any thread.
    /// </summary>
    public static class StateCapture
    {
        public static StateSnapshot Capture(Guid correlationId)
        {
            var character = TryCaptureCharacter();
            var location = TryCaptureLocation();
            var weather = TryCaptureWeather();
            var time = TryCaptureTime();
            var shader = TryCaptureShader();
            var display = TryCaptureDisplay();
            var flags = CaptureFlags();

            return new StateSnapshot
            {
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow,
                Character = character,
                Location = location,
                Weather = weather,
                Time = time,
                Shader = shader,
                Display = display,
                Flags = flags,
            };
        }

        private static CharacterInfo? TryCaptureCharacter()
        {
            try
            {
                var player = Plugin.ObjectTable.LocalPlayer;
                if (player == null) return null;

                var name = player.Name.TextValue;
                if (string.IsNullOrEmpty(name)) return null;

                WorldInfo? world = null;
                try
                {
                    var current = player.CurrentWorld.Value.Name.ExtractText();
                    var home = player.HomeWorld.Value.Name.ExtractText();
                    if (!string.IsNullOrEmpty(current) || !string.IsNullOrEmpty(home))
                        world = new WorldInfo(NullIfEmpty(current), NullIfEmpty(home));
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.Debug($"World capture failed: {ex.Message}");
                }

                JobInfo? job = null;
                try
                {
                    var classJob = player.ClassJob.Value;
                    var jobName = classJob.Name.ExtractText();
                    if (!string.IsNullOrEmpty(jobName))
                        job = new JobInfo(classJob.RowId, jobName, player.Level);
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.Debug($"Job capture failed: {ex.Message}");
                }

                return new CharacterInfo
                {
                    Name = name,
                    World = world,
                    Job = job,
                };
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Character capture failed: {ex.Message}");
                return null;
            }
        }

        private static LocationInfo? TryCaptureLocation()
        {
            try
            {
                var mapId = Plugin.ClientState.MapId;
                if (mapId == 0) return null;

                var mapSheet = Plugin.DataManager.GetExcelSheet<Map>();
                if (mapSheet == null) return null;

                var mapRow = mapSheet.GetRow(mapId);
                if (mapRow.RowId == 0) return null;

                var mapName = mapRow.PlaceName.Value.Name.ExtractText();
                NamedId? mapNamed = !string.IsNullOrEmpty(mapName)
                    ? new NamedId(mapId, mapName)
                    : null;

                NamedId? territoryNamed = null;
                try
                {
                    var territoryId = Plugin.ClientState.TerritoryType;
                    if (territoryId != 0)
                    {
                        var territorySheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
                        var territoryRow = territorySheet?.GetRow(territoryId);
                        var territoryName = territoryRow?.PlaceName.Value.Name.ExtractText();
                        if (!string.IsNullOrEmpty(territoryName))
                            territoryNamed = new NamedId(territoryId, territoryName);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.Debug($"Territory capture failed: {ex.Message}");
                }

                Position? position = null;
                try
                {
                    var player = Plugin.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        var playerPos = player.Position;
                        var mapVector = MapUtil.WorldToMap(
                            playerPos, mapRow.OffsetX, mapRow.OffsetY, 0, mapRow.SizeFactor);
                        position = new Position(
                            (float)Math.Round(mapVector.X, 1),
                            (float)Math.Round(mapVector.Y, 1),
                            (float)Math.Round(mapVector.Z, 1));
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.Debug($"Position capture failed: {ex.Message}");
                }

                var (area, subArea) = TryCaptureSubArea();

                if (mapNamed == null && territoryNamed == null && position == null
                    && area == null && subArea == null)
                    return null;

                return new LocationInfo(territoryNamed, mapNamed, position, area, subArea);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Location capture failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads the map's live breadcrumb tiers below the zone from the game's
        /// <c>TerritoryInfo</c> singleton: Area (mid tier, e.g. "Summerford") and
        /// SubArea (landmark, e.g. "Summerford Farms"). Both are commonly absent —
        /// the IDs are 0 over open ground and in most instances — so absence is the
        /// normal case, not an error. Must run on the framework thread (same context
        /// as <see cref="TryCaptureWeather"/>).
        /// </summary>
        private static unsafe (NamedId? Area, NamedId? SubArea) TryCaptureSubArea()
        {
            try
            {
                var info = FFXIVClientStructs.FFXIV.Client.Game.UI.TerritoryInfo.Instance();
                if (info == null) return (null, null);

                var placeNames = Plugin.DataManager.GetExcelSheet<PlaceName>();
                if (placeNames == null) return (null, null);

                NamedId? Resolve(uint id)
                {
                    if (id == 0) return null;
                    var row = placeNames.GetRow(id);
                    if (row.RowId == 0) return null;
                    var name = row.Name.ExtractText();
                    return string.IsNullOrEmpty(name) ? null : new NamedId(id, name);
                }

                var area = Resolve(info->AreaPlaceNameId);
                var subArea = Resolve(info->SubAreaPlaceNameId);
                return (area, subArea);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"SubArea capture failed: {ex.Message}");
                return (null, null);
            }
        }

        private static unsafe NamedId? TryCaptureWeather()
        {
            try
            {
                var weatherManager = FFXIVClientStructs.FFXIV.Client.Game.WeatherManager.Instance();
                if (weatherManager == null) return null;

                var id = weatherManager->GetCurrentWeather();
                var sheet = Plugin.DataManager.GetExcelSheet<Weather>();
                if (sheet == null) return null;

                var row = sheet.GetRow(id);
                var name = row.Name.ExtractText();
                if (string.IsNullOrEmpty(name)) return null;

                return new NamedId(id, name);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Weather capture failed: {ex.Message}");
                return null;
            }
        }

        private static TimeInfo? TryCaptureTime()
        {
            try
            {
                var eorzea = Client.GetCurrentEorzeaDateTime();
                if (eorzea == DateTime.MinValue) return null;

                var period = eorzea.DetermineDayPeriod();
                return new TimeInfo(new EorzeaTime(period, eorzea.Hour));
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Time capture failed: {ex.Message}");
                return null;
            }
        }

        private static ShaderInfo? TryCaptureShader()
        {
            try
            {
                if (!IO.EffectsEnabled) return null;

                var preset = IO.CurrentShadingwayState?.Preset;
                if (preset == null || string.IsNullOrEmpty(preset.Name)) return null;

                return new ShaderInfo(
                    Collection: NullIfEmpty(preset.Collection),
                    Preset: preset.Name,
                    Path: NullIfEmpty(preset.Path));
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Shader capture failed: {ex.Message}");
                return null;
            }
        }

        private static DisplayInfo? TryCaptureDisplay()
        {
            try
            {
                var display = IO.CurrentShadingwayState?.Display;
                if (display == null || display.Width <= 0 || display.Height <= 0) return null;

                return new DisplayInfo(
                    Width: display.Width,
                    Height: display.Height,
                    AspectRatio: Math.Round(display.AspectRatio, 3),
                    ScreenType: NullIfEmpty(display.ScreenType));
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Display capture failed: {ex.Message}");
                return null;
            }
        }

        private static IReadOnlyList<string> CaptureFlags()
        {
            var flags = new List<string>(4);
            try
            {
                var clientState = Plugin.ClientState;
                var condition = Plugin.Condition;

                var inGpose = clientState.IsGPosing;
                var inCutscene = condition?[ConditionFlag.WatchingCutscene] == true ||
                                 condition?[ConditionFlag.WatchingCutscene78] == true ||
                                 condition?[ConditionFlag.OccupiedInCutSceneEvent] == true;

                if (inGpose) flags.Add("gpose");
                else if (inCutscene) flags.Add("cutscene");
                else flags.Add("gameplay");

                if (clientState.IsPvP) flags.Add("pvp");

                if (condition != null)
                {
                    if (condition[ConditionFlag.Mounted] || condition[ConditionFlag.RidingPillion])
                        flags.Add("mounted");
                    if (condition[ConditionFlag.Swimming]) flags.Add("swimming");
                    if (condition[ConditionFlag.Diving]) flags.Add("swimming");
                    if (condition[ConditionFlag.InFlight]) flags.Add("flying");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Flag capture failed: {ex.Message}");
            }

            return flags;
        }

        private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
    }
}
