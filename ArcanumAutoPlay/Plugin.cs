using System;
using System.Collections.Generic;
using System.Threading;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ArcanumDrawer
{

    public class Plugin : IDalamudPlugin
    {
        public string Name => "ArcanumAutoPlay";

        private ClientState ClientState { get; }
        private ChatGui ChatGui { get; }
        private CommandManager CommandManager { get; }
        private PartyList PartyList { get; }

        private Thread GcdThread_;

        private bool GcdCheckRun_ = false;

        private GCDState GcdState_ = GCDState.IDLE;

        public Plugin(
            [RequiredVersion("0.1")] DalamudPluginInterface pluginInterface,
            ClientState clientState,
            ChatGui chatGui,
            CommandManager commandManager,
            PartyList partyList)
        {
            ClientState = clientState;
            ChatGui = chatGui;
            CommandManager = commandManager;
            PartyList = partyList;

            CommandManager.AddHandler("/parcanumautoplay", new CommandInfo(OnCommandArcanumAutoPlay)
            {
                HelpMessage = "Dummy Help Message"
            });

            pluginInterface.UiBuilder.Draw += () => { };
            pluginInterface.UiBuilder.OpenConfigUi += () => { };
        }

        public void Dispose()
        {
            CommandManager.RemoveHandler("/parcanumautoplay");
        }

        private enum Arcanum
        {
            Balance,
            Arrow,
            Spear,
            Bole,
            Ewer,
            Spire
        }
        private enum GCDState { IDLE, POST_ACTION, QUEUE_AVAIL, SAFE, ERROR };

        private static readonly Dictionary<string, uint> MeleeJobClassWeights = new Dictionary<string, uint>()
    {
        { "GLA", 0 }, { "PLD", 2 },
        { "MRD", 0 }, { "WAR", 2 },
        { "DRK", 2 },
        { "GNB", 2 },
        { "PGL", 1 }, { "MNK", 4 },
        { "LNC", 1 }, { "DRG", 3 },
        { "ROG", 1 }, { "NIN", 3 },
        { "SAM", 4 },
        { "RPR", 4 }
    };

        private static bool IsMeleeArcanum(Arcanum arcanum) => arcanum switch
        {
            Arcanum.Balance or Arcanum.Arrow or Arcanum.Spear => true,
            _ => false
        };


        private static readonly Dictionary<string, uint> RangeJobClassWeights = new Dictionary<string, uint>()
    {
        { "CNJ", 0 }, { "WHM", 2 },
        { "ACN", 1 }, { "SCH", 2 },
        { "AST", 2 },
        { "SGE", 2 },
        { "ARC", 1 }, { "BRD", 3 },
        { "MCH", 3 },
        { "DNC", 3 },
        { "THM", 1 }, { "BLM", 4 },
        { "SMN", 4 },
        { "RDM", 4 },
    };

        private static bool IsRangeArcanum(Arcanum arcanum) => arcanum switch
        {
            Arcanum.Bole or Arcanum.Ewer or Arcanum.Spire => true,
            _ => false
        };

        private unsafe bool HasArcanumDrew()
        {
            if (JobGaugeManager.Instance()->Astrologian.CurrentCard != FFXIVClientStructs.FFXIV.Client.Game.Gauge.AstrologianCard.None)
            {
                return true;
            }
            return false;

        }

        private static readonly Dictionary<Arcanum, uint> PlayActionId = new Dictionary<Arcanum, uint>()
        {
            { Arcanum.Balance, 4401 },
            { Arcanum.Arrow, 4402 },
            { Arcanum.Spear, 4403 },
            { Arcanum.Bole, 4404 },
            { Arcanum.Ewer, 4405 },
            { Arcanum.Spire, 4406 }
        };

        private static readonly Random Random = new Random();
        private static bool _workerThreadOnHold;

        private unsafe void GcdCheckRun()
        {
            while (GcdCheckRun_)
            {
                if (ActionManager.Instance()->IsRecastTimerActive(ActionType.Spell, 3599)){
                    // Is Casting
                    var GcdRecastTime = ActionManager.Instance()->GetRecastTime(ActionType.Spell, 3599);
                    if (GcdRecastTime <= 0)
                    {
                        this.GcdState_ = GCDState.ERROR;
                    }
                    var GcdRecastElasped = ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Spell, 3599);
                    if (GcdRecastElasped > (GcdRecastTime / 3))
                    {
                        this.GcdState_ = GCDState.POST_ACTION;
                    } else if (GcdRecastElasped > (GcdRecastTime * 2 / 3)) {
                        this.GcdState_ = GCDState.QUEUE_AVAIL;
                    }
                    else
                    {
                        this.GcdState_ = GCDState.SAFE;
                    }
                    ChatGui.Print("GCD State: " + this.GcdState_.ToString());
                    continue;
                }
                this.GcdState_ = GCDState.IDLE;
                ChatGui.Print("GCD State: " + this.GcdState_.ToString());
                Thread.Sleep(150);
            }
            return;
        }

        private unsafe void ArcanumAutoPlay()
        {
            if (_workerThreadOnHold) return;
            _workerThreadOnHold = true;
            PluginLog.Debug(Name + "Start AutoPlay Thread;");
            //GCDStateManager GcdStateManager_ = new GCDStateManager(ClientState, ChatGui);
            //var GcdThread = new Thread(GcdStateManager_.MonitorGCDState);
            //GcdThread.Start();
            ChatGui.Print("A");
            if (!ClientState.LocalPlayer.StatusFlags.HasFlag(Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat))
            {
                ChatGui.Print("Not in Combat");
                _workerThreadOnHold = false;
                return;
            }
            ChatGui.Print("B");
            try
            {
                //var actionManager = ActionManager.Instance();
                var localPlayer = ClientState.LocalPlayer!.ObjectId;

                const uint actionIdDraw = 3590;
                const ActionType actionTypeDraw = ActionType.Spell;
                PluginLog.Debug(Name + "Got ActionManager and localPlayer");
                if (!HasArcanumDrew())
                {
                    if (ActionManager.Instance()->GetActionStatus(actionTypeDraw, actionIdDraw, localPlayer, 0, 0) != 0)
                    {
                        return;
                    }
                    ChatGui.Print("C");
                    // Combust ID - 3599
                    var gcd = ActionManager.Instance()->GetRecastTime(ActionType.Spell, 3599);
                    var gcdElapsed = ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Spell, 3599);

                    while ((gcd != 0) && (((gcdElapsed <= 1) || (gcd - gcdElapsed <= 1))))
                    {
                        //ChatGui.Print("GCD: " + gcd.ToString() + " Elapsed: " + gcdElapsed.ToString());
                        gcdElapsed = ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Spell, 3599);
                        //Thread.Sleep(100);
                        Thread.Yield();
                    }

                    ChatGui.Print("here");
                    ChatGui.Print("D");
                    try
                    {
                        bool status = ActionManager.Instance()->UseAction(actionTypeDraw, actionIdDraw, ClientState.LocalPlayer!.ObjectId);
                    }
                    catch (InvalidOperationException e)
                    {
                        ChatGui.Print(e.ToString());
                        ChatGui.Print("failed");
                        _workerThreadOnHold = false;
                        return;
                    }
                    /*
                    if (!ActionManager.Instance()->UseAction(actionTypeDraw, actionIdDraw, ClientState.LocalPlayer!.ObjectId))
                    {
                        ChatGui.Print("failed");
                        return;
                    }
                    */
                    ChatGui.Print("E");
                    ChatGui.Print("success");
                }

                var arcanum = Arcanum.Balance;
                ChatGui.Print("F");
                while (!HasArcanumDrew())
                {
                    Thread.Yield();
                }
                ChatGui.Print("G");
                if (JobGaugeManager.Instance()->Astrologian.CurrentCard != FFXIVClientStructs.FFXIV.Client.Game.Gauge.AstrologianCard.None)
                {
                    arcanum = JobGaugeManager.Instance()->Astrologian.CurrentCard switch
                    {
                        FFXIVClientStructs.FFXIV.Client.Game.Gauge.AstrologianCard.Balance => Arcanum.Balance,
                        FFXIVClientStructs.FFXIV.Client.Game.Gauge.AstrologianCard.Bole => Arcanum.Bole,
                        FFXIVClientStructs.FFXIV.Client.Game.Gauge.AstrologianCard.Spear => Arcanum.Spear,
                        FFXIVClientStructs.FFXIV.Client.Game.Gauge.AstrologianCard.Spire => Arcanum.Spire,
                        FFXIVClientStructs.FFXIV.Client.Game.Gauge.AstrologianCard.Ewer => Arcanum.Ewer,
                        FFXIVClientStructs.FFXIV.Client.Game.Gauge.AstrologianCard.Arrow => Arcanum.Arrow,
                        _ => arcanum
                    };
                }
                ChatGui.Print("H");

                var target = FindBestCandidate(arcanum);
                var actionIdPlay = PlayActionId[arcanum];
                const ActionType actionTypePlay = ActionType.Spell;

                ChatGui.Print($"[Arcanum] {arcanum} -> {target}");

                var gcd2 = ActionManager.Instance()->GetRecastTime(ActionType.Spell, 3599);
                var gcdElapsed2 = ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Spell, 3599);

                while ((gcd2 != 0) && (((gcdElapsed2 <= 1) || (gcd2 - gcdElapsed2 <= 1))))
                {
                    gcdElapsed2 = ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Spell, 3599);
                    Thread.Yield();
                }
                ChatGui.Print("I");
                ActionManager.Instance()->UseAction(actionTypePlay, actionIdPlay, target.ObjectId);
                ChatGui.Print("Z");
            }
            catch (Exception exception)
            {
                ChatGui.Print(exception.ToString());
            }
            finally
            {
                _workerThreadOnHold = false;

            }
            ChatGui.Print("[Arcanum] Thread Exit Successfully");
            GcdCheckRun_ = false;
            return;
        }


        private GameObject FindBestCandidate(Arcanum arcanum)
        {
            var candidates = new List<KeyValuePair<PartyMember, uint>>(8);

            foreach (var partyMember in PartyList)
            {
                var jobAbbr = partyMember.ClassJob.GameData!.Abbreviation.ToString().ToUpper();

                if (MeleeJobClassWeights.ContainsKey(jobAbbr) && IsMeleeArcanum(arcanum))
                {
                    var rank = MeleeJobClassWeights[jobAbbr];
                    candidates.Add(new KeyValuePair<PartyMember, uint>(partyMember, rank));
                }

                if (RangeJobClassWeights.ContainsKey(jobAbbr) && IsRangeArcanum(arcanum))
                {
                    var rank = RangeJobClassWeights[jobAbbr];
                    candidates.Add(new KeyValuePair<PartyMember, uint>(partyMember, rank));
                }
            }

            if (candidates.Count == 0)
            {
                return ClientState.LocalPlayer!;
            }

            candidates.Sort((lhs, rhs) => lhs.Value.CompareTo(rhs.Value));
            candidates.Reverse();

            var bestRank = candidates[0].Value;
            candidates = candidates.FindAll(candidate => candidate.Value == bestRank);

            var chosenOne = candidates[Random.Next(candidates.Count)].Key;

            return chosenOne.GameObject!;
        }


        private void OnCommandArcanumAutoPlay(string command, string args)
        {   
            if (_workerThreadOnHold == true)
            {
                ChatGui.Print("Still a thread working");
            }
            //var GcdCheckThread = new Thread(GcdCheckRun);
            var workerThread = new Thread(ArcanumAutoPlay);
            //this.GcdCheckRun_ = true;
            //GcdCheckThread.Start();
            workerThread.Start();
        }
    }

    public class GCDStateManager
    {

        private ClientState ClientState_;

        private ChatGui ChatGui_;

        public enum GCDState { IDLE, POST_ACTION, QUEUE_AVAIL, SAFE };

        private GCDState GcdState_;

        public GCDStateManager(ClientState ClientState, ChatGui chatGui)
        {
            this.ClientState_ = ClientState;
            this.ChatGui_ = chatGui;
            this.GcdState_ = GCDState.IDLE;
        }

        public unsafe void MonitorGCDState()
        {
            while (ClientState_.LocalPlayer.StatusFlags.HasFlag(Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat))
            {
                this.ChatGui_.Print("In Combat;");
                Thread.Sleep(5000);
            }
        }

    }

}