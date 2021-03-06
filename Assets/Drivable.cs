﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Drivable : MonoBehaviour
{

    //The raycast with important hit information related to the normal of the surface.
    RaycastHit hit;

    public bool seeFloor;
    public bool isMagnetised;
    public bool isLevelling = false;
    public bool isRolling = false;
    public bool isBoosting = false;

    [Space(5)]
    [Header("Required References")]

    public Cart playerCart;

    public GameObject forcePointFront;
    public GameObject forcePointBack;
    public GameObject gimbal;
    public GameObject dynamicUpPoint;
    public GameObject body;
    public GameObject normalMarker;
    [Space(5)]
    public NormalProbe forwardProbe;
    public NormalProbe backProbe;
    public NormalProbe leftProbe;
    public NormalProbe rightProbe;
    public List<NormalProbe> normalProbes = new List<NormalProbe>();
    [Space(5)]
    public Rigidbody parentRigidbody;
    public Rigidbody chassisRigidbody;
    public List<Transform> wheels = new List<Transform>();
    [Space(5)]
    public LayerMask layerMask;

    [Space(5)]
    public ConfigurableJoint bodyJoint;

    Vector3 forwardDirection;
    Vector3 reverseDirection;
    public Vector3 forwardForce;
    public Vector3 reverseForce;

    Vector3 lastNormal = Vector3.zero;
    Vector3 lastPoint = Vector3.zero;
    Vector3 attraction;
    Vector3 repulsion;
    Vector3 dynamicUpPos;
    Vector3 dynamicUpHeading;
    Vector3 hoverPos;

    [Space(5)]
    [Header("Physics Limits")]
    public float maxVelocity;
    public float maxYVelocity;
    public float yDamp;

    [Space(5)]
    [Header("Physics Parameters")]
    public float falseGravity;
    public float dynamicUpHeight;
    public float magnetiseDistance;
    public float orientationSpeed;
    public float magnetStrength;
    public float predictiveLift;
    public float floorCheckDist;
    public float levelSpeed;
    public float hoverHeight;
    //Distance to target height.
    float magnetSnapModifier;
    //Rotate the gimbal equal to the dive offset.
    float dive;
    float positionFactor;

    [Space(5)]
    [Header("Customisable Parameters")]
    public float spoilerDownforce;

    [Space(5)]
    [Header("Turning")]
    public float turnStrength;
    public float handbrakeStrength;
    float turnModifier;

    float currTurn;
    [Header("Movement")]
    public float maxThrust;
    public float acceleration;
    public float brakeStrength;

    public float currThrust;
    public float currBrake = 0.0f;

    void Start()
    {
        dynamicUpPos = transform.position + chassisRigidbody.transform.up * dynamicUpHeight;
        dynamicUpPoint.transform.position = dynamicUpPos;

        //Heading to dynamic up.
        Vector3 dynamicUp = dynamicUpPoint.transform.position - transform.position;

        //Lock the gimbal to our position.
        gimbal.transform.position = transform.position;

        // GameManager.ins.mainCamera.GetComponent<CameraMotionControl>().FreezeRoll(false);

    }

    Vector3 GetAverageNormal(Vector3 hitNormal)
    {
        Vector3 avgNormal = Vector3.zero;

        foreach (NormalProbe normalProbe in normalProbes)
        {
            avgNormal += normalProbe.smoothedNormal;
        }

        avgNormal += hitNormal;
        avgNormal = avgNormal / normalProbes.Count;
        return avgNormal;

    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying == false)
        {

            dynamicUpPos = transform.position + chassisRigidbody.transform.up * dynamicUpHeight;
            dynamicUpPoint.transform.position = dynamicUpPos;

            //Heading to dynamic up.
            Vector3 dynamicUp = dynamicUpPoint.transform.position - transform.position;

            //Lock the gimbal to our position.
            gimbal.transform.position = transform.position;
            // //Have the gimbal look at the up position.
            // gimbal.transform.LookAt(dynamicUpPoint.transform, transform.up);
        }

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(dynamicUpPoint.transform.position, 0.5f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(dynamicUpPos, 0.5f);

        // Gizmos.DrawRay(transform.position, accelDirection,);

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.position + -chassisRigidbody.transform.up * floorCheckDist, 0.5f);
        Gizmos.color = Color.green;
        //Gizmos.DrawSphere(transform.position + -chassisRigidbody.transform.up * magnetiseDistance, 0.5f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(transform.position, attraction);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, repulsion);

        Gizmos.color = Color.blue;
        Gizmos.DrawCube(hoverPos, new Vector3(1f, 1f, 1f));

    }

    //If we're upside down and in freefall.
    void LevelOut()
    {
        dynamicUpPos = transform.position + (Vector3.up * dynamicUpHeight);

        if (isLevelling == false)
        {
            StartCoroutine(LevelSelf());
        }
    }

    IEnumerator LevelSelf()
    {

        isLevelling = true;

        Debug.Log("Levelling");

        orientationSpeed = 0;
        //Set drag
        SetDrag(0.25f, 0.25f);

        //While we haven't remagnetised.
        while (isMagnetised == false)
        {
            orientationSpeed = Mathf.Lerp(orientationSpeed, 50.0f, Time.deltaTime / 100 * levelSpeed);

            yield return null;
        }

        orientationSpeed = 50.0f;
        isLevelling = false;
    }

    void SetDrag(float parentDrag, float chassisDrag)
    {
        parentRigidbody.drag = parentDrag;
        chassisRigidbody.drag = chassisDrag;
    }
    //If we are sticking to a surface.
    void UpdatePlayerUp()
    {
        //Update the dynamic up point. Lerp from current point to new point.
        dynamicUpPoint.transform.position = dynamicUpPos;

        //Have the gimbal look at the up position.
        gimbal.transform.LookAt(dynamicUpPoint.transform, transform.up);

        //Lerp the parent player object to match the gimbal rotation.
        transform.rotation = Quaternion.Lerp(transform.rotation, gimbal.transform.rotation, Time.deltaTime * orientationSpeed);

        //Then update our heading.
        dynamicUpHeading = dynamicUpPoint.transform.position - transform.position;
    }

    IEnumerator DoPop()
    {
        //For popping off of magnetised surfaces.
        Debug.Log("Pop");

        float md = magnetiseDistance;

        //de magnetise.
        magnetiseDistance = 0;
        //Add upwards force(relative to chassis)
        parentRigidbody.AddForceAtPosition(chassisRigidbody.transform.up * 3000, forcePointFront.transform.position);

        //While we are still in magnetise range.
        while (hit.distance <= md)
        {
            yield return null;
        }

        //Once we escape magnet range.
        magnetiseDistance = md;

    }

    IEnumerator DoHandbrake()
    {

        Debug.Log("Handbraking");

        //Initial boost.
        parentRigidbody.AddForce(chassisRigidbody.transform.forward * ((VelocityFilter.GetLocalVelocity(chassisRigidbody).z / 10 * Mathf.Abs(Input.GetAxisRaw("LeftStickHorizontal"))) * handbrakeStrength));

        while (Input.GetButton("RightBumper"))
        {
            //Modify turn based on velocity and handbrake strength.

            turnModifier = Mathf.Abs(VelocityFilter.GetLocalVelocity(chassisRigidbody).x) / 100 * handbrakeStrength;

            yield return null;
        }

        //Reset turn
        turnModifier = turnStrength;

    }

    IEnumerator DoBoost()
    {

        if (isBoosting == false)
        {

            isBoosting = true;

            Debug.Log("Boosting");

            float t = 0;
            float mt = maxThrust;
            float a = acceleration;
            float er = playerCart.energyRegen;

            //Modify turn based on velocity and handbrake strength.
            playerCart.energyRegen = 0;
            maxThrust *= 1.25f;
            acceleration *= 1.5f;
            while (Input.GetButton("ControllerA") && t < 4f && playerCart.currEnergy > 0)
            {

                playerCart.currEnergy -= 0.4f;
                t += Time.deltaTime;

                yield return null;
            }

            playerCart.energyRegen = er;
            maxThrust = mt;
            acceleration = a;

            isBoosting = false;
        }

    }

    IEnumerator StartRoll()
    {

        isRolling = true;

        float roll = 0;

        GameManager.ins.mainCamera.GetComponent<CameraMotionControl>().FreezeRoll(true);

        //chassisRigidbody.isKinematic = true;

        LeanTween.rotateAround(chassisRigidbody.gameObject, chassisRigidbody.transform.forward, 360, 1.0f).setEase(LeanTweenType.easeOutQuad).setOnComplete(FinishRoll);

        while (isRolling == true)
        {

            yield return null;
        }

        GameManager.ins.mainCamera.GetComponent<CameraMotionControl>().FreezeRoll(false);

    }

    void FinishRoll()
    {
        isRolling = false;
    }

    void WheelBehaviour()
    {
        //Turn wheels according to left stick input.
        wheels[0].localEulerAngles = new Vector3(wheels[0].localEulerAngles.x, currTurn, wheels[0].localEulerAngles.z);
        wheels[1].localEulerAngles = new Vector3(wheels[1].localEulerAngles.x, currTurn, wheels[1].localEulerAngles.z);

        foreach (Transform wheel in wheels)
        {
            wheel.RotateAround(wheel.position, wheel.transform.right, VelocityFilter.GetLocalVelocity(chassisRigidbody).z);
        }

    }

    void Update()
    {

        //Thrust
        //Thrust increases towards max power over time depending on the the right trigger state.
        currThrust = Mathf.Lerp(currThrust, maxThrust, Time.deltaTime / 10 * acceleration * Input.GetAxis("RightTrigger"));
        //Thrust decreases as the trigger is released.
        currThrust = Mathf.Lerp(currThrust, 0, Time.deltaTime * (1 - Input.GetAxis("RightTrigger")));

        //Brake increase towards brake strength as left trigger is pressed.
        currBrake = Mathf.Lerp(currBrake, brakeStrength, Time.deltaTime * Input.GetAxis("LeftTrigger"));
        //It also decreased as left trigger is released.
        currBrake = Mathf.Lerp(currBrake, 0, Time.deltaTime * (1 - Input.GetAxis("LeftTrigger")));

        //Turning

        currTurn = Input.GetAxis("LeftStickHorizontal") * turnModifier;
        WheelBehaviour();
        forcePointFront.transform.localEulerAngles = new Vector3(forcePointFront.transform.localEulerAngles.x, currTurn, forcePointFront.transform.localEulerAngles.z);
        forcePointBack.transform.localEulerAngles = new Vector3(forcePointBack.transform.localEulerAngles.x, currTurn * -1, forcePointBack.transform.localEulerAngles.z);

        //Calculate forward/backwards direction based on this.
        //Adjust the acceleration direction to use the predictive forward from our forward probe.
        forwardDirection = forwardProbe.probedForwardAdj;
        reverseDirection = backProbe.probedForwardAdj;

        Debug.Log(VelocityFilter.GetLocalVelocity(parentRigidbody));

    }

    void FixedUpdate()
    {

        Debug.DrawRay(transform.position, -chassisRigidbody.transform.up * magnetiseDistance, Color.black, 1.0f);

        UpdatePlayerUp();

        //chassisRigidbody.AddTorque(chassisRigidbody.transform.up * currTurn);

        if (Physics.Raycast(transform.position, -chassisRigidbody.transform.up, out hit, floorCheckDist, layerMask))
        {

            seeFloor = true;

            //Do this always when we are inside floor check distance.
            LevelOut();
            float orientationSpeedAdj = orientationSpeed * (1 / hit.distance);
            float magnetStrengthAdj = magnetStrength * Mathf.Pow(hit.distance, 3);

            Vector3 smoothNormal = NormalSmoother.SmoothedNormal(hit);

            Debug.DrawRay(hit.point, smoothNormal * 5.0f, Color.white, 2.0f);

            //Set pos to smooth normal.
            hoverPos = hit.point + smoothNormal * hoverHeight;

            // //Set pos to regular normal.
            // hoverPos = hit.point + hit.normal * hoverHeight;

            if (hit.distance > magnetiseDistance)
            {

                isMagnetised = false;

                if (isRolling == false)
                {
                    //Barrel Roll
                    if (Input.GetButtonDown("LeftBumper"))
                    {
                        Debug.Log("Press barrel roll button");
                        StartCoroutine(StartRoll());
                    }
                }

                //If we haven't reached magnetise distance.
                LevelOut();
                gimbal.transform.position = transform.position;

                parentRigidbody.AddForce(Vector3.down * 50);
                turnModifier = turnStrength;

            }

            else if (hit.distance < magnetiseDistance)
            {

                isMagnetised = true;
                SetDrag(3, 1);
                //If we are in magentise ditance.

                //Set to smooth normal.
                dynamicUpPos = hit.point + smoothNormal * dynamicUpHeight;

                // //Set to regular normal.
                // dynamicUpPos = hit.point + hit.normal * dynamicUpHeight;

                //Normal probe has a prediction factor that lifts the predicted normal.
                //When we are driving forwards, up slopes, we need to adjust this to reflect the change in angle.
                float angleSlack = Vector3.Angle(new Vector3(0, VelocityFilter.GetLocalVelocity(chassisRigidbody).y, 0), forwardDirection);
                //Account for LOOK AT rotational offset.
                angleSlack -= 90;
                Debug.Log("Angle slack is " + angleSlack);
                //Adjust the forward prediction based on the discrepency between current forward velocity and predictied forward.
                //The higher/lower this number means we are driving into or away from the surface. Very useful.
                forwardProbe.predictionFactor = (angleSlack / 100) * predictiveLift;

                //Snap our gimbal to our hover height.
                gimbal.transform.position = hoverPos;
                //Lerp the player position to the gimbal position.
                transform.position = Vector3.Lerp(transform.position, gimbal.transform.position, Time.deltaTime * magnetStrengthAdj);

                Vector3 normalHeading = hit.point - chassisRigidbody.transform.position;

                //Rotate the gimbal equal to the dive offset.
                dive = Vector3.Angle(forwardProbe.probedForward, chassisRigidbody.transform.forward);

                positionFactor = Vector3.Dot(chassisRigidbody.transform.up, hoverPos - transform.position);

                //Distance to target height.
                magnetSnapModifier = Vector3.Distance(transform.position, hoverPos);

                //Handbrake
                if (Input.GetButtonDown("RightBumper"))
                {

                    StartCoroutine(DoHandbrake());

                }
                //Boost
                if (Input.GetButtonDown("ControllerA"))
                {

                    StartCoroutine(DoBoost());

                }
                //Pop
                if (Input.GetButtonDown("ControllerX"))
                {

                    StartCoroutine(DoPop());

                }

                forwardForce = ((forwardDirection + chassisRigidbody.transform.forward) / 2) * currThrust;
                reverseForce = ((reverseDirection - chassisRigidbody.transform.forward) / 2) * currBrake;
                // Forward force.
                parentRigidbody.AddForceAtPosition(forwardForce, forcePointFront.transform.position);
                // Backward force.
                parentRigidbody.AddForceAtPosition(reverseForce, forcePointBack.transform.position);

                lastNormal = hit.normal;
                lastPoint = hit.point;

            }

        }
        //If we are outside floor check distance.
        else
        {

            seeFloor = false;
            isMagnetised = false;

            //Do this outside floor check distance.

            LevelOut();

            gimbal.transform.position = transform.position;
            //VelocityFilter.LockUpwards(parentRigidbody, yDamp);

            parentRigidbody.AddForceAtPosition((Vector3.down * falseGravity + parentRigidbody.velocity) / 2, forcePointFront.transform.position);
            turnModifier = turnStrength;

        }

        Debug.Log("Position factor is " + positionFactor);
        Debug.Log("Absolute PF is " + Mathf.Abs(positionFactor));
        Debug.Log("Distance from hover height is " + magnetSnapModifier);
        Debug.Log("Velocity magnitude is " + parentRigidbody.velocity.magnitude);
        Debug.Log("Normal up is " + hit.normal);
        Debug.Log("Dive angle is " + dive);
        Debug.Log("Current turn = " + currTurn);
        Debug.Log("Sideways velocity is " + Mathf.Abs(VelocityFilter.GetLocalVelocity(parentRigidbody).x));

        //Debug.DrawRay(forwardProbe.transform.position, forwardProbe.probedForward * 5, Color.white, 3.0f);
        Debug.DrawRay(transform.position, parentRigidbody.velocity / 20, Color.yellow, 3.0f);

    }

}