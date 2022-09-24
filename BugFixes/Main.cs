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
using Kingmaker.Blueprints.JsonSystem.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Kingmaker.Localization;

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
                
                PatchBody();
                PatchRadiance();
                PatchGrace();

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
                    if (buff.ComponentsArray[i] is AddFacts original)
                    {
                        ReferenceArrayProxy<BlueprintUnitFact, BlueprintUnitFactReference> facts = original.Facts;
                        facts[0] = ability;
                        
                    }

                }
            }


            Main.modEntry.Logger.Log("Done patching radiance buffs");
        }

        static void PatchGrace()
        {
            Main.modEntry.Logger.Log("About to start patching grace");


            BlueprintAbility blue = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(BlueprintGuid.Parse("199d585bff173c74b86387856919242c"));
            blue.AvailableMetamagic |= Metamagic.Extend;

            Main.modEntry.Logger.Log("Done patching grace");
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
                    if (blue.ComponentsArray[i] is AddStatBonusAbilityValue original)
                    {
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

    [HarmonyPatch(typeof(SharedStringConverter), nameof(SharedStringConverter.ReadJson))]
    static class FixSharedStringConverter
    {
        // It is an illegal operation in Unity to instantiate SharedStringAsset using new(). This replaces the read
        // operation with the correct implementation.
        [HarmonyPrefix]
        static bool ReadJson(
          JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer, object __result)
        {
            try
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    __result = null;
                }
                else
                {
                    string key = (string)JObject.Load(reader)["stringkey"];
                    var asset = ScriptableObject.CreateInstance<SharedStringAsset>();
                    asset.String = new LocalizedString() { Key = key };
                    __result = asset;
                }
                // Need to just replace the method.
                return false;
            }
            catch (Exception e)
            {
                Main.modEntry.Logger.LogException("FixSharedStringConverter", e);
                return true;
            }
        }
    }
}


