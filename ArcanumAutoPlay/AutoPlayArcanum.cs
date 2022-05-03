using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace ArcanumAutoPlay;

public static class AutoPlayArcanum
{
    private static GcdStateManager gcd_state_manager = new GcdStateManager();

    private static bool isCardAvailable = false;

    private static bool isGcdSafeToPlay = false;

    public static bool conditionCheckStopFlag = false;

    public static bool AutoPlayAracnumOnNextGcdStopFlag = false;
    private static uint GetPlayAracnumCardId(AstrologianCard arcanum) => arcanum switch
    {
        AstrologianCard.Arrow => ConstantsActionId.ActionId("AST_Play_TheArrow"),
        AstrologianCard.Bole => ConstantsActionId.ActionId("AST_Play_TheBole"),
        AstrologianCard.Spear => ConstantsActionId.ActionId("AST_Play_TheSpear"),
        AstrologianCard.Ewer => ConstantsActionId.ActionId("AST_Play_TheEwer"),
        AstrologianCard.Spire => ConstantsActionId.ActionId("AST_Play_TheSpire"),
        AstrologianCard.Balance => ConstantsActionId.ActionId("AST_Play_TheBalance"),
        _ => 0u,
    };
    public static unsafe void AutoPlayAracnumOnNextGcd()
    {
        //Services.ChatGui.Print("[AutoPlayAracnumOnNextGcd] Start");
        if (!gcd_state_manager.IsGcdCheckRunning())
        {
            gcd_state_manager.GcdCheckStart();
        }
        Thread conditionCheckThread = new Thread(PlayConditionCheck);
        conditionCheckThread.Start();
        while (!AutoPlayAracnumOnNextGcdStopFlag)
        {
            while (!AutoPlayAracnumOnNextGcdStopFlag && (!(isCardAvailable && isGcdSafeToPlay)))
            {
                Thread.Sleep(150);
            }
            PlayAracnum();
            isCardAvailable = false;
            isGcdSafeToPlay = false;
        }
        gcd_state_manager.GcdCheckStop();
        //conditionCheckStopFlag = true;
        //Services.ChatGui.Print("[AutoPlayAracnumOnNextGcd] End");
    }

    public static unsafe void PlayConditionCheck()
    {
        //Services.ChatGui.Print("[PlayConditionCheck] Start");
        while (!conditionCheckStopFlag)
        {
            if ((JobGaugeManager.Instance()->Astrologian.CurrentCard == AstrologianCard.None) && GetPlayAracnumCardId(JobGaugeManager.Instance()->Astrologian.CurrentCard) == 0)
            {
                //Services.ChatGui.Print("isFalse: " + JobGaugeManager.Instance()->Astrologian.CurrentCard.ToString());
                isCardAvailable = false;
            }
            else if ((int)JobGaugeManager.Instance()->Astrologian.CurrentCard > (int)AstrologianCard.Lord &&
                     (int)JobGaugeManager.Instance()->Astrologian.CurrentCard < (int)AstrologianCard.Lady)
            {
                //Services.ChatGui.Print("Has Lord");
                isCardAvailable = true;
            }
            else if ((int)JobGaugeManager.Instance()->Astrologian.CurrentCard > (int)AstrologianCard.Lady &&
                     (int)JobGaugeManager.Instance()->Astrologian.CurrentCard <= 134) // 134 is Lady + spire
            {
                //Services.ChatGui.Print("Has Lady");
                isCardAvailable = true;
            }
            else
            {
                isCardAvailable = true;
            }
            if (gcd_state_manager.IsGcdSafe())
            {
                isGcdSafeToPlay = true;
            }
            else
            {
                isGcdSafeToPlay = false;
            }
            Thread.Sleep(150);
        }
        //Services.ChatGui.Print("[PlayConditionCheck] End");
    }

    public static unsafe void PlayAracnum()
    {
        if (AutoPlayArcanum.isCardAvailable)
        {
            var strategy = new AstrologianArcanumPlayStrategy();
            Dalamud.Game.ClientState.Objects.Types.GameObject target;
            try
            {
                target = strategy.SelectTarget();
            }
            catch (System.NullReferenceException e)
            {
                Services.ChatGui.Print("[Arcanum] " + e.ToString());
                target = Services.ClientState.LocalPlayer!;
            }
            if (JobGaugeManager.Instance()->Astrologian.CurrentCard > AstrologianCard.Lord)
            {
                
                AstrologianCard actual = (AstrologianCard)((int)JobGaugeManager.Instance()->Astrologian.CurrentCard - AstrologianCard.Lord);
                Services.ChatGui.Print($"[Arcanum] {actual} -> {target}");
                ActionManager.Instance()->UseAction(ActionType.Spell, GetPlayAracnumCardId(actual), target.ObjectId);
            }
            else if (JobGaugeManager.Instance()->Astrologian.CurrentCard > AstrologianCard.Lady)
            {
                
                AstrologianCard actual = (AstrologianCard)((int)JobGaugeManager.Instance()->Astrologian.CurrentCard - AstrologianCard.Lady);
                Services.ChatGui.Print($"[Arcanum] {actual} -> {target}");
                ActionManager.Instance()->UseAction(ActionType.Spell, GetPlayAracnumCardId(actual), target.ObjectId);
            }
            else
            {

                Services.ChatGui.Print($"[Arcanum] {JobGaugeManager.Instance()->Astrologian.CurrentCard} -> {target}");
                ActionManager.Instance()->UseAction(ActionType.Spell, GetPlayAracnumCardId(JobGaugeManager.Instance()->Astrologian.CurrentCard), target.ObjectId);
            }
                AutoPlayAracnumOnNextGcdStopFlag = true;
                conditionCheckStopFlag = true;

        }
    }
}

