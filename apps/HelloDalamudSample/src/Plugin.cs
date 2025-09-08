using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace HelloDalamudSample;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Hello Dalamud Sample";

    private readonly IDalamudPluginInterface _pi;
    private readonly ICommandManager _cmd;
    private readonly IChatGui _chat;
    private readonly IPluginLog _log;

    private const string CmdHello = "/hello";
    private const string CmdRoot = "/hds";

    public Configuration Config { get; }

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chat,
        IPluginLog log)
    {
        _pi = pluginInterface;
        _cmd = commandManager;
        _chat = chat;
        _log = log;

        Config = _pi.GetPluginConfig() as Configuration ?? new Configuration();

        _cmd.AddHandler(CmdHello, new CommandInfo(OnCmdHello)
        {
            HelpMessage = "Say hello from the sample plugin"
        });
        _cmd.AddHandler(CmdRoot, new CommandInfo(OnCmdRoot)
        {
            HelpMessage = "HelloDalamudSample root. Try: /hds help"
        });

        _log.Information("HelloDalamudSample initialized");
    }

    public void Dispose()
    {
        _cmd.RemoveHandler(CmdHello);
        _cmd.RemoveHandler(CmdRoot);
    }

    private void OnCmdHello(string cmd, string args)
    {
        if (Config.EnabledGreeting)
            _chat.Print("[HDS] Hello from Dalamud sample!");
        else
            _chat.Print("[HDS] Greeting is disabled in config.");
    }

    private void OnCmdRoot(string cmd, string args)
    {
        var a = (args ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(a) || a == "help")
        {
            _chat.Print("[HDS] Commands: /hello, /hds on, /hds off");
            return;
        }

        switch (a)
        {
            case "on":
                Config.EnabledGreeting = true;
                Config.Save(_pi);
                _chat.Print("[HDS] Greeting enabled.");
                break;
            case "off":
                Config.EnabledGreeting = false;
                Config.Save(_pi);
                _chat.Print("[HDS] Greeting disabled.");
                break;
            default:
                _chat.Print($"[HDS] Unknown: {args}");
                break;
        }
    }
}

