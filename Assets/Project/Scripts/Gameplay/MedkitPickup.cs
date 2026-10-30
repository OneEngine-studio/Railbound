using UnityEngine;

namespace Railbound.Gameplay
{
    /// <summary>
    /// World pickup for first aid / medkit items in the Battle Royale map.
    /// Can be attached to Survival Tools firstaid box or First aid jar models.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MedkitPickup : MonoBehaviour
    {
        [Header("Medkit Reward")]
        [Tooltip("How many medkits this pickup awards to the player.")]
        public int medkitsToGive = 1;

        [Header("Animation & Floating")]
        [Tooltip("Disabled by default: items use realistic PhysX physics rather than floating.")]
        public bool enableIdleAnimation = false;
        public float rotationSpeed = 0f;
        public float bobHeight = 0f;
        public float bobSpeed = 0f;

        [Header("Physics Settings")]
        [SerializeField] private Rigidbody rb;

        [Header("Visual Effects")]
        public GameObject pickupVfxPrefab;
        public Color glowColor = new Color(0.2f, 1f, 0.4f, 0.8f);

        private Vector3 _startPosition;
        private bool _isPickedUp = false;

        private void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.centerOfMass = new Vector3(0f, -0.08f, 0f);
            }
        }

        private void Start()
        {
            _startPosition = transform.position;
        }

        /// <summary>
        /// Launches the medkit with physical impulse when dropped by an enemy.
        /// </summary>
        public void SpawnWithImpulse(Vector3 velocity, Vector3 angularVelocity)
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.velocity = velocity;
                rb.angularVelocity = angularVelocity;
            }
        }

        private void Update()
        {
            if (!enableIdleAnimation || _isPickedUp) return;

            // Gentle rotation
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

            // Smooth floating bob
            float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = _startPosition + new Vector3(0f, yOffset, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isPickedUp) return;

            // Check if collider belongs to Player
            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerMedkitSystem>() != null)
            {
                var medkitSys = other.GetComponent<PlayerMedkitSystem>() ?? other.GetComponentInParent<PlayerMedkitSystem>();
                if (medkitSys == null)
                {
                    medkitSys = PlayerMedkitSystem.Instance;
                }

                if (medkitSys != null)
                {
                    bool added = medkitSys.AddMedkits(medkitsToGive);
                    if (added)
                    {
                        _isPickedUp = true;
                        TriggerPickupEffects();
                        Destroy(gameObject, 0.05f);
                    }
                }
            }
        }

        private void TriggerPickupEffects()
        {
            if (pickupVfxPrefab != null)
            {
                var vfx = Instantiate(pickupVfxPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);
                Destroy(vfx, 3f);
            }
        }
    }
}

