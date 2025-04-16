using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace TeamBalance;

public class TeamBalance : BasePlugin, IPluginConfig<TeamBalanceConfig>
{
    public override string ModuleName => "TeamBalance";
    public override string ModuleDescription => "https://github.com/asapverneri/CS2-Teambalance";
    public override string ModuleAuthor => "verneri";
    public override string ModuleVersion => "1.3";

    public TeamBalanceConfig Config { get; set; } = new();

    private bool ForceScrambleNextRound = false;
    private Dictionary<ulong, int> PlayerKills = new();
    private Dictionary<ulong, int> PlayerDeaths = new(); 

    public void OnConfigParsed(TeamBalanceConfig config)
	{
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        Logger.LogInformation($"Loaded (version {ModuleVersion})");

        RegisterListener<Listeners.OnMapStart>(OnMapStart);


        AddCommand($"{Config.AdminForceScramble}", "Admin command for forcing scramble", TBAdminForceScramble);

        if (string.IsNullOrEmpty(Config.BalanceMode) || (Config.BalanceMode != "TopScore" && Config.BalanceMode != "StoredStats" && Config.BalanceMode != "Random"))
        {
            Logger.LogWarning("Invalid BalanceMode detected. Defaulting to 'Random'. Please check the Github readme.");
            Config.BalanceMode = "Random";
        }

        AddTimer(10.0f, () =>
        {
            ConVar.Find("mp_autoteambalance")!.SetValue(false);
        });
    }

    private void OnMapStart(string map)
    {
        TeamWins[CsTeam.Terrorist] = 0;
        TeamWins[CsTeam.CounterTerrorist] = 0;

        if (Config.BalanceMode == "StoredStats")
        {
            PlayerKills.Clear();
            PlayerDeaths.Clear();
        }
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        CsTeam winningTeam = (CsTeam)@event.Winner;
        DebugMode($"(OnRoundEnd) Winning team: {winningTeam}");

        if (winningTeam == CsTeam.Terrorist || winningTeam == CsTeam.CounterTerrorist)
        {
            TeamWins[winningTeam]++;
            DebugMode($"(OnRoundEnd) {winningTeam} wins. Total wins: {TeamWins[winningTeam]}");
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundPreStart(EventRoundPrestart @event, GameEventInfo info)
    {
        if (ForceScrambleNextRound)
        {
            DebugMode("(EventRoundPrestart) Forced team scramble triggered.");
            ScrambleTeams();
            Server.PrintToChatAll($"{Localizer["teams.scrambled"]}");
            ForceScrambleNextRound = false;
        }
        else if (Config.EnableScramble)
        {
            int winDifference = Math.Abs(TeamWins[CsTeam.Terrorist] - TeamWins[CsTeam.CounterTerrorist]);
            if (winDifference >= Config.WinsBeforeScramble)
            {
                DebugMode($"(EventRoundPrestart) Win difference of {winDifference} exceeds scramble threshold. Scrambling teams.");
                ScrambleTeams();
                Server.PrintToChatAll($"{Localizer["teams.scrambled"]}");
            }
        }

        Balance();
        DebugMode($"(EventRoundPrestart) Fired.");
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (@event == null)
            return HookResult.Continue;

        if (Config.BalanceMode != "StoredStats")
            return HookResult.Continue;

        var victim = @event.Userid;
        var attacker = @event.Attacker;

        if (victim == null || !victim.IsValid)
            return HookResult.Continue;

        if (attacker == null || !attacker.IsValid || attacker.IsBot)
            return HookResult.Continue;

        if(attacker == victim)
            return HookResult.Continue;

        if (PlayerDeaths.ContainsKey(victim.SteamID))
        {
            PlayerDeaths[victim.SteamID]++;
        }
        else
        {
            PlayerDeaths[victim.SteamID] = 1;
        }

        if (PlayerKills.ContainsKey(attacker.SteamID))
        {
            PlayerKills[attacker.SteamID]++;
        }
        else
        {
            PlayerKills[attacker.SteamID] = 1;
        }

        DebugMode($"(EventPlayerDeath) Attacker: {attacker.PlayerName} Kills: {PlayerKills[attacker.SteamID]}");
        DebugMode($"(EventPlayerDeath) Victim: {victim.PlayerName} Deaths: {PlayerDeaths[victim.SteamID]}");

        return HookResult.Continue;
    }

    private void Balance()
    {
        var tPlayers = GetTPlayers();
        var ctPlayers = GetCTPlayers();

        DebugMode($"(Balance) Terrorists: {tPlayers.Count}, Counter-Terrorists: {ctPlayers.Count}");

        if (Math.Abs(tPlayers.Count - ctPlayers.Count) <= Config.AllowedDifference)
        {
            DebugMode("(Balance) Teams are already balanced.");
            return;
        }

        bool isTeamUnbalanced = tPlayers.Count > ctPlayers.Count;
        var biggerTeam = isTeamUnbalanced ? tPlayers : ctPlayers;
        var smallerTeam = isTeamUnbalanced ? ctPlayers : tPlayers;
        var targetTeam = isTeamUnbalanced ? CsTeam.CounterTerrorist : CsTeam.Terrorist;

        int difference = biggerTeam.Count - smallerTeam.Count;

        int playersToMove = Math.Min(difference - Config.AllowedDifference, (biggerTeam.Count - smallerTeam.Count) / 2);

        playersToMove = Math.Max(0, playersToMove);

        DebugMode($"(Balance) Players to move: {playersToMove}");

        if (playersToMove <= 0)
        {
            DebugMode("(Balance) No players to move.");
            return;
        }

        switch (Config.BalanceMode)
        {
            case "TopScore":
                biggerTeam = biggerTeam.OrderByDescending(p => p.Score).ToList();
                break;

            case "StoredStats":
                biggerTeam = biggerTeam.OrderByDescending(p =>
                {
                    int kills = PlayerKills.TryGetValue(p.SteamID, out var playerKills) ? playerKills : 0;
                    int deaths = PlayerDeaths.TryGetValue(p.SteamID, out var playerDeaths) ? playerDeaths : 0;

                    return kills - deaths;
                }).ToList();
                break;

            case "Random":
            default:
                var random = new Random();
                biggerTeam = biggerTeam.OrderBy(_ => random.Next()).ToList();
                break;
        }

        for (int i = 0; i < playersToMove; i++)
        {
            var player = biggerTeam[i];
            player.SwitchTeam(targetTeam);
            player.PrintToChat($"{Localizer["player.moved"]}");
            DebugMode($"(Balance) Moved player {player.PlayerName} to {targetTeam}");
        }
        DebugMode("(Balance) Teams balanced.");
    }

    private void ScrambleTeams()
    {
        var players = GetAllPlayers();

        TeamWins[CsTeam.Terrorist] = 0;
        TeamWins[CsTeam.CounterTerrorist] = 0;

        if (players.Count < Config.PlayersBeforeScramble)
        {
            DebugMode("(ScrambleTeams) Not enough players to scramble.");
            return;
        }

        var random = new Random();
        players = players.OrderBy(_ => random.Next()).ToList();

        int half = players.Count / 2;
        for (int i = 0; i < players.Count; i++)
        {
            players[i].SwitchTeam(i < half ? CsTeam.Terrorist : CsTeam.CounterTerrorist);
        }
        DebugMode($"(ScrambleTeams) Teams scrambled. {half} players moved to each team.");
    }

    private void TBAdminForceScramble(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        if (!AdminManager.PlayerHasPermissions(player, Config.AdminFlag))
        {
            player.PrintToChat($"[TeamBalance] You're missing permission '{Config.AdminFlag}'.");
            return;
        }

        ForceScrambleNextRound = true;
        Server.PrintToChatAll($"{Localizer["admin.scrambled"]}");
        DebugMode($"(TBAdminForceScramble) {player.PlayerName} has forced a team scramble.");

    }

    private static List<CCSPlayerController> GetAllPlayers()
    {
        return Utilities.GetPlayers()
            .Where(p => !p.IsHLTV && p.TeamNum != 0 && p.TeamNum != 1 && p.Connected == PlayerConnectedState.PlayerConnected)
            .ToList();
    }

    private static List<CCSPlayerController> GetTPlayers()
    {
        return Utilities.GetPlayers()
            .Where(p => !p.IsHLTV && p.TeamNum == 2 && p.Connected == PlayerConnectedState.PlayerConnected)
            .ToList();
    }

    private static List<CCSPlayerController> GetCTPlayers()
    {
        return Utilities.GetPlayers()
            .Where(p => !p.IsHLTV && p.TeamNum == 3 && p.Connected == PlayerConnectedState.PlayerConnected)
            .ToList();
    }

    private Dictionary<CsTeam, int> TeamWins = new()
    {
        { CsTeam.Terrorist, 0 },
        { CsTeam.CounterTerrorist, 0 }
    };

    private void DebugMode(string message)
    {
        if (Config.DebugMode)
        {
            Server.PrintToChatAll($"[DEBUG] {message}");
            Logger.LogInformation($"[DEBUG] {message}");
        }
    }
}