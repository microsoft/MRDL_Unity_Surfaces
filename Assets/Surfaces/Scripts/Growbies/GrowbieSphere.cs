// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using Microsoft.MRDL;
using UnityEngine;

public class GrowbieSphere : FingerSurface
{
    [SerializeField]
    private GrowbieSeasons[] growbies;
    [SerializeField]
    private float fingerInfluence = 0.15f;
    [SerializeField]
    private float seasonRevertSpeed = 2f;
    [SerializeField]
    private float seasonChangeSpeed = 1.5f;
    [SerializeField]
    private float[] seasonTargets;
    [SerializeField]
    private Gradient mainLightColor;
    [SerializeField]
    private float overallSeason;
    [SerializeField]
    private GrowbieParticles growbieParticles;

    [Header("Audio")]
    [SerializeField]
    private AudioSource winterAudio;
    [SerializeField]
    private AnimationCurve winterAudioCurve;
    [SerializeField]
    private AudioSource summerAudio;
    [SerializeField]
    private AnimationCurve summerAudioCurve;
    [SerializeField]
    private float masterVolume = 0.25f;

    protected override void Awake()
    {
        base.Awake();

        winterAudio.volume = 0;
        summerAudio.volume = 0;
    }

    public override void Initialize(Vector3 surfacePosition)
    {
        base.Initialize(surfacePosition);

        // Reset season influence
        foreach (GrowbieSeasons g in growbies)
            g.Season = 0.5f;

        seasonTargets = new float[growbies.Length];
        for (int i = 0; i < seasonTargets.Length; i++)
            seasonTargets[i] = 0.5f;
    }

    private void Update()
    {   
        if (!Initialized)
            return;

        // Slowly revert to neutral
        overallSeason = 0;
        for (int i = 0; i < seasonTargets.Length; i++)
        {
            seasonTargets[i] = Mathf.Lerp(seasonTargets[i], 0.5f, Time.deltaTime * seasonRevertSpeed);
            overallSeason += seasonTargets[i];
        }
        overallSeason /= seasonTargets.Length;
        //mainLight.color = mainLightColor.Evaluate(overallSeason);

        // Set season based on proximity to fingers
        foreach (Transform finger in fingers)
        {
            if (!finger.gameObject.activeSelf)
                continue;

            for (int i = 0; i < growbies.Length; i++)
            {
                GrowbieSeasons g = growbies[i];

                float dist = Vector3.Distance(finger.position, g.InfluenceCenter);
                if (dist > g.InfluenceRange)
                    continue;

                float seasonInfluence = finger.CompareTag("Winter") ? fingerInfluence : -fingerInfluence;
                seasonTargets[i] = Mathf.Clamp01(seasonTargets[i] + seasonInfluence);
            }
        }

        for (int i = 0; i < growbies.Length; i++)
        {
            GrowbieSeasons g = growbies[i];
            g.Season = Mathf.Lerp(g.Season, seasonTargets[i], Time.deltaTime * seasonChangeSpeed);
        }

        growbieParticles.SeasonBlend = overallSeason;

        winterAudio.volume = winterAudioCurve.Evaluate(overallSeason) * masterVolume;
        summerAudio.volume = summerAudioCurve.Evaluate(overallSeason) * masterVolume;
    }

    private void OnDrawGizmos()
    {
        foreach (Transform finger in fingers)
        {
            Gizmos.color = finger.CompareTag("Winter") ? Color.blue : Color.yellow;
            Gizmos.DrawSphere(finger.position, 0.035f);
        }
    }
}
