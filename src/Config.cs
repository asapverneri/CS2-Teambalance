using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace TeamBalance
{
    public class TeamBalanceConfig : BasePluginConfig
    {
		[JsonPropertyName("AllowedDifference")]
		public int AllowedDifference { get; set; } = 1;

        [JsonPropertyName("BalanceByScore")]
        public bool BalanceByScore { get; set; } = false;

        [JsonPropertyName("ScrambleTeams")]
        public bool EnableScramble { get; set; } = true;

        [JsonPropertyName("WinsBeforeScramble")]
        public int WinsBeforeScramble { get; set; } = 5;

    }
}
