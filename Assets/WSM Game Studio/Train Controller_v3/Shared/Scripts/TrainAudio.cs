using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    public class TrainAudio
    {
        private SFX _sfx;

        public TrainAudio(SFX sfx)
        {
            _sfx = sfx;
        }

        public bool BellOn
        {
            get
            {
                if (_sfx.bellSFX != null)
                    return _sfx.bellSFX.isPlaying;

                return false;
            }
        }

        /// <summary>
        /// Trains SFX, motor, wheels on trails, etc
        /// </summary>
        public void UpdateSFX(float currentSpeed, float brake, bool enginesOn, bool isGrounded)
        {
            currentSpeed = Mathf.Abs(currentSpeed);
            float pitchInterpolation = Mathf.Clamp01(currentSpeed * 0.01f);

            if (_sfx.engineSFX != null)
            {
                _sfx.engineSFX.pitch = Mathf.Lerp(_sfx.idleEnginePitch, _sfx.maxEnginePitch, pitchInterpolation);

                if (enginesOn && !_sfx.engineSFX.isPlaying)
                    _sfx.engineSFX.Play();
                else if (!enginesOn && _sfx.engineSFX.isPlaying)
                    _sfx.engineSFX.Stop();
            }

            if (isGrounded)
            {
                if (_sfx.wheelsSFX != null)
                {
                    _sfx.wheelsSFX.pitch = Mathf.Lerp(_sfx.minWheelsPitch, _sfx.maxWheelsPitch, pitchInterpolation);

                    if (currentSpeed >= 1f && !_sfx.wheelsSFX.isPlaying)
                        _sfx.wheelsSFX.Play();
                    else if (currentSpeed <= 1f && _sfx.wheelsSFX.isPlaying)
                        _sfx.wheelsSFX.Stop();
                }

                if (_sfx.brakesSFX != null)
                {
                    if (currentSpeed >= 0.5f && brake > 0.5f && !_sfx.brakesSFX.isPlaying)
                        _sfx.brakesSFX.Play();
                    else if (_sfx.brakesSFX.isPlaying && currentSpeed <= 0.5f || brake < 0.5f)
                        _sfx.brakesSFX.Stop();
                }
            }
            else
            {
                if (_sfx.wheelsSFX != null) _sfx.wheelsSFX.Stop();
                if (_sfx.brakesSFX != null) _sfx.brakesSFX.Stop();
            }
        }

        /// <summary>
        /// play the train horn
        /// </summary>
        /// <param name="sfx"></param>
        public void Honk()
        {
            if (_sfx.hornSFX == null)
                return;

            if (!_sfx.hornSFX.isPlaying)
                _sfx.hornSFX.Play();
        }

        /// <summary>
        /// Toggle train security bell
        /// </summary>
        /// <param name="sfx"></param>
        public void ToogleBell()
        {
            if (_sfx.bellSFX != null)
            {
                if (_sfx.bellSFX.isPlaying)
                    _sfx.bellSFX.Stop();
                else
                    _sfx.bellSFX.Play();
            }
        }

        /// <summary>
        /// Play wagon connection SFX
        /// </summary>
        public void PlayConnectionSFX()
        {
            if (_sfx.wagonConnectionSFX != null)
                _sfx.wagonConnectionSFX.Play();
        }
    }
}
