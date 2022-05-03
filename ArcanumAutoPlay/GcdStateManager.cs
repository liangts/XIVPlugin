using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Dalamud.IoC;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ArcanumAutoPlay;

public class GcdStateManager
{
    private enum GCDState { IDLE, POST_ACTION, QUEUE_AVAIL, SAFE, ERROR };

    private bool GcdCheckRun_ = false;

    private GCDState GcdState_ = GCDState.IDLE;

    private Thread? GcdCheckThread_ = null;

    private unsafe void GcdCheckRun()
    {
        //Services.ChatGui.Print("Check Action: " + ConstantsActionId.ActionId("Malefic IV").ToString());

        while (GcdCheckRun_)
        {

            if (ActionManager.Instance()->IsRecastTimerActive(ActionType.Spell, ConstantsActionId.ActionId("Malefic IV")))
            {
                // Is Casting
                float GcdRecastTime = ActionManager.Instance()->GetRecastTime(ActionType.Spell, ConstantsActionId.ActionId("Malefic IV"));
                float GcdRecastElasped = ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Spell, ConstantsActionId.ActionId("Malefic IV"));
                //Services.ChatGui.Print("GCD Recast: " + GcdRecastTime.ToString() + "GCD Elasped: " + GcdRecastElasped.ToString());
                if (GcdRecastTime <= 0) this.GcdState_ = GCDState.ERROR;
                if (GcdRecastElasped < (GcdRecastTime / 3.0f))
                {
                    this.GcdState_ = GCDState.POST_ACTION;
                }
                else if (GcdRecastElasped > (GcdRecastTime * 2.0f / 3.0f))
                {
                    this.GcdState_ = GCDState.QUEUE_AVAIL;
                }
                else
                {
                    this.GcdState_ = GCDState.SAFE;
                }

            }
            else
            {
                this.GcdState_ = GCDState.IDLE;
            }
            //Services.ChatGui.Print("GCD State: " + GcdState_.ToString());
            Thread.Sleep(250);
        }
        return;
    }

    public void GcdCheckStart()
    {
        this.GcdCheckRun_ = true;
        this.GcdCheckThread_ = new Thread(GcdCheckRun);
        this.GcdCheckThread_.Start();
    }

    public void GcdCheckStop()
    {
        this.GcdCheckRun_ = false;
        if (this.GcdCheckThread_ != null) 
        {
            this.GcdCheckThread_.Join();
            this.GcdCheckThread_ = null;
        }
    }

    public bool IsGcdCheckRunning()
    {
        return this.GcdCheckRun_;
    }

    public bool IsGcdSafe()
    {
        return this.GcdState_ == GCDState.SAFE;
    }
}