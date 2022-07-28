﻿using UnityModManagerNet;
using Kingmaker.UnitLogic.FactLogic;
using HarmonyLib;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.Enums;
using Kingmaker.Items;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;


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


    [HarmonyPatch(typeof(UnitPartConcealment), nameof(UnitPartConcealment.Calculate))]
    static class FixTrueSeeing
    {

        static bool Prefix(ref Concealment __result, [NotNull] UnitEntityData initiator, [NotNull] UnitEntityData target, bool attack = false)
        {
            try
            {
                __result = FixedCalculate(initiator, target, attack);
            }
            catch (Exception ex)
            {
                Main.modEntry.Logger.LogException(ex);
            }
            return false;
        }

        public static Concealment FixedCalculate([NotNull] UnitEntityData initiator, [NotNull] UnitEntityData target, bool attack = false)
        {
            UnitPartConcealment unitPartConcealment = initiator.Get<UnitPartConcealment>();
            UnitPartConcealment unitPartConcealment2 = target.Get<UnitPartConcealment>();
            if (unitPartConcealment != null && unitPartConcealment.IgnoreAll)
            {
                return Concealment.None;
            }

            List<(Feet Range, UnitConditionExceptions Exceptions)> unitPartConcealment_m_BlindsightRanges = null;
            if (unitPartConcealment != null)
            {
                unitPartConcealment_m_BlindsightRanges = Traverse.Create(unitPartConcealment).Field("m_BlindsightRanges").GetValue<List<(Feet Range, UnitConditionExceptions Exceptions)>>();
            }
            if (unitPartConcealment_m_BlindsightRanges != null)
            {
                Feet feet = 0.Feet();
                foreach (var blindsightRange in unitPartConcealment_m_BlindsightRanges)
                {
                    if ((blindsightRange.Exceptions == null || !blindsightRange.Exceptions.IsExceptional(target)) && feet < blindsightRange.Range)
                    {
                        (feet, _) = blindsightRange;
                    }
                }
                float num = initiator.View.Corpulence + target.View.Corpulence;
                if (initiator.DistanceTo(target) - num <= feet.Meters)
                {
                    return Concealment.None;
                }
            }
            Concealment concealment = ((unitPartConcealment2 != null && Traverse.Create(unitPartConcealment2).Method("IsConcealedFor", new object[] { initiator }).GetValue<bool>()) ? Concealment.Total : Concealment.None);
            bool seeInvisibility = initiator.Descriptor.State.HasCondition(UnitCondition.SeeInvisibility) || initiator.Descriptor.State.HasCondition(UnitCondition.TrueSeeing);
            if (target.Descriptor.State.HasCondition(UnitCondition.Invisible) && (!seeInvisibility || !initiator.Descriptor.State.GetConditionExceptions(UnitCondition.SeeInvisibility).Any((UnitConditionExceptions _exception) => _exception == null || !_exception.IsExceptional(target))))
            {
                concealment = Concealment.Total;
            }
            List<UnitPartConcealment.ConcealmentEntry> unitPartConcealment2_m_Concealments = null;
            if (unitPartConcealment2 != null)
            {
                unitPartConcealment2_m_Concealments = Traverse.Create(unitPartConcealment2).Field("m_Concealments").GetValue<List<UnitPartConcealment.ConcealmentEntry>>();
            }
            if (concealment < Concealment.Total && unitPartConcealment2_m_Concealments != null)
            {
                foreach (UnitPartConcealment.ConcealmentEntry concealment2 in unitPartConcealment2_m_Concealments)
                {
                    if (concealment2.OnlyForAttacks && !attack)
                    {
                        continue;
                    }
                    if (concealment2.DistanceGreater > 0.Feet())
                    {
                        float num2 = initiator.DistanceTo(target);
                        float num3 = initiator.View.Corpulence + target.View.Corpulence;
                        if (num2 <= concealment2.DistanceGreater.Meters + num3)
                        {
                            continue;
                        }
                    }
                    if (concealment2.RangeType.HasValue)
                    {
                        RuleAttackRoll ruleAttackRoll = Rulebook.CurrentContext.LastEvent<RuleAttackRoll>();
                        ItemEntityWeapon itemEntityWeapon = ((ruleAttackRoll != null) ? ruleAttackRoll.Weapon : initiator.GetFirstWeapon());
                        if (itemEntityWeapon == null || !concealment2.RangeType.Value.IsSuitableWeapon(itemEntityWeapon))
                        {
                            continue;
                        }
                    }
                    if ((concealment2.Descriptor != ConcealmentDescriptor.Blur && concealment2.Descriptor != ConcealmentDescriptor.Displacement) || !initiator.Descriptor.State.HasCondition(UnitCondition.TrueSeeing) || !initiator.Descriptor.State.GetConditionExceptions(UnitCondition.TrueSeeing).EmptyIfNull().Any((UnitConditionExceptions _exception) => !(_exception is UnitConditionExceptionsTargetHasFacts unitConditionExceptionsTargetHasFacts) || !unitConditionExceptionsTargetHasFacts.IsExceptional(target)))
                    {
                        concealment = Max(concealment, concealment2.Concealment);
                    }
                }
            }
            if (unitPartConcealment2 != null && unitPartConcealment2.Disable)
            {
                concealment = Concealment.None;
            }
            if (initiator.Descriptor.State.HasCondition(UnitCondition.Blindness))
            {
                concealment = Concealment.Total;
            }
            if (initiator.Descriptor.State.HasCondition(UnitCondition.PartialConcealmentOnAttacks))
            {
                concealment = Concealment.Partial;
            }
            if (concealment == Concealment.None && Game.Instance.Player.Weather.ActualWeather >= BlueprintRoot.Instance.WeatherSettings.ConcealmentBeginsOn)
            {
                RuleAttackRoll ruleAttackRoll2 = Rulebook.CurrentContext.LastEvent<RuleAttackRoll>();
                ItemEntityWeapon itemEntityWeapon2 = ((ruleAttackRoll2 != null) ? ruleAttackRoll2.Weapon : initiator.GetFirstWeapon());
                if (itemEntityWeapon2 != null && WeaponRangeType.Ranged.IsSuitableWeapon(itemEntityWeapon2))
                {
                    concealment = Concealment.Partial;
                }
            }
            if (unitPartConcealment != null && unitPartConcealment.IgnorePartial && concealment == Concealment.Partial)
            {
                concealment = Concealment.None;
            }
            if (unitPartConcealment != null && unitPartConcealment.TreatTotalAsPartial && concealment == Concealment.Total)
            {
                concealment = Concealment.Partial;
            }
            return concealment;
        }

        private static Concealment Max(Concealment a, Concealment b)
        {
            if (a <= b)
            {
                return b;
            }
            return a;
        }
    }



}
