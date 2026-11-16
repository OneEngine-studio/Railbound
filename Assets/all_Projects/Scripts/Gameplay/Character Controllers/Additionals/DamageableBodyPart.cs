using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JUTPS.ArmorSystem
{
    [AddComponentMenu("JU TPS/Armor System/Damageable Body Part")]
    public class DamageableBodyPart : MonoBehaviour
    {
        public JUHealth Health;
        public float DamageMultiplier = 1;
        [HideInInspector] public Armor ArmorProtecting;
        private void Start()
        {
            if (Health == null)
            {
                Health = GetComponentInParent<JUHealth>();
            }
        }
        private int isHead = -1;   // -1 = not looked up yet

        /// <summary>Is this part the head bone of its character (the part DamageableBody gives the head multiplier)?</summary>
        public bool IsHead
        {
            get
            {
                if (isHead < 0)
                {
                    var anim = Health != null ? Health.GetComponent<Animator>() : GetComponentInParent<Animator>();
                    isHead = anim != null && anim.isHuman && anim.GetBoneTransform(HumanBodyBones.Head) == transform ? 1 : 0;
                }
                return isHead == 1;
            }
        }

        public float DoDamage(float Damage)
        {
            if (Health == null)
            {
                Debug.LogWarning("Could not do damage as the Health variable is null");
                return 0;
            }
            // remembered before the damage: a lethal hit raises the kill events at once, and the HUD reads it there
            Health.LastHitWasHead = IsHead;
            Health.LastHitTime = Time.time;
            Health.DoDamage(DamageMultiplier * Damage);
            if (ArmorProtecting != null)
            {
                ArmorProtecting.DoDamageOnArmor(Damage);
            }

            return DamageMultiplier * Damage;
        }
    }

}