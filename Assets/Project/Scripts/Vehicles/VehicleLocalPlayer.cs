using System;
using JUTPS;
using JUTPS.CameraSystems;
using JUTPS.JUInputSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.Vehicles
{
    /// <summary>
    /// The local player's side of vehicles (only on machines with a screen):
    ///  * on foot near a vehicle: the prompt ("F  DRIVE  NIVA" / "F  RIDE  NIVA" / "NIVA · FULL"), F asks for a seat;
    ///  * seated: F gets out, C moves to the next free seat; the driving panel (speed, gear, seat, the keys) and
    ///    a third-person chase camera that follows the vehicle smoothly, orbits with the mouse and swings back behind
    ///    the vehicle when the mouse rests. The JU camera is handed back, pointing the same way, on the way out.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class VehicleLocalPlayer : MonoBehaviour
    {
        private static VehicleLocalPlayer instance;

        public static void Ensure()
        {
            if (instance != null || Headless) return;
            instance = new GameObject("[Vehicles] Local Player").AddComponent<VehicleLocalPlayer>();
        }

        private static bool Headless
        {
            get
            {
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return true;
                foreach (string a in System.Environment.GetCommandLineArgs())
                    if (string.Equals(a, "-server", System.StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-dedicated", System.StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }

        public static VehicleOccupant Seated => instance != null ? instance.seated : null;
        /// <summary>Test aid: the vehicle the prompt offers right now.</summary>
        public static RailboundVehicle Offered => instance != null ? instance.offered : null;

        private VehicleOccupant seated;
        private RailboundVehicle offered;
        private float nextScan, inputCooldown;

        // ---------------------------------------------------------------- seat events (from VehicleOccupant)
        public static void NotifySeated(VehicleOccupant occ)
        {
            Ensure();
            if (instance == null) return;
            if (instance.seated == occ && instance.cameraActive) return;
            instance.seated = occ;
            instance.BeginCamera(occ.Vehicle);
        }

        public static void NotifyLeft(VehicleOccupant occ, RailboundVehicle vehicle)
        {
            if (instance == null || instance.seated != occ) return;
            instance.seated = null;
            instance.EndCamera(occ);
        }

        private void OnEnable() { RailboundVehicle.Denied += OnDenied; }
        private void OnDisable() { RailboundVehicle.Denied -= OnDenied; }
        private void OnDestroy() { if (instance == this) instance = null; if (cameraActive) EndCamera(null); }

        private void OnDenied(RailboundVehicle v, string why) { Toast(why); }

        // ---------------------------------------------------------------- input
        private void Update()
        {
            BuildUi();
            var pc = JUGameManager.PlayerController;
            bool alive = pc != null && pc.gameObject.activeInHierarchy && !pc.IsDead;
            if (seated != null && (seated.Vehicle == null || !alive)) { if (cameraActive) EndCamera(seated); seated = null; }

            bool controls = alive && !JUPauseGame.IsPaused && Cursor.lockState == CursorLockMode.Locked;
            bool interact = controls && Time.unscaledTime >= inputCooldown && JUInput.GetButtonDown(JUInput.Buttons.EnterVehicleButton);
            var kb = UnityEngine.InputSystem.Keyboard.current;

            if (seated != null)
            {
                offered = null;
                var v = seated.Vehicle;
                if (interact)
                {
                    inputCooldown = Time.unscaledTime + 0.4f;
                    v.RequestExit(pc.gameObject);
                }
                else if (controls && kb != null && kb.cKey.wasPressedThisFrame)
                {
                    int next = NextFreeSeat(v, seated.Seat);
                    if (next >= 0) v.RequestMove(pc.gameObject, next); else Toast("NO FREE SEAT");
                }
            }
            else
            {
                if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 0.1f; offered = alive ? FindOffer(pc.transform.position) : null; }
                if (interact && offered != null)
                {
                    inputCooldown = Time.unscaledTime + 0.5f;
                    if (offered.PreferredFreeSeat(pc.transform.position) < 0) Toast("VEHICLE FULL");
                    else offered.RequestEnter(pc.gameObject);
                }
            }
            UpdateUi(pc);
        }

        private static RailboundVehicle FindOffer(Vector3 from)
        {
            RailboundVehicle best = null;
            float bestD = float.MaxValue;
            foreach (var v in RailboundVehicle.All)
            {
                if (v == null || !v.isActiveAndEnabled || !v.IsUpright || v.IsWrecked) continue;
                float d = v.DistanceTo(from);
                if (d <= v.EnterRange && d < bestD) { bestD = d; best = v; }
            }
            return best;
        }

        private static int NextFreeSeat(RailboundVehicle v, int current)
        {
            var s = v.Seats;
            for (int k = 1; k <= v.SeatCount; k++)
            {
                int i = (current + k) % v.SeatCount;
                if (s[i] == 0) return i;
            }
            return -1;
        }

        // ---------------------------------------------------------------- chase camera
        private TPSCameraController tps;
        private Camera cam;
        private bool cameraActive;
        private float yaw, pitch, fov, baseFov, lastMouse;
        private Vector3 pivot;
        private RailboundVehicle camVehicle;
        private readonly RaycastHit[] camHits = new RaycastHit[16];

        private void BeginCamera(RailboundVehicle v)
        {
            if (v == null) return;
            if (tps == null) tps = FindFirstObjectByType<TPSCameraController>();
            if (tps == null) return;
            cam = tps.mCamera != null ? tps.mCamera : Camera.main;
            if (cam == null) return;
            camVehicle = v;
            if (!cameraActive)
            {
                baseFov = tps.NormalCameraState != null && tps.NormalCameraState.CameraFieldOfView > 1f ? tps.NormalCameraState.CameraFieldOfView : cam.fieldOfView;
                fov = cam.fieldOfView;
                yaw = cam.transform.eulerAngles.y;
                pitch = 12f;
                pivot = Anchor(v);
                lastMouse = Time.unscaledTime;
            }
            cameraActive = true;
            tps.enabled = false;
        }

        private void EndCamera(VehicleOccupant occ)
        {
            cameraActive = false;
            camVehicle = null;
            if (tps == null) return;
            tps.rotytarget = yaw;
            tps.rotxtarget = Mathf.Clamp(pitch, -30f, 45f);
            if (tps.TargetToFollow != null) tps.transform.position = tps.TargetToFollow.position;
            if (cam != null) cam.fieldOfView = baseFov;
            tps.enabled = true;
        }

        private static Vector3 Anchor(RailboundVehicle v)
        {
            var b = v.LocalBounds;
            return v.transform.TransformPoint(new Vector3(0f, b.max.y + 0.3f, b.center.z));
        }

        private void LateUpdate()
        {
            if (!cameraActive || camVehicle == null || cam == null || tps == null) return;
            if (tps.enabled) tps.enabled = false;
            float dt = Time.deltaTime;
            var v = camVehicle;

            // orbit with the mouse
            bool look = !JUPauseGame.IsPaused && Cursor.lockState == CursorLockMode.Locked;
            float mx = look ? JUInput.GetAxis(JUInput.Axis.RotateHorizontal) : 0f;
            float my = look ? JUInput.GetAxis(JUInput.Axis.RotateVertical) : 0f;
            float sens = tps.DrivingVehicleCameraState != null ? tps.DrivingVehicleCameraState.RotationSensibility : 4f;
            yaw += mx * sens * tps.GeneralSensibility;
            pitch -= my * sens * tps.GeneralVerticalSensibility;
            pitch = Mathf.Clamp(pitch, -20f, 60f);
            if (Mathf.Abs(mx) + Mathf.Abs(my) > 0.01f) lastMouse = Time.unscaledTime;

            // swing back behind the vehicle when the mouse rests and the vehicle moves forward
            float kmh = v.SpeedKmh;
            float forwardSpeed = Vector3.Dot(v.Body != null && !v.Body.isKinematic ? v.Body.velocity : Vector3.zero, v.transform.forward);
            if (Time.unscaledTime - lastMouse > 1.4f && kmh > 12f && (forwardSpeed >= 0f || !v.IsSimulating))
            {
                float k = 1f - Mathf.Exp(-2.2f * dt);
                yaw = Mathf.LerpAngle(yaw, v.transform.eulerAngles.y, k);
                pitch = Mathf.Lerp(pitch, 10f, k);
            }

            // follow (a soft spring: the vehicle pulls ahead a little when it accelerates)
            Vector3 anchor = Anchor(v);
            pivot = Vector3.Lerp(pivot, anchor, 1f - Mathf.Exp(-18f * dt));
            if ((pivot - anchor).sqrMagnitude > 64f) pivot = anchor;

            var b = v.LocalBounds;
            float distance = Mathf.Clamp(b.size.z * 1.2f + 1.5f, 5.5f, 9.5f);
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 dir = rot * Vector3.forward;
            float d = CameraClearance(pivot, -dir, distance, v);

            tps.transform.SetPositionAndRotation(pivot, Quaternion.Euler(0f, yaw, 0f));
            cam.transform.position = pivot - dir * d;
            // aim just below the roof line so the whole vehicle sits around the centre of the screen (clear of the speed panel)
            cam.transform.rotation = Quaternion.LookRotation((pivot - Vector3.up * 0.35f) - cam.transform.position);

            // a little wider at speed
            fov = Mathf.Lerp(fov, baseFov + Mathf.Clamp01(kmh / 170f) * 12f, 1f - Mathf.Exp(-3f * dt));
            cam.fieldOfView = fov;

            var test = TestCamera?.Invoke();
            if (test.HasValue)
            {
                cam.transform.position = test.Value.position;
                cam.transform.rotation = Quaternion.LookRotation(test.Value.lookAt - test.Value.position);
                cam.fieldOfView = 50f;
            }
        }

        /// <summary>Test aid: a fixed camera (position, point to look at) instead of the chase camera while it returns a value.</summary>
        public static Func<(Vector3 position, Vector3 lookAt)?> TestCamera;

        private float CameraClearance(Vector3 from, Vector3 dir, float distance, RailboundVehicle v)
        {
            int n = Physics.SphereCastNonAlloc(from, 0.25f, dir, camHits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = distance;
            for (int i = 0; i < n; i++)
            {
                var c = camHits[i].collider;
                if (c == null || camHits[i].distance <= 0f) continue;
                if (c.attachedRigidbody != null && c.attachedRigidbody == v.Body) continue;
                if (c.transform.IsChildOf(v.transform)) continue;
                if (c.GetComponentInParent<JUCharacterController>() != null) continue;
                best = Mathf.Min(best, camHits[i].distance - 0.15f);
            }
            return Mathf.Max(best, 1.2f);
        }

        // ---------------------------------------------------------------- HUD
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Faint = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color Panel = new Color(0f, 0f, 0f, 0.55f);
        private Canvas canvas;
        private CanvasGroup promptGroup, driveGroup, toastGroup;
        private TextMeshProUGUI promptKey, promptVerb, promptName, speedNum, speedUnit, gearText, seatText, keysText, toastText;
        private Image speedFill, keyBox, healthFill;
        private static readonly Color HealthLow = new Color(1f, 0.32f, 0.25f, 1f);
        private readonly Image[] seatDots = new Image[VehicleSeats.Max];
        private TMP_FontAsset fontBold, fontSemi;
        private Sprite px;
        private float toastUntil;
        private int shownSpeed = -1;

        private void Toast(string text)
        {
            BuildUi();
            if (toastText == null) return;
            toastText.text = text;
            toastUntil = Time.unscaledTime + 1.8f;
        }

        private void UpdateUi(JUCharacterController pc)
        {
            if (canvas == null) return;
            float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);

            // prompt
            bool showPrompt = seated == null && offered != null;
            if (showPrompt)
            {
                int seat = offered.PreferredFreeSeat(pc.transform.position);
                bool full = seat < 0;
                promptKey.text = "F";
                keyBox.color = full ? Faint : Color.white;
                promptKey.color = full ? Faint : Color.black;
                promptVerb.text = full ? "FULL" : offered.SeatDef(seat).Driver ? "DRIVE" : "RIDE";
                promptVerb.color = full ? Dim : Color.white;
                promptName.text = offered.DisplayName;
            }
            promptGroup.alpha = Mathf.Lerp(promptGroup.alpha, showPrompt ? 1f : 0f, k);

            // driving panel
            bool showDrive = seated != null && seated.Vehicle != null;
            if (showDrive)
            {
                var v = seated.Vehicle;
                int kmh = Mathf.RoundToInt(v.SpeedKmh);
                if (kmh != shownSpeed) { shownSpeed = kmh; speedNum.text = kmh.ToString("000"); }
                speedFill.fillAmount = Mathf.Lerp(speedFill.fillAmount, Mathf.Clamp01(kmh / 200f), k);
                float hp = v.Health01;
                healthFill.fillAmount = Mathf.Lerp(healthFill.fillAmount, hp, k);
                // white while healthy, red under a third, pulsing when it is about to go
                Color hc = hp > 0.33f ? Color.white : HealthLow;
                if (hp < 0.15f) hc.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
                healthFill.color = hc;
                int gear = v.Controller != null ? v.Controller.powertrain.transmission.Gear : 0;
                gearText.text = gear < 0 ? "R" : gear == 0 ? "N" : gear.ToString();
                bool driver = seated.IsDriver;
                seatText.text = (driver ? "DRIVER" : "PASSENGER") + "  ·  " + v.DisplayName;
                keysText.text = driver
                    ? "<b>F</b> EXIT    <b>C</b> SEAT    <b>SPACE</b> HANDBRAKE    <b>SHIFT</b> BOOST    <b>H</b> HORN    <b>L</b> LIGHTS"
                    : "<b>F</b> EXIT    <b>C</b> SEAT";
                var s = v.Seats;
                for (int i = 0; i < seatDots.Length; i++)
                {
                    bool exists = i < v.SeatCount;
                    seatDots[i].gameObject.SetActive(exists);
                    if (!exists) continue;
                    seatDots[i].color = i == seated.Seat ? Color.white : s[i] != 0 ? Dim : Faint;
                    seatDots[i].rectTransform.sizeDelta = i == seated.Seat ? new Vector2(18f, 6f) : new Vector2(10f, 6f);
                }
            }
            driveGroup.alpha = Mathf.Lerp(driveGroup.alpha, showDrive ? 1f : 0f, k);

            toastGroup.alpha = Mathf.Lerp(toastGroup.alpha, Time.unscaledTime < toastUntil ? 1f : 0f, k);
        }

        private void BuildUi()
        {
            if (canvas != null) return;
            fontBold = FindFont("Rajdhani-Bold") ?? TMP_Settings.defaultFontAsset;
            fontSemi = FindFont("Rajdhani-SemiBold") ?? fontBold;
            px = MakePixel();

            var cgo = new GameObject("[HUD] Vehicles", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 38;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            var root = cgo.GetComponent<CanvasGroup>(); root.blocksRaycasts = false; root.interactable = false;

            // prompt: [F]  DRIVE  NIVA, bottom centre above the hotbar
            var prompt = Rect("Prompt", cgo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(420f, 44f));
            promptGroup = prompt.gameObject.AddComponent<CanvasGroup>(); promptGroup.alpha = 0f;
            var row = prompt.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter; row.spacing = 14f; row.childControlWidth = true; row.childControlHeight = true; row.childForceExpandWidth = false;
            var keyRect = Rect("Key", prompt, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(34f, 34f));
            keyBox = keyRect.gameObject.AddComponent<Image>(); keyBox.sprite = px; keyBox.color = Color.white;
            var keyLe = keyRect.gameObject.AddComponent<LayoutElement>(); keyLe.preferredWidth = 34f; keyLe.preferredHeight = 34f;
            promptKey = Text("K", keyRect, "F", 24f, fontBold, Color.black, 0f, TextAlignmentOptions.Center); Stretch(promptKey.rectTransform);
            promptVerb = Text("Verb", prompt, "DRIVE", 26f, fontBold, Color.white, 6f, TextAlignmentOptions.MidlineLeft);
            promptName = Text("Name", prompt, "", 26f, fontSemi, Dim, 6f, TextAlignmentOptions.MidlineLeft);
            foreach (var t in new[] { promptVerb, promptName }) { var le = t.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 34f; }

            // driving panel, bottom centre (the weapon and ability HUD has the bottom right)
            var panel = Rect("Drive", cgo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(380f, 150f), new Vector2(0.5f, 0f));
            driveGroup = panel.gameObject.AddComponent<CanvasGroup>(); driveGroup.alpha = 0f;
            var bg = panel.gameObject.AddComponent<Image>(); bg.sprite = px; bg.color = Panel;
            var accent = Rect("Accent", panel, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(4f, 0f), new Vector2(0f, 0.5f));
            accent.gameObject.AddComponent<Image>().sprite = px;

            speedNum = Text("Speed", panel, "000", 72f, fontBold, Color.white, 2f, TextAlignmentOptions.BottomRight);
            Place(speedNum.rectTransform, new Vector2(1f, 1f), new Vector2(-86f, -6f), new Vector2(200f, 76f), new Vector2(1f, 1f));
            speedUnit = Text("Unit", panel, "KM/H", 18f, fontSemi, Dim, 4f, TextAlignmentOptions.BottomLeft);
            Place(speedUnit.rectTransform, new Vector2(1f, 1f), new Vector2(-80f, -60f), new Vector2(64f, 22f), new Vector2(0f, 1f));
            gearText = Text("Gear", panel, "N", 40f, fontBold, Color.white, 0f, TextAlignmentOptions.Center);
            Place(gearText.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -12f), new Vector2(56f, 56f), new Vector2(0f, 1f));
            var gearBox = Rect("GearBox", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -12f), new Vector2(56f, 56f), new Vector2(0f, 1f));
            var gearOutline = gearBox.gameObject.AddComponent<Image>(); gearOutline.sprite = px; gearOutline.color = new Color(1f, 1f, 1f, 0.08f);
            gearBox.SetSiblingIndex(gearText.transform.GetSiblingIndex());

            var barBack = Rect("SpeedBar", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 58f), new Vector2(-44f, 4f));
            var barBackImg = barBack.gameObject.AddComponent<Image>(); barBackImg.sprite = px; barBackImg.color = new Color(1f, 1f, 1f, 0.12f);
            var fill = Rect("Fill", barBack, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            speedFill = fill.gameObject.AddComponent<Image>(); speedFill.sprite = px; speedFill.type = Image.Type.Filled; speedFill.fillMethod = Image.FillMethod.Horizontal; speedFill.fillAmount = 0f;

            // vehicle health along the top edge of the panel
            var hpBack = Rect("Health", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(2f, 0f), new Vector2(-4f, 4f), new Vector2(0.5f, 1f));
            var hpBackImg = hpBack.gameObject.AddComponent<Image>(); hpBackImg.sprite = px; hpBackImg.color = new Color(1f, 1f, 1f, 0.12f);
            var hpFill = Rect("Fill", hpBack, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            healthFill = hpFill.gameObject.AddComponent<Image>(); healthFill.sprite = px; healthFill.type = Image.Type.Filled; healthFill.fillMethod = Image.FillMethod.Horizontal; healthFill.fillAmount = 1f;

            seatText = Text("Seat", panel, "", 18f, fontBold, Color.white, 4f, TextAlignmentOptions.MidlineLeft);
            Place(seatText.rectTransform, new Vector2(0f, 0f), new Vector2(22f, 30f), new Vector2(260f, 22f), new Vector2(0f, 0.5f));
            var dots = Rect("Seats", panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-22f, 30f), new Vector2(90f, 8f), new Vector2(1f, 0.5f));
            var dotRow = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            dotRow.childAlignment = TextAnchor.MiddleRight; dotRow.spacing = 5f; dotRow.childControlWidth = false; dotRow.childControlHeight = false; dotRow.childForceExpandWidth = false;
            for (int i = 0; i < seatDots.Length; i++)
            {
                var d = Rect("Seat" + i, dots, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(10f, 6f));
                seatDots[i] = d.gameObject.AddComponent<Image>(); seatDots[i].sprite = px;
            }
            keysText = Text("Keys", panel, "", 14f, fontSemi, Dim, 2f, TextAlignmentOptions.MidlineLeft);
            Place(keysText.rectTransform, new Vector2(0f, 0f), new Vector2(22f, 10f), new Vector2(560f, 18f), new Vector2(0f, 0.5f));
            keysText.enableWordWrapping = false;
            keysText.rectTransform.anchorMin = keysText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            keysText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            keysText.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            keysText.alignment = TextAlignmentOptions.Center;

            // toast, centre
            var toast = Rect("Toast", cgo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 310f), new Vector2(600f, 40f));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0f;
            toastText = Text("Text", toast, "", 24f, fontBold, Color.white, 6f, TextAlignmentOptions.Center); Stretch(toastText.rectTransform);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string text, float size, TMP_FontAsset font, Color c, float spacing, TextAlignmentOptions al)
        {
            var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            t.font = font; t.fontSize = size; t.color = c; t.characterSpacing = spacing; t.alignment = al; t.text = text;
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private static TMP_FontAsset FindFont(string namePart)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (f != null && f.name.Contains(namePart)) return f;
            return null;
        }

        private static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[16]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
