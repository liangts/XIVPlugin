using System;
using System.Threading;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace ArcanumAutoPlay;

public unsafe class Plugin : IDalamudPlugin
{
    public string Name => "ArcanumAutoPlay";

    private Thread auto_play_thread = null;

    private static Thread? auto_play_thread_static = null;

    private CommandManager commandManager = null;

    private delegate byte UseActionDelegate(ActionManager* actionManager, ActionType actionType, uint actionId,
        long targetId, uint a4, uint a5, uint a6, void* a7);

    private static Hook<UseActionDelegate> UseActionHook;

    private static byte UseActionDetour(ActionManager* actionManager, ActionType actionType, uint actionId,
        long targetId, uint a4, uint a5, uint a6, void* a7)
    {

        if (actionId == ConstantsActionId.ActionId("AST_Draw_Arcanum"))
        {
            HookArcanumAutoPlay();
            return UseActionHook.Original(actionManager, actionType, actionId, targetId, a4, a5, a6, a7);
        }

        if (actionId != ConstantsActionId.ActionId("AST_Play_Arcanum"))
        {
            return UseActionHook.Original(actionManager, actionType, actionId, targetId, a4, a5, a6, a7);
        }

        if (JobGaugeManager.Instance()->Astrologian.CurrentCard is
            AstrologianCard.None or AstrologianCard.Lady or AstrologianCard.Lord)

        {
            Services.ChatGui.Print($"[[Arcanum] Direct Pass");
            return UseActionHook.Original(actionManager, actionType, actionId, targetId, a4, a5, a6, a7);
        }

        var strategy = new AstrologianArcanumPlayStrategy();
        var target = strategy.SelectTarget();

        Services.ChatGui.Print($"[Arcanum] {JobGaugeManager.Instance()->Astrologian.CurrentCard} -> {target}");
        return UseActionHook.Original(actionManager, actionType, actionId, target.ObjectId, a4, a5, a6, a7);
    }

    public Plugin([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface, CommandManager commandManager)
    {
        pluginInterface.Create<Services>();
        pluginInterface.UiBuilder.Draw += () => { };
        pluginInterface.UiBuilder.OpenConfigUi += () => { };

        UseActionHook = new Hook<UseActionDelegate>((IntPtr)ActionManager.fpUseAction, UseActionDetour);
        UseActionHook.Enable();

        this.commandManager = commandManager;
        this.commandManager.AddHandler("/parcanumautoplay", new CommandInfo(OnCommandArcanumAutoPlay)
        {
            HelpMessage = "Dummy Help Message"
        });
    }

    private void OnCommandArcanumAutoPlay(string command, string args)
    {
        return;
        if (auto_play_thread == null)
        {
            AutoPlayArcanum.AutoPlayAracnumOnNextGcdStopFlag = false;
            AutoPlayArcanum.conditionCheckStopFlag = false;
            auto_play_thread = new Thread(AutoPlayArcanum.AutoPlayAracnumOnNextGcd);
            auto_play_thread.Start();
        }
        else
        {
            AutoPlayArcanum.conditionCheckStopFlag = true;
            AutoPlayArcanum.AutoPlayAracnumOnNextGcdStopFlag = true;
            //auto_play_thread.Abort();
            auto_play_thread = null;
        }

    }

    private static void HookArcanumAutoPlay()
    {
        if (auto_play_thread_static == null)
        {
            AutoPlayArcanum.AutoPlayAracnumOnNextGcdStopFlag = false;
            AutoPlayArcanum.conditionCheckStopFlag = false;
            auto_play_thread_static = new Thread(AutoPlayArcanum.AutoPlayAracnumOnNextGcd);
            auto_play_thread_static.Start();
            return;
        }
        if (auto_play_thread_static != null && AutoPlayArcanum.AutoPlayAracnumOnNextGcdStopFlag == false)
        {
            // A thread already in running.
            return;
        }
        if (auto_play_thread_static != null && AutoPlayArcanum.AutoPlayAracnumOnNextGcdStopFlag == true)
        {
            // A finished thread.
            AutoPlayArcanum.conditionCheckStopFlag = false;
            AutoPlayArcanum.AutoPlayAracnumOnNextGcdStopFlag = false;
            //auto_play_thread_static.Abort();
            auto_play_thread_static = new Thread(AutoPlayArcanum.AutoPlayAracnumOnNextGcd);
            auto_play_thread_static.Start();
            return;
        }
    }

    public void Dispose()
    {
        if (UseActionHook.IsEnabled)
        {
            UseActionHook.Disable();
        }
        AutoPlayArcanum.AutoPlayAracnumOnNextGcdStopFlag = true;
        auto_play_thread_static = null;
        this.commandManager.RemoveHandler("/parcanumautoplay");
    }
}