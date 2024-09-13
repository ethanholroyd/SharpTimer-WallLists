using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using K4WorldTextSharedAPI;
using System.Drawing;
using System.Data.SQLite;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;

namespace SharpTimerWallLists
{
    [MinimumApiVersion(205)]
    public class PluginSharpTimerWallLists : BasePlugin, IPluginConfig<PluginConfig>
    {
        public override string ModuleName => "SharpTimer Wall Lists";
        public override string ModuleAuthor => "Marchand + K4ryuu";
        public override string ModuleVersion => "1.0.1";

        public required PluginConfig Config { get; set; } = new PluginConfig();
        public static PluginCapability<IK4WorldTextSharedAPI> Capability_SharedAPI { get; } = new("k4-worldtext:sharedapi");
        private List<int> _currentPointsList = new();
        private List<int> _currentMapList = new();
        private CounterStrikeSharp.API.Modules.Timers.Timer? _updateTimer;
        private string? _databasePath;
        private string? _connectionString;

        public void OnConfigParsed(PluginConfig config)
        {
            if (config.Version < Config.Version)
                Logger.LogWarning("Configuration version mismatch (Expected: {0} | Current: {1})", Config.Version, config.Version);

            Config = config;
        }

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            InitializeDatabasePathAndConnectionString();

            AddTimer(3, () => LoadWorldTextFromFile(Server.MapName));

            AddCommand($"css_{Config.PointsListCommand}", "Sets up the points list", OnPointsListAdd);
            AddCommand($"css_{Config.TimesListCommand}", "Sets up the map records list", OnMapListAdd);
            AddCommand($"css_{Config.RemoveListCommand}", "Removes the closest list, whether points or map", OnRemoveList);

            if (Config.TimeBasedUpdate)
            {
                _updateTimer = AddTimer(Config.UpdateInterval, RefreshLists, TimerFlags.REPEAT);
            }

            RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
            {
                RefreshLists();
                return HookResult.Continue;
            });

            RegisterListener<Listeners.OnMapStart>((mapName) =>
            {
                AddTimer(1, () => LoadWorldTextFromFile(mapName));
            });

            RegisterListener<Listeners.OnMapEnd>(() =>
            {
                var checkAPI = Capability_SharedAPI.Get();
                if (checkAPI != null)
                {
                    _currentPointsList.ForEach(id => checkAPI.RemoveWorldText(id, false));
                    _currentMapList.ForEach(id => checkAPI.RemoveWorldText(id, false));
                }
                _currentPointsList.Clear();
                _currentMapList.Clear();
            });
        }

        public override void Unload(bool hotReload)
        {
            var checkAPI = Capability_SharedAPI.Get();
            if (checkAPI != null)
            {
                _currentPointsList.ForEach(id => checkAPI.RemoveWorldText(id, false));
                _currentMapList.ForEach(id => checkAPI.RemoveWorldText(id, false));
            }
            _currentPointsList.Clear();
            _currentMapList.Clear();
            _updateTimer?.Kill();
        }

        private void InitializeDatabasePathAndConnectionString()
        {
            var dbSettings = Config.DatabaseSettings;
            if (Config.DatabaseType == 1)
            {
                var mySqlSslMode = dbSettings.Sslmode.ToLower() switch
                {
                    "none" => MySqlSslMode.None,
                    "preferred" => MySqlSslMode.Preferred,
                    "required" => MySqlSslMode.Required,
                    "verifyca" => MySqlSslMode.VerifyCA,
                    "verifyfull" => MySqlSslMode.VerifyFull,
                    _ => MySqlSslMode.None
                };
                _connectionString = $@"Server={dbSettings.Host};Port={dbSettings.Port};Database={dbSettings.Database};Uid={dbSettings.Username};Pwd={dbSettings.Password};SslMode={mySqlSslMode};";
            }
            else if (Config.DatabaseType == 2)
            {
                _databasePath = Path.Combine(Server.GameDirectory, "csgo", "cfg", "SharpTimer", "database.db");
                _connectionString = $"Data Source={_databasePath};Version=3;";
            }
            else if (Config.DatabaseType == 3)
            {
                var npgSqlSslMode = dbSettings.Sslmode.ToLower() switch
                {
                    "disable" => SslMode.Disable,
                    "require" => SslMode.Require,
                    "prefer" => SslMode.Prefer,
                    "allow" => SslMode.Allow,
                    "verify-full" => SslMode.VerifyFull,
                    _ => SslMode.Disable
                };
                _connectionString = $"Host={dbSettings.Host};Port={dbSettings.Port};Database={dbSettings.Database};Username={dbSettings.Username};Password={dbSettings.Password};SslMode={npgSqlSslMode};";
            }
        }

        [ConsoleCommand($"css_{DefaultCommandNames.PointsListCommand}", "Sets up the points list")]
        [RequiresPermissions($"{DefaultCommandNames.CommandPermission}")]
        public void OnPointsListAdd(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null || command == null) return;
            CreateTopList(player, command, ListType.Points);
        }

        [ConsoleCommand($"css_{DefaultCommandNames.TimesListCommand}", "Sets up the map top list")]
        [RequiresPermissions($"{DefaultCommandNames.CommandPermission}")]
        public void OnMapListAdd(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null || command == null) return;
            CreateTopList(player, command, ListType.Maps);
        }

        [ConsoleCommand($"css_{DefaultCommandNames.RemoveListCommand}", "Removes the closest list, whether points or map")]
        [RequiresPermissions($"{DefaultCommandNames.CommandPermission}")]
        public void OnRemoveList(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null || command == null) return;
            RemoveClosestList(player, command);
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                Server.NextWorldUpdate(() => RemoveClosestList(player, command));
            });
        }

        private void CreateTopList(CCSPlayerController player, CommandInfo command, ListType listType)
        {
            var checkAPI = Capability_SharedAPI.Get();
            if (checkAPI is null)
            {
                command.ReplyToCommand($" {ChatColors.Purple}[{ChatColors.Red}{listType}WallLists{ChatColors.Purple}] {ChatColors.LightRed}Failed to get the shared API.");
                return;
            }

            var mapName = Server.MapName;

            Task.Run(async () =>
            {
                try
                {
                    var topList = await GetTopPlayersAsync(Config.TopCount, listType, mapName);
                    var linesList = GetTopListTextLines(topList, listType);

                    Server.NextWorldUpdate(() =>
                    {
                        try
                        {
                            int messageID = checkAPI.AddWorldTextAtPlayer(player, TextPlacement.Wall, linesList);
                            if (listType == ListType.Points)
                                _currentPointsList.Add(messageID);
                            if (listType == ListType.Maps)
                                _currentMapList.Add(messageID);

                            var lineList = checkAPI.GetWorldTextLineEntities(messageID);
                            if (lineList?.Count > 0)
                            {
                                var location = lineList[0]?.AbsOrigin;
                                var rotation = lineList[0]?.AbsRotation;

                                if (location != null && rotation != null)
                                {
                                    SaveWorldTextToFile(location, rotation, listType);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error during NextWorldUpdate for CreateTopList.");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error creating wall list in CreateTopList method.");
                }
            });
        }

        private void RemoveClosestList(CCSPlayerController player, CommandInfo command)
        {
            var checkAPI = Capability_SharedAPI.Get();
            if (checkAPI is null)
            {
                command.ReplyToCommand($" {ChatColors.Purple}[{ChatColors.LightPurple}WallLists{ChatColors.Purple}] {ChatColors.LightRed}Failed to get the shared API.");
                return;
            }

            var combinedList = _currentPointsList.Concat(_currentMapList).ToList();

            var target = combinedList
                .SelectMany(id => checkAPI.GetWorldTextLineEntities(id)?.Select(entity => new { Id = id, Entity = entity, IsPointsList = _currentPointsList.Contains(id) }) ?? Enumerable.Empty<dynamic>())
                .Where(x => x.Entity.AbsOrigin != null && player.PlayerPawn.Value?.AbsOrigin != null && DistanceTo(x.Entity.AbsOrigin, player.PlayerPawn.Value!.AbsOrigin) < 100)
                .OrderBy(x => x.Entity.AbsOrigin != null && player.PlayerPawn.Value?.AbsOrigin != null ? DistanceTo(x.Entity.AbsOrigin, player.PlayerPawn.Value!.AbsOrigin) : float.MaxValue)
                .FirstOrDefault();

            if (target is null)
            {
                command.ReplyToCommand($" {ChatColors.Purple}[{ChatColors.LightPurple}WallLists{ChatColors.Purple}] {ChatColors.Red}Move closer to the list that you want to remove.");
                return;
            }

            try
            {
                checkAPI.RemoveWorldText(target.Id, false);
                if (target.IsPointsList)
                {
                    _currentPointsList.Remove(target.Id);
                }
                else
                {
                    _currentMapList.Remove(target.Id);
                }

                var mapName = Server.MapName;
                var mapsDirectory = Path.Combine(ModuleDirectory, "maps");

                var path = target.IsPointsList
                    ? Path.Combine(mapsDirectory, $"{mapName}_pointslist.json")
                    : Path.Combine(mapsDirectory, $"{mapName}_timeslist.json");

                if (File.Exists(path))
                {
                    var data = JsonSerializer.Deserialize<List<WorldTextData>>(File.ReadAllText(path));
                    if (data != null)
                    {
                        Vector entityVector = target.Entity.AbsOrigin;
                        data.RemoveAll(x =>
                        {
                            Vector location = ParseVector(x.Location);
                            return location.X == entityVector.X &&
                                location.Y == entityVector.Y &&
                                x.Rotation == target.Entity.AbsRotation.ToString();
                        });

                        var options = new JsonSerializerOptions
                        {
                            WriteIndented = true
                        };

                        string jsonString = JsonSerializer.Serialize(data, options);
                        File.WriteAllText(path, jsonString);
                    }
                }
                command.ReplyToCommand($" {ChatColors.Purple}[{ChatColors.LightPurple}WallLists{ChatColors.Purple}] {ChatColors.Green}List removed!");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error removing list in RemoveClosestList method.");
            }
        }

        private float DistanceTo(Vector a, Vector b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private void SaveWorldTextToFile(Vector location, QAngle rotation, ListType listType)
        {
            try
            {
                var mapName = Server.MapName;
                var mapsDirectory = Path.Combine(ModuleDirectory, "maps");
                if (!Directory.Exists(mapsDirectory))
                {
                    Directory.CreateDirectory(mapsDirectory);
                }

                var filename = listType == ListType.Maps ? $"{mapName}_timeslist.json" : $"{mapName}_{listType.ToString().ToLower()}list.json";
                var path = Path.Combine(mapsDirectory, filename);

                var worldTextData = new WorldTextData
                {
                    Location = location.ToString(),
                    Rotation = rotation.ToString()
                };

                List<WorldTextData> data;
                if (File.Exists(path))
                {
                    data = JsonSerializer.Deserialize<List<WorldTextData>>(File.ReadAllText(path)) ?? new List<WorldTextData>();
                }
                else
                {
                    data = new List<WorldTextData>();
                }

                data.Add(worldTextData);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string jsonString = JsonSerializer.Serialize(data, options);
                File.WriteAllText(path, jsonString);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving world text to file in SaveWorldTextToFile method.");
            }
        }

        private void LoadWorldTextFromFile(string path, ListType listType, string mapName)
        {
            if (File.Exists(path))
            {
                var data = JsonSerializer.Deserialize<List<WorldTextData>>(File.ReadAllText(path));
                if (data == null) return;

                Task.Run(async () =>
                {
                    try
                    {
                        var topList = await GetTopPlayersAsync(Config.TopCount, listType, mapName);
                        var linesList = GetTopListTextLines(topList, listType);

                        Server.NextWorldUpdate(() =>
                        {
                            try
                            {
                                var checkAPI = Capability_SharedAPI.Get();
                                if (checkAPI is null) return;

                                foreach (var worldTextData in data)
                                {
                                    if (!string.IsNullOrEmpty(worldTextData.Location) && !string.IsNullOrEmpty(worldTextData.Rotation))
                                    {
                                        var messageID = checkAPI.AddWorldText(TextPlacement.Wall, linesList, ParseVector(worldTextData.Location), ParseQAngle(worldTextData.Rotation));
                                        if (listType == ListType.Points)
                                            _currentPointsList.Add(messageID);
                                        else if (listType == ListType.Maps)
                                            _currentMapList.Add(messageID);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, "Error during NextWorldUpdate in LoadWorldTextFromFile.");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error loading world text from file in LoadWorldTextFromFile method.");
                    }
                });
            }
        }

        private void LoadWorldTextFromFile(string? passedMapName = null)
        {
            var mapName = passedMapName ?? Server.MapName;
            var mapsDirectory = Path.Combine(ModuleDirectory, "maps");

            var pointsPath = Path.Combine(mapsDirectory, $"{mapName}_pointslist.json");
            var mapsPath = Path.Combine(mapsDirectory, $"{mapName}_timeslist.json");

            LoadWorldTextFromFile(pointsPath, ListType.Points, mapName);
            LoadWorldTextFromFile(mapsPath, ListType.Maps, mapName);
        }

        public static Vector ParseVector(string vectorString)
        {
            string[] components = vectorString.Split(' ');
            if (components.Length == 3 &&
                float.TryParse(components[0], out float x) &&
                float.TryParse(components[1], out float y) &&
                float.TryParse(components[2], out float z))
            {
                return new Vector(x, y, z);
            }
            throw new ArgumentException("Invalid vector string format.");
        }

        public static QAngle ParseQAngle(string qangleString)
        {
            string[] components = qangleString.Split(' ');
            if (components.Length == 3 &&
                float.TryParse(components[0], out float x) &&
                float.TryParse(components[1], out float y) &&
                float.TryParse(components[2], out float z))
            {
                return new QAngle(x, y, z);
            }
            throw new ArgumentException("Invalid QAngle string format.");
        }

        private void RefreshLists()
        {
            var mapName = Server.MapName;
            
            Task.Run(async () =>
            {
                try
                {
                    var pointsTopList = await GetTopPlayersAsync(Config.TopCount, ListType.Points, mapName);
                    var mapsTopList = await GetTopPlayersAsync(Config.TopCount, ListType.Maps, mapName);

                    var pointsLinesList = GetTopListTextLines(pointsTopList, ListType.Points);
                    var mapsLinesList = GetTopListTextLines(mapsTopList, ListType.Maps);

                    Server.NextWorldUpdate(() =>
                    {
                        try
                        {
                            var checkAPI = Capability_SharedAPI.Get();
                            if (checkAPI != null)
                            {
                                _currentPointsList.ForEach(id => checkAPI.UpdateWorldText(id, pointsLinesList));
                                _currentMapList.ForEach(id => checkAPI.UpdateWorldText(id, mapsLinesList));
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error during NextWorldUpdate in RefreshLists.");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error refreshing lists in RefreshLists method.");
                }
            });
        }

        private string TruncateString(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }

        private List<TextLine> GetTopListTextLines(List<PlayerPlace> topList, ListType listType)
        {
            Color ParseColor(string colorName)
            {
                try
                {
                    var colorProperty = typeof(Color).GetProperty(colorName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (colorProperty == null)
                    {
                        throw new ArgumentException($"Invalid color name: {colorName}");
                    }

                    var colorValue = colorProperty.GetValue(null);
                    if (colorValue == null)
                    {
                        throw new InvalidOperationException($"Color property '{colorName}' has no value.");
                    }

                    return (Color)colorValue;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, $"Invalid color name: {colorName}. Falling back to White.");
                    return Color.White;
                }
            }

            PointWorldTextJustifyHorizontal_t GetTextAlignment()
            {
                return Config.TextAlignment.ToLower() switch
                {
                    "left" => PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_LEFT,
                    _ => PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER,
                };
            }

            int maxNameLength = Config.MaxNameLength;
            var linesList = new List<TextLine>();

            if (listType == ListType.Points)
            {
                linesList.Add(new TextLine
                {
                    Text = Config.PointsTitleText,
                    Color = ParseColor(Config.TitleTextColor),
                    FontSize = Config.TitleFontSize,
                    FullBright = true,
                    Scale = Config.TitleTextScale,
                    JustifyHorizontal = GetTextAlignment()
                });
            }
            else if (listType == ListType.Maps)
            {
                linesList.Add(new TextLine
                {
                    Text = Config.MapsTitleText,
                    Color = ParseColor(Config.TitleTextColor),
                    FontSize = Config.TitleFontSize,
                    FullBright = true,
                    Scale = Config.TitleTextScale,
                    JustifyHorizontal = GetTextAlignment()
                });
            }

            for (int i = 0; i < topList.Count; i++)
            {
                var topplayer = topList[i];
                var truncatedName = TruncateString(topplayer.PlayerName, maxNameLength);
                var color = i switch
                {
                    0 => ParseColor(Config.FirstPlaceColor),
                    1 => ParseColor(Config.SecondPlaceColor),
                    2 => ParseColor(Config.ThirdPlaceColor),
                    _ => ParseColor(Config.DefaultColor)
                };

                var pointsOrTime = listType == ListType.Points ? topplayer.GlobalPoints.ToString() : topplayer.FormattedTime;
                var lineText = $"{i + 1}. {truncatedName} - {pointsOrTime}";

                linesList.Add(new TextLine
                {
                    Text = lineText,
                    Color = color,
                    FontSize = Config.ListFontSize,
                    FullBright = true,
                    Scale = Config.ListTextScale,
                    JustifyHorizontal = GetTextAlignment()
                });
            }

            return linesList;
        }

        public async Task<List<PlayerPlace>> GetTopPlayersAsync(int topCount, ListType listType, string mapName)
        {
            string query;
            string tablePrefix = Config.DatabaseSettings.TablePrefix;

            if (Config.DatabaseType == 1) // MySQL
            {
                query = listType switch
                {
                    ListType.Points => $@"
                    WITH RankedPlayers AS (
                        SELECT
                            SteamID,
                            PlayerName,
                            GlobalPoints,
                            DENSE_RANK() OVER (ORDER BY GlobalPoints DESC) AS playerPlace
                        FROM {tablePrefix}PlayerStats
                    )
                    SELECT SteamID, PlayerName, GlobalPoints, playerPlace
                    FROM RankedPlayers
                    ORDER BY GlobalPoints DESC
                    LIMIT @TopCount",
                    ListType.Maps => $@"
                    WITH RankedPlayers AS (
                        SELECT
                            SteamID,
                            PlayerName,
                            FormattedTime,
                            DENSE_RANK() OVER (ORDER BY STR_TO_DATE(FormattedTime, '%i:%s.%f') ASC) AS playerPlace
                        FROM {tablePrefix}PlayerRecords
                        WHERE MapName = @MapName
                    )
                    SELECT SteamID, PlayerName, FormattedTime, playerPlace
                    FROM RankedPlayers
                    ORDER BY STR_TO_DATE(FormattedTime, '%i:%s.%f') ASC
                    LIMIT @TopCount",
                    _ => throw new ArgumentException("Invalid list type")
                };

                try
                {
                    using var connection = new MySqlConnection(_connectionString);
                    object parameters = listType switch
                    {
                        ListType.Points => new { TopCount = topCount },
                        ListType.Maps => new { TopCount = topCount, MapName = mapName },
                        _ => throw new ArgumentException("Invalid list type")
                    };

                    return (await connection.QueryAsync<PlayerPlace>(query, parameters)).ToList();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Failed to retrieve top players from MySQL for {listType}, please check your database credentials in the config");
                    return new List<PlayerPlace>();
                }
            }

            else if (Config.DatabaseType == 2) // SQLite
            {
                query = listType switch
                {
                    ListType.Points => $@"
                    WITH RankedPlayers AS (
                        SELECT
                            SteamID,
                            PlayerName,
                            GlobalPoints,
                            DENSE_RANK() OVER (ORDER BY GlobalPoints DESC) AS playerPlace
                        FROM {tablePrefix}PlayerStats
                    )
                    SELECT SteamID, PlayerName, GlobalPoints, playerPlace
                    FROM RankedPlayers
                    ORDER BY GlobalPoints DESC
                    LIMIT @TopCount",
                    ListType.Maps => $@"
                    WITH RankedPlayers AS (
                        SELECT
                            SteamID,
                            PlayerName,
                            FormattedTime,
                            DENSE_RANK() OVER (ORDER BY strftime('%M:%S.%f', FormattedTime) ASC) AS playerPlace
                        FROM {tablePrefix}PlayerRecords
                        WHERE MapName = @MapName
                    )
                    SELECT SteamID, PlayerName, FormattedTime, playerPlace
                    FROM RankedPlayers
                    ORDER BY strftime('%M:%S.%f', FormattedTime) ASC
                    LIMIT @TopCount",
                    _ => throw new ArgumentException("Invalid list type")
                };

                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    connection.Open();
                        object parameters = listType switch
                        {
                            ListType.Points => new { TopCount = topCount },
                            ListType.Maps => new { TopCount = topCount, MapName = mapName },
                            _ => throw new ArgumentException("Invalid list type")
                        };

                        return (await connection.QueryAsync<PlayerPlace>(query, parameters)).ToList();
                }
                catch (Exception)
                {
                    return new List<PlayerPlace>();
                }
            }

            else if (Config.DatabaseType == 3) // PostgreSQL
            {
                query = listType switch
                {
                    ListType.Points => $@"
                    WITH RankedPlayers AS (
                        SELECT
                            ""SteamID"",
                            ""PlayerName"",
                            ""GlobalPoints"",
                            DENSE_RANK() OVER (ORDER BY ""GlobalPoints"" DESC) AS playerPlace
                        FROM ""{tablePrefix}PlayerStats""
                    )
                    SELECT ""SteamID"", ""PlayerName"", ""GlobalPoints"", playerPlace
                    FROM RankedPlayers
                    ORDER BY ""GlobalPoints"" DESC
                    LIMIT @TopCount",
                    ListType.Maps => $@"
                    WITH RankedPlayers AS (
                        SELECT
                            ""SteamID"",
                            ""PlayerName"",
                            ""FormattedTime"",
                            DENSE_RANK() OVER (ORDER BY to_timestamp(""FormattedTime"", 'MI:SS.US') ASC) AS playerPlace
                        FROM ""{tablePrefix}PlayerRecords""
                        WHERE ""MapName"" = @MapName
                    )
                    SELECT ""SteamID"", ""PlayerName"", ""FormattedTime"", playerPlace
                    FROM RankedPlayers
                    ORDER BY to_timestamp(""FormattedTime"", 'MI:SS.US') ASC
                    LIMIT @TopCount",
                    _ => throw new ArgumentException("Invalid list type")
                };

                try
                {
                    using var connection = new NpgsqlConnection(_connectionString);
                    object parameters = listType switch
                    {
                        ListType.Points => new { TopCount = topCount },
                        ListType.Maps => new { TopCount = topCount, MapName = mapName },
                        _ => throw new ArgumentException("Invalid list type")
                    };

                    return (await connection.QueryAsync<PlayerPlace>(query, parameters)).ToList();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Failed to retrieve top players from PostgreSQL for {listType}, please check your database credentials in the config");
                    return new List<PlayerPlace>();
                }
            }
            else
            {
                Logger.LogError("Invalid DatabaseType specified in config");
                return new List<PlayerPlace>();
            }
        }

    }

    public enum ListType
    {
        Points,
        Maps
    }

    public class PlayerPlace
    {
        public required string PlayerName { get; set; }
        public int GlobalPoints { get; set; }
        public string? FormattedTime { get; set; }
        public int Placement { get; set; }
    }

    public static class DefaultCommandNames
    {
        public const string PointsListCommand = "pointslist";
        public const string TimesListCommand = "timeslist";
        public const string RemoveListCommand = "removelist";
        public const string CommandPermission = "@css/root";
    }

    public class WorldTextData
    {
        public required string Location { get; set; }
        public required string Rotation { get; set; }
    }
}