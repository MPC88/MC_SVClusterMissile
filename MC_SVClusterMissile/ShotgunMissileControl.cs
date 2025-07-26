using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;
using HarmonyLib;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace MC_SVClusterMissile
{
    class ShotgunMissileControl : MonoBehaviour
    {
        // Projectile names
        // Bullet, Laser_Blue, Cannon_Bullet, Bullet, Laser_Red, Laser_Purple, Laser_Green,
        // Laser_Cyan, Plasma_Blast, Missle_1, Missle_2, Mine_1, Plasma_Torpedo, Quantum_Pulse,
        // Railgun_Bullet, Laser_Green, Orb
        private const string PROJECTILE = "Cannon_Bullet";
        private const float DEPLOY_TIME = 3.0f;
        private const float ARC = 20f;
        private const int NUMCHILDREN = 5;

        private static FieldInfo projContEntityField = AccessTools.Field(typeof(ProjectileControl), "Entity");
        private static FieldInfo projContProxDmgModField = AccessTools.Field(typeof(ProjectileControl), "proximityDmgMod");
        private static FieldInfo projContSpawnPos = AccessTools.Field(typeof(ProjectileControl), "spawnPosition");
        private static FieldInfo projContMaxRange = AccessTools.Field(typeof(ProjectileControl), "maxRange");
        private static int projIndex = -1;

        private float elapsedTime = 0;
        private ProjectileControl originalProjCont;     

        public void Awake()
        {            
            if(projIndex == -1)
                projIndex = GameManager.instance.GetProjectilePoolIndex(PROJECTILE);
        }

        public void Reset()
        {
            originalProjCont = this.gameObject.GetComponent<ProjectileControl>();
            elapsedTime = 0;
        }

        public void Update()
        {
            if (!enabled)
                return;

            elapsedTime += Time.deltaTime;
            if(elapsedTime >= DEPLOY_TIME)
            {
                if (!Main.allowClusterThisFrame)
                    return;
                Main.allowClusterThisFrame = false;

                float rotOffset = 0 - (ARC/2);                
                for (int i = 0; i < NUMCHILDREN; i++)
                {
                    SpawnChildProjectile(Vector3.zero, rotOffset);
                    rotOffset += ARC/NUMCHILDREN;
                }
                if (originalProjCont.hasEntity)
                    ((Entity)projContEntityField.GetValue(originalProjCont)).Die();
                else
                    originalProjCont.DisableAndHide();
            }
        }

        private void SpawnChildProjectile(Vector3 posOffset, float rotOffset)
        {
            Vector3 pos = originalProjCont.gameObject.transform.position + posOffset;
            GameObject gameObject = GameManager.instance.SpawnProjectile(ShotgunMissileControl.projIndex, pos, originalProjCont.gameObject.transform.rotation);
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

            //ApplyProjectileMods_Missile(projControl);

            projControl.Fire();
        }

        private void ApplyProjectileMods_Missile(ProjectileControl projControl)
        {
            projControl.turnSpeed = originalProjCont.turnSpeed;
            projControl.homing = originalProjCont.homing;
            projControl.autoTargeting = originalProjCont.autoTargeting;
            projControl.timeToDestroy += 0.48f;
        }

        [HarmonyPatch(typeof(ProjectileControl), nameof(ProjectileControl.DisableAndHide))]
        [HarmonyPrefix]
        private static void ProjContDisableHide_Pre(ProjectileControl __instance)
        {
            // Remove this component before projectile is added back into object pool and
            // before gameobject is disabled.
            ShotgunMissileControl smc = __instance.gameObject.GetComponent<ShotgunMissileControl>();
            if (smc != null)
                Object.Destroy(smc);
        }
    }
}
