using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
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

        private static int num = 0;

        public void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Main));
            Harmony.CreateAndPatchAll(typeof(ShotgunMissileControl));
        }

        [HarmonyPatch(typeof(Weapon), nameof(Weapon.Fire))]
        [HarmonyPatch(typeof(Weapon), "FireExtra")]
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
        [HarmonyPatch(typeof(Weapon), "FireExtra")]
        [HarmonyPostfix]
        private static void WeaponFire_Post(Weapon __instance, ProjectileControl ___projControl, bool __state)
        {
            if (!__state)
                return;

            if (__instance.wRef.type == WeaponType.Missile)
                // TODO: ___projControl == null in FireExtras
                ___projControl.gameObject.AddComponent(typeof(ShotgunMissileControl));
        }

        private static bool CanPayCost(Weapon instance, bool isDrone, SpaceShip ss, AmmoBuffer ammoBuffer, CargoSystem cs)
        {
            if (isDrone)
            {
                return true;
            }
            if (instance.wRef.energyCost != 0f && instance.wRef.energyCost * ss.energyMmt.energyMod(0) > ss.stats.currEnergy)
            {
                return false;
            }
            if (ammoBuffer != null && !CanPayAmmo(ammoBuffer, instance.wRef.ammo.qnt))
            {
                if (cs == null || !CanReplicateAmmo(cs, instance.wRef.ammo.itemID, ss))
                {
                    return false;
                }
                if (!CanPayAmmo(ammoBuffer, instance.wRef.ammo.qnt))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool CanPayAmmo(AmmoBuffer ammoBuffer, int qntToPay)
        {
            if (ammoBuffer is AmmoBuffer_CargoSystem)
            {
                AmmoBuffer_CargoSystem abcs = ammoBuffer as AmmoBuffer_CargoSystem;
                if (abcs.qnt >= qntToPay)
                {
                    return true;
                }
                return false;
            }
            else if (ammoBuffer is AmmoBuffer_ItemStock)
            {
                AmmoBuffer_ItemStock abis = ammoBuffer as AmmoBuffer_ItemStock;
                if (abis.isd != null && abis.isd.stock >= qntToPay)
                {
                    return true;
                }
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
