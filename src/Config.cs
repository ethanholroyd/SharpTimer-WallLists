using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace SharpTimerWallLists
{
    public class PluginConfig : BasePluginConfig
    {
        [JsonPropertyName("TopCount")]
        public int TopCount { get; set; } = 5;

        [JsonPropertyName("TimeBasedUpdate")]
        public bool TimeBasedUpdate { get; set; } = false;

        [JsonPropertyName("UpdateInterval")]
        public int UpdateInterval { get; set; } = 60;

        [JsonPropertyName("DatabaseType")]
        public int DatabaseType { get; set; } = 1; // 1 = MySQL, 2 = SQLite. 3 = Postgres

        [JsonPropertyName("DatabaseSettings")]
        public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();

        [JsonPropertyName("PointsTitleText")]
        public string PointsTitleText { get; set; } = "|--- Points Leaders ---|";

        [JsonPropertyName("MapsTitleText")]
        public string MapsTitleText { get; set; } = "|---- Map Records ----|";

        [JsonPropertyName("TitleFontSize")]
        public int TitleFontSize { get; set; } = 26;

        [JsonPropertyName("TitleTextScale")]
        public float TitleTextScale { get; set; } = 0.45f;

        [JsonPropertyName("ListFontSize")]
        public int ListFontSize { get; set; } = 24;

        [JsonPropertyName("ListTextScale")]
        public float ListTextScale { get; set; } = 0.35f;

        [JsonPropertyName("MaxNameLength")]
        public int MaxNameLength { get; set; } = 32; // Default value, 32 is max Steam name length

        [JsonPropertyName("TextAlignment")]
        public string TextAlignment { get; set; } = "center";

        [JsonPropertyName("TitleTextColor")]
        public string TitleTextColor { get; set; } = "Magenta";

        [JsonPropertyName("FirstPlaceColor")]
        public string FirstPlaceColor { get; set; } = "Lime";

        [JsonPropertyName("SecondPlaceColor")]
        public string SecondPlaceColor { get; set; } = "Coral";

        [JsonPropertyName("ThirdPlaceColor")]
        public string ThirdPlaceColor { get; set; } = "Cyan";

        [JsonPropertyName("DefaultColor")]
        public string DefaultColor { get; set; } = "White";

        [JsonPropertyName("PointsListCommand")]
        public string PointsListCommand { get; set; } = "pointslist";

        [JsonPropertyName("TimesListCommand")]
        public string TimesListCommand { get; set; } = "timeslist";

        [JsonPropertyName("RemoveListCommand")]
        public string RemoveListCommand { get; set; } = "removelist";

        [JsonPropertyName("CommandPermission")]
        public string CommandPermission { get; set; } = "@css/root";

        [JsonPropertyName("ConfigVersion")]
        public override int Version { get; set; } = 2;
    }

    public sealed class DatabaseSettings
    {
        [JsonPropertyName("host")]
        public string Host { get; set; } = "localhost";

        [JsonPropertyName("username")]
        public string Username { get; set; } = "root";

        [JsonPropertyName("database")]
        public string Database { get; set; } = "database";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "password";

        [JsonPropertyName("port")]
        public int Port { get; set; } = 3306;

        [JsonPropertyName("sslmode")]
        public string Sslmode { get; set; } = "none";

        [JsonPropertyName("table-prefix")]
        public string TablePrefix { get; set; } = "";
    }
}
