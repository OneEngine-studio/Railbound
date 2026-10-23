using System.Collections.Generic;
using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    public static class TrainPhysics
    {
        /// <summary>
        /// Calculate current max speed
        /// </summary>
        /// <param name="currentMaxSpeed"></param>
        /// <param name="maxSpeed"></param>
        /// <param name="accelerationRate"></param>
        /// <param name="brakingDecelerationRate"></param>
        /// <param name="inertiaDecelerationRate"></param>
        /// <returns></returns>
        public static float CalculateCurrentMaxSpeed(float currentMaxSpeed, float maxSpeed, float accelerationRate, float brakingDecelerationRate, float inertiaDecelerationRate)
        {
            if (currentMaxSpeed < maxSpeed)
                currentMaxSpeed = Mathf.MoveTowards(currentMaxSpeed, maxSpeed, accelerationRate * Time.deltaTime);
            if (currentMaxSpeed > maxSpeed)
                currentMaxSpeed = Mathf.MoveTowards(currentMaxSpeed, maxSpeed, brakingDecelerationRate * Time.deltaTime);

            return currentMaxSpeed;
        }

        /// <summary>
        /// Physics based trains speed control
        /// </summary>
        /// <param name="rigidbody"></param>
        /// <param name="engineOn"></param>
        /// <param name="isGrounded"></param>
        /// <param name="maxSpeed"></param>
        /// <param name="currentSpeed"></param>
        /// <param name="acceleration"></param>
        /// <param name="brake"></param>
        /// <param name="accelerationRate"></param>
        /// <param name="brakingDecelerationRate"></param>
        /// <param name="inertiaDecelerationRate"></param>
        /// <param name="targetVelocityIn"></param>
        /// <param name="targetVelocityOut"></param>
        /// <param name="currentSpeedIn"></param>
        /// <param name="currentSpeedOut"></param>
        /// <param name="targetSpeedIn"></param>
        /// <param name="targetSpeedOut"></param>
        public static void SpeedControl_PhysicsBased(Rigidbody rigidbody, bool engineOn, bool isGrounded, float maxSpeed, float currentSpeed, float acceleration, float brake, float accelerationRate, float brakingDecelerationRate, float inertiaDecelerationRate, Vector3 targetVelocityIn, out Vector3 targetVelocityOut, float currentSpeedIn, out float currentSpeedOut, float targetSpeedIn, out float targetSpeedOut)
        {
            currentSpeedOut = currentSpeedIn;
            targetSpeedOut = targetSpeedIn;
            targetVelocityOut = targetVelocityIn;

            if (isGrounded)
            {
                float speedChangeRate, accelerationForce, brakingForce;

                speedChangeRate = GetSpeedChangeRate(engineOn, acceleration, brake, accelerationRate, brakingDecelerationRate, inertiaDecelerationRate, out accelerationForce, out brakingForce);
                targetSpeedOut = GetTargetSpeed(engineOn, currentSpeedOut, maxSpeed, acceleration, speedChangeRate, accelerationForce, brakingForce);
                currentSpeedOut = SoftAcceleration(currentSpeedOut, targetSpeedOut, speedChangeRate);

                //Apply velocity to rigidbody
                targetVelocityOut = currentSpeedOut == 0f ? Vector3.zero : (rigidbody.transform.forward * currentSpeedOut);
                rigidbody.velocity = targetVelocityOut;
            }
        }

        /// <summary>
        /// Spline based trains speed control
        /// </summary>
        /// <param name="engineOn"></param>
        /// <param name="maxSpeed"></param>
        /// <param name="acceleration"></param>
        /// <param name="brake"></param>
        /// <param name="accelerationRate"></param>
        /// <param name="brakingDecelerationRate"></param>
        /// <param name="inertiaDecelerationRate"></param>
        /// <param name="currentSpeedIn"></param>
        /// <param name="currentSpeedOut"></param>
        /// <param name="targetSpeedIn"></param>
        /// <param name="targetSpeedOut"></param>
        public static void SpeedControl_SplineBased(bool engineOn, float maxSpeed, float acceleration, float brake, float accelerationRate, float brakingDecelerationRate, float inertiaDecelerationRate, float currentSpeedIn, out float currentSpeedOut, float targetSpeedIn, out float targetSpeedOut)
        {
            currentSpeedOut = currentSpeedIn;
            targetSpeedOut = targetSpeedIn;

            float speedChangeRate, accelerationForce, brakingForce;

            speedChangeRate = GetSpeedChangeRate(engineOn, acceleration, brake, accelerationRate, brakingDecelerationRate, inertiaDecelerationRate, out accelerationForce, out brakingForce);
            targetSpeedOut = GetTargetSpeed(engineOn, currentSpeedOut, maxSpeed, acceleration, speedChangeRate, accelerationForce, brakingForce);
            currentSpeedOut = SoftAcceleration(currentSpeedOut, targetSpeedOut, speedChangeRate);
        }

        /// <summary>
        /// Calculates how fast the train should accelerate/decelerate based on acceleration and brake inputs
        /// </summary>
        /// <param name="engineOn"></param>
        /// <param name="acceleration"></param>
        /// <param name="brake"></param>
        /// <param name="accelerationRate"></param>
        /// <param name="brakingDecelerationRate"></param>
        /// <param name="inertiaDecelerationRate"></param>
        /// <param name="accelerationForce"></param>
        /// <param name="brakingForce"></param>
        /// <returns></returns>
        private static float GetSpeedChangeRate(bool engineOn, float acceleration, float brake, float accelerationRate, float brakingDecelerationRate, float inertiaDecelerationRate, out float accelerationForce, out float brakingForce)
        {
            float speedChangeRate;
            accelerationForce = Mathf.Abs(accelerationRate * acceleration);
            brakingForce = Mathf.Abs(brakingDecelerationRate * brake);

            if ((!engineOn) || acceleration == 0f)
                speedChangeRate = brake == 0f ? Mathf.Abs(inertiaDecelerationRate) : Mathf.Max(brakingForce, Mathf.Abs(inertiaDecelerationRate));
            else
                speedChangeRate = Mathf.Max(accelerationForce, brakingForce) - Mathf.Min(accelerationForce, brakingForce);

            return speedChangeRate;
        }

        /// <summary>
        /// Calculates target speed
        /// </summary>
        /// <param name="acceleration"></param>
        /// <param name="maxSpeed"></param>
        /// <returns></returns>
        public static float GetTargetSpeed(bool engineOn, float currentSpeed, float maxSpeed, float acceleration, float speedChangeRate, float accelerationForce, float brakingForce)
        {
            float targetSpeed = 0f;

            if ((!engineOn) || (acceleration == 0.0f) || (brakingForce > accelerationForce)) //Stopping
                targetSpeed = Mathf.MoveTowards(currentSpeed, 0f, speedChangeRate * Time.deltaTime);
            else if (acceleration > 0.0f) //Moving forwards
            {
                targetSpeed = Mathf.MoveTowards(currentSpeed, Mathf.Lerp(0.0f, maxSpeed, acceleration), speedChangeRate * Time.deltaTime);
                targetSpeed = (targetSpeed > maxSpeed) ? maxSpeed : targetSpeed; //Speed cap validation
            }
            else if (acceleration < 0.0f) //Moving backwards
            {
                targetSpeed = Mathf.MoveTowards(currentSpeed, Mathf.Lerp(0.0f, -maxSpeed, -acceleration), speedChangeRate * Time.deltaTime);
                targetSpeed = (targetSpeed < -maxSpeed) ? -maxSpeed : targetSpeed;
            }

            return targetSpeed;
        }

        /// <summary>
        /// Physics based brakes used on wheels
        /// </summary>
        /// <param name="rigidbody"></param>
        /// <param name="brake"></param>
        /// <param name="brakingDecelerationRate"></param>
        /// <param name="targetSpeed"></param>
        /// <returns></returns>
        public static float ApplyBrakes_PhysicsBasedWheels(Rigidbody rigidbody, float brake, float brakingDecelerationRate, float targetSpeed)
        {
            if (brake > 0.0f)
            {
                targetSpeed = 0.0f;
                rigidbody.velocity = Vector3.MoveTowards(rigidbody.velocity, Vector3.zero, Time.deltaTime * (brakingDecelerationRate * brake));
            }
            else
            {
                rigidbody.angularDrag = GeneralSettings.IdleDrag;
            }

            return targetSpeed;
        }

        /// <summary>
        /// Slowly goes from current speed to target speed
        /// </summary>
        /// <param name="currentSpeed"></param>
        /// <param name="targetSpeed"></param>
        /// <param name="speedChangeRate"></param>
        /// <returns></returns>
        public static float SoftAcceleration(float currentSpeed, float targetSpeed, float speedChangeRate)
        {
            if (Mathf.Abs(currentSpeed) >= Mathf.Abs(targetSpeed))
                currentSpeed = targetSpeed;
            else
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, speedChangeRate * Time.deltaTime);

            return currentSpeed;
        }

        /// <summary>
        /// Update physics based wheels properties
        /// </summary>
        /// <param name="wheelsScripts"></param>
        /// <param name="brake"></param>
        /// <param name="speed"></param>
        /// <param name="brakingDecelerationRate"></param>
        public static void UpdateWheels(List<TrainWheel_v3> wheelsScripts, float brake, float speed, float brakingDecelerationRate)
        {
            foreach (var wheel in wheelsScripts)
            {
                wheel.Brake = brake;
                wheel.Speed = speed;
                wheel.BrakingDecelerationRate = brakingDecelerationRate;
            }
        }

        /// <summary>
        /// Update spline based wheels properties
        /// </summary>
        /// <param name="wheelsScripts"></param>
        /// <param name="brake"></param>
        /// <param name="speed"></param>
        public static void UpdateWheels(List<TrainWheel_v3> wheelsScripts, float brake, float speed)
        {
            foreach (var wheel in wheelsScripts)
            {
                wheel.Brake = brake;
                wheel.Speed = speed;
            }
        }

        /// <summary>
        /// Connect new car joint to front car
        /// </summary>
        /// <param name="newTrainCar"></param>
        /// <param name="previousCarCoupler"></param>
        public static void ConnectTrainCar(HingeJoint newTrainCar, Rigidbody frontCarCoupler)
        {
            newTrainCar.connectedBody = frontCarCoupler;
        }

        /// <summary>
        /// Disconnect train car
        /// </summary>
        /// <param name="trainCar"></param>
        public static void DisconnectTrainCar(HingeJoint trainCar)
        {
            trainCar.connectedBody = null;
        }

        /// <summary>
        /// Reposition wagons on a straight line behind target locomotive
        /// </summary>
        /// <param name="locomotiveTransform"></param>
        /// <param name="wagons"></param>
        /// <param name="locomotiveRearJointTransform"></param>
        public static void CalculateWagonsPositions(Transform locomotiveTransform, List<IRailwayVehicle> wagons, Transform locomotiveRearJointTransform)
        {
            if (wagons == null)
            {
                Debug.LogWarning("Wagons list cannot be null");
                return;
            }

            if (locomotiveRearJointTransform == null)
            {
                Debug.LogWarning("Locomotive rear coupler cannot be null");
                return;
            }

            float lastWagonJointDistance = 0f;
            float totalDistance = 0f;

            for (int index = 0; index < wagons.Count; index++)
            {
                GameObject wagonInstance = wagons[index].GetGameObject;

                wagonInstance.SetActive(false); //Deactivate wagon before moving to avoid physics engine miscalculations

                wagonInstance.transform.position = locomotiveTransform.position;
                wagonInstance.transform.rotation = locomotiveTransform.rotation;

                if (index == 0)
                {
                    lastWagonJointDistance = Mathf.Abs(locomotiveRearJointTransform.localPosition.z);
                    totalDistance -= ((wagons[index].CouplersDistance * 0.5f) + lastWagonJointDistance);
                }
                else
                    totalDistance -= ((wagons[index].CouplersDistance * 0.5f) + (lastWagonJointDistance * 0.5f));

                wagonInstance.transform.position += (wagonInstance.transform.forward * totalDistance);

                lastWagonJointDistance = wagons[index].CouplersDistance;

                wagonInstance.SetActive(true); //Renable wagon after moving
            }
        }
    }
}
