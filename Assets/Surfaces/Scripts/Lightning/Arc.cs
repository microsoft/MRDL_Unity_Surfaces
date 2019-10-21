// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using UnityEngine;

public class Arc : MonoBehaviour
{
    [SerializeField]
    protected LineRenderer lineRenderer;
    [SerializeField]
    protected ParticleSystem point1Particles;
    [SerializeField]
    protected ParticleSystem point2Particles;
    [SerializeField]
    protected Transform point1;
    [SerializeField]
    protected Transform point2;

    [Header("Noise")]
    [SerializeField]
    protected float jitter = 0.5f;
    [SerializeField]
    protected float maxLightIntensity = 10;
    [SerializeField]
    protected float noiseScale = 14f;
    [SerializeField]
    protected float noiseSpeed = 50;

    [Header("Width")]
    [SerializeField]
    protected float minWidth = 1f;
    [SerializeField]
    protected float maxWidth = 5f;
    [SerializeField]
    protected AnimationCurve widthCurve;

    protected Keyframe[] widthCurveKeys;
    protected Keyframe[] widthCurveAdjustedKeys;
    protected AnimationCurve widthCurveAdjusted = AnimationCurve.Linear(0, 0, 1, 0);

    protected float randomWidth;
    protected FastSimplexNoise noise = new FastSimplexNoise();
    protected float timeLastSet;

    [SerializeField]
    private Texture[] boltTextures;

    private MaterialPropertyBlock lightningMatBlock;

    protected virtual void Awake()
    {
        widthCurveKeys = widthCurve.keys;
        widthCurveAdjustedKeys = widthCurve.keys;
        lineRenderer.useWorldSpace = false;
        lineRenderer.widthCurve = widthCurve;

        lightningMatBlock = new MaterialPropertyBlock();
    }

    protected void UpdateMaterial()
    {
        lightningMatBlock.SetTexture("_MainTex", boltTextures[Random.Range(0, boltTextures.Length)]);
        lineRenderer.SetPropertyBlock(lightningMatBlock);
    }

    protected void UpdateWidth()
    {
        for (int i = 0; i < widthCurveKeys.Length; i++)
        {
            Keyframe key = widthCurveKeys[i];
            key.value *= (float)(noise.Evaluate(i, Time.time * 15));
            key.value *= randomWidth;
            key.value = Mathf.Max(0.0025f, key.value);
            widthCurveAdjustedKeys[i] = key;
        }

        widthCurveAdjusted.keys = widthCurveAdjustedKeys;
        lineRenderer.widthCurve = widthCurveAdjusted;
    }

    protected virtual void OnEnable()
    {
        for (int i = 0; i < lineRenderer.positionCount; i++)
        {
            float normalizedPosition = (float)i / lineRenderer.positionCount;
            lineRenderer.SetPosition(i, Vector3.Lerp(point1.localPosition, point2.localPosition, normalizedPosition));
        }
    }

    protected Vector3 GetJitter(Vector3 pos)
    {
        Vector3 jitterNoise = Vector3.zero;
        jitterNoise.x = (float)noise.Evaluate(pos.x * noiseScale, Time.time * noiseSpeed) * jitter;
        jitterNoise.y = (float)noise.Evaluate(pos.y * noiseScale, Time.time * noiseSpeed) * jitter;
        jitterNoise.z = (float)noise.Evaluate(pos.z * noiseScale, Time.time * noiseSpeed) * jitter;
        return jitterNoise;
    }
}