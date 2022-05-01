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

        private bool HasArcanumDrew()
        {
            foreach (var status in ClientState.LocalPlayer!.StatusList)
            {
                if (status.StatusId == 2713) // Status: Clarifying Draw
                {
                    return true;
                }
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

        private unsafe void ArcanumAutoPlay()
        {
            if (_workerThreadOnHold) return;
            _workerThreadOnHold = true;

            //GCDStateManager GcdStateManager_ = new GCDStateManager(ClientState, ChatGui);
            //var GcdThread = new Thread(GcdStateManager_.MonitorGCDState);
            //GcdThread.Start();
            //if (!ClientState.LocalPlayer.StatusFlags.HasFlag(Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat))
            //{
            //    ChatGui.Print("Not in Combat");
            //    return;
            //}

            try
            {
                var actionManager = ActionManager.Instance();
                var localPlayer = ClientState.LocalPlayer!.ObjectId;

                const uint actionIdDraw = 3590;
                const ActionType actionTypeDraw = ActionType.Spell;

                if (!HasArcanumDrew())
                {
                    if (actionManager->GetActionStatus(actionTypeDraw, actionIdDraw, localPlayer, 0, 0) != 0)
                    {
                        return;
                    }

                    var gcd = actionManager->GetRecastTime(ActionType.Spell, 3599);
                    var gcdElapsed = actionManager->GetRecastTimeElapsed(ActionType.Spell, 3599);

                    while ((gcd != 0) && (((gcdElapsed <= 1) || (gcd - gcdElapsed <= 1))))
                    {
                        Thread.Yield();
                    }

                    ChatGui.Print("here");

                    if (!actionManager->UseAction(actionTypeDraw, actionIdDraw, ClientState.LocalPlayer!.ObjectId))
                    {
                        ChatGui.Print("failed");
                        return;
                    }

                    ChatGui.Print("success");
                }

                var arcanum = Arcanum.Balance;

                while (!HasArcanumDrew())
                {
                    Thread.Sleep(1000);
                    Thread.Yield();
                }

                foreach (var status in ClientState.LocalPlayer!.StatusList)
                {
                    arcanum = status.StatusId switch
                    {
                        913 => Arcanum.Balance,
                        915 => Arcanum.Arrow,
                        916 => Arcanum.Spear,
                        914 => Arcanum.Bole,
                        917 => Arcanum.Ewer,
                        918 => Arcanum.Spire,
                        _ => arcanum
                    };
                }

                var target = FindBestCandidate(arcanum);
                var actionIdPlay = PlayActionId[arcanum];
                const ActionType actionTypePlay = ActionType.Spell;

                ChatGui.Print($"[Arcanum] {arcanum} -> {target}");

                var gcd2 = actionManager->GetRecastTime(ActionType.Spell, 3599);
                var gcdElapsed2 = actionManager->GetRecastTimeElapsed(ActionType.Spell, 3599);

                while ((gcd2 != 0) && (((gcdElapsed2 <= 1) || (gcd2 - gcdElapsed2 <= 1))))
                {
                    Thread.Yield();
                }

                actionManager->UseAction(actionTypePlay, actionIdPlay, target.ObjectId);
            }
            catch (Exception exception)
            {
                ChatGui.Print(exception.Message);
            }
            finally
            {
                _workerThreadOnHold = false;
            }
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
            var workerThread = new Thread(ArcanumAutoPlay);
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