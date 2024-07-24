using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using OSCPlugin.Windows;
using Rug.Osc;
using System.Threading;
using System;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace OSCPlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog loggers { get; private set; } = null!;
    [PluginService] internal static IGameConfig gamecfg { get; private set; } = null!;

    private const string CommandName = "/osc";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("OSCPlugin");
    private ConfigWindow ConfigWindow { get; init; }
    static OscReceiver receiver;
    static Thread thread; 
    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "opens the config window for OSCPlugin"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        receiver = new OscReceiver(42069);
        loggers.Info("starting listener for OSC");

        // Create a thread to do the listening
        thread = new Thread(new ThreadStart(ListenLoop));

        // Connect the receiver
        receiver.Connect();

        // Start the listen thread
        thread.Start();


    }

    static void ListenLoop()
    {
        try
        {
            while (receiver.State != OscSocketState.Closed)
            {
                if (receiver.State == OscSocketState.Connected)
                {
                    OscPacket packet = receiver.Receive();
                    string[] values = packet.ToString().Split(", ");
                    if (values[0].Equals("/slider/1"))
                    {
                        string betterstring = values[1].Remove(values[1].Length - 1);
                        float f = float.Parse(betterstring);
                        int slidervolume = (int)Math.Round(f*100);
                        gamecfg.Set(Dalamud.Game.Config.SystemConfigOption.SoundBgm, (uint)slidervolume);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            loggers.Error(ex, "error in listening thread.");
        }
    }

    public void Dispose()
    {

                // close the Reciver 
        receiver.Close();

        // Wait for the listen thread to exit
        thread.Join();
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleConfigUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
}
