// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using System;
using UnityEngine;

public class ChunkImpacts : MonoBehaviour
{
    public enum ImpactType
    {
        Finger,
        OtherBlock,
    }

    public const float minCollisionVelocity = 0.0175f;
    public const float minFingerImpactIntensity = 0.5f;
    public const float minChunkImpactIntensity = 0.075f;

    public float ChunkImpactIntensity { get; set; }
    public float FingerImpactIntensity { get; set; }

    public Action<ChunkImpacts, Vector3> OnImpact { get; set; }
    public AudioSource ImpactAudio { get { return impactAudio; } }
    public Collider LastOtherCollider { get; private set; }
    public float LastImpactIntensity { get; private set; }
    public ImpactType LastImpactType { get; private set; }
    public float Radius { get; set; }

    [SerializeField]
    private AudioSource impactAudio;

    public void OnCollisionEnter(Collision collision)
    {
        ContactPoint contactPoint = collision.contacts[0];
        if (contactPoint.otherCollider.CompareTag("Player"))
        {
            LastImpactType = ImpactType.Finger;
            LastOtherCollider = contactPoint.otherCollider;
            LastImpactIntensity = collision.relativeVelocity.magnitude;
            // Finger impacts get set to 1 regardless of actual intensity
            FingerImpactIntensity += Mathf.Max(LastImpactIntensity, minFingerImpactIntensity);
            OnImpact?.Invoke(this, contactPoint.point);
        }
        else if (contactPoint.otherCollider.CompareTag("VolumeBlock"))
        {
            float impactVelocity = collision.relativeVelocity.magnitude;
            if (impactVelocity < minCollisionVelocity)
                return;

            impactVelocity = Mathf.Max(impactVelocity, minChunkImpactIntensity);
            LastImpactType = ImpactType.OtherBlock;
            LastOtherCollider = contactPoint.otherCollider;
            LastImpactIntensity = impactVelocity;
            ChunkImpactIntensity += impactVelocity;
            OnImpact?.Invoke(this, contactPoint.point);
        }
    }
}
