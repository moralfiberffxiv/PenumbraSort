using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;

namespace PenumbraSort;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "PenumbraSort";
    private const string CommandName = "/penumbrasort";

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; init; }
    private PluginUI PluginUI { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        PluginUI = new PluginUI(Configuration);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open PenumbraSort mod organizer"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += DrawMainUI;
    }

    public void Dispose()
    {
        PluginUI.Dispose();
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= DrawMainUI;
    }

    private void OnCommand(string command, string args) => PluginUI.Visible = !PluginUI.Visible;
    private void DrawUI() => PluginUI.Draw();
    private void DrawConfigUI() => PluginUI.Visible = true;
    private void DrawMainUI() => PluginUI.Visible = true;
}
