using UnityModManagerNet;
using Kingmaker.UnitLogic.FactLogic;
using HarmonyLib;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic.Abilities;
using System;


namespace BugFixes
{
    static class Main
    {
        public static UnityModManager.ModEntry modEntry;

        static bool Load(UnityModManager.ModEntry mE)
        {
            modEntry = mE;
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();
            return true;
        }
        
    }

    //HarmonyPatch attribute allows PatchAll to find the patch
    [HarmonyPatch(typeof(AddStatBonus), nameof(AddStatBonus.TryApplyArcanistPowerfulChange))]
    static class AddStatBonus_TryApplyArcanistPowerfulChange_Patch
    {
        static bool Prefix(EntityFact fact, StatType stat, int value, ref PowerfulChangeType? powerfulChange, ref int __result)
        {

            __result = FixedTryApplyArcanistPowerfulChange(fact, stat, value, ref powerfulChange);
            return false;
        }

        public static int FixedTryApplyArcanistPowerfulChange(EntityFact fact, StatType stat, int value, ref PowerfulChangeType? powerfulChange)
        {
            try
            {
                MechanicsContext mechanicsContext = fact?.MaybeContext;
                if (mechanicsContext == null)
                {
                    return value;
                }
                UnitEntityData maybeCaster = mechanicsContext.MaybeCaster;
                if (maybeCaster == null)
                {
                    Main.modEntry.Logger.Log("Caster is missing");
                    return value;
                }
                if (!stat.IsAttribute() || value < 0)
                {
                    return value;
                }
                AbilityExecutionContext sourceAbilityContext = mechanicsContext.SourceAbilityContext;
                if (!powerfulChange.HasValue)
                {
                    if (sourceAbilityContext?.Ability?.Spellbook == null)
                    {
                        return value;
                    }
                    if (!sourceAbilityContext.Ability.Blueprint.IsSpell)
                    {
                        return value;
                    }
                    if (!sourceAbilityContext.Ability.Spellbook.Blueprint.IsArcanist)
                    {
                        return value;
                    }
                    if (sourceAbilityContext.SpellSchool != SpellSchool.Transmutation)
                    {
                        return value;
                    }
                }
                if ((bool)maybeCaster.State.Features.ImprovedPowerfulChange || powerfulChange == PowerfulChangeType.Improved)
                {
                    powerfulChange = PowerfulChangeType.Improved;
                    value += 4;
                }
                else if ((bool)maybeCaster.State.Features.PowerfulChange || powerfulChange == PowerfulChangeType.Simple)
                {
                    powerfulChange = PowerfulChangeType.Simple;
                    value += 2;
                }
                
                return value;
            }
            catch (Exception ex)
            {
                Main.modEntry.Logger.LogException(ex);
                return value;
            }
        }
    }

   
 
}
