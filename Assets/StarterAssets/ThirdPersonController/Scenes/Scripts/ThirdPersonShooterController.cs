using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using StarterAssets;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class ThirdPersonShooterController : MonoBehaviour
{

    [SerializeField]private CinemachineVirtualCamera aimVirtualCamera;
    [SerializeField] private float aimSensitivity;
    [SerializeField] private float normalSensitivity;
    [SerializeField] private LayerMask aimColliderLayerMask = new LayerMask();
   [SerializeField] private Transform debugTransform;
   [SerializeField] private Transform pfBulletProjectile;
   [SerializeField] private Transform spawnBulletPosition;
   [SerializeField] private Transform vfxHitGreen;
   [SerializeField] private Transform vfxHitRed;
        
    private ThirdPersonController _thirdPersonController;
    private StarterAssetsInputs starterAssetsInputs;
    private Animator animator;
    

    private void Awake()
    {
        _thirdPersonController = GetComponent<ThirdPersonController>();
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        Vector3 mouseWorldPosition = Vector3.zero;
        
        Vector2 screenCenterPoint = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = Camera.main.ScreenPointToRay(screenCenterPoint);
        
        if (Physics.Raycast(ray, out RaycastHit raycastHit, 99f, aimColliderLayerMask))
        {
            debugTransform.position = raycastHit.point;
            mouseWorldPosition = raycastHit.point;

        }

        if (starterAssetsInputs.aim)
        {
            aimVirtualCamera.gameObject.SetActive(true);
            _thirdPersonController.SetSensitivity(aimSensitivity);
            _thirdPersonController.SetRotateOnMove(false);
            animator.SetLayerWeight(1,Mathf.Lerp(animator.GetLayerWeight(1),1f,Time.deltaTime * 10f));

            Vector3 worldAimTarget = mouseWorldPosition;
            worldAimTarget.y = transform.position.y;
            Vector3 aimDirection = (worldAimTarget - transform.position).normalized;
            
            transform.forward = Vector3.Lerp(transform.forward, aimDirection, Time.deltaTime * 20f);
        }
        else
        {
            aimVirtualCamera.gameObject.SetActive(false);
            _thirdPersonController.SetSensitivity(normalSensitivity);
            _thirdPersonController.SetRotateOnMove(true);
            animator.SetLayerWeight(1,Mathf.Lerp(animator.GetLayerWeight(1),0f,Time.deltaTime * 10f));

            

        }

        if (starterAssetsInputs.shoot)
        {

            Vector3 aimDir = (mouseWorldPosition - spawnBulletPosition.position).normalized;
            Instantiate(pfBulletProjectile, spawnBulletPosition.position, Quaternion.LookRotation(aimDir,Vector3.up));
            starterAssetsInputs.shoot = false;
        }
        

        
    }
}
