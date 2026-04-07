using BepInEx;
using BepInEx.Configuration;

namespace SuperBattleGolf.BetterCommunication
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "local.marki.superbattlegolf.bettercommunication";
        public const string PluginName = "BetterCommunication";
        public const string PluginVersion = "0.2.2";

        private ConfigEntry<bool> _enableSteamProfileLinks;
        private ConfigEntry<bool> _useSteamProtocol;
        private ConfigEntry<bool> _enableWorldNameTags;
        private ConfigEntry<bool> _enableAvatarLinks;
        private ConfigEntry<bool> _enableChatLinks;
        private ConfigEntry<bool> _enableChatHistory;
        private ConfigEntry<int> _maxChatHistoryMessages;
        private ConfigEntry<bool> _persistChatHistoryAcrossLaunches;

        private SteamProfileLinkInjector _steamProfileLinkInjector;
        private ChatHistoryManager _chatHistoryManager;

        private void Awake()
        {
            _enableSteamProfileLinks = Config.Bind("Steam Profiles", "EnableSteamProfileLinks", true, "Make player names in supported UI screens clickable to open the player's Steam profile.");
            _useSteamProtocol = Config.Bind("Steam Profiles", "UseSteamProtocol", true, "Open profiles through steam://openurl/... instead of a plain https URL.");
            _enableWorldNameTags = Config.Bind("Steam Profiles", "EnableWorldNameTags", false, "Also make in-world floating name tags clickable.");
            _enableAvatarLinks = Config.Bind("Steam Profiles", "EnableAvatarLinks", true, "Also make supported player avatar images clickable.");
            _enableChatLinks = Config.Bind("Chat", "EnableChatLinks", true, "Make URLs in text chat clickable.");
            _enableChatHistory = Config.Bind("Chat", "EnableChatHistory", true, "Keep recent chat messages in memory and restore them when the chat UI is recreated.");
            _maxChatHistoryMessages = Config.Bind("Chat", "MaxChatHistoryMessages", 60, "Maximum number of chat messages kept in local history.");
            _persistChatHistoryAcrossLaunches = Config.Bind("Chat", "PersistChatHistoryAcrossLaunches", false, "Also save chat history to disk so it survives a full game restart.");

            _steamProfileLinkInjector = new SteamProfileLinkInjector(Logger, _enableSteamProfileLinks, _useSteamProtocol, _enableWorldNameTags, _enableAvatarLinks, _enableChatLinks);
            _chatHistoryManager = new ChatHistoryManager(Logger, _enableChatHistory, _maxChatHistoryMessages, _persistChatHistoryAcrossLaunches);

            Logger.LogInfo("Plugin loaded.");
            Logger.LogInfo("BetterCommunication adds UI-only Steam profile links and chat improvements without changing gameplay state.");
        }

        private void Update()
        {
            if (_steamProfileLinkInjector != null)
            {
                _steamProfileLinkInjector.Update();
            }

            if (_chatHistoryManager != null)
            {
                _chatHistoryManager.Update();
            }
        }
    }
}
