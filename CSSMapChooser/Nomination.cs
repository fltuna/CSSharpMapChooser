using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;

namespace CSSMapChooser;

public class Nomination {
    private List<NominationData> nominatedMaps = new();

    public List<NominationData> GetNominatedMaps() {
        return nominatedMaps;
    }

    private CSSMapChooser plugin;
    private Dictionary<string, MapData> mapDataDict;

    enum NominationStatus {
        NOMINATION_SUCCESS,
        NOMINATION_DUPLICATE,
        NOMINATION_CHANGED,
        NOMINATION_FORCED,
    }

    public Nomination(CSSMapChooser plugin, Dictionary<string, MapData> mapDataDict) {
        this.plugin = plugin;
        this.mapDataDict = mapDataDict;
        plugin.AddCommand("css_nominate", "nominate the specified map", CommandNominate);
        plugin.AddCommand("css_nominate_addmap", "Insert map to nomination", CommandNominateAddMap);
        plugin.AddCommand("css_nominate_removemap", "Remove map from nomination", CommandNominateRemoveMap);
        plugin.AddCommand("css_nomlist", "Show nomination list", CommandNomList);
        plugin.AddCommand("css_maplist", "Show map list", CommandMapList);
    }

    public void initializeNominations() {
        nominatedMaps.Clear();
    }

    private void CommandNominate(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;
        
        MapData newNominationMap = default!;

        string mapName = info.GetArg(1);

        foreach(MapData mapData in mapDataDict.Values) {
            if(mapData.MapName.Equals(mapName, StringComparison.OrdinalIgnoreCase)) {
                newNominationMap = mapData;
                break;
            }
        }

        
        List<MapData> foundMaps = mapDataDict.Values.ToList().FindAll(v => v.MapName.Contains(mapName, StringComparison.OrdinalIgnoreCase));

        if(foundMaps.Count() == 0 && !mapName.Equals("")) {
            client.PrintToChat($"{plugin.CHAT_PREFIX} No maps found with {mapName}");
            return;
        }
        else if (mapName.Equals("")) {
            ShowFullNominationMenu(client);
            return;
        }
        else if (foundMaps.Count() == 1) {
            newNominationMap = foundMaps.First();
        }
        else {
            client.PrintToChat($"{plugin.CHAT_PREFIX} {foundMaps.Count()} maps found with {mapName}!");
            ShowNominationMenu(client, foundMaps);
            return;
        }



        NominationData? existingMapNomination = null;
        
        foreach(NominationData nominatedMap in nominatedMaps) {
            if(nominatedMap.mapData.MapName.Equals(newNominationMap.MapName, StringComparison.OrdinalIgnoreCase)) {
                existingMapNomination = nominatedMap;
            }
        }

        if(existingMapNomination != null) {
            NominationStatus nominationStatus = NominateMap(existingMapNomination, client);

            switch(nominationStatus) {
                case NominationStatus.NOMINATION_SUCCESS: {
                    Server.PrintToChatAll($"{plugin.CHAT_PREFIX} {client.PlayerName} nominated {existingMapNomination.mapData.MapName}. Nomination counts: {existingMapNomination.GetNominators().Count()}");
                    break;
                }
                case NominationStatus.NOMINATION_DUPLICATE: {
                    client.PrintToChat($"{plugin.CHAT_PREFIX} You already nominated the {existingMapNomination.mapData.MapName}!");
                    break;
                }
                case NominationStatus.NOMINATION_CHANGED: {
                    Server.PrintToChatAll($"{plugin.CHAT_PREFIX} {client.PlayerName} changed their nomination to {existingMapNomination.mapData.MapName}. Nomination counts: {existingMapNomination.GetNominators().Count()}");
                    break;
                }
                case NominationStatus.NOMINATION_FORCED: {
                    client.PrintToChat($"{plugin.CHAT_PREFIX} {existingMapNomination.mapData.MapName} is already nominated by admin!");
                    break;
                }
            }

        }
        else {
            NominationData newNomination = new NominationData(newNominationMap);
            nominatedMaps.Add(newNomination);

            NominationStatus nominationStatus = NominateMap(newNomination, client);

            switch(nominationStatus) {
                case NominationStatus.NOMINATION_SUCCESS: {
                    Server.PrintToChatAll($"{plugin.CHAT_PREFIX} {client.PlayerName} nominated {newNomination.mapData.MapName}");
                    break;
                }
                case NominationStatus.NOMINATION_CHANGED: {
                    Server.PrintToChatAll($"{plugin.CHAT_PREFIX} {client.PlayerName} changed their nomination to {newNomination.mapData.MapName}");
                    break;
                }
            }
        }
    }


    private void CommandNominateAddMap(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;
        
        MapData newNominationMap = default!;

        string mapName = info.GetArg(1);

        foreach(MapData mapData in mapDataDict.Values) {
            if(mapData.MapName.Equals(mapName, StringComparison.OrdinalIgnoreCase)) {
                newNominationMap = mapData;
                break;
            }
        }
        
        List<MapData> foundMaps = mapDataDict.Values.ToList().FindAll(v => v.MapName.Contains(mapName, StringComparison.OrdinalIgnoreCase));

        if(foundMaps.Count() == 0 && !mapName.Equals("")) {
            client.PrintToChat($"{plugin.CHAT_PREFIX} No maps found with {mapName}");
            return;
        }
        else if (mapName.Equals("")) {
            ShowFullNominationMenu(client, true);
            return;
        }
        else if (foundMaps.Count() == 1) {
            newNominationMap = foundMaps.First();
        }
        else {
            client.PrintToChat($"{plugin.CHAT_PREFIX} {foundMaps.Count()} maps found with {mapName}!");
            ShowNominationMenu(client, foundMaps, true);
            return;
        }



        NominationData? existingMapNomination = null;
        
        foreach(NominationData nominatedMap in nominatedMaps) {
            if(nominatedMap.mapData.MapName.Equals(newNominationMap.MapName, StringComparison.OrdinalIgnoreCase)) {
                existingMapNomination = nominatedMap;
            }
        }

        if(existingMapNomination != null) {
            client.PrintToChat($"{plugin.CHAT_PREFIX} Map {existingMapNomination.mapData.MapName} is already nominated!");
        }
        else {
            NominationData newNomination = new NominationData(newNominationMap, true);
            nominatedMaps.Add(newNomination);

            Server.PrintToChatAll($"{plugin.CHAT_PREFIX} Admin {client.PlayerName} inserted {newNomination.mapData.MapName} to nomination");
        }
    }

    private void ShowNominationMenu(CCSPlayerController client, List<MapData> mapList, bool doForceNominate = false) {

        CenterHtmlMenu menu = new CenterHtmlMenu("Nomination menu", plugin);

        if(!doForceNominate) {
            foreach(MapData map in mapList) {
                menu.AddMenuOption(truncateString(map.MapName), (controller, option) => {
                    controller.ExecuteClientCommandFromServer($"css_nominate {option.Text}");
                    MenuManager.CloseActiveMenu(controller);
                });
            }
        }
        else {
            foreach(MapData map in mapList) {
                menu.AddMenuOption(truncateString(map.MapName), (controller, option) => {
                    controller.ExecuteClientCommandFromServer($"css_nominate_addmap {option.Text}");
                    MenuManager.CloseActiveMenu(controller);
                });
            }
        }


        MenuManager.OpenCenterHtmlMenu(plugin, client, menu);
    }

    private void ShowFullNominationMenu(CCSPlayerController client, bool doForceNominate = false) {

        CenterHtmlMenu menu = new CenterHtmlMenu("Nomination menu", plugin);

        if(!doForceNominate) {
            foreach(MapData map in mapDataDict.Values) {
                menu.AddMenuOption(truncateString(map.MapName), (controller, option) => {
                    controller.ExecuteClientCommandFromServer($"css_nominate {option.Text}");
                    MenuManager.CloseActiveMenu(controller);
                });
            }
        }
        else {
            foreach(MapData map in mapDataDict.Values) {
                menu.AddMenuOption(truncateString(map.MapName), (controller, option) => {
                    controller.ExecuteClientCommandFromServer($"css_nominate_addmap {option.Text}");
                    MenuManager.CloseActiveMenu(controller);
                });
            }
        }


        MenuManager.OpenCenterHtmlMenu(plugin, client, menu);
    }

    private void CommandNominateRemoveMap(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;

        string mapName = info.GetArg(1);

        NominationData? existingMapNomination = null;
        
        foreach(NominationData nominatedMap in nominatedMaps) {
            if(nominatedMap.mapData.MapName.Equals(mapName, StringComparison.OrdinalIgnoreCase)) {
                existingMapNomination = nominatedMap;
            }
        }

        if(existingMapNomination == null) {
            client.PrintToChat($"{plugin.CHAT_PREFIX} Map {mapName} is not nominated!");
            if(nominatedMaps.Count() > 0) {
                ShowNominationRemoveMenu(client);
            }
        }
        else {
            Server.PrintToChatAll($"{plugin.CHAT_PREFIX} Admin {client.PlayerName} removed {mapName} from nomination");
            nominatedMaps.Remove(existingMapNomination);
        }
    }
    
    private void ShowNominationRemoveMenu(CCSPlayerController client) {

        CenterHtmlMenu menu = new CenterHtmlMenu("Nomination Remove Menu", plugin);

        foreach(NominationData nominatedMap in nominatedMaps) {
            menu.AddMenuOption(truncateString(nominatedMap.mapData.MapName), (controller, option) => {
                controller.ExecuteClientCommandFromServer($"css_nominate_removemap {option.Text}");
                MenuManager.CloseActiveMenu(controller);
            });
        }

        MenuManager.OpenCenterHtmlMenu(plugin, client, menu);
    }


    private NominationData? FindExistingClientNomination(CCSPlayerController client) {
        foreach(NominationData nominationData in nominatedMaps) {
            if(nominationData.GetNominators().Contains(client))
                return nominationData;
        }

        return null;
    }

    private void CommandNomList(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;

        if(nominatedMaps.Count() == 0) {
            client.PrintToChat($"{plugin.CHAT_PREFIX} There is no nominated maps!");
            return;
        }

        bool shouldShowFullList = info.GetArg(1).Equals("full", StringComparison.OrdinalIgnoreCase);

        if(shouldShowFullList) {
            client.PrintToChat($"{plugin.CHAT_PREFIX} ===== Nominations (FULL) =====");

            foreach(NominationData nominationData in nominatedMaps) {
                if(nominationData.isForceNominate) {
                    client.PrintToChat($"{plugin.CHAT_PREFIX} {nominationData.mapData.MapName}: {ChatColors.Red}Admin");
                }
                else {
                    client.PrintToChat($"{plugin.CHAT_PREFIX} {nominationData.mapData.MapName}: {ChatColors.Lime}{string.Join($"{ChatColors.Default}, {ChatColors.Lime}" , nominationData.GetNominators().Select(p => p.PlayerName))}");
                }
            }

        }
        else {
            client.PrintToChat($"{plugin.CHAT_PREFIX} ===== Nominations =====");

            foreach(NominationData nominationData in nominatedMaps) {
                if(nominationData.isForceNominate) {
                    client.PrintToChat($"{plugin.CHAT_PREFIX} {nominationData.mapData.MapName}: {ChatColors.Red}Admin");
                }
                else {
                    client.PrintToChat($"{plugin.CHAT_PREFIX} {nominationData.mapData.MapName}: {ChatColors.Lime}{nominationData.GetNominators().Count()} votes");
                }
            }
        }

    }

    
    private void CommandMapList(CCSPlayerController? client, CommandInfo info) {
        if(client == null)
            return;

        client.PrintToChat($"{plugin.CHAT_PREFIX} See client console to map list.");

        foreach(MapData map in mapDataDict.Values) {
            client.PrintToConsole(map.MapName);
        }

    }

    private NominationStatus NominateMap(NominationData nomination, CCSPlayerController nominator) {
        if(nomination.isForceNominate)
            return NominationStatus.NOMINATION_FORCED;

        NominationData? existingClientNomination = FindExistingClientNomination(nominator);

        if(existingClientNomination == null) {
            nomination.AddNominator(nominator);
            return NominationStatus.NOMINATION_SUCCESS;
        }
        else {

            if(existingClientNomination.GetNominators().Contains(nominator) && existingClientNomination.mapData.MapName.Equals(nomination.mapData.MapName, StringComparison.OrdinalIgnoreCase)) {
                return NominationStatus.NOMINATION_DUPLICATE;
            }

            existingClientNomination.RemoveNominator(nominator);
            if(existingClientNomination.GetNominators().Count() <= 0)
                nominatedMaps.Remove(existingClientNomination);

            nomination.AddNominator(nominator);
            return NominationStatus.NOMINATION_CHANGED;
        }
    }

    private string truncateString(string input) {
        return input.Length > 25 ? input.Substring(0, 25) : input;
    }
}