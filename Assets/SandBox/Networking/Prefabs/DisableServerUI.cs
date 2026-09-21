using UnityEngine;

public class DisableServerUI : MonoBehaviour
{
    private void Awake()
    {
#if UNITY_SERVER
        Destroy(gameObject);
#endif
    }
}