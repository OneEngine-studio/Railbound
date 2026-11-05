using System.Collections;
using UnityEngine;
using Michsky.UI.Reach;

namespace Railbound.UI
{
    /// <summary>
    /// Put on the Settings "Panel Content" (the object with the tab PanelManager).
    /// Every time the Settings screen opens it shows the first tab (General) and makes sure only that
    /// tab button looks selected, no matter which tab was used last or what state was saved in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsTabsReset : MonoBehaviour
    {
        [SerializeField] private PanelManager panelManager;
        [SerializeField] private int defaultTab = 0;

        void Awake()
        {
            if (panelManager == null) panelManager = GetComponent<PanelManager>();
        }

        void OnEnable()
        {
            if (panelManager == null) return;
            StopAllCoroutines();
            StartCoroutine(ResetNextFrame());
        }

        private IEnumerator ResetNextFrame()
        {
            // let PanelManager.Start/OnEnable finish first (it initializes the panels on the first frame)
            yield return null;

            var panels = panelManager.panels;
            if (panels == null || panels.Count == 0) yield break;
            int target = Mathf.Clamp(defaultTab, 0, panels.Count - 1);

            if (panelManager.currentPanelIndex != target) panelManager.OpenPanelByIndex(target);

            for (int i = 0; i < panels.Count; i++)
            {
                var btn = panels[i].panelButton;
                if (btn == null) continue;
                btn.SetSelected(i == target);
            }
        }
    }
}
