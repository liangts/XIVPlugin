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
    private static readonly Dictionary<string, uint> ActionNameId = new Dictionary<string, uint>()
    {
        { "Combust", 3599u},
        {"Combust II", 3608u },
        {"Combust III", 16554u },
        { "Malefic", 3596u},
        {"Malefic II", 3598u },
        {"Malefic III", 7442u },
        {"Malefic IV", 16555u },
        {"AST_Draw_Arcanum", 3590u },
        {"AST_Play_Arcanum", 17055u },
        {"AST_Play_TheBalance", 4401u },
        {"AST_Play_TheArrow", 4402u },
        {"AST_Play_TheSpear", 4403u },
        {"AST_Play_TheBole", 4404u },
        {"AST_Play_TheEwer", 4405u },
        {"AST_Play_TheSpire", 4406u },
        
    };

    public static uint ActionId(string ActionName)
    {
        return ActionNameId[ActionName];
    }
}
