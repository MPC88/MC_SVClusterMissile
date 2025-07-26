using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;
using HarmonyLib;

namespace MC_SVClusterMissile
{
    class ShotgunMissileControl : MonoBehaviour
    {
        private const float DEPLOY_TIME = 3.0f;
        private const float ARC = 20f;
        private const int NUMCHILDREN = 5;

        private static int projIndex = -1;

        internal float elapsedTime = 0;

        private ProjectileControl originalProjCont;        

        public void Awake()
        {
            // Projectile names
            // Bullet, Laser_Blue, Cannon_Bullet, Bullet, Laser_Red, Laser_Purple, Laser_Green,
            // Laser_Cyan, Plasma_Blast, Missle_1, Missle_2, Mine_1, Plasma_Torpedo, Quantum_Pulse,
            // Railgun_Bullet, Laser_Green, Orb
            if(projIndex == -1)
                projIndex = GameManager.instance.GetProjectilePoolIndex("Cannon_Bullet");
        }

        public void Start()
        {
            originalProjCont = this.gameObject.GetComponent<ProjectileControl>();
            elapsedTime = 0;
        }

        public void Update()
        {
            elapsedTime += Time.deltaTime;
            if(elapsedTime >= DEPLOY_TIME)
            {
                float rotOffset = 0 - (ARC/2);
                for (int i = 0; i < NUMCHILDREN; i++)
                {
                    SpawnChildProjectile(Vector3.zero, rotOffset);
                    rotOffset += ARC/NUMCHILDREN;
                }
                if (originalProjCont.hasEntity)
                    ((Entity)AccessTools.Field(typeof(ProjectileControl), "entity").GetValue(originalProjCont)).Die();
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
                        
            float newProxDmgMod = AccessTools.FieldRefAccess<float>(typeof(ProjectileControl), "proximityDmgMod")(projControl);
            newProxDmgMod = (float)AccessTools.Field(typeof(ProjectileControl), "proximityDmgMod").GetValue(originalProjCont);
            if (newProxDmgMod != 0f)
            {
                Vector3 newSpawnPos = AccessTools.FieldRefAccess<Vector3>(typeof(ProjectileControl), "spawnPosition")(projControl);
                newSpawnPos = base.transform.position;

                float newMaxRange = AccessTools.FieldRefAccess<float>(typeof(ProjectileControl), "maxRange")(projControl);
                newMaxRange = (float)AccessTools.Field(typeof(ProjectileControl), "maxRange").GetValue(originalProjCont);
            }

            projControl.timeToDestroy = originalProjCont.timeToDestroy;
            projControl.explodeOnDestroy = originalProjCont.explodeOnDestroy;
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
