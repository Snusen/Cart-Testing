﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NormalProbe : MonoBehaviour
{

    public bool drawGizmo;

    public float probeDist;
    //This float will adjust forward prediction power based on velocity.
    public float predictionFactor;

    public LayerMask layerMask;

    [Range(0, 3)]
    public float markerSize;

    Vector3 helperPosition;

    public Vector3 normal;
    public Vector3 smoothedNormal;
    public Vector3 point;

    public Vector3 probedForward;
    public Vector3 probedForwardAdj;

    private void OnDrawGizmos()
    {
        if (drawGizmo == true)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, -transform.up, out hit, probeDist, layerMask))
            {

                normal = hit.normal;
                smoothedNormal = NormalSmoother.SmoothedNormal(hit);
                point = hit.point;

                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, hit.point);
                Gizmos.DrawSphere(hit.point, markerSize);

                helperPosition = transform.position + -transform.up * hit.distance / 2;

                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(helperPosition, markerSize);

            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, transform.position + -transform.up * probeDist);
            }
        }

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -transform.up, out hit, probeDist, layerMask))
        {
            normal = hit.normal;
            smoothedNormal = NormalSmoother.SmoothedNormal(hit);
            point = hit.point;
            probedForward = Quaternion.AngleAxis((90), transform.right) * smoothedNormal;
            probedForwardAdj = Quaternion.AngleAxis((90 - predictionFactor), transform.right) * smoothedNormal;
            Debug.DrawRay(helperPosition, probedForward * 5.0f, Color.blue, 2.0f);
            Debug.DrawRay(helperPosition, probedForwardAdj * 5.0f, Color.cyan, 2.0f);

        }
    }
}