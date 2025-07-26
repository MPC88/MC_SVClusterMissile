using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace MC_SVClusterMissile
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class Main : BaseUnityPlugin
    {
        // BepInEx
        public const string pluginGuid = "mc.starvalor.clustermissile";
        public const string pluginName = "SV Cluster Missile";
        public const string pluginVersion = "0.0.1";

        private static FieldInfo iEnumWeaponField = null;
        private static FieldInfo wepProjContField = null;

        private static int num = 0;

        public void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Main));
            Harmony.CreateAndPatchAll(typeof(ShotgunMissileControl));
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
            if (!__state)
                return;

            if (__instance.wRef.type == WeaponType.Missile)
                ___projControl.gameObject.AddComponent(typeof(ShotgunMissileControl));
        }

        [HarmonyPatch(typeof(Weapon), "FireExtra")]
        [HarmonyPostfix]
        static IEnumerator FireExtraIEnumerator_Post(IEnumerator __result)
        {
            // Run original enumerator code
            while (__result.MoveNext())
                yield return __result;

            if (iEnumWeaponField == null)
            {
                Type ienumType = __result.GetType();
                foreach (FieldInfo fi in ienumType.GetFields())
                {
                    if (fi.FieldType == typeof(Weapon))
                    {
                        iEnumWeaponField = fi;
                        break;
                    }
                }
            }

            if (iEnumWeaponField != null)
            {
                Weapon weaponFieldVal = (Weapon)iEnumWeaponField.GetValue(__result);

                if (wepProjContField == null)
                {
                    foreach (FieldInfo fi in weaponFieldVal.GetType().GetFields(AccessTools.all))
                    {
                        if (fi.FieldType == typeof(ProjectileControl))
                            wepProjContField = fi;
                    }
                }

                if (wepProjContField != null)
                {
                    ProjectileControl projControl = (ProjectileControl)wepProjContField.GetValue(weaponFieldVal);
                    if (weaponFieldVal.wRef.type == WeaponType.Missile)
                        projControl.gameObject.AddComponent(typeof(ShotgunMissileControl));
                }
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
                if (cs == null || !CanReplicateAmmo(cs, instance.wRef.ammo.itemID, ss))
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

        private static bool CanReplicateAmmo(CargoSystem cs, int ammoID, SpaceShip ss)
        {
            if (ss.stats.modelData.manufacturer != TFaction.Miners)
                return false;

            int num = cs.ExistItemInShipCargo(3, 42, 1);
            if (num >= 1)
                return true;

            return false;
        }
    }
}
