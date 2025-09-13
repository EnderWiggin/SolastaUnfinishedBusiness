using System;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
internal static class UserCharacterPatcher
{
    [HarmonyPatch(typeof(UserCharacter), nameof(UserCharacter.CreateMonsterDefinition))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class CreateMonsterDefinition_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(UserCharacter __instance, out MonsterDefinition __result)
        {
            //PATCH: if attack definition is not overriden - use raw base definition
            //this fixes cases where attack has effects that are not damage or conditions (e.g. prone on some wolves)
            __result = CreateMonsterDefinition(__instance);
            return false;
        }

        private static MonsterDefinition CreateMonsterDefinition(UserCharacter data)
        {
            var attackDb = DatabaseRepository.GetDatabase<MonsterAttackDefinition>();
            var monster = ScriptableObject.CreateInstance<MonsterDefinition>();
            monster.ForceName(data.InternalName);
            monster.Copy(data.referenceMonsterDefinition);
            monster.IsUserContent = true;
            monster.GuiPresentation.Title = data.DisplayTitle;
            monster.GuiPresentation.Description = data.DisplayDescription;
            if (data.overridenAttributes.Contains(nameof(data.AbilityScores)))
            {
                Array.Copy(data.abilityScores, monster.AbilityScores, data.abilityScores.Length);
            }

            monster.ArmorClass = data.ArmorClass;
            monster.StandardHitPoints = data.HitPoints;
            monster.ChallengeRating = data.ChallengeRating;

            monster.SavingThrowScores.Clear();
            foreach (var savingThrow in data.savingThrows)
            {
                monster.SavingThrowScores.Add(new MonsterSavingThrowProficiency(savingThrow.Name, savingThrow.Bonus));
            }

            monster.SkillScores.Clear();
            foreach (var skill in data.skills)
            {
                monster.SkillScores.Add(new MonsterSkillProficiency(skill.Name, skill.Bonus));
            }

            monster.AttackIterations.Clear();
            ProcessAttack(data.attack1, nameof(data.Attack1));
            ProcessAttack(data.attack2, nameof(data.Attack2));
            ProcessAttack(data.attack3, nameof(data.Attack3));
            ProcessAttack(data.attack4, nameof(data.Attack4));

            var num = 0;
            if (data.damageAffinities != null)
            {
                foreach (var affinity in data.damageAffinities.Affinities)
                {
                    monster.Features.Add(affinity.CreateFeature(data.InternalName, num++));
                }
            }

            if (!string.IsNullOrEmpty(data.droppedLoot))
            {
                if (DatabaseRepository.GetDatabase<LootPackDefinition>()
                    .TryGetElement(data.droppedLoot, out var result))
                {
                    monster.DroppedLootDefinition = result;
                }
            }

            return monster;

            void ProcessAttack(UserMonsterAttack attack, string name)
            {
                if (attack == null) { return; }

                if (data.overridenAttributes.Contains(name)
                    || !attackDb.TryGetElement(attack.monsterAttackDefinitionName, out var def))
                {
                    data.AddAttackIteration(monster, attack);
                }
                else
                {
                    monster.AttackIterations.Add(new MonsterAttackIteration(def, attack.Iterations));
                }
            }
        }
    }
}
