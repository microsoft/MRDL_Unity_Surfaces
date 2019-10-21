// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using System.Runtime.InteropServices;
using UnityEngine;

public class Lava : FingerSurface
{
    private struct Fingertip
    {
        public float Heat;
        public MeshRenderer Renderer;
        public MaterialPropertyBlock Block;
        public ParticleSystem Particles;
        public ParticleSystem.EmissionModule Emission;
    }

    [SerializeField]
    private LavaChunk[] chunks;
    [SerializeField]
    private float initialImpulseForce = 0.01f;
    [SerializeField]
    private float fingerForceDist = 0.075f;
    [SerializeField]
    private float fingerPushForce = 1f;
    [SerializeField]
    private SphereCollider innerCollider;

    [Header("Heat")]
    [SerializeField]
    private float heatIncreaseSpeed = 0.5f;
    [SerializeField]
    private float heatDecreaseSpeed = 0.1f;
    [SerializeField]
    private Gradient heatColor;
    [SerializeField]
    private Gradient fingertipHeatColor;
    [SerializeField]
    private Color baseColor;

    [Header("Audio")]
    [SerializeField]
    private float minTimeBetweenImpacts = 0.2f;
    [SerializeField]
    private AudioClip[] impactClips;
    [SerializeField]
    private AnimationCurve volumeCurve;

    [Header("Bubbles")]
    [SerializeField]
    private Renderer[] lavaBubbles;
    [SerializeField]
    private float bubbleDistance = 0.4404f;
    [SerializeField]
    private float spawnOdds = 0.95f;
    [SerializeField]
    private float bubbleCheckRadius = 0.01f;
    [SerializeField]
    private LayerMask chunkLayer;

    [Header("Lava")]
    [SerializeField]
    private Material lavaMat;
    [SerializeField]
    private Vector2 scrollSpeed;
    [SerializeField]
    private GameObject[] impactParticles;

    private Fingertip[] fingertips;
    private float[] chunkHeat;
    private MaterialPropertyBlock[] chunkPropertyBocks;
    private Vector2 lavaScrollPos;
    private float lastImpactTime;

    public override void Initialize(Vector3 surfacePosition)
    {
        base.Initialize(surfacePosition);

        // Set up our bubbles
        for (int i = 0; i < lavaBubbles.Length; i++)
        {
            Vector3 randomPos = Random.onUnitSphere * bubbleDistance;
            lavaBubbles[i].transform.up = randomPos.normalized;
            lavaBubbles[i].transform.Rotate(-90f, 0f, 0f, Space.Self);
            lavaBubbles[i].transform.localPosition = randomPos;
            lavaBubbles[i].gameObject.SetActive(false);
        }

        chunkHeat = new float[chunks.Length];
        chunkPropertyBocks = new MaterialPropertyBlock[chunks.Length];

        for (int i = 0; i < chunks.Length; i++)
        {
            LavaChunk chunk = chunks[i];
            chunk.RigidBody.AddForce(UnityEngine.Random.insideUnitSphere * initialImpulseForce, ForceMode.Force);
            chunkPropertyBocks[i] = new MaterialPropertyBlock();
            chunk.OnCollision += OnCollision;
        }

        fingertips = new Fingertip[fingers.Length];
        for (int i = 0; i < fingertips.Length; i++)
        {
            Fingertip ft = new Fingertip();
            ft.Heat = 0;
            ft.Block = new MaterialPropertyBlock();
            //ft.Rigidbody = fingers[i].GetComponentInChildren<Rigidbody>();
            ft.Renderer = fingers[i].GetComponentInChildren<MeshRenderer>();
            ft.Particles = fingers[i].GetComponentInChildren<ParticleSystem>();
            ft.Emission = ft.Particles.emission;
            ft.Emission.enabled = false;
            fingertips[i] = ft;
        }
    }

    private void OnCollision(Vector3 point, float intensity)
    {
        if (Time.time < lastImpactTime + minTimeBetweenImpacts)
            return;

        lastImpactTime = Time.time;
        AudioSource.PlayClipAtPoint(impactClips[UnityEngine.Random.Range(0, impactClips.Length)], point, volumeCurve.Evaluate(intensity));

        for (int i = 0; i < impactParticles.Length; i++)
        {
            if (!impactParticles[i].activeSelf)
            {
                impactParticles[i].transform.position = point;
                impactParticles[i].transform.forward = SurfacePosition - point;
                impactParticles[i].gameObject.SetActive(true);
                break;
            }
        }
    }

    protected override void FixedUpdate()
    {
        if (!Initialized)
            return;

        base.FixedUpdate();

        for (int i = 0; i < fingers.Length; i++)
        {
            Transform finger = fingers[i];
            Fingertip fingertip = fingertips[i];

            if (!finger.gameObject.activeSelf)
                continue;

            // If the finger is below the surface, ignore it
            float distToSurface = Vector3.Distance(finger.position, transform.position);
            if (distToSurface < SurfaceRadius)
                continue;

            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                LavaChunk chunk = chunks[chunkIndex];
                Vector3 fingerPos = finger.position;
                Vector3 closestPos = chunk.Collider.ClosestPoint(fingerPos);
                float dist = Vector3.Distance(fingerPos, closestPos);
                if (dist > fingerForceDist)
                    continue;

                // Add heat and force to the chunk
                Vector3 force = (closestPos - fingerPos).normalized * fingerPushForce;
                chunk.RigidBody.AddForceAtPosition(force, fingerPos, ForceMode.Force);
                chunkHeat[chunkIndex] = Mathf.Lerp(chunkHeat[chunkIndex], 1f, Time.fixedDeltaTime * heatIncreaseSpeed);
                chunkHeat[i] = Mathf.Lerp(chunkHeat[i], 1f, Time.fixedDeltaTime * heatIncreaseSpeed * chunks[i].SubmergedAmount);
                // Add heat to the fingertip
                fingertip.Heat = Mathf.Lerp(fingertip.Heat, 1f, Time.fixedDeltaTime * heatIncreaseSpeed);
            }

            fingertips[i] = fingertip;
        }
    }

    private void Update()
    {
        if (!Initialized)
            return;

        for (int i = 0; i < chunks.Length; i++)
        {
            chunkHeat[i] = Mathf.Lerp(chunkHeat[i], 0f, Time.deltaTime * heatDecreaseSpeed);

            LavaChunk chunk = chunks[i];
            chunk.Heat = chunkHeat[i];
            Vector4 hdrColor = Vector4.zero;
            hdrColor.x = baseColor.r;
            hdrColor.y = baseColor.g;
            hdrColor.z = baseColor.b;

            Color color = heatColor.Evaluate(chunkHeat[i]);
            hdrColor.x += color.r * 6;
            hdrColor.y += color.g * 6;
            hdrColor.z += color.b * 6;

            chunkPropertyBocks[i].SetColor("_EmissiveColor", hdrColor);
            chunk.Renderer.SetPropertyBlock(chunkPropertyBocks[i]);
        }

        for (int i = 0; i < fingertips.Length; i++)
        {
            Fingertip fingertip = fingertips[i];
            fingertip.Emission.enabled = (fingertip.Heat > 0.25f);
            fingertip.Heat = Mathf.Lerp(fingertip.Heat, 0f, Time.deltaTime * heatDecreaseSpeed);
            fingertips[i] = fingertip;
        }

        for (int i = 0; i < lavaBubbles.Length; i++)
        {
            if (lavaBubbles[i].gameObject.activeSelf)
            {
                if (!lavaBubbles[i].enabled)
                {
                    lavaBubbles[i].gameObject.SetActive(false);
                }
            }
            else
            {
                if (Random.value > spawnOdds)
                {
                    if (!Physics.CheckSphere(lavaBubbles[i].transform.position, bubbleCheckRadius, chunkLayer))
                    {
                        lavaBubbles[i].gameObject.SetActive(true);
                    }
                }
            }
        }

        lavaScrollPos += scrollSpeed * Time.deltaTime;
        lavaMat.SetTextureOffset("_MainTex", lavaScrollPos);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        foreach (Transform finger in fingers)
            Gizmos.DrawWireSphere(finger.position, fingerForceDist);
    }
}
