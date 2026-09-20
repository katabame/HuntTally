using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using HuntTally.Windows;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Game.Chat;
using System.Linq;

namespace HuntTally;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/hunttally";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("HuntTally");
    private MainWindow MainWindow { get; init; }

    private static readonly Regex KillRegex = new(@"^(?<playerName>.+?)は、(?<mobName>.+?)を倒した。?$", RegexOptions.Compiled);

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "HuntTallyのメインウィンドウを表示/非表示します。"
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // Adds another button doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        ChatGui.ChatMessage += OnChatMessage;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [SamplePlugin] ===A cool log message from Sample Plugin===
        Log.Information($"{PluginInterface.Manifest.Name} loaded.");
    }

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        
        WindowSystem.RemoveAllWindows();
        ((System.IDisposable)MainWindow).Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // In response to the slash command, toggle the display status of our main ui
        MainWindow.Toggle();
    }

    public void ToggleMainUi() => MainWindow.Toggle();

    private void OnChatMessage(IHandleableChatMessage message)
    {
        //Log.Debug($"{message.Message}: SourceKind {message.SourceKind}, TargetKind {message.TargetKind}");
        if (message.TargetKind != XivChatRelationKind.UnengagedEnemy) return;
        var match = KillRegex.Match(message.Message.ToString());
        if (!match.Success) return;
        var mobName = match.Groups["mobName"].Value.Trim();
        if (string.IsNullOrEmpty(mobName)) return;

        if (!Configuration.KillCounts.TryGetValue(mobName, out var territories))
        {
            territories = [];
            Configuration.KillCounts[mobName] = territories;
        }

        var territoryId = ClientState.TerritoryType;
        territories.TryGetValue(territoryId, out var count);
        territories[territoryId] = count + 1;

        Configuration.Save();

        var total = territories.Values.Sum();

        Log.Debug($"Kill counted: {mobName} in territory {territoryId} (this area: {territories[territoryId]}, total: {total})");
    }
}
