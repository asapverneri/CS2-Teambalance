using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace TeamBalance;

public class TeamBalance : BasePlugin, IPluginConfig<TeamBalanceConfig>
{
    public override string ModuleName => "TeamBalance";
    public override string ModuleDescription => "Simple teambalance for CS2";
    public override string ModuleAuthor => "verneri";
    public override string ModuleVersion => "1.0";

    public TeamBalanceConfig Config { get; set; } = new();

    public void OnConfigParsed(TeamBalanceConfig config)
	{
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        Logger.LogInformation($"Loaded (version {ModuleVersion})");

        RegisterListener<Listeners.OnMapStart>(OnMapStart);

        AddTimer(10.0f, () =>
        {
            ConVar.Find("mp_autoteambalance")!.SetValue(false);
        });
    }

    private void OnMapStart(string map)
    {
        TeamWins[CsTeam.Terrorist] = 0;
        TeamWins[CsTeam.CounterTerrorist] = 0;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (Config.EnableScramble) 
        {
            CsTeam winningTeam = (CsTeam)@event.Winner;
            //Logger.LogInformation($"(OnRoundEnd) Winning team: {winningTeam}");

            if (winningTeam == CsTeam.Terrorist || winningTeam == CsTeam.CounterTerrorist)
            {
                TeamWins[winningTeam]++;
                //Logger.LogInformation($"(OnRoundEnd) {winningTeam} wins. Total wins: {TeamWins[winningTeam]}");
            }

            int winDifference = Math.Abs(TeamWins[CsTeam.Terrorist] - TeamWins[CsTeam.CounterTerrorist]);
            //Logger.LogInformation($"(OnRoundEnd) Current win difference: {winDifference}");
            if (winDifference >= Config.WinsBeforeScramble)
            {
                //Logger.LogInformation($"(OnRoundEnd) Win difference of {winDifference} exceeds scramble threshold of {Config.WinsBeforeScramble}. Scrambling teams.");
                ScrambleTeams();
                Server.PrintToChatAll($"{Localizer["teams.scrambled"]}");
            }
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundPoststart(EventRoundPoststart @event, GameEventInfo info)
    {
        Balance();
        return HookResult.Continue;
    }

    private void Balance()
    {
        var tPlayers = GetTPlayers();
        var ctPlayers = GetCTPlayers();

        //Logger.LogInformation($"(Balance) Terrorists: {tPlayers.Count}, Counter-Terrorists: {ctPlayers.Count}");

        if (Math.Abs(tPlayers.Count - ctPlayers.Count) <= Config.AllowedDifference)
        {
            //Logger.LogInformation("(Balance) Teams are already balanced.");
            return;
        }

        bool moveFromT = tPlayers.Count > ctPlayers.Count;
        var biggerTeam = moveFromT ? tPlayers : ctPlayers;
        var smallerTeam = moveFromT ? ctPlayers : tPlayers;
        var targetTeam = moveFromT ? CsTeam.CounterTerrorist : CsTeam.Terrorist;

        int difference = biggerTeam.Count - smallerTeam.Count;

        int playersToMove = Math.Min(difference - Config.AllowedDifference, (biggerTeam.Count - smallerTeam.Count) / 2);

        playersToMove = Math.Max(0, playersToMove);

        //Logger.LogInformation($"(Balance) Players to move: {playersToMove}");

        if (playersToMove <= 0)
        {
            //Logger.LogInformation("(Balance) No players to move.");
            return;
        }

        if (Config.BalanceByScore)
        {
            biggerTeam = biggerTeam.OrderByDescending(p => p.Score).ToList();
        }
        else
        {
            var random = new Random();
            biggerTeam = biggerTeam.OrderBy(_ => random.Next()).ToList();
        }

        for (int i = 0; i < playersToMove; i++)
        {
            var player = biggerTeam[i];
            player.ChangeTeam(targetTeam);
            player.PrintToChat($"{Localizer["player.moved"]}");
            //Logger.LogInformation($"(Balance) Moved player {player.PlayerName} to {targetTeam}");
        }

        //Logger.LogInformation("(Balance) Teams balanced.");
    }

    private void ScrambleTeams()
    {
        var players = GetAllPlayers();

        TeamWins[CsTeam.Terrorist] = 0;
        TeamWins[CsTeam.CounterTerrorist] = 0;

        var random = new Random();
        players = players.OrderBy(_ => random.Next()).ToList();

        int half = players.Count / 2;
        for (int i = 0; i < players.Count; i++)
        {
            players[i].ChangeTeam(i < half ? CsTeam.Terrorist : CsTeam.CounterTerrorist);
        }
        //Logger.LogInformation($"(ScrambleTeams) Teams scrambled due to a {Config.WinsBeforeScramble} win difference.");
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
}