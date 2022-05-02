using System;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace ArcanumAutoPlay;

public unsafe class Plugin : IDalamudPlugin
{
    public string Name => "ArcanumAutoPlay";

    private delegate byte UseActionDelegate(ActionManager* actionManager, ActionType actionType, uint actionId,
        long targetId, uint a4, uint a5, uint a6, void* a7);

    private static Hook<UseActionDelegate> UseActionHook;

    private static byte UseActionDetour(ActionManager* actionManager, ActionType actionType, uint actionId,
        long targetId, uint a4, uint a5, uint a6, void* a7)
    {
        if (actionId != 17055)
        {
            return UseActionHook.Original(actionManager, actionType, actionId, targetId, a4, a5, a6, a7);
        }

        if (JobGaugeManager.Instance()->Astrologian.CurrentCard is
            AstrologianCard.None or AstrologianCard.Lady or AstrologianCard.Lord)
        {
            return UseActionHook.Original(actionManager, actionType, actionId, targetId, a4, a5, a6, a7);
        }

        var strategy = new AstrologianArcanumPlayStrategy();
        var target = strategy.SelectTarget();

        Services.ChatGui.Print($"[Arcanum] {JobGaugeManager.Instance()->Astrologian.CurrentCard} -> {target}");
        return UseActionHook.Original(actionManager, actionType, actionId, target.ObjectId, a4, a5, a6, a7);
    }

    public Plugin([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
        pluginInterface.UiBuilder.Draw += () => { };
        pluginInterface.UiBuilder.OpenConfigUi += () => { };

        UseActionHook = new Hook<UseActionDelegate>((IntPtr)ActionManager.fpUseAction, UseActionDetour);
        UseActionHook.Enable();
    }

    public void Dispose()
    {
        if (UseActionHook.IsEnabled)
        {
            UseActionHook.Disable();
        }
    }
}