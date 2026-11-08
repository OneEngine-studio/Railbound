using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using JUTPS.FX;
using JUTPSEditor.JUHeader;

namespace JUTPS
{

	[AddComponentMenu("JU TPS/Third Person System/Additionals/JU Health")]
	public class JUHealth : MonoBehaviour
	{
		[JUHeader("Settings")]
		public float Health = 100;
		public float MaxHealth = 100;

		[JUHeader("Effects")]
		public bool BloodScreenEffect = false;
		public GameObject BloodHitParticle;

		[JUHeader("On Death Event")]
		public UnityEvent OnDeath;

		public event System.Action<float, Vector3> OnTakeDamage;
		public static event System.Action<JUHealth, float, Vector3> OnAnyDamaged;

		[JUHeader("Stats")]
		public bool IsDead;

		/// <summary>Set by the code that is about to deal damage (bullets) so the victim can remember who hit it.</summary>
		public static GameObject PendingAttacker;
		/// <summary>Who damaged this character last, and when (Time.time). Used for kill credit.</summary>
		[System.NonSerialized] public GameObject LastAttacker;
		[System.NonSerialized] public float LastAttackTime = -999f;

		void Start()
		{
			LimitHealth();
			InvokeRepeating(nameof(CheckHealthState), 0, 0.5f);
		}
		private void LimitHealth()
		{
			Health = Mathf.Clamp(Health, 0, MaxHealth);
		}
		public static void DoDamage(JUHealth health, float damage, Vector3 hitPosition = default(Vector3))
		{
			health.DoDamage(damage, hitPosition);
		}
		public void DoDamage(float damage, Vector3 hitPosition = default(Vector3))
		{
			if (PendingAttacker != null && PendingAttacker != gameObject)
			{
				LastAttacker = PendingAttacker;
				LastAttackTime = Time.time;
			}

			Health -= damage;
			LimitHealth();
			Invoke(nameof(CheckHealthState), 0.016f);

			OnTakeDamage?.Invoke(damage, hitPosition);
			OnAnyDamaged?.Invoke(this, damage, hitPosition);

			if (BloodScreenEffect) BloodScreen.PlayerTakingDamaged();
			if (hitPosition != Vector3.zero && BloodHitParticle != null)
			{
				GameObject fxParticle = Instantiate(BloodHitParticle, hitPosition, Quaternion.identity);
				fxParticle.hideFlags = HideFlags.HideInHierarchy;
				Destroy(fxParticle, 3);
			}
		}

		public void CheckHealthState()
		{
			LimitHealth();

			if (Health <= 0 && IsDead == false)
			{
				Health = 0;
				IsDead = true;

				//Disable all damagers0
				foreach (Damager dmg in GetComponentsInChildren<Damager>()) dmg.gameObject.SetActive(false);

				OnDeath.Invoke();
			}

			if (Health > 0) IsDead = false;
		}
	}

}
