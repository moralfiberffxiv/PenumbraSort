using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;

namespace PenumbraSort;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/penumbrasort";

    // Services injected via constructor by Dalamud
    public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    private readonly ICommandManager _commandManager;
    private readonly IPluginLog      _log;

    public Configuration Configuration { get; init; }
    private PluginUI    PluginUI    { get; init; }
    private LiveWatcher LiveWatcher { get; init; }

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log)
    {
        PluginInterface  = pluginInterface;
        _commandManager  = commandManager;
        _log             = log;

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        LiveWatcher = new LiveWatcher(_log);
        PluginUI    = new PluginUI(Configuration, LiveWatcher);

        _commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open PenumbraSort mod organizer"
        });

        PluginInterface.UiBuilder.Draw       += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        PluginInterface.UiBuilder.OpenMainUi  += DrawMainUI;
    }

    public void Dispose()
    {
        PluginUI.Dispose();
        LiveWatcher.Dispose();
        _commandManager.RemoveHandler(CommandName);
        PluginInterface.UiBuilder.Draw       -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        PluginInterface.UiBuilder.OpenMainUi  -= DrawMainUI;
    }

    private void OnCommand(string command, string args) => PluginUI.Visible = !PluginUI.Visible;
    private void DrawUI()       => PluginUI.Draw();
    private void DrawConfigUI() => PluginUI.Visible = true;
    private void DrawMainUI()   => PluginUI.Visible = true;
}
