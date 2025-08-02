using UnityEngine;
using HarmonyLib;
using System.Reflection;

namespace MC_SVClusterMissile
{
    class ClusterMissileControl : MonoBehaviour
    {
        // Projectile names
        // Bullet, Laser_Blue, Cannon_Bullet, Bullet, Laser_Red, Laser_Purple, Laser_Green,
        // Laser_Cyan, Plasma_Blast, Missle_1, Missle_2, Mine_1, Plasma_Torpedo, Quantum_Pulse,
        // Railgun_Bullet, Laser_Green, Orb
        private const string PROJECTILE = "Cannon_Bullet";
        private const float DEPLOY_TIME = 3.0f;
        private const float ARC = 20f;
        private const int NUMCHILDREN = 5;

        private static FieldInfo projContEntityField = AccessTools.Field(typeof(ProjectileControl), "entity");
        private static FieldInfo projContProxDmgModField = AccessTools.Field(typeof(ProjectileControl), "proximityDmgMod");
        private static FieldInfo projContSpawnPos = AccessTools.Field(typeof(ProjectileControl), "spawnPosition");
        private static FieldInfo projContMaxRange = AccessTools.Field(typeof(ProjectileControl), "maxRange");

        internal new bool enabled = false;

        private float elapsedTime = 0;
        private int projIndex = -1;

        public void Reset()
        {
            elapsedTime = 0;
            enabled = true;
        }

        public void Update()
        {
            if (!enabled)
                return;

            elapsedTime += Time.deltaTime;
            if(elapsedTime >= DEPLOY_TIME)
            {
                //if (Main.clustersThisFrame >= Main.maxClusterPerFrame)
                //    return;
                //Main.clustersThisFrame++;

                ProjectileControl originalProjCont = this.gameObject.GetComponent<ProjectileControl>();
                if(projIndex == -1)
                    projIndex = GameManager.instance.GetProjectilePoolIndex(PROJECTILE);

                float rotOffset = 0 - (ARC/2);                
                for (int i = 0; i < NUMCHILDREN; i++)
                {
                    SpawnChildProjectile(originalProjCont, Vector3.zero, rotOffset);
                    rotOffset += ARC/NUMCHILDREN;
                }

                if (originalProjCont.hasEntity)
                    ((Entity)projContEntityField.GetValue(originalProjCont)).Die();
                else
                    originalProjCont.DisableAndHide();                
            }
        }

        private void SpawnChildProjectile(ProjectileControl originalProjCont, Vector3 posOffset, float rotOffset)
        {
            Vector3 pos = originalProjCont.gameObject.transform.position + posOffset;
            GameObject gameObject = GameManager.instance.SpawnProjectile(projIndex, pos, originalProjCont.gameObject.transform.rotation);
            gameObject.transform.Rotate(new Vector3(0, 1, 0), rotOffset);
            ProjectileControl projControl = gameObject.GetComponent<ProjectileControl>();

            projControl.target = originalProjCont.target;
            projControl.damage = originalProjCont.damage;

            projControl.SetFFSystem(originalProjCont.ffSys);

            //audioS.PlayOneShot(audioToPlay, SoundSys.SFXvolume * (isDrone ? 0.3f : 1f) * audioMod);
            projControl.aoe = originalProjCont.aoe;
            projControl.transform.localScale = originalProjCont.transform.localScale;
            projControl.tDmg = originalProjCont.tDmg;
            projControl.impact = originalProjCont.impact;
            projControl.speed = originalProjCont.speed;

            float newProxDmgMod = (float)projContProxDmgModField.GetValue(originalProjCont);
            projContProxDmgModField.SetValue(projControl, newProxDmgMod);
            if (newProxDmgMod != 0f)
            {
                projContSpawnPos.SetValue(projControl, projContSpawnPos.GetValue(originalProjCont));
                projContMaxRange.SetValue(projControl, projContMaxRange.GetValue(originalProjCont));
            }

            //projControl.timeToDestroy = originalProjCont.timeToDestroy;
            //projControl.explodeOnDestroy = originalProjCont.explodeOnDestroy;
            projControl.timeToDestroy = Random.Range(1.5f, 1.7f);
            projControl.explodeOnDestroy = true;
            projControl.damageType = originalProjCont.damageType;
            projControl.ownerSS = originalProjCont.ownerSS;
            projControl.canHitProjectiles = originalProjCont.canHitProjectiles;
            projControl.piercing = originalProjCont.piercing;
            Rigidbody originalRB = (Rigidbody)AccessTools.Field(typeof(ProjectileControl), "rb").GetValue(originalProjCont);
            projControl.SetRbVelocity(originalRB.velocity);
            projControl.turnSpeed = originalProjCont.turnSpeed;
            projControl.homing = originalProjCont.homing;
            projControl.autoTargeting = originalProjCont.autoTargeting;

            projControl.Fire();            
        }

        [HarmonyPatch(typeof(ProjectileControl), nameof(ProjectileControl.DisableAndHide))]
        [HarmonyPrefix]
        private static void ProjContDisableHide_Pre(ProjectileControl __instance)
        {
            // Disable this component before projectile is added back into object pool and
            // before gameobject is disabled.
            ClusterMissileControl smc = __instance.gameObject.GetComponent<ClusterMissileControl>();
            if (smc != null)
                smc.enabled = false;
        }
    }
}
