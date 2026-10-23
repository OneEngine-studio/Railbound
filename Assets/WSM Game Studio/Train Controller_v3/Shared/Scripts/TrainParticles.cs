using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    public class TrainParticles
    {
        private VFX _vfx;
        private Transform[] _brakingSparksTransforms;
        private Quaternion _reversedRotation = Quaternion.Euler(0f, 180f, 0f);

        public TrainParticles(VFX vfx)
        {
            _vfx = vfx;

            if (_vfx.brakingSparksParticles != null)
            {
                _brakingSparksTransforms = new Transform[_vfx.brakingSparksParticles.Length];

                for (int i = 0; i < _vfx.brakingSparksParticles.Length; i++)
                {
                    _brakingSparksTransforms[i] = _vfx.brakingSparksParticles[i].GetComponent<Transform>();
                }
            }
        }

        /// <summary>
        /// Train VFX, (smoke, braking sparks)
        /// </summary>
        /// <param name="currentSpeed"></param>
        /// <param name="acceleration"></param>
        /// <param name="brake"></param>
        /// <param name="enginesOn"></param>
        /// <param name="smokeEnabled"></param>
        /// <param name="brakingParticlesEnabled"></param>
        public void UpdateVFX(float currentSpeed, float acceleration, float brake, bool enginesOn, bool smokeEnabled, bool brakingParticlesEnabled, int localDirection)
        {
            #region SMOKE PARTICLES

            if (smokeEnabled && _vfx.smokeParticles != null)
            {
                if (_vfx.smokeParticles.isPlaying)
                {
                    float emissionRate = Mathf.Lerp(_vfx.minSmokeEmission, _vfx.maxSmokeEmission, Mathf.Abs(acceleration));
                    if (emissionRate != _vfx.smokeParticles.emission.rateOverTime.constant)
                    {
                        ParticleSystem.EmissionModule emission = _vfx.smokeParticles.emission;
                        emission.rateOverTime = emissionRate;
                    }
                }

                if (enginesOn && !_vfx.smokeParticles.isPlaying)
                    _vfx.smokeParticles.Play();
                else if (!enginesOn && _vfx.smokeParticles.isPlaying)
                    _vfx.smokeParticles.Stop();
            }

            #endregion

            #region BRAKING PARTICLES

            if (brakingParticlesEnabled && _vfx.brakingSparksParticles != null)
            {
                bool sparks = Mathf.Abs(currentSpeed) > 1f && brake > 0.5f;

                for (int i = 0; i < _vfx.brakingSparksParticles.Length; i++)
                {
                    if (sparks && !_vfx.brakingSparksParticles[i].isPlaying)
                        _vfx.brakingSparksParticles[i].Play();
                    else if (!sparks && _vfx.brakingSparksParticles[i].isPlaying)
                        _vfx.brakingSparksParticles[i].Stop();

                    //Sparks direction
                    if (sparks && _vfx.brakingSparksParticles[i].isPlaying)
                    {
                        if (localDirection > 0)
                            _brakingSparksTransforms[i].localRotation = Quaternion.identity;
                        else if(localDirection < 0)
                            _brakingSparksTransforms[i].localRotation = _reversedRotation;
                    }
                }
            }
            
            #endregion
        }
    }
}
