using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace JUTPS.FX
{

    [AddComponentMenu("JU TPS/FX/Shake One Time")]
    public class ShakeOneTime : MonoBehaviour
    {
        public Shaker ShakerToShake;
        public bool ShakeOnAwake = true;
        [Range(0, 1f)] public float ShakeIntensity = 1;
        [Range(0, 50)] public float ShakeStartIntensity = 50;
        [Range(0, 20)] public float ShakeEndIntensity = 5f;
        [Range(0, 20)] public float ShakeSpeed = 5f;
        [Range(0, 20)] public float MaxAngle = 15f;
        [Range(0, 20)] public float ShakeDuration = 1f;
        public float ShakeRadious = 50;

        void Start()
        {
            if (ShakeOnAwake)
            {
                Shake(ShakeRadious);
            }
        }

        public void Shake(float Radious = 10)
        {
            if (ShakerToShake == null)
            {
                Shaker shakerToShake = Shaker.GetCurrentCameraInstance();
                if (shakerToShake == null)
                    return;

                float distance = Vector3.Distance(shakerToShake.transform.position, transform.position);
                float intensityByDistance = Radious > 0f
                    ? Mathf.Lerp(1f, 0f, distance / Radious)
                    : (distance <= 0f ? 1f : 0f);

                shakerToShake.Shake(
                    ShakeSpeed,
                    ShakeDuration,
                    ShakeStartIntensity,
                    ShakeEndIntensity,
                    MaxAngle,
                    intensityByDistance * ShakeIntensity);
            }
            else
            {
                ShakerToShake.Shake(
                    ShakeSpeed,
                    ShakeDuration,
                    ShakeStartIntensity,
                    ShakeEndIntensity,
                    MaxAngle,
                    ShakeIntensity);
            }
        }
    }

}
