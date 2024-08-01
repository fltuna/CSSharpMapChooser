using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace CSSMapChooser;

public partial class CSSMapChooser : BasePlugin
{
    public override string ModuleName => "CounterStrikeSharp Map Chooser";

    public override string ModuleVersion => "0.0.1";

    public override string ModuleAuthor => "faketuna";

    public override string ModuleDescription => "CounterStrikeSharp implementation of map chooser";

    public Dictionary<string, MapData> mapDataDict {get; private set;} = new();

    private ConVar? mp_timelimit = null;

    public int timeleft {get; private set;} = 0;

    public int extendsCount {get; private set;} = 0;

    public void incrementExtendsCount() {
        extendsCount++;
    }

    private Timer? mapVoteTimer = null;

    public VoteManager? voteManager {get; private set;} = null;

    public void setVoteManager(VoteManager voteManager) {
        if(this.voteManager != null && this.voteManager?.voteProgress == VoteManager.VoteProgress.VOTE_INITIATING ||
            this.voteManager?.voteProgress == VoteManager.VoteProgress.VOTE_IN_PROGRESS ||
            this.voteManager?.voteProgress == VoteManager.VoteProgress.VOTE_NEXTMAP_CONFIRMED)
            throw new InvalidOperationException("VoteManager setter is should only be called when vote is pending.");

        this.voteManager = voteManager;
    }

    public readonly string CHAT_PREFIX = $" {ChatColors.Green}[CSSMC]{ChatColors.Default}";


    public RockTheVote rockTheVoteModule {get; private set;} = default!;
    public Nomination nominationModule {get; private set;} = default!;

    public override void Load(bool hotReload)
    {
        Logger.LogInformation("Plugin load started");

        Logger.LogInformation("Initializing the plugin settings instance");
        new PluginSettings(this);

        Logger.LogInformation("Initializing the Next map data");

        Logger.LogInformation("Registering timeleft calculation timer.");
        RegisterListener<Listeners.OnTick>(() => {
            if (GetGameRules() != null && mp_timelimit != null)
				timeleft = (int)((GetGameRules().GameStartTime + mp_timelimit.GetPrimitiveValue<float>() * 60.0f) - Server.CurrentTime);
			else if (mp_timelimit == null) {
				Logger.LogWarning("Failed to find the mp_timelimit ConVar and try to find again.");
				mp_timelimit = ConVar.Find("mp_timelimit");
			}
			else {
				Logger.LogError("Failed to find the Game Rules entity!");
			}
        });

        CreateMapVoteTimer();
        
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);

        Logger.LogInformation("Adding commands...");
        AddCommand("css_nextmap", "shows nextmap information", CommandNextMap);
        AddCommand("css_timeleft", "shows current map limit time", CommandTimeLeft);
        AddCommand("css_revote", "Re-vote the current vote.", CommandReVote);

        AddCommand("css_cancelvote", "Cancel the current vote", CommandCancelVote);

        AddCommandListener("say", ChatCommandTrigger, HookMode.Pre);
        AddCommandListener("say_team", ChatCommandTrigger, HookMode.Pre);
    }

    private bool loadMapData() {
        string mapsTxtLocation = Path.Combine(ModuleDirectory + "\\maps.txt");

        if(!Path.Exists(mapsTxtLocation)) {
            Logger.LogError("Failed to find maps.txt! If you haven't created yet, please create maps.txt in the {moduleDir} directory.", ModuleDirectory);
            return false;
        }

        mapDataDict = MapDataParser.ParseConfig(mapsTxtLocation);
        return true;
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        Logger.LogInformation("Loading MapData. This is a late load.");
        loadMapData();

        Logger.LogInformation("Initializing Nomination module. This is a late load.");
        nominationModule = new Nomination(this, mapDataDict);

        Logger.LogInformation("Initializing Nomination RTV module. This is a late load.");
        rockTheVoteModule = new RockTheVote(this, mapDataDict);
    }

    public override void Unload(bool hotReload)
    {
        RemoveCommandListener("say", ChatCommandTrigger, HookMode.Pre);
        RemoveCommandListener("say_team", ChatCommandTrigger, HookMode.Pre);
    }

    private HookResult ChatCommandTrigger(CCSPlayerController? client, CommandInfo commandInfo) {
        if(client == null)
            return HookResult.Continue;

        string arg1 = commandInfo.GetArg(1);
        bool isTriggerContains = false;


        if(arg1.Equals("nextmap", StringComparison.OrdinalIgnoreCase)) {
            client.ExecuteClientCommandFromServer("css_nextmap");
            isTriggerContains = true;
        }
        else if (arg1.Equals("timeleft", StringComparison.OrdinalIgnoreCase)) {
            client.ExecuteClientCommandFromServer("css_timeleft");
            isTriggerContains = true;
        }
        else if (arg1.Equals("rtv", StringComparison.OrdinalIgnoreCase)) {
            client.ExecuteClientCommandFromServer("css_rtv");
            isTriggerContains = true;
        }

        if(isTriggerContains)
            return HookResult.Handled;

        return HookResult.Continue;
    }

    private void OnMapStart(string mapName) {
        CreateMapVoteTimer();
        voteManager = null;
        extendsCount = 0;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info) {
        if(voteManager != null && voteManager.shouldRestartAfterRoundEnd) {
            if(voteManager.nextMap == null) {
                Server.PrintToChatAll($"{CHAT_PREFIX} {ChatColors.DarkRed} Failed to transition to next map! See console to error log");
                Logger.LogError("Failed to transition to next map! Because next map is null! also this is should not be happened!");
                return HookResult.Continue;
            }
            ChangeToNextMap(voteManager.nextMap);
            return HookResult.Continue;
        }


        if(0 < timeleft || mp_timelimit?.GetPrimitiveValue<float>() < 0.0)
            return HookResult.Continue;
        
        float endMatchExtraTime = ConVar.Find("mp_competitive_endofmatch_extra_time")?.GetPrimitiveValue<float>() ?? 15.0F;

        AddTimer(endMatchExtraTime, () => {
            MapData? nextMap = voteManager?.nextMap;

            if(nextMap == null) {
                Random rand = new Random();
                int index = rand.Next(mapDataDict.Count());
                MapData data = mapDataDict.ElementAt(index).Value;
                ChangeToNextMap(data);
            }
            else {
                ChangeToNextMap(nextMap);
            }
        }, TimerFlags.STOP_ON_MAPCHANGE);

        return HookResult.Continue;
    }

    [RequiresPermissions(@"css/map")]
    private void CommandCancelVote(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;

        if(voteManager == null || voteManager.voteProgress != VoteManager.VoteProgress.VOTE_IN_PROGRESS) {

            client.PrintToChat($"{CHAT_PREFIX} There is no active vote!");
            return;
        }

        voteManager.CancelVote(client);
    }



    private void CommandReVote(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;

        if(voteManager == null || voteManager.voteProgress != VoteManager.VoteProgress.VOTE_IN_PROGRESS) {
            client.PrintToChat($"{CHAT_PREFIX} There is no active vote!");
            return;
        }

        voteManager.ShowVotingMenu(client);
    }

    private void CommandNextMap(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;
        
        ShowNextMapInfo(client);
    }

    private void ShowNextMapInfo(CCSPlayerController client) {
        string nextMapInfo = "Pending vote";

        if (voteManager != null) {
            MapData? mapDataDict = voteManager.nextMap;
            if (mapDataDict != null) {
                nextMapInfo = mapDataDict.MapName;
            }
        }

        client.PrintToChat($"{CHAT_PREFIX} Next map: {nextMapInfo}");
    }



    private void CommandTimeLeft(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;
        
        ShowTimeLeft(client);
    }

    private void ShowTimeLeft(CCSPlayerController client) {
        if(mp_timelimit?.GetPrimitiveValue<float>() <= 0.0) {
            client.PrintToChat($"{CHAT_PREFIX} No time limit");
        }
        else if(timeleft < 1) {
            client.PrintToChat($"{CHAT_PREFIX} Last round!");
        }
        else {
            client.PrintToChat($"{CHAT_PREFIX} Timeleft: {GetFormattedTimeLeft(timeleft)}");
        }
    }

    private string GetFormattedTimeLeft(int timeleft) {
        int minutes = timeleft / 60;
        int seconds = timeleft % 60;
        string formatted = "";

        if(minutes > 0) {
            formatted = $"{minutes} minutes {seconds} seconds";
        }
        else {
            formatted = $"{seconds} seconds";
        }

        return formatted;
    }

    private CCSGameRules GetGameRules()
	{
		return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
	}

    public void ChangeToNextMap(MapData nextMap) {
        string serverCmd = "";

        if(nextMap.MapProperties.GetValue<int>("workshop") == 1) {
            // TODO: get workshop id for executing host_workshop_map instead of ds_workshop_changelevel
            // serverCmd += $"host_workshop_map {}";
            
            serverCmd += $"ds_workshop_changelevel {nextMap.MapName}";
        }
        else {
            serverCmd += $"map {nextMap.MapName}";
        }

        SimpleLogging.LogDebug($"Changing map. cmd: {serverCmd}");
        Server.ExecuteCommand(serverCmd);
    }

    public void ExtendCurrentMap(float extendingTime) {
        if(mp_timelimit == null) {
            Server.PrintToChatAll($"{CHAT_PREFIX} {ChatColors.DarkRed}Failed to extending the map! See console to detailed information.");
            Logger.LogError($"Failed to extending the map! mp_timelimit ConVar not found!");
            return;
        }

        float oldTime = mp_timelimit.GetPrimitiveValue<float>();
        float newTime = oldTime + extendingTime;

        mp_timelimit.SetValue(newTime);
        Server.PrintToChatAll($"{CHAT_PREFIX} Timelimit extended by {extendingTime} minutes");
    }

    public int getHumanPlayersCount() {
        int totalHumanPlayers = 0;

        foreach(CCSPlayerController client in Utilities.GetPlayers()) {
            if(!client.IsValid || client.IsBot || client.IsHLTV)
                continue;
            
            totalHumanPlayers++;
        }

        return totalHumanPlayers;
    }


    private void CreateMapVoteTimer() {
        mapVoteTimer = AddTimer(1.0F, () => {
            if((int)PluginSettings.GetInstance().cssmcMapVoteStartTime.Value < timeleft || mp_timelimit?.GetPrimitiveValue<float>() <= 0.0)
                return;
            
            if(voteManager != null) {                
                switch(voteManager.voteProgress) {
                    case VoteManager.VoteProgress.VOTE_FINISHED: {
                        return;
                    }
                    case VoteManager.VoteProgress.VOTE_IN_PROGRESS: {
                        return;
                    }
                    case VoteManager.VoteProgress.VOTE_INITIATING: {
                        return;
                    }
                    case VoteManager.VoteProgress.VOTE_NEXTMAP_CONFIRMED: {
                        return;
                    }
                    case VoteManager.VoteProgress.VOTE_PENDING: {
                        break;
                    }
                }
            }

            voteManager = new VoteManager(mapDataDict, this, false);
            
            voteManager.StartVoteProcess();
        }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }
}