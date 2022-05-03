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
        Services.ChatGui.Print("[AutoPlayAracnumOnNextGcd] Start");
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
                Thread.Yield();
            }
            PlayAracnum();
            isCardAvailable = false;
            isGcdSafeToPlay = false;
        }
        gcd_state_manager.GcdCheckStop();
        //conditionCheckStopFlag = true;
        Services.ChatGui.Print("[AutoPlayAracnumOnNextGcd] End");
    }

    public static unsafe void PlayConditionCheck()
    {
        Services.ChatGui.Print("[PlayConditionCheck] Start");
        while (!conditionCheckStopFlag)
        {
            if (GetPlayAracnumCardId(JobGaugeManager.Instance()->Astrologian.CurrentCard) == 0)
            {
                isCardAvailable = false;
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
            Thread.Sleep(50);
        }
        Services.ChatGui.Print("[PlayConditionCheck] End");
    }

    public static unsafe void PlayAracnum()
    {
        if (AutoPlayArcanum.isCardAvailable)
        {
            var strategy = new AstrologianArcanumPlayStrategy();
            var target = strategy.SelectTarget();

            ActionManager.Instance()->UseAction(ActionType.Spell, GetPlayAracnumCardId(JobGaugeManager.Instance()->Astrologian.CurrentCard), target.ObjectId);
            AutoPlayAracnumOnNextGcdStopFlag = true;
            conditionCheckStopFlag = true;

        }
    }
}

