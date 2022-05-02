using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace ArcanumAutoPlay;

public class AstrologianArcanumPlayStrategy
{
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

    private static bool IsMeleeArcanum(AstrologianCard arcanum) => arcanum switch
    {
        AstrologianCard.Balance or AstrologianCard.Arrow or AstrologianCard.Spear => true,
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

    private static bool IsRangeArcanum(AstrologianCard arcanum) => arcanum switch
    {
        AstrologianCard.Bole or AstrologianCard.Ewer or AstrologianCard.Spire => true,
        _ => false
    };

    private static readonly Dictionary<AstrologianCard, uint> ArcanumStatuses = new Dictionary<AstrologianCard, uint>()
    {
        { AstrologianCard.Balance, 829 },
        { AstrologianCard.Bole, 830 },
        { AstrologianCard.Spear, 832 },
        { AstrologianCard.Spire, 834 },
        { AstrologianCard.Ewer, 833 },
        { AstrologianCard.Arrow, 831 }
    };

    private static bool HasAstrologianStatus(PartyMember partyMember) =>
        partyMember.Statuses
            .Select(status =>
            {
                if (status.RemainingTime <= 2.5) return false;
                return status.StatusId switch
                {
                    829 or 830 or 831 or 832 or 833 or 834 => true,
                    _ => false
                };
            })
            .Any(isAstrologianStatus => isAstrologianStatus);

    private static bool HasCorrectAstrologianStatus(PartyMember partyMember)
    {
        var jobAbbr = partyMember.ClassJob.GameData!.Abbreviation.ToString().ToUpper();

        if (MeleeJobClassWeights.ContainsKey(jobAbbr))
        {
            return partyMember.Statuses.Select(status =>
            {
                if (status.RemainingTime <= 2.5) return false;
                return status.StatusId switch
                {
                    829 or 831 or 832 => true,
                    _ => false
                };
            }).Any(isAstrologianStatus => isAstrologianStatus);
        }

        if (RangeJobClassWeights.ContainsKey(jobAbbr))
        {
            return partyMember.Statuses.Select(status =>
            {
                if (status.RemainingTime <= 2.5) return false;
                return status.StatusId switch
                {
                    830 or 833 or 834 => true,
                    _ => false
                };
            }).Any(isAstrologianStatus => isAstrologianStatus);
        }

        throw new InvalidOperationException("unknown party member job");
    }

    private static readonly Random Random = new Random();

    private static GameObject FindBestCandidate(AstrologianCard arcanum, uint strictLevel = 3)
    {
        if (Services.PartyList.Length == 0)
        {
            /* Solo Party */
            return Services.ClientState.LocalPlayer!;
        }

        var candidates = new List<KeyValuePair<PartyMember, uint>>(8);

        if (strictLevel == 0)
        {
            /* We are losing hope on choosing the best target, just pick anyone randomly */
            return Services.PartyList[Random.Next(Services.PartyList.Length)]!.GameObject!;
        }

        foreach (var partyMember in Services.PartyList)
        {
            var jobAbbr = partyMember.ClassJob.GameData!.Abbreviation.ToString().ToUpper();

            if (strictLevel == 3)
            {
                /* Ignore players that already have an arcanum */
                if (HasAstrologianStatus(partyMember)) continue;
            }

            if (strictLevel == 2)
            {
                /* Ignore players that already have an 'correct' arcanum */
                if (HasCorrectAstrologianStatus(partyMember)) continue;
            }

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
            return FindBestCandidate(arcanum, strictLevel - 1);
        }

        candidates.Sort((lhs, rhs) => lhs.Value.CompareTo(rhs.Value));
        candidates.Reverse();

        var bestRank = candidates[0].Value;
        candidates = candidates.FindAll(candidate => candidate.Value == bestRank);

        var chosenOne = candidates[Random.Next(candidates.Count)].Key;

        return chosenOne.GameObject!;
    }

    private static unsafe AstrologianCard GetCurrentDrewCard() =>
        JobGaugeManager.Instance()->Astrologian.CurrentCard;

    private static unsafe AstrologianSeal[] GetCurrentSeals() =>
        JobGaugeManager.Instance()->Astrologian.CurrentSeals;

    public GameObject SelectTarget()
    {
        if (Services.ClientState.LocalPlayer!.ClassJob.GameData!.Abbreviation.ToString() != "AST")
            throw new InvalidOperationException("job of the character must be astrologian");

        var arcanumDrew = GetCurrentDrewCard();
        if (arcanumDrew == AstrologianCard.None)
            throw new InvalidOperationException("an arcanum must be drew before selects target");

        return FindBestCandidate(arcanumDrew);
    }
}