using UnityEngine;
using UnityEngine.UI;

namespace WSMGameStudio.RailroadSystem
{
    public class DemoUI_v3 : MonoBehaviour
    {
        public GameObject train;
        public Slider maxSpeedSlider;
        public Slider accelerationSlider;
        public Slider brakeSlider;
        public Toggle automaticBrakes;
        public Text playerInputText;

        private ILocomotive _locomotive;
        private ITrainDoorsController _doorController;
        private TrainPlayerInput _playerInput;

        private void Start()
        {
            if (train == null) return;

            _locomotive = train.GetComponent<ILocomotive>();
            _doorController = train.GetComponent<ITrainDoorsController>();
            _playerInput = train.GetComponent<TrainPlayerInput>();

            ConfigureInputText();
        }

        private void Update()
        {
            if (_locomotive == null)
                return;

            if (_playerInput != null && _playerInput.enablePlayerInput)
                return;

            if (maxSpeedSlider != null)
                _locomotive.MaxSpeed = maxSpeedSlider.value;

            if (accelerationSlider != null)
                _locomotive.Acceleration = accelerationSlider.value;

            if (automaticBrakes != null)
                _locomotive.AutomaticBrakes = automaticBrakes.isOn;

            if (brakeSlider != null)
            {
                brakeSlider.enabled = !_locomotive.AutomaticBrakes;

                if (_locomotive.AutomaticBrakes)
                    brakeSlider.value = _locomotive.Brake;
                else
                    _locomotive.Brake = brakeSlider.value;
            }
        }

        private void ConfigureInputText()
        {
            if (playerInputText != null && _playerInput != null && _playerInput.inputSettings != null)
            {
                playerInputText.text = string.Format("Engines: {1}{0}" +
                    "Forward: {2}{0}" +
                    "Reverse: {3}{0}" +
                    "Speed (+): {4}{0}" +
                    "Speed (-): {5}{0}" +
                    "Brakes: {6}{0}" +
                    "Lights: {7}{0}" +
                    "Cabin Lights: {8}{0}" +
                    "Honk: {9}{0}" +
                    "Bell: {10}{0}" +
                    "Cabin Door: {11}{0}"
                    , System.Environment.NewLine, _playerInput.inputSettings.toggleEngine
                    , _playerInput.inputSettings.forward
                    , _playerInput.inputSettings.reverse
                    , _playerInput.inputSettings.increaseSpeed
                    , _playerInput.inputSettings.decreaseSpeed
                    , _playerInput.inputSettings.brakes
                    , _playerInput.inputSettings.lights
                    , _playerInput.inputSettings.internalLights
                    , _playerInput.inputSettings.honk
                    , _playerInput.inputSettings.bell
                    , _playerInput.inputSettings.cabinRightDoor
                    );
            }
        }

        public void ToggleEngine()
        {
            if (_locomotive == null)
                return;

            _locomotive.ToggleEngine();
        }

        public void ToggleLights()
        {
            if (_locomotive == null)
                return;

            _locomotive.ToggleLights();
        }

        public void ToggleInternalLights()
        {
            if (_locomotive == null)
                return;

            _locomotive.ToggleInternalLights();
        }

        public void Honk()
        {
            if (_locomotive == null)
                return;

            _locomotive.Honk();
        }

        public void ToggleBell()
        {
            if (_locomotive == null)
                return;

            _locomotive.ToogleBell();
        }

        public void CabinDoor()
        {
            if (_doorController.CabinRightDoorOpen)
                _doorController.CloseCabinDoorRight();
            else
            {
                _doorController.OpenCabinDoorRight();
            }
        }
    }
}
