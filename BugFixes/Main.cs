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
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.Visual.Animation.Kingmaker;
using Kingmaker.Visual.Animation.Kingmaker.Actions;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Items.Slots;
using System.Runtime.CompilerServices;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.ElementsSystem;
using Kingmaker.UI.GenericSlot;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Controllers;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.Settings.Difficulty;
using Kingmaker.Settings;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Craft;

namespace BugFixes
{
    static class Main
    {
        public static UnityModManager.ModEntry modEntry;

        static bool Load(UnityModManager.ModEntry mE)
        {
            try
            {

                modEntry = mE;
                var harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll();
                return true;
            } catch (Exception ex)
            {
                modEntry.Logger.LogException(ex);
                return false;
            }
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

    [HarmonyPatch(typeof(BlueprintsCache), nameof(BlueprintsCache.Init))]
    static class BlueprintsCache_Init_Patch
    {
        static bool loaded = false;
        static void Postfix()
        {
            try
            {
                if (loaded) return;
                loaded = true;

                PatchDazzling();
                PatchBody();
                PatchRadiance();

                FixEnduringEnchanment.GetBlueprints();
            }
            catch (Exception e)
            {
                Main.modEntry.Logger.LogException(e);
            } 
        }


        static void PatchRadiance()
        {
            Main.modEntry.Logger.Log("About to start patching radiance buffs");
            string[] radiance_buffs = new string[] {
                "f10cba2c41612614ea28b5fc2743bc4c", // Good buff
                "b894f848bf557df47aacb00f2463c8f9", // Bad buff
            };
            BlueprintAbility ability = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(BlueprintGuid.Parse("aefc25a5e96a484fbc93941da960cd20"));

            foreach (string radiance_buff in radiance_buffs)
            {
                BlueprintBuff buff = ResourcesLibrary.TryGetBlueprint<BlueprintBuff>(BlueprintGuid.Parse(radiance_buff));
                for (int i = 0; i < buff.ComponentsArray.Length; ++i)
                {
                    if (buff.ComponentsArray[i] is AddFacts)
                    {
                        AddFacts original = (AddFacts)buff.ComponentsArray[i];

                        ReferenceArrayProxy<BlueprintUnitFact, BlueprintUnitFactReference> facts = original.Facts;
                        facts[0] = ability;
                        
                    }

                }
            }


            Main.modEntry.Logger.Log("Done patching radiance buffs");

        }

        static void PatchBody()
        {
            Main.modEntry.Logger.Log("About to start patching body buffs");
            string[] body_buffs = new string[] {
                "b574e1583768798468335d8cdb77e94c", // FieryBody
                "2eabea6a1f9a58246a822f207e8ca79e", // IronBody
            };

            foreach (string body_buff in body_buffs)
            {
                
                BlueprintBuff blue = ResourcesLibrary.TryGetBlueprint<BlueprintBuff>(BlueprintGuid.Parse(body_buff));
                for (int i = 0; i < blue.ComponentsArray.Length; ++i)
                {
                    if (blue.ComponentsArray[i] is AddStatBonusAbilityValue)
                    {
                        AddStatBonusAbilityValue original = (AddStatBonusAbilityValue)blue.ComponentsArray[i];

                        AddStatBonus replacement = new AddStatBonus();
                        replacement.Descriptor = original.Descriptor;
                        replacement.Value = original.Value.Value;
                        replacement.Stat = original.Stat;
                        replacement.ScaleByBasicAttackBonus = false;
                        replacement.OwnerBlueprint = original.OwnerBlueprint;
                        replacement.name = original.name;

                        blue.ComponentsArray[i] = replacement;
                    }
                    
                }
            }


            Main.modEntry.Logger.Log("Done patching body buffs");

        }

        static void PatchDazzling()
        {
            Main.modEntry.Logger.Log("About to start patching Dazzling");
            string[] dazzling_actions = new string[] {
                "5f3126d4120b2b244a95cb2ec23d69fb", // Normal version
                "08c10a3914fa5bf459054a34a7541704", // Move action
                "ba2951ffa6bcbfb4c83cdfb6919862c5", // Standard action
            };

            foreach (string dazzling_action in dazzling_actions)
            {
                BlueprintAbility blue = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(BlueprintGuid.Parse(dazzling_action));
                blue.Animation = UnitAnimationActionCastSpell.CastAnimationStyle.Omni;
            }


            Main.modEntry.Logger.Log("Done patching Dazzling");
        }
    }

    //HarmonyPatch attribute allows PatchAll to find the patch
    [HarmonyPatch(typeof(ContextActionEnchantWornItem), nameof(ContextActionEnchantWornItem.RunAction))]
    static class FixEnduringEnchanment
    {

        static BlueprintUnitFact enduring = null;
        static BlueprintUnitFact greater_enduring = null;

        public static void GetBlueprints()
        {
            try
            {
                enduring = ResourcesLibrary.TryGetBlueprint<BlueprintUnitFact>(BlueprintGuid.Parse("2f206e6d292bdfb4d981e99dcf08153f"));
                greater_enduring = ResourcesLibrary.TryGetBlueprint<BlueprintUnitFact>(BlueprintGuid.Parse("13f9269b3b48ae94c896f0371ce5e23c"));
                

            }
            catch (Exception ex)
            {
                Main.modEntry.Logger.LogException(ex);
            }
        }

        static bool Prefix(ContextActionEnchantWornItem __instance)
        {
            try
            {
                FixedRunAction(__instance);
            } catch (Exception ex)
            {
                Main.modEntry.Logger.LogException(ex);
            }

            return false;
        }

        public static void FixedRunAction(ContextActionEnchantWornItem enchant)
        {
            MechanicsContext context = ContextData<MechanicsContext.Data>.Current?.Context;
            if (context == null)
            {
                Main.modEntry.Logger.Error("Unable to apply buff: no context found");
            }
            else
            {
                
                Rounds rounds = enchant.DurationValue.Calculate(context);
                UnitEntityData first = context.MaybeCaster;
                
                TargetWrapper wrapper = Traverse.Create(enchant).Property("Target").GetValue<TargetWrapper>();
                UnitEntityData second = wrapper?.Unit;

                UnitEntityData unitEntityData = enchant.ToCaster ? first : second;
                if (unitEntityData == (UnitDescriptor)null)
                {
                    Main.modEntry.Logger.Error("Can't apply buff: target is null");
                }
                else
                {
                    
                    ItemSlot slot = EquipSlotBase.ExtractSlot(enchant.Slot, unitEntityData.Body);
                    if (!slot.HasItem)
                        return;
                    
                    ItemEnchantment fact = slot.Item.Enchantments.GetFact<ItemEnchantment>((BlueprintScriptableObject)enchant.Enchantment);
                    
                    if (fact != null)
                    {
                        if (!fact.IsTemporary)
                            return;
                        slot.Item.RemoveEnchantment(fact);
                    }
               

                    bool has_enduring = context.MaybeCaster.HasFact(enduring);
                    bool has_greater_enduring = context.MaybeCaster.HasFact(greater_enduring);
                    

                    if ((greater_enduring && (rounds.Seconds >= 5.Minutes())) || (has_enduring && (rounds.Seconds >= 60.Minutes())))
                    {
                        rounds = new Rounds((int)(24.Hours().TotalSeconds) / 6);
                    }
                    

                    slot.Item.AddEnchantment(enchant.Enchantment, context, new Rounds?(rounds)).RemoveOnUnequipItem = enchant.RemoveOnUnequip;
                }
            }
        }
    }
}


