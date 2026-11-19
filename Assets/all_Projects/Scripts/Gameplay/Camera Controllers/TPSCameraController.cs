using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using JUTPS.JUInputSystem;
using JUTPS.WeaponSystem;
using JUTPS.ActionScripts;

namespace JUTPS.CameraSystems
{
    [AddComponentMenu("JU TPS/Third Person System/Cameras/JU Third Person Camera Controller")]
    public class TPSCameraController : JUCameraController
    {
        public JUCharacterController characterTarget;

        [Header("Settings")]
        public bool FollowUpTarget;

        [Header("Auto Rotator Settings")]
        public bool EnableAutoRotator;
        public float AutoRotateTime = 5;
        public float AutoRotationSpeed = 4;

        public bool EnableVehicleAutoRotation;
        public float VehicleAutoRotateTime = 3;
        public float VehicleAutoRotationSpeed = 8;

        public WeaponSwayOptions AimingSwaySettings = new WeaponSwayOptions(true, 1, 1, 1);

        public CameraState NormalCameraState = new CameraState("Normal Camera State");
        public CameraState FireModeCameraState = new CameraState("Fire Mode Camera State", movementSpeed: 40);
        public CameraState AimModeCameraState = new CameraState("Scope Mode Camera State", 0, 15, 40, 0, 0, 0, 0, 0, 0, 2.5f);
        public CameraState DrivingVehicleCameraState = new CameraState("Driving Vehicle Camera State", 8, 25, 70, 1.5f, 0, 0, 0, 0, 0, 5, -20, 80);
        public CameraState DeadPlayerCameraState = new CameraState("Dead Player Camera State", 6, 5, 40, 0, 0, 0, 0, 0, 0, 2.5f, -30, 60);

        protected float CurrentTimeToAutoRotation;
        protected bool IsAutoRotationActivated;

        float xmouse;
        float ymouse;

        float SmoothedXMouse;
        float SmoothedYMouse;

        // ------------------------------------------------------------------ Railbound: steady aim
        // JU glued the sight camera to the animated weapon, sampled it in Update (before this frame's animation) while the body only
        // moves in physics steps, stopped the pivot while aiming and froze the camera when the sight was not usable (reload, switch).
        // Walking or running while aiming shook and stuttered the view. Now the sight camera is placed in LateUpdate from a stable
        // frame (where the player looks, anchored at the chest height of a smoothly interpolated body), keeping only a small part of
        // the weapon's walk bob, and HDRP motion blur is off on this camera while looking down the sights.
        [Header("Steady Aim (Railbound)")]
        public bool SteadyAim = true;
        [Tooltip("How much of the walk / run bob of the weapon reaches the sight camera (0 = none, 1 = all of it).")]
        [Range(0f, 1f)] public float AimBobKeep = 0.12f;
        [Tooltip("How quickly the sight camera settles onto the weapon (seconds); also the time to raise the sights.")]
        public float AimSettleTime = 0.09f;
        [Tooltip("No motion blur on this camera while looking down the sights.")]
        public bool NoMotionBlurInSights = true;

        private Vector3 bodyPrev, bodyCurr, adsLocal, adsLocalAverage, adsVelocity;
        private float bodyCurrTime = -1f, chestHeight = 1.4f;
        private bool adsOn, adsBlurOff;
        private HDAdditionalCameraData hdCamera;

        /// <summary>Looking down a usable sight right now (the weapon is up and has an aim mode).</summary>
        public bool ScopeActive
        {
            get
            {
                if (characterTarget == null || mCamera == null || !characterTarget.IsAiming || !characterTarget.FiringMode || !characterTarget.IsItemEquiped) return false;
                Weapon gun = characterTarget.WeaponInUseRightHand;
                return gun != null && gun.AimMode != Weapon.WeaponAimMode.None;
            }
        }

        /// <summary>The body's position between its last two physics steps (the rigidbody is not interpolated: JU turns it itself).</summary>
        private Vector3 SmoothBodyPosition()
        {
            Vector3 p = characterTarget.transform.position;
            if (bodyCurrTime < 0f || (p - bodyCurr).sqrMagnitude > 9f) { bodyPrev = bodyCurr = p; bodyCurrTime = Time.time; }
            else if ((p - bodyCurr).sqrMagnitude > 1e-10f) { bodyPrev = bodyCurr; bodyCurr = p; bodyCurrTime = Time.time; }
            float a = Mathf.Clamp01((Time.time - bodyCurrTime) / Mathf.Max(0.001f, Time.fixedDeltaTime));
            return Vector3.Lerp(bodyPrev, bodyCurr, a);
        }

        /// <summary>
        /// Where the sight camera goes for the weapon's sight at <paramref name="raw"/>. Measured in the frame of the player's view
        /// (pitch and yaw) around the chest, the sight barely moves when the player turns or looks up; what is left is the walk and
        /// run bob of the animation. A slow average follows where the sight really sits, only <see cref="AimBobKeep"/> of the bob
        /// around it is kept, and the result settles in smoothly (no snap when raising the sights).
        /// </summary>
        public Vector3 SteadyScopePosition(Vector3 raw)
        {
            if (!SteadyAim || characterTarget == null || mCamera == null || mCamera.transform.parent == null) return raw;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 body = SmoothBodyPosition();
            if (characterTarget.HumanoidSpine != null)
                chestHeight = Mathf.Lerp(chestHeight, characterTarget.HumanoidSpine.position.y - characterTarget.transform.position.y, 1f - Mathf.Exp(-dt / 0.5f));
            Vector3 origin = body + Vector3.up * chestHeight;
            Quaternion view = mCamera.transform.parent.rotation;
            Vector3 local = Quaternion.Inverse(view) * (raw - origin);
            if (!adsOn)
            {
                adsOn = true;
                adsLocalAverage = local;
                adsLocal = Quaternion.Inverse(view) * (mCamera.transform.position - origin);   // raise the sights from where the camera is
                adsVelocity = Vector3.zero;
            }
            adsLocalAverage = Vector3.Lerp(adsLocalAverage, local, 1f - Mathf.Exp(-dt / 0.3f));
            Vector3 target = adsLocalAverage + (local - adsLocalAverage) * AimBobKeep;
            adsLocal = Vector3.SmoothDamp(adsLocal, target, ref adsVelocity, AimSettleTime, Mathf.Infinity, dt);
            return origin + view * adsLocal;
        }

        /// <summary>HDRP motion blur on this camera only while looking down the sights (the player's setting stays as it is otherwise).</summary>
        private void SightMotionBlur(bool inSights)
        {
            bool off = inSights && NoMotionBlurInSights;
            if (off == adsBlurOff || mCamera == null) return;
            if (hdCamera == null) hdCamera = mCamera.GetComponent<HDAdditionalCameraData>();
            if (hdCamera == null) return;
            adsBlurOff = off;
            hdCamera.customRenderingSettings = true;
            var mask = hdCamera.renderingPathCustomFrameSettingsOverrideMask;
            mask.mask[(uint)FrameSettingsField.MotionBlur] = off;
            hdCamera.renderingPathCustomFrameSettingsOverrideMask = mask;
            var frame = hdCamera.renderingPathCustomFrameSettings;
            frame.SetEnabled(FrameSettingsField.MotionBlur, !off);
            hdCamera.renderingPathCustomFrameSettings = frame;
        }

        protected enum PlayerStates { Normal, FireMode, Aiming, Driving, Dead }
        protected PlayerStates CharacterState;

        protected virtual void Awake()
        {
            // Disables camera controller on headless server builds (e.g. Linux container/Docker builds)
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                enabled = false;
            }
        }

        protected override void Start()
        {
            base.Start();

            if (TargetToFollow == null) return;

            if (TargetToFollow.TryGetComponent(out JUCharacterController JUcharacter))
            {
                characterTarget = JUcharacter;
                if (characterTarget.HumanoidSpine != null)
                {
                    TargetToFollow = characterTarget.HumanoidSpine;
                }
            }
        }

        protected virtual void Update()
        {
            if (TargetToFollow == null && characterTarget == null) return;

            SetRotationInput();

            if (FollowUpTarget)
            {
                Transform upTarget = characterTarget != null ? characterTarget.transform : TargetToFollow;
                if (upTarget != null)
                {
                    RotateCamera(xmouse, ymouse, upward: upTarget.up);
                }
            }
            else
            {
                RotateCamera(xmouse, ymouse);
            }

            if (EnableAutoRotator)
            {
                if (characterTarget != null)
                {
                    NormalAutoRotation(characterTarget);
                }
                else if (TargetToFollow != null)
                {
                    NormalAutoRotation(TargetToFollow);
                }
            }

            UpdateCharacterState(characterTarget);
            ChangeCameraStateAccordingCharacterState(CharacterState);
            if (characterTarget != null) Aiming = characterTarget.IsAiming;
        }

        protected virtual void SetRotationInput()
        {
            if (Cursor.lockState != CursorLockMode.Locked && JUGameManager.IsMobileControls == false)
            {
                xmouse = 0;
                ymouse = 0;
                return;
            }

            xmouse = JUInput.GetAxis(JUInput.Axis.RotateVertical);
            ymouse = JUInput.GetAxis(JUInput.Axis.RotateHorizontal);
        }

        protected virtual void FixedUpdate()
        {
            if (TargetToFollow == null && characterTarget == null) return;

            if (characterTarget != null)
            {
                DriveVehicles driveAbility = characterTarget.DriveVehicleAbility;

                if (driveAbility && driveAbility.IsDriving && driveAbility.CurrentVehicle)
                {
                    SetPivotCameraPosition(GetCurrentCameraState.GetCameraPivotPosition(driveAbility.CurrentVehicle.transform), true);
                    if (EnableVehicleAutoRotation) DrivingVehicleAutoRotation(driveAbility.CurrentVehicle);
                }
                else if (TargetToFollow != null)
                {
                    SetPivotCameraPosition(GetCurrentCameraState.GetCameraPivotPosition(TargetToFollow), true);
                }
            }
            else if (TargetToFollow != null)
            {
                SetPivotCameraPosition(GetCurrentCameraState.GetCameraPivotPosition(TargetToFollow), true);
            }
        }

        protected virtual void LateUpdate()
        {
            if (mCamera == null) return;
            bool inSights = ScopeActive;
            SightMotionBlur(inSights);
            if (inSights) { SetCameraToScopePosition(); return; }
            adsOn = false;

            SetCameraPosition(GetCurrentCameraState.GetCameraPosition(mCamera.transform), false);
            SetCameraCollision(GetCurrentCameraState.CollisionLayers);
            SetFieldOfView(GetCurrentCameraState.CameraFieldOfView);
        }

        public override void RecoilReaction(float Force)
        {
            base.RecoilReaction(Force);
            if (characterTarget == null) return;
            Aiming = characterTarget.IsAiming;
        }

        protected override void OnCameraRotate()
        {
            StopAutoRotation();
        }

        protected virtual void UpdateCharacterState(JUCharacterController character)
        {
            if (character == null) return;
            if (!character.IsAiming && !character.IsDriving && !character.FiringMode && !character.IsDead) { CharacterState = PlayerStates.Normal; }

            if (character.IsAiming) { CharacterState = PlayerStates.Aiming; }
            if (character.FiringMode) { CharacterState = PlayerStates.FireMode; }
            if (character.IsDriving) { CharacterState = PlayerStates.Driving; }
            if (character.IsDead) { CharacterState = PlayerStates.Dead; }
        }

        protected virtual void ChangeCameraStateAccordingCharacterState(PlayerStates characterState)
        {
            if (IsTransitioningToCustomState) return;

            switch (characterState)
            {
                case PlayerStates.Normal:
                    SetCameraStateTransition(GetCurrentCameraState, NormalCameraState);
                    break;
                case PlayerStates.FireMode:
                    SetCameraStateTransition(GetCurrentCameraState, FireModeCameraState);
                    break;
                case PlayerStates.Aiming:
                    SetCameraStateTransition(GetCurrentCameraState, AimModeCameraState);
                    break;
                case PlayerStates.Driving:
                    SetCameraStateTransition(GetCurrentCameraState, DrivingVehicleCameraState);
                    break;
                case PlayerStates.Dead:
                    SetCameraStateTransition(GetCurrentCameraState, DeadPlayerCameraState);
                    break;
            }
        }

        protected virtual void SetCameraToScopePosition()
        {
            if (characterTarget == null || mCamera == null) return;

            Aiming = characterTarget.IsAiming;

            if (characterTarget.IsItemEquiped == false) return;

            Weapon gun = characterTarget.WeaponInUseRightHand;
            if (gun == null) return;

            if (Aiming && gun.AimMode != Weapon.WeaponAimMode.None && characterTarget.FiringMode)
            {
                SmoothedYMouse = Mathf.Lerp(SmoothedYMouse, ymouse, 10 * Time.deltaTime);
                SmoothedXMouse = Mathf.Lerp(SmoothedXMouse, xmouse, 10 * Time.deltaTime);
                float xSway = AimingSwaySettings.EnableWeaponSway ? (gun.CameraAimingPosition.x - AimingSwaySettings.GeneralIntensity * AimingSwaySettings.HorizontalIntensity * SmoothedYMouse / 80) : gun.CameraAimingPosition.x;
                float ySway = AimingSwaySettings.EnableWeaponSway ? (gun.CameraAimingPosition.y - AimingSwaySettings.GeneralIntensity * AimingSwaySettings.VerticalIntensity * SmoothedXMouse / 80) : gun.CameraAimingPosition.y;

                Vector3 scopePosition = gun.transform.position
                    + gun.transform.right * xSway
                    + gun.transform.up * ySway
                    + mCamera.transform.parent.forward * gun.CameraAimingPosition.z;

                SetCameraPosition(SteadyScopePosition(scopePosition), false);

                AimModeCameraState.CameraFieldOfView = Mathf.Lerp(AimModeCameraState.CameraFieldOfView, gun.CameraFOV, 15 * Time.deltaTime);
                SetFieldOfView(AimModeCameraState.CameraFieldOfView);
            }
            else
            {
                AimModeCameraState.CameraFieldOfView = GetCurrentCameraState.CameraFieldOfView;
            }
        }

        protected virtual void NormalAutoRotation(JUCharacterController character)
        {
            if (character == null || EnableAutoRotator == false) return;
            if (character.FiringMode) { CurrentTimeToAutoRotation = 0; return; }
            if (character.IsMoving) CurrentTimeToAutoRotation += 2 * Time.deltaTime;
            AutoRotator(character.transform, AutoRotateTime, AutoRotationSpeed, AutoRotationSpeed);
        }

        protected virtual void NormalAutoRotation(Transform targetToFollow)
        {
            if (targetToFollow == null || EnableAutoRotator == false) return;

            AutoRotator(targetToFollow, AutoRotateTime, AutoRotationSpeed, AutoRotationSpeed);
        }

        protected virtual void DrivingVehicleAutoRotation(JUTPS.VehicleSystem.Vehicle drivingVehicle)
        {
            if (drivingVehicle == null || drivingVehicle.IsOn == false) return;
            AutoRotator(drivingVehicle.transform, VehicleAutoRotateTime, VehicleAutoRotationSpeed, VehicleAutoRotationSpeed);
        }

        public virtual void AutoRotator(Transform targetRotation, float MaxTimeToAutoRotation, float HorizontalSpeed = 5, float VerticalSpeed = 3, float AngleToStopAutoRotation = 90)
        {
            if (targetRotation == null) return;

            if (Vector3.Angle(targetRotation.up, Vector3.up) > AngleToStopAutoRotation)
            {
                Debug.Log("Disabled Camera Auto Rotation in angle " + AngleToStopAutoRotation);
                return;
            }

            if (IsAutoRotationActivated)
            {
                rotytarget = Mathf.LerpAngle(rotytarget, targetRotation.rotation.eulerAngles.y, HorizontalSpeed * Time.fixedDeltaTime);
                rotxtarget = Mathf.LerpAngle(rotxtarget, 0, VerticalSpeed * Time.fixedDeltaTime);
                if (FollowUpTarget)
                {
                    Transform upTarget = characterTarget != null ? characterTarget.transform : TargetToFollow;
                    if (upTarget != null)
                    {
                        RotateCamera(xmouse, ymouse, upward: upTarget.up);
                    }
                }
                else
                {
                    RotateCamera(xmouse, ymouse);
                }
            }
            else
            {
                CurrentTimeToAutoRotation += Time.deltaTime;
                if (CurrentTimeToAutoRotation >= MaxTimeToAutoRotation) { IsAutoRotationActivated = true; CurrentTimeToAutoRotation = 0; }
            }
        }

        public virtual void StopAutoRotation()
        {
            CurrentTimeToAutoRotation = 0; IsAutoRotationActivated = false;
        }

        public virtual void DisableVehicleAutoRotation()
        {
            StopAutoRotation(); EnableVehicleAutoRotation = false;
        }
    }
}