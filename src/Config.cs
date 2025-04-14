using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace TeamBalance
{
    public class TeamBalanceConfig : BasePluginConfig
    {
		[JsonPropertyName("AllowedDifference")]
		public int AllowedDifference { get; set; } = 1;

        [JsonPropertyName("BalanceMode")]
        public string BalanceMode { get; set; } = "StoredStats";

        [JsonPropertyName("Line")]
        public string Line { get; set; } = "---------------------------------";

        [JsonPropertyName("ScrambleTeams")]
        public bool EnableScramble { get; set; } = true;

        [JsonPropertyName("MinPlayersBeforeScramble")]
        public int PlayersBeforeScramble { get; set; } = 2;

        [JsonPropertyName("WinsBeforeScramble")]
        public int WinsBeforeScramble { get; set; } = 5;

        [JsonPropertyName("Line2")]
        public string Line2 { get; set; } = "---------------------------------";

        [JsonPropertyName("AdminForceScramble")]
        public string AdminForceScramble { get; set; } = "css_forcescramble";

        [JsonPropertyName("DebugMode")]
        public bool DebugMode { get; set; } = false;

    }
}
