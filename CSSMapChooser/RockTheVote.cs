using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Timers;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace CSSMapChooser;

public class RockTheVote {
    
    private readonly CSSMapChooser plugin;
    private Dictionary<string, MapData> mapDataDict;

    private List<CCSPlayerController> votedPlayers = new ();
    private int playersRequiredToRestart = 0;

    private bool isRTVEnabled = true;

    public RockTheVote(CSSMapChooser plugin, Dictionary<string, MapData> mapDataDict) {
        this.plugin = plugin;
        this.mapDataDict = mapDataDict;
        plugin.AddCommand("css_forcertv", "Initiate the force rtv", CommandForceRTV);
        plugin.AddCommand("css_rtv", "Vote for initiating the RTV", CommandRTV);
        plugin.AddCommand("css_enablertv", "Enable RTV", CommandEnableRTV);
        plugin.AddCommand("css_disablertv", "Enable RTV", CommandDisableRTV);
    }

    private void CommandRTV(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;

        VoteManager? voteManager = plugin.voteManager;

        if(voteManager != null && voteManager.voteProgress == VoteManager.VoteProgress.VOTE_INITIATING ||
            voteManager != null && voteManager.voteProgress == VoteManager.VoteProgress.VOTE_IN_PROGRESS
        ) {
            client.PrintToChat($"{plugin.CHAT_PREFIX} Vote is in progress!");
            return;
        }

        if(!isRTVEnabled) {
            client.PrintToChat($"{plugin.CHAT_PREFIX} RTV is disabled at the moment.");
            return;
        }

        if(votedPlayers.Contains(client)) {
            client.PrintToChat($"{plugin.CHAT_PREFIX} You have already voted to RTV the map!");
            return;
        }

        votedPlayers.Add(client);

        float voteThreshold = PluginSettings.GetInstance().cssmcRTVVoteThreshold.Value;

        playersRequiredToRestart = (int)Math.Ceiling(Utilities.GetPlayers().Count(player => !player.IsBot && !player.IsHLTV) * voteThreshold);

        Server.PrintToChatAll($"{plugin.CHAT_PREFIX} {client.PlayerName} wants to Rock The Vote! ({votedPlayers.Count()} voted, {playersRequiredToRestart} required)");

        if(votedPlayers.Count() >= playersRequiredToRestart) {
            InitiateRTV(voteManager);
        }
    }


    [RequiresPermissions(@"css/map")]
    private void CommandEnableRTV(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;

        if(isRTVEnabled) {
            client.PrintToChat($"{plugin.CHAT_PREFIX} RTV is already enabled.");
            return;
        }

        Server.PrintToChatAll($"{plugin.CHAT_PREFIX} RTV enabled.");
        isRTVEnabled = true;
    }

    [RequiresPermissions(@"css/map")]
    private void CommandDisableRTV(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;

        if(!isRTVEnabled) {
            client.PrintToChat($"{plugin.CHAT_PREFIX} RTV is already disabled.");
            return;
        }

        Server.PrintToChatAll($"{plugin.CHAT_PREFIX} RTV disabled.");
        isRTVEnabled = false;

    }

    [RequiresPermissions(@"css/map")]
    private void CommandForceRTV(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;

        VoteManager? voteManager = plugin.voteManager;

        if(voteManager != null && voteManager.voteProgress == VoteManager.VoteProgress.VOTE_INITIATING ||
            voteManager != null && voteManager.voteProgress == VoteManager.VoteProgress.VOTE_IN_PROGRESS
        ) {
            client.PrintToChat($"{plugin.CHAT_PREFIX} Vote is in progress!");
            return;
        }

        InitiateRTV(voteManager);
    }

    private bool ForceChangeMapWithRTV() {
        VoteManager? voteManager = plugin.voteManager;
        MapData? mapData = voteManager?.nextMap;

        if(mapData == null)
            return false;

        Server.PrintToChatAll($"{plugin.CHAT_PREFIX} Changing map to {mapData.MapName}! Rock The Vote has spoken!");

        plugin.AddTimer(PluginSettings.GetInstance().cssmcRTVMapChangingDelay.Value, () => {
            plugin.ChangeToNextMap(mapData);
        }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);
        return true;
    }

    private void InitiateRTV(VoteManager? voteManager) {
        if(voteManager != null && voteManager.nextMap != null) {
            ForceChangeMapWithRTV();
            ResetRTVStatus();
            return;
        }

        VoteManager newVoteManager = new VoteManager(mapDataDict, plugin, true);

        plugin.setVoteManager(newVoteManager);

        newVoteManager.StartVoteProcess();
    }

    public void ResetRTVStatus() {
        votedPlayers.Clear();
    }
}