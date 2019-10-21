// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using UnityEngine;

public class Lightning : FingerSurface
{
    const int maxRandomSamples = 20;

    [SerializeField]
    private MeshSampler sampler;
    [SerializeField]
    private SurfaceArc[] surfaceArcs;
    [SerializeField]
    private FingerArc[] fingerArcs;
    [SerializeField]
    private Transform[] fingerArcSources;
    [SerializeField]
    private float randomSurfaceArcChange = 0.05f;
    [SerializeField]
    private float randomFingerArcChange = 0.05f;
    [SerializeField]
    private float fingerEngageDistance = 0.15f;
    [SerializeField]
    private float fingerRandomPosRadius = 0.15f;
    [SerializeField]
    private float fingerDisengageDistance = 1f;
    [SerializeField]
    private Gradient glowGradient;
    [SerializeField]
    private Renderer coreRenderer;
    [SerializeField]
    private float glowSpeed = 0.5f;
    [SerializeField]
    private AnimationCurve intensityCurve;
    [SerializeField]
    private AnimationCurve flickerCurve;
    [SerializeField]
    private AnimationCurve buzzAudioPitch;
    [SerializeField]
    private AudioSource buzzAudio;

    private int numFingersEngaged = 0;
    private float glow = 0;


    public override void Initialize(Vector3 surfacePosition)
    {
        base.Initialize(surfacePosition);

        sampler.SampleMesh(true);

        for (int i = 0; i < surfaceArcs.Length; i++)
        {
            MeshSample point1 = sampler.Samples[Random.Range(0, sampler.Samples.Length)];
            MeshSample point2 = sampler.Samples[Random.Range(0, sampler.Samples.Length)];
            surfaceArcs[i].SetArc(point1, point2, Vector3.zero, SurfaceRadius);
        }

        buzzAudio.pitch = 0;
    }


    private void Update()
    {
        if (!Initialized)
            return;

        UpdateFingers();
        UpdateArcs();
        UpdateGlow();
        UpdateAudio();
    }

    private void UpdateArcs()
    {
        FingerArc.UpdateArcLights();

        int maxActiveArcs = surfaceArcs.Length - numFingersEngaged;
        for (int i = 0; i < surfaceArcs.Length; i++)
            surfaceArcs[i].gameObject.SetActive(i < maxActiveArcs);

        if (Random.value < randomSurfaceArcChange)
        {
            SurfaceArc arc = surfaceArcs[Random.Range(0, surfaceArcs.Length)];
            if (Random.value > 0.5f)
            {
                arc.gameObject.SetActive(true);
                MeshSample point1 = sampler.Samples[Random.Range(0, sampler.Samples.Length)];
                MeshSample point2 = sampler.Samples[Random.Range(0, sampler.Samples.Length)];
                arc.SetArc(point1, point2, transform.position, SurfaceRadius);
            }
            else
            {
                arc.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateFingers()
    {
        numFingersEngaged = 0;
        for (int i = 0; i < fingers.Length; i++)
        {
            Transform finger = fingers[i];
            FingerArc arc = fingerArcs[i];

            if (!finger.gameObject.activeSelf)
            {
                arc.gameObject.SetActive(false);
                continue;
            }

            if (arc.gameObject.activeSelf)
            {
                // See if we're too far away
                MeshSample sample = sampler.Samples[arc.Point1SampleIndex];
                if (Vector3.Distance(sample.Point, finger.position) > fingerDisengageDistance)
                {   // If we are, disable the arc and move on
                    arc.gameObject.SetActive(false);
                    continue;
                }
                else
                {   // If we aren't, see if it's time to zap to a different position
                    if (Random.value < randomFingerArcChange)
                    {   // Get the closest point on the sphere
                        MeshSample point1 = sampler.ClosestSample(finger.position);
                        // Then get a random sample somewhere nearby
                        point1 = sampler.RandomSample(point1.Point, fingerRandomPosRadius);
                        arc.SetArc(point1, fingerArcSources[i]);
                    }
                    numFingersEngaged++;
                }
            }
            else
            {   // See if we're close enough to any samples to start
                // Get the closest point on the sphere
                MeshSample point1 = sampler.ClosestSample(finger.position);
                if (Vector3.Distance(point1.Point, finger.position) < fingerEngageDistance)
                {   // Then get a random sample somewhere nearby
                    point1 = sampler.RandomSample(point1.Point, fingerRandomPosRadius);
                    arc.gameObject.SetActive(true);
                    arc.SetArc(point1, fingerArcSources[i]);
                    numFingersEngaged++;
                }
            }
        }
    }

    private void UpdateGlow()
    {
        float targetGlow = (float)numFingersEngaged / 10;
        glow = Mathf.Lerp(glow, targetGlow, Time.deltaTime * glowSpeed);
        float randomValue = flickerCurve.Evaluate(targetGlow) * Random.Range(-1f, 1f);
        float intensity = intensityCurve.Evaluate(targetGlow);
        coreRenderer.material.SetColor("_EmissiveColor", glowGradient.Evaluate(glow + randomValue) * intensity);
    }

    private void UpdateAudio()
    {
        buzzAudio.pitch = buzzAudioPitch.Evaluate(glow);
    }

    private void OnDrawGizmos()
    {
        for (int i = 0; i < fingers.Length; i++)
        {
            Transform finger = fingers[i];
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(finger.position, 0.01f);
            Gizmos.color = Color.Lerp(Color.green, Color.clear, 0.5f);
            Gizmos.DrawWireSphere(finger.position, fingerEngageDistance);
            if (fingerArcs[i].gameObject.activeSelf)
            {
                Gizmos.color = Color.Lerp(Color.red, Color.clear, 0.5f);
                Gizmos.DrawWireSphere(finger.position, fingerDisengageDistance);
            }
        }
    }
}
