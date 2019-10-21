// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using UnityEngine;
public class GoopSlime : MonoBehaviour
{
    private static float[] droop;
    private static float[] normalizedTimes;
    private static float[] follow;

    [SerializeField]
    private LineRenderer lineRenderer;
    [SerializeField]
    private float maxStretch = 0.5f;
    [SerializeField]
    private float minWidth = 0.1f;
    [SerializeField]
    private float maxWidth = 0.2f;
    [SerializeField]
    private AnimationCurve droopSpeedCurve;
    [SerializeField]
    private AnimationCurve droopCurve;
    [SerializeField]
    private AnimationCurve widthCurve;
    [SerializeField]
    private AnimationCurve followCurve;

    [SerializeField]
    private MeshSample origin;
    [SerializeField]
    private Transform target;

    [Header("Physics")]
    [SerializeField]
    private float inertia = 25f;
    [SerializeField]
    private float targetSeekStrength = 0.25f;
    [SerializeField]
    private float inertiaStrength = 3f;
    [SerializeField]
    private float inheritedStrength = 6f;
    [SerializeField]
    private float gravityStrength = 10f;

    private float timeStarted;
    private float lastTotalDist;
    private Vector3[] inertialVelocities = new Vector3[0];
    private Vector3[] positions = new Vector3[0];
    private Vector3[] targetPositions = new Vector3[0];
    private Vector3[] prevPositions = new Vector3[0];

    public void SetGoop(MeshSample origin, Transform target)
    {
        this.target = target;
        this.origin = origin;

        if (droop == null)
        {
            droop = new float[lineRenderer.positionCount];
            normalizedTimes = new float[lineRenderer.positionCount];
            follow = new float[lineRenderer.positionCount];

            for (int i = 0; i < lineRenderer.positionCount; i++)
            {
                normalizedTimes[i] = (float)i / (lineRenderer.positionCount - 1);
                droop[i] = droopCurve.Evaluate(normalizedTimes[i]);
                follow[i] = followCurve.Evaluate(normalizedTimes[i]);
            }
        }

        if (positions.Length != lineRenderer.positionCount)
        {
            positions = new Vector3[lineRenderer.positionCount];
            targetPositions = new Vector3[lineRenderer.positionCount];
            prevPositions = new Vector3[lineRenderer.positionCount];
            inertialVelocities = new Vector3[lineRenderer.positionCount];
        }

        for (int i = 0; i < lineRenderer.positionCount; i++)
        {
            lineRenderer.SetPosition(i, origin.Point);
            positions[i] = transform.TransformPoint(origin.Point);
            targetPositions[i] = transform.TransformPoint(origin.Point);
            prevPositions[i] = transform.TransformPoint(origin.Point);
            inertialVelocities[i] = Vector3.zero;
        }

        gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        timeStarted = Time.time;
        lineRenderer.widthCurve = widthCurve;
        lineRenderer.useWorldSpace = true;
    }

    private void Update()
    {
        for (int i = 0; i < positions.Length; i++)
        {
            inertialVelocities[i] = Vector3.Lerp(inertialVelocities[i], positions[i] - prevPositions[i], Time.deltaTime * inertia);
            prevPositions[i] = positions[i];
        }

        float timeSinceStarted = Time.time - timeStarted;
        float totalDist = 0;
        Vector3 originPos = transform.TransformPoint(origin.Point);
        Vector3 lastPos = originPos;
        float droopSpeed = droopSpeedCurve.Evaluate(lastTotalDist);

        for (int i = 0; i < positions.Length; i++)
        {
            // Find our target position - drooping goop
            float normalizedTime = normalizedTimes[i];
            float droopAmount = timeSinceStarted * droopSpeed * droop[i];
            targetPositions[i] = Vector3.Lerp(originPos, target.position, normalizedTime) + Vector3.down * droopAmount;

            if (i == 0 || i == positions.Length - 1)
            {   // First and last points are pinned
                positions[i] = targetPositions[i];
                continue;
            }

            Vector3 position = prevPositions[i];
            Vector3 targetVelocity = targetPositions[i] - position;
            Vector3 inertialVelocity = inertialVelocities[i];
            Vector3 gravity = Vector3.down * droop[i];

            // We've already skipped the first and last points so we're safe to move to either side here
            Vector3 inheritedVelocity = Vector3.zero;
            inheritedVelocity += inertialVelocities[i - 1];
            inheritedVelocity += inertialVelocities[i + 1];

            position += targetVelocity * targetSeekStrength * Time.deltaTime;
            position += inertialVelocity * inertiaStrength * Time.deltaTime;
            position += inheritedVelocity * inheritedStrength * Time.deltaTime;
            position += gravity * gravityStrength * Time.deltaTime;

            positions[i] = position;

            totalDist += Vector3.Distance(lastPos, position);
            lastPos = position;
        }

        float width = Mathf.Lerp(maxWidth, minWidth, (Time.time - timeStarted) * droopSpeed);
        width *= 1f - Mathf.Clamp01(totalDist / maxStretch);
        lineRenderer.widthMultiplier = width;

        // Lerp positions from front to back to make sure they don't get too spread out
        float idealDist = totalDist / positions.Length;
        for (int i = 1; i < positions.Length - 1; i++)
        {
            float dist = Vector3.Distance(positions[i], positions[i + 1]);
            if (dist > idealDist)
                positions[i] = Vector3.MoveTowards(positions[i], positions[i + 1], dist - idealDist);
        }

        if (totalDist > maxStretch)
            gameObject.SetActive(false);

        lastTotalDist = totalDist;

        for (int i = 0; i < positions.Length; i++)
            lineRenderer.SetPosition(i, positions[i]);
    }

    private void OnDrawGizmos()
    {
        for (int i = 0; i < positions.Length; i++)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(positions[i], 0.01f);
            Gizmos.DrawLine(positions[i], positions[i] + inertialVelocities[i] * 10);
        }
    }
}
