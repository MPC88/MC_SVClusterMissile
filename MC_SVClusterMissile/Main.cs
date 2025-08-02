using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.Purchasing;

namespace MC_SVClusterMissile
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class Main : BaseUnityPlugin
    {
        // BepInEx
        public const string pluginGuid = "mc.starvalor.clustermissile";
        public const string pluginName = "SV Cluster Missile";
        public const string pluginVersion = "0.0.1";

        internal const int maxClusterPerFrame = 10;
        internal static int clustersThisFrame = 0;

        private static FieldInfo iEnumWeaponField = null;
        private static FieldInfo wepProjContField = typeof(Weapon).GetField("projControl", AccessTools.all);

        public void Awake()
        {
            Harmony harmInst = Harmony.CreateAndPatchAll(typeof(Main));
            Harmony.CreateAndPatchAll(typeof(ClusterMissileControl));
        }

        public void LateUpdate()
        {
            clustersThisFrame = 0;
        }

        [HarmonyPatch(typeof(Weapon), nameof(Weapon.Fire))]
        [HarmonyPrefix]
        private static void WeaponFire_Pre(out bool __state, Weapon __instance, bool ___isDrone, SpaceShip ___ss, float ___currCoolDown, float ___chargedFireCount, AmmoBuffer ___ammoBuffer, CargoSystem ___cs)
        {
            __state = true;

            // Only leave state true if Weapon.Fire will get as far as ProjectileControl.Fire call
            if ((!___isDrone && ___ss.energyMmt.valueMod(0) == 0f) ||
                (__instance.chargeTime > 0f && !(___chargedFireCount > 0f)) ||
                !(___currCoolDown <= 0f) ||
                !CanPayCost(__instance, ___isDrone, ___ss, ___ammoBuffer, ___cs))
                __state = false;
        }

        [HarmonyPatch(typeof(Weapon), nameof(Weapon.Fire))]
        [HarmonyPostfix]
        private static void WeaponFire_Post(Weapon __instance, ProjectileControl ___projControl, bool __state)
        {
            if (__state)
                AddClusterMissileControl(__instance, ___projControl);
        }

        // This method is called from FireExtraMoveNext_Trans
        private static void FireExtraAddComponent(Weapon weapon)
        {            
            AddClusterMissileControl(weapon, (ProjectileControl)wepProjContField.GetValue(weapon));
        }

        private static void AddClusterMissileControl(Weapon weapon, ProjectileControl projControl)
        {
            if (weapon.wRef.type == WeaponType.Missile)
            {
                ClusterMissileControl cmc = (ClusterMissileControl)(projControl.gameObject.GetComponent<ClusterMissileControl>() ?? projControl.gameObject.AddComponent(typeof(ClusterMissileControl)));
                cmc.Reset();
            }
        }

        [HarmonyPatch(typeof(Weapon), "FireExtra", MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> FireExtraMoveNext_Trans(IEnumerable<CodeInstruction> instructions)
        {
            // Using CodeMatcher instead of manipulating the instructions directly, but that is also an option.
            var codeMatcher = new CodeMatcher(instructions);

            //IL_03c7: ldloc.1
            //IL_03c8: callvirt instance void Weapon::ApplyProjectileMods()
            CodeMatch[] toMatch = new CodeMatch[]
            {
                new CodeMatch(OpCodes.Ldloc_1),
                new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Weapon), "ApplyProjectileMods"))
            };

            CodeInstruction[] newInstructions = new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Main), "FireExtraAddComponent")),
            };

            // Match the provided CodeMatch's, stopping at the last instruction of the match, or out of bounds if no match is found.
            codeMatcher.MatchEndForward(toMatch);

            if (!codeMatcher.IsInvalid)
            {
                // Add instructions right *after* find.
                codeMatcher.Advance(1);
                codeMatcher.Insert(newInstructions);

                return codeMatcher.InstructionEnumeration();
            }
            else
            {
                Debug.Log("Cluster missile: Fire extra transpiler failed");
                return instructions;
            }
        }

        private static bool CanPayCost(Weapon instance, bool isDrone, SpaceShip ss, AmmoBuffer ammoBuffer, CargoSystem cs)
        {
            if (isDrone)
                return true;
            if (instance.wRef.energyCost != 0f && instance.wRef.energyCost * ss.energyMmt.energyMod(0) > ss.stats.currEnergy)
                return false;
            if (ammoBuffer != null && !CanPayAmmo(ammoBuffer, instance.wRef.ammo.qnt))
            {
                if (cs == null || !CanReplicateAmmo(cs, ss))
                    return false;
                if (!CanPayAmmo(ammoBuffer, instance.wRef.ammo.qnt))
                    return false;
            }
            return true;
        }

        private static bool CanPayAmmo(AmmoBuffer ammoBuffer, int qntToPay)
        {
            if (ammoBuffer is AmmoBuffer_CargoSystem)
            {
                AmmoBuffer_CargoSystem abcs = ammoBuffer as AmmoBuffer_CargoSystem;
                if (abcs.qnt >= qntToPay)
                    return true;
                return false;
            }
            else if (ammoBuffer is AmmoBuffer_ItemStock)
            {
                AmmoBuffer_ItemStock abis = ammoBuffer as AmmoBuffer_ItemStock;
                if (abis.isd != null && abis.isd.stock >= qntToPay)
                    return true;
                return false;
            }

            return false;
        }

        private static bool CanReplicateAmmo(CargoSystem cs, SpaceShip ss)
        {
            if (ss.stats.modelData.manufacturer != TFaction.Miners)
                return false;

            int num = cs.ExistItemInShipCargo(3, 42, 1);
            if (num >= 1)
                return true;

            return false;
        }


        /*************************************
         * Projectile pool fuckery
         *************************************/
        // Method called by GetProjPoolInd_Trans
        private static void IncreaseInstancedProjectiles(ProjPool pool)
        {
            pool.preInstances = 1000; // Default is 20
        }

        //and the fun part....
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.GetProjectilePoolIndex))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> GetProjPoolInd_Trans(IEnumerable<CodeInstruction> instructions)
        {
            // Using CodeMatcher instead of manipulating the instructions directly, but that is also an option.
            var codeMatcher = new CodeMatcher(instructions);

            //IL_0044: call class [UnityEngine.CoreModule]UnityEngine.GameObject ObjManager::GetProj(string)
            //IL_0049: ldarg.0
            //IL_004a: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform GameManager::projObjGroup
            //IL_004f: newobj instance void ProjPool::.ctor(string, class [UnityEngine.CoreModule] UnityEngine.GameObject, class [UnityEngine.CoreModule] UnityEngine.Transform)
            //IL_0054: stloc.1
            CodeMatch[] toMatch = new CodeMatch[]
            {
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(ObjManager), nameof(ObjManager.GetProj))),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(GameManager), "projObjGroup")),
                new CodeMatch(OpCodes.Newobj),
                new CodeMatch(OpCodes.Stloc_1)
            };

            CodeInstruction[] newInstructions = new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Main), "IncreaseInstancedProjectiles"))
            };

            // Match the provided CodeMatch's, stopping at the last instruction of the match, or out of bounds if no match is found.
            codeMatcher.MatchEndForward(toMatch);

            if (!codeMatcher.IsInvalid)
            {
                // Add instructions right *after* find.
                codeMatcher.Advance(1);
                codeMatcher.Insert(newInstructions);

                return codeMatcher.InstructionEnumeration();
            }
            else
            {
                Debug.Log("Cluster missile: Projectile pool transpiler failed");
                return instructions;
            }
        }
    }
}
