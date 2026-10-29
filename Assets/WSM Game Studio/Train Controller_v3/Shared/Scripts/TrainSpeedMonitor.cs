using UnityEngine;
using UnityEngine.UI;

namespace WSMGameStudio.RailroadSystem
{
    public class TrainSpeedMonitor : MonoBehaviour
    {
        public Text outputText;

        private ILocomotive _locomotive;
        private float _kph;
        private float _mph;

        void Start()
        {
            _locomotive = GetComponent<ILocomotive>();
        }

        void Update()
        {
            if (_locomotive == null)
                return;

            _kph = Mathf.Abs(_locomotive.Speed_KPH);
            _mph = Mathf.Abs(_locomotive.Speed_MPH);

            if (outputText != null)
            {
                switch (_locomotive.SpeedUnit)
                {
                    case SpeedUnits.kph:
                        outputText.text = string.Format("{0} KPH", _kph.ToString("0"));
                        break;
                    case SpeedUnits.mph:
                        outputText.text = string.Format("{0} MPH", _mph.ToString("0"));
                        break;
                }
            }
        }
    }
}
