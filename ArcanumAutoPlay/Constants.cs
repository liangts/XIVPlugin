using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArcanumAutoPlay;

public class ConstantsStatusId
{
    private static readonly Dictionary<string, uint[]> StatusNameId = new Dictionary<string, uint[]>()
    {
        {"Weakness", new uint [] {43} }, {"Brink of Death", new uint [] {44} },
        {"衰弱", new uint [] {43} }, { "衰弱［強］", new uint [] {44}},

        {"Dance Partner", new uint [] {1824, 2027} },
        {"クローズドポジション［被］", new uint [] {1824, 2027} },
    };


    public static uint[] StatusId(string StatusName)
    {
        return StatusNameId[StatusName];
    }

}

public class ConstantsActionId
{
    private static readonly Dictionary<string, uint> ActionNameId = new Dictionary<string, uint>();

    public static uint ActionId(string ActionName)
    {
        return ActionNameId[ActionName];
    }
}
