using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SleeperCheck", "ScarDev", "1.0.0")]
    [Description("Finds pairs of sleepers who are sleeping close together inside the same building and outside safe zones.")]
    public class SleeperCheck : RustPlugin
    {
        private const string PermUse = "sleepercheck.use";

        private Configuration _config;

        #region Config

        private class Configuration
        {
            public float SleeperProximityMeters = 6f;
            public float BuildingSearchRadiusMeters = 5f;
            public int MaxResultsToPrint = 100;
            public bool RequireSameBuildingId = true;
            public bool IncludeGridIfGridApiPresent = true;
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                    throw new Exception("Config was null.");
            }
            catch
            {
                PrintWarning("Configuration file is invalid; using defaults.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
        }

        #endregion

        #region Commands

        [ChatCommand("sleepercheck")]
        private void CmdSleeperCheck(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player))
            {
                ReplyToPlayer(player, "You do not have permission to use this command.");
                return;
            }

            RunSleeperCheck(
                msg => ReplyToPlayer(player, msg),
                msg => ReplyToPlayer(player, msg)
            );
        }

        [ConsoleCommand("sleepercheck")]
        private void CcmdSleeperCheck(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (player != null && !HasAccess(player))
            {
                ReplyToConsole(arg, "You do not have permission to use this command.");
                return;
            }

            RunSleeperCheck(
                msg => ReplyToConsole(arg, msg),
                msg => ReplyToConsole(arg, msg)
            );
        }

        #endregion

        #region Core

        private class SleeperInfo
        {
            public BasePlayer Player;
            public ulong UserId;
            public string Name;
            public Vector3 Position;
            public uint BuildingId;
            public string Grid;
        }

        private class SleeperPair
        {
            public SleeperInfo A;
            public SleeperInfo B;
            public float Distance;
        }

        private void RunSleeperCheck(Action<string> reply, Action<string> replyLine)
        {
            var sleepers = GatherSleepers();

            if (sleepers.Count < 2)
            {
                reply("SleeperCheck: fewer than 2 qualifying sleepers found.");
                return;
            }

            var matches = FindMatches(sleepers);

            reply($"SleeperCheck: scanned {sleepers.Count} qualifying sleepers. Found {matches.Count} matching pair(s).");

            if (matches.Count == 0)
                return;

            int printed = 0;
            foreach (var match in matches)
            {
                if (printed >= _config.MaxResultsToPrint)
                {
                    reply($"SleeperCheck: output truncated at {_config.MaxResultsToPrint} results.");
                    break;
                }

                var aGrid = string.IsNullOrWhiteSpace(match.A.Grid) ? "n/a" : match.A.Grid;
                var bGrid = string.IsNullOrWhiteSpace(match.B.Grid) ? "n/a" : match.B.Grid;

                replyLine(
                    $"[{printed + 1}] " +
                    $"{match.A.Name} ({match.A.UserId}) [{aGrid}] <-> " +
                    $"{match.B.Name} ({match.B.UserId}) [{bGrid}] | " +
                    $"Distance: {match.Distance:0.00}m | " +
                    $"BuildingID: {match.A.BuildingId}"
                );

                printed++;
            }
        }

        private List<SleeperInfo> GatherSleepers()
        {
            var result = new List<SleeperInfo>();
            var seen = new HashSet<ulong>();

            foreach (var sleeper in BasePlayer.sleepingPlayerList)
            {
                if (sleeper == null || !sleeper.IsValid())
                    continue;

                if (sleeper.userID == 0 || !seen.Add(sleeper.userID))
                    continue;

                if (IsInSafeZone(sleeper))
                    continue;

                uint buildingId = GetNearbyBuildingId(sleeper.transform.position);
                if (buildingId == 0)
                    continue;

                result.Add(new SleeperInfo
                {
                    Player = sleeper,
                    UserId = sleeper.userID,
                    Name = sleeper.displayName ?? sleeper.userID.ToString(),
                    Position = sleeper.transform.position,
                    BuildingId = buildingId,
                    Grid = TryGetGrid(sleeper.transform.position)
                });
            }

            return result;
        }

        private List<SleeperPair> FindMatches(List<SleeperInfo> sleepers)
        {
            var matches = new List<SleeperPair>();
            float maxDistance = Mathf.Max(0.1f, _config.SleeperProximityMeters);

            for (int i = 0; i < sleepers.Count; i++)
            {
                var a = sleepers[i];

                for (int j = i + 1; j < sleepers.Count; j++)
                {
                    var b = sleepers[j];

                    if (_config.RequireSameBuildingId && (a.BuildingId == 0 || b.BuildingId == 0 || a.BuildingId != b.BuildingId))
                        continue;

                    float distance = Vector3.Distance(a.Position, b.Position);
                    if (distance > maxDistance)
                        continue;

                    matches.Add(new SleeperPair
                    {
                        A = a,
                        B = b,
                        Distance = distance
                    });
                }
            }

            return matches
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.A.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.B.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        #endregion

        #region Helpers

        private bool HasAccess(BasePlayer player)
        {
            if (player == null)
                return true;

            return player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermUse);
        }

        private bool IsInSafeZone(BasePlayer player)
        {
            if (player == null)
                return false;

            try
            {
                return player.InSafeZone();
            }
            catch
            {
                return false;
            }
        }

        private uint GetNearbyBuildingId(Vector3 position)
        {
            var entities = Pool.GetList<BaseEntity>();
            try
            {
                Vis.Entities(position, _config.BuildingSearchRadiusMeters, entities, Rust.Layers.Mask.Construction);

                float bestDistance = float.MaxValue;
                uint bestBuildingId = 0;

                foreach (var entity in entities)
                {
                    if (entity == null || !entity.IsValid())
                        continue;

                    var block = entity as BuildingBlock;
                    if (block == null)
                        continue;

                    if (block.buildingID == 0)
                        continue;

                    float dist = Vector3.Distance(position, block.transform.position);
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestBuildingId = block.buildingID;
                    }
                }

                return bestBuildingId;
            }
            finally
            {
                Pool.FreeList(ref entities);
            }
        }

        private string TryGetGrid(Vector3 position)
        {
            if (!_config.IncludeGridIfGridApiPresent)
                return null;

            var gridApi = plugins.Find("GridAPI");
            if (gridApi == null)
                return null;

            try
            {
                var result = gridApi.Call("GetGrid", position) as string[];
                if (result == null || result.Length < 2)
                    return null;

                return $"{result[0]}{result[1]}";
            }
            catch
            {
                return null;
            }
        }

        private void ReplyToPlayer(BasePlayer player, string message)
        {
            if (player == null)
                return;

            SendReply(player, message);
        }

        private void ReplyToConsole(ConsoleSystem.Arg arg, string message)
        {
            if (arg == null)
                return;

            arg.ReplyWith(message);
        }

        #endregion
    }
}
