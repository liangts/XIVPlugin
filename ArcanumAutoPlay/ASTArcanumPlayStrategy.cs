using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace ArcanumAutoPlay;

public class AstrologianArcanumPlayStrategy
{
    private static readonly Dictionary<string, float> MeleeJobClassWeights = new Dictionary<string, float>()
    {
        { "GLA", 0.0f }, { "PLD", 0.7f },
        { "MRD", 0.0f }, { "WAR", 0.7f },
        { "DRK", 0.7f },
        { "GNB", 0.7f },
        { "PGL", 1.0f }, { "MNK", 1.0f },
        { "LNC", 1.0f }, { "DRG", 1.0f },
        { "ROG", 1.0f }, { "NIN", 1.0f },
        { "SAM", 1.0f },
        { "RPR", 1.0f }
    };

    private static bool IsMeleeArcanum(AstrologianCard arcanum) => arcanum switch
    {
        AstrologianCard.Balance or AstrologianCard.Arrow or AstrologianCard.Spear => true,
        _ => false
    };

    private static readonly Dictionary<string, float> RangeJobClassWeights = new Dictionary<string, float>()
    {
        { "CNJ", 0.0f }, { "WHM", 0.7f },
        { "ACN", 1.0f }, { "SCH", 0.7f },
        { "AST", 0.6f },
        { "SGE", 0.7f },
        { "ARC", 0.7f }, { "BRD", 0.7f },
        { "MCH", 0.9f },
        { "DNC", 0.8f },
        { "THM", 0.7f }, { "BLM", 1.1f },
        { "SMN", 1.0f },
        { "RDM", 1.0f }, {"BLU", 1.0f},
    };

    private static bool IsRangeArcanum(AstrologianCard arcanum) => arcanum switch
    {
        AstrologianCard.Bole or AstrologianCard.Ewer or AstrologianCard.Spire => true,
        _ => false
    };

    private static readonly Dictionary<AstrologianCard, uint> ArcanumStatuses = new Dictionary<AstrologianCard, uint>()
    {
        { AstrologianCard.Balance, 1882 }, // Melee
        { AstrologianCard.Bole, 1883 }, // Range
        { AstrologianCard.Spear, 1885 }, // Melee
        { AstrologianCard.Spire, 1887 }, // Range
        { AstrologianCard.Ewer, 1886 }, // Range
        { AstrologianCard.Arrow, 1884 } // Melee
    };

    private static bool HasAstrologianStatus(PartyMember partyMember) =>
        partyMember.Statuses
            .Select(status =>
            {
                if (status.RemainingTime <= 2.5) return false;
                return status.StatusId switch
                {
                    1882 or 1883 or 1884 or 1885 or 1886 or 1887 => true,
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
                    1882 or 1884 or 1885 => true,
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
                    1883 or 1886 or 1887 => true,
                    _ => false
                };
            }).Any(isAstrologianStatus => isAstrologianStatus);
        }

        throw new InvalidOperationException("unknown party member job");
    }

    private static bool HasWeakness(PartyMember partyMember) =>
        partyMember.Statuses.Select(status =>
        {
            if (status.RemainingTime <= 3) return false;
            if (ConstantsStatusId.StatusId("Ë¥Èõ").Any(id => status.StatusId == id)) return true;
            if (ConstantsStatusId.StatusId("Weakness").Any(id => status.StatusId == id)) return true;
            if (ConstantsStatusId.StatusId("Brink of Death").Any(id => status.StatusId == id)) return true;
            if (ConstantsStatusId.StatusId("Ë¥Èõ£ÛŠ£Ý").Any(id => status.StatusId == id)) return true;
            return false;
        }).Any(isWeakNess => isWeakNess);

    private static float ApplyDebuffCoefficient(PartyMember partyMember) =>
        partyMember.Statuses.Select(status =>
        {
            float coef = 1.0f;
            if (status.RemainingTime <= 3) return coef;
            //if (ConstantsStatusId.StatusId("Ë¥Èõ").Any(id => status.StatusId == id)) coef = coef * 0.75f;
            if (ConstantsStatusId.StatusId("Weakness").Any(id => status.StatusId == id)) coef = coef * 0.75f;
            if (ConstantsStatusId.StatusId("Brink of Death").Any(id => status.StatusId == id)) coef = coef * 0.5f;
            //if (ConstantsStatusId.StatusId("Ë¥Èõ£ÛŠ£Ý").Any(id => status.StatusId == id)) coef = coef * 0.5f;
            return coef;
        }).Aggregate(1.0f, (x, y) => x * y);

    private static float ApplyBuffCoefficient(PartyMember partyMember) =>
        partyMember.Statuses.Select(status =>
        {
            float coef = 1.0f;
            if (status.RemainingTime <= 3) return coef;
            if (ConstantsStatusId.StatusId("¥¯¥í©`¥º¥É¥Ý¥¸¥·¥ç¥ó£Û±»£Ý").Any(id => status.StatusId == id)) coef = coef * 1.1f;
            return coef;
        }).Aggregate(1.0f, (x, y) => x * y);

    private static float DistanceBetween(PartyMember partyMember)
    {
        Vector3 selfPos = Services.ClientState.LocalPlayer!.Position;
        return Vector3.Distance(selfPos, partyMember.Position);

    }

    private static readonly Random Random = new Random();

    private static GameObject FindBestCandidate(AstrologianCard arcanum, uint strictLevel = 3)
    {
        if (Services.PartyList.Length == 0)
        {
            /* Solo Party */
            return Services.ClientState.LocalPlayer!;
        }

        var candidates = new List<KeyValuePair<PartyMember, float>>(8);

        if (strictLevel == 0)
        {
            /* We are losing hope on choosing the best target, just pick anyone randomly */
            return Services.PartyList[Random.Next(Services.PartyList.Length)]!.GameObject!;
        }

        foreach (var partyMember in Services.PartyList)
        {
            if (partyMember.GameObject == null) continue;
            if (partyMember.CurrentHP == 0u) continue;
            if (DistanceBetween(partyMember) > 30.0f) continue;
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
                float rank = MeleeJobClassWeights[jobAbbr];
                rank = rank * ApplyBuffCoefficient(partyMember) * ApplyDebuffCoefficient(partyMember);
                candidates.Add(new KeyValuePair<PartyMember, float>(partyMember, rank));
            }

            if (RangeJobClassWeights.ContainsKey(jobAbbr) && IsRangeArcanum(arcanum))
            {
                float rank = RangeJobClassWeights[jobAbbr];
                rank = rank * ApplyBuffCoefficient(partyMember) * ApplyDebuffCoefficient(partyMember);
                candidates.Add(new KeyValuePair<PartyMember, float>(partyMember, rank));
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