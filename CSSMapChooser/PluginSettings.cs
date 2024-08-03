using System.Runtime.CompilerServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Cvars.Validators;
using Microsoft.Extensions.Logging;

namespace CSSMapChooser {
    public class PluginSettings {

        private static PluginSettings? settingsInstance;

        public const string CONFIG_FOLDER = "cssmc/";
        private const string CONFIG_FILE = "config.cfg";

        public static PluginSettings GetInstance() {
            if(settingsInstance == null)
                throw new InvalidOperationException("Settings instance is not initialized yet.");

            return settingsInstance;
        }


        /*
        * Rock the Vote
        */
        public FakeConVar<float> cssmcRTVMapChangingDelay = new("cssmc_rtv_map_changing_delay", "When map changes after map vote with rtv.", 10.0F);
        public FakeConVar<bool> cssmcRTVMapChangingAfterRoundEnd = new("cssmc_rtv_map_changing_after_round_end", "Should change map after round end?", true);
        public FakeConVar<float> cssmcRTVVoteThreshold = new("cssmc_rtv_vote_threshold", "Threshold of RTV vote", 0.5F, ConVarFlags.FCVAR_NONE, new RangeValidator<float>(0.0F, 1.0F));

        /*
        * Voting general
        */
        public FakeConVar<float> cssmcMapVoteCountdownTime = new("cssmc_map_vote_countdown_time", "How long to wait before vote starts after map vote notification?", 15.0F);
        public FakeConVar<double> cssmcMapVoteTime = new("cssmc_map_vote_time", "How long to take voting time?", 35.0D);
        public FakeConVar<float> cssmcMapVoteStartTime = new("cssmc_map_vote_start_time", "Start map vote when timeleft goes below the specified time.", 180.0F);
        public FakeConVar<float> cssmcMapVoteRunoffThreshold = new("cssmc_map_vote_runoff_threshold", "When lower than this ConVar's percentage it will start a runoff vote.", 0.6F, ConVarFlags.FCVAR_NONE, new RangeValidator<float>(0.0F, 1.0F));
        public FakeConVar<float> cssmcMapVoteWinnerPickupThreshold = new("cssmc_map_vote_winner_pickup_threshold", "When vote percentage of map is higher than this ConVar it will add to runoff vote maps", 0.2F, ConVarFlags.FCVAR_NONE, new RangeValidator<float>(0.0F, 1.0F));
        public FakeConVar<int> cssmcMapVoteMapCount = new("cssmc_map_vote_map_count", "How many maps will appear in map vote?", 5, ConVarFlags.FCVAR_NONE, new RangeValidator<int>(2, 32));
        public FakeConVar<int> cssmcMapVoteAvailableExtends = new("cssmc_map_vote_available_extends", "Global value of extends count.", 3);


        /*
        *   Debugging
        */
        public FakeConVar<int> cssmcDebugLevel = new("cssmc_debug_level", "0: Nothing, 1: Show debug message, 2: Show debug and trace", 0);
        public FakeConVar<bool> cssmcDebugShowClientConsole = new("cssmc_debug_show_client_console", "Debug message should shown in client console?", true);

        private CSSMapChooser plugin;

        public PluginSettings(CSSMapChooser plugin) {
            plugin.Logger.LogDebug("Setting the instance info");
            settingsInstance = this;
            plugin.Logger.LogDebug("Setting the plugin instance");
            this.plugin = plugin;
            plugin.Logger.LogDebug("Initializing the settings");
            initializeSettings();
            plugin.Logger.LogDebug("Registering the fake convar");
            plugin.RegisterFakeConVars(typeof(PluginSettings), this);
        }

        public bool initializeSettings() {
            plugin.Logger.LogDebug("Generate path to config folder");
            string configFolder = Path.Combine(Server.GameDirectory, "csgo/cfg/", CONFIG_FOLDER);

            plugin.Logger.LogDebug("Checking existence of config folder");
            if(!Directory.Exists(configFolder)) {
                plugin.Logger.LogInformation($"Failed to find the config folder. Trying to generate...");

                Directory.CreateDirectory(configFolder);

                if(!Directory.Exists(configFolder)) {
                    plugin.Logger.LogError($"Failed to generate the Config folder!");
                    return false;
                }
            }

            plugin.Logger.LogDebug("Generate path to config file");
            string configLocation = Path.Combine(configFolder, CONFIG_FILE);

            plugin.Logger.LogDebug("Checking existence of config file");
            if(!File.Exists(configLocation)) {
                plugin.Logger.LogInformation($"Failed to find the config file. Trying to generate...");

                try {
                    generateCFG(configLocation);
                } catch(Exception e) {
                    plugin.Logger.LogError($"Failed to generate config file!\n{e.StackTrace}");
                    return false;
                }
                
                plugin.Logger.LogInformation($"Config file created.");
            }

            plugin.Logger.LogDebug("Executing config");
            plugin.Logger.LogDebug($"exec {CONFIG_FOLDER}{CONFIG_FILE}");
            Server.ExecuteCommand($"exec {CONFIG_FOLDER}{CONFIG_FILE}");
            return true;
        }

        private void generateCFG(string configPath) {
            StreamWriter config = File.CreateText(configPath);

            writeConVarConfig(config, cssmcRTVMapChangingDelay);
            writeConVarConfig(config, cssmcRTVMapChangingAfterRoundEnd);
            config.WriteLine("\ns");

            writeConVarConfig(config, cssmcMapVoteCountdownTime);


            config.Close();
        }

        private static void writeConVarConfig<T>(StreamWriter configFile, FakeConVar<T> convar)where T : IComparable<T>{
            configFile.WriteLine($"// {convar.Description}");
            if(typeof(T) == typeof(bool)) {
                var conValue = convar.Value;
                bool value = Unsafe.As<T, bool>(ref conValue);
                configFile.WriteLine($"{convar.Name} {Convert.ToInt32(value)}");
            } else {
                configFile.WriteLine($"{convar.Name} {convar.Value}");
            }
            configFile.WriteLine();
        }
    }
}