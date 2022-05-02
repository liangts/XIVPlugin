using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;

namespace ArcanumAutoPlay;

public class Services
{
    [PluginService] public static ChatGui ChatGui { get; private set; }
    [PluginService] public static PartyList PartyList { get; private set; }
    [PluginService] public static ClientState ClientState { get; private set; }
}