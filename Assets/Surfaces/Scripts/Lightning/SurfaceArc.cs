// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using UnityEngine;

public class SurfaceArc : Arc
{
    [SerializeField]
    private AnimationCurve gravityCurve;
    [SerializeField]
    private AnimationCurve gravityResetCurve;
    [SerializeField]
    private float arcEscapeStrength = 5f;

    public void SetArc(MeshSample point1, MeshSample point2, Vector3 gravityOrigin, float surfaceRadius)
    {
        this.point1.position = point1.Point;
        this.point2.position = point2.Point;
        this.point1.forward = point1.Normal;
        this.point2.forward = point2.Normal;
        this.gravityOrigin = gravityOrigin;
        this.surfaceRadius = surfaceRadius;

        timeLastSet = Time.time;
        randomWidth = Random.Range(minWidth, maxWidth);
    }

    private Vector3 gravityOrigin;
    private float surfaceRadius;

    private void Update()
    {
        UpdateWidth();
        UpdateMaterial();

        for (int i = 0; i < lineRenderer.positionCount; i++)
        {
            float normalizedPos = (float)i / (lineRenderer.positionCount - 1);
            float gravity = gravityCurve.Evaluate(normalizedPos);
            float gravityResetValue = gravityResetCurve.Evaluate(Time.time - timeLastSet);

            Vector3 pos = Vector3.Lerp(point1.position, point2.position, normalizedPos);
            // Move pos to surface radius at a minimum
            float distToCenter = Vector3.Distance(pos, gravityOrigin);
            Vector3 dir = (pos - gravityOrigin).normalized;
            if (distToCenter < surfaceRadius)
            {
                pos = dir * surfaceRadius;
            }

            Vector3 force = dir * (arcEscapeStrength + (0.01f * (float)noise.Evaluate(pos.x + i, Time.time * 50)));
            force += GetJitter(pos);
            pos += force * gravity * gravityResetValue;

            lineRenderer.SetPosition(i, pos);
        }
    }
}
