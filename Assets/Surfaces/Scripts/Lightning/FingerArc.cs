// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using UnityEngine;

public class FingerArc : Arc
{
    private const int MaxIterations = 150;
    private const int minFramesBetweenUpdates = 3;
    private static int currentFrames = 0;
    private static List<HoverLight> arcLights = new List<HoverLight>();

    [SerializeField]
    private AudioSource fingerAudio;

    public static void UpdateArcLights()
    {
        if (arcLights.Count == 0)
            return;

        currentFrames++;
        if (currentFrames < minFramesBetweenUpdates)
            return;

        currentFrames = 0;

        for (int i = 0; i < arcLights.Count; i++)
            arcLights[i].gameObject.SetActive(false);

        int numActiveLights = 0;
        int numIterations = 0;
        while (numActiveLights < HoverLight.HoverLightCount)
        {
            HoverLight randomLight = arcLights[Random.Range(0, arcLights.Count)];
            if (randomLight.transform.parent.gameObject.activeSelf)
            {
                randomLight.gameObject.SetActive(true);
                numActiveLights++;
            }
            numIterations++;
            if (numIterations > MaxIterations)
                break;
        }
    }

    public static void ClearArcLights()
    {
        arcLights.Clear();
    }

    [SerializeField]
    private AnimationCurve jitterCurve;
    [SerializeField]
    private HoverLight point1Light;

    public int Point1SampleIndex { get; set; }
    private Transform fingerTarget;

    protected override void Awake()
    {
        base.Awake();
        arcLights.Add(point1Light);
    }

    private void OnDestroy()
    {
        arcLights.Remove(point1Light);
    }

    public void SetArc(MeshSample point1, Transform fingerTarget)
    {
        Point1SampleIndex = point1.Index;

        this.point1.position = point1.Point;
        this.point1.forward = point1.Normal;

        this.fingerTarget = fingerTarget;
        this.point2.position = fingerTarget.position;
        this.point2.forward = fingerTarget.forward;

        randomWidth = Random.Range(minWidth, maxWidth);

        fingerAudio.pitch = Random.Range(0.7f, 1.3f);

        timeLastSet = Time.time;
    }

    private void Update()
    {
        UpdateWidth();
        UpdateMaterial();

        point1Light.transform.localPosition = point1.localPosition;

        for (int i = 0; i < lineRenderer.positionCount; i++)
        {
            float normalizedPos = (float)i / lineRenderer.positionCount;

            this.point2.position = fingerTarget.position;
            this.point2.forward = fingerTarget.forward;

            Vector3 pos = Vector3.Lerp(point1.localPosition, point2.localPosition, normalizedPos);
            pos += (GetJitter(pos) * jitterCurve.Evaluate(normalizedPos));

            lineRenderer.SetPosition(i, pos);
        }
    }
}
