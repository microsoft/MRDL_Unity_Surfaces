// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Volume : HandSurface
{
    [SerializeField]
    private GameObject volumePrefab;
    [SerializeField]
    private GameObject centralParticles;

    [Header("Gen 0")]
    [SerializeField]
    private int gen0Core = 10;
    [SerializeField]
    private int gen0Surface = 5;
    [SerializeField]
    private float coreRadius = 0.1f;
    [SerializeField]
    private float gen0RadiusMin = 0.075f;
    [SerializeField]
    private float gen0RadiusMax = 0.1f;

    [Header("Gen 1")]
    [SerializeField]
    private int gen1Count = 25;
    [SerializeField]
    private float gen1RadiusMin = 0.025f;
    [SerializeField]
    private float gen1RadiusMax = 0.015f;

    [Header("Gen 2")]
    [SerializeField]
    private int gen2Count = 100;
    [SerializeField]
    private float gen2RadiusMin = 0.005f;
    [SerializeField]
    private float gen2RadiusMax = 0.008f;

    [Header("Gen 3")]
    [SerializeField]
    private int gen3Count = 200;
    [SerializeField]
    private float gen3RadiusMin = 0.005f;
    [SerializeField]
    private float gen3RadiusMax = 0.008f;

    [Header("Connections")]
    [SerializeField]
    private Gradient connectionColor = null;
    [SerializeField]
    private GameObject linePrefab = null;
    [SerializeField]
    private Gradient connectionGradientGen1 = null;
    [SerializeField]
    private Gradient connectionGradientGen2 = null;
    [SerializeField]
    private Gradient connectionGradientGen3 = null;

    [Header("Physics")]
    [SerializeField]
    private float driftIntensity = 0.01f;
    [SerializeField]
    private float driftScale = 25f;
    [SerializeField]
    private float driftTimeScale = 0.5f;
    [SerializeField]
    private float seekForce = 0.1f;

    [Header("Audio")]
    [SerializeField]
    private AudioClip[] impactClips = new AudioClip[0];
    [SerializeField]
    private AudioClip fingerImpactClip = null;
    [SerializeField]
    private float impactVolume = 0.65f;

    [Header("Impact Colors")]
    [SerializeField]
    private Gradient fingerImpactGradient = null;
    [SerializeField]
    private Gradient volumeImpactGradient = null;
    [SerializeField]
    private float chunkImpactFadeTime = 2f;
    [SerializeField]
    private float fingerImpactFadeTime = 0.25f;
    [SerializeField]
    private Color baseFingerColor;

    [SerializeField]
    private bool drawConnections = false;
    [SerializeField]
    private float minImpactParticleIntensity = 0.1f;

    [SerializeField]
    private ParticleSystem[] impactParticles;

    private Chunk[] gen0 = new Chunk[0];
    private Chunk[] gen1 = new Chunk[0];
    private Chunk[] gen2 = new Chunk[0];
    private Chunk[] gen3 = new Chunk[0];
    private Matrix4x4[] matrixes = new Matrix4x4[0];
    private LineRenderer[] lineRenderers = new LineRenderer[0];
    private FingerTip[] fingerTips;

    private MaterialPropertyBlock emptyBlock;
    private FastSimplexNoise noise = new FastSimplexNoise();
    private System.Random random = new System.Random();

    private float largestRadius;

    [Serializable]
    private struct FingerTip
    {
        public AudioSource ImpactAudio;
        public Renderer Renderer;
        public Color BaseColor;
        public float ImpactIntensity;
        public MaterialPropertyBlock Block;
    }

    private struct Chunk
    {
        public float Diameter { get { return Radius * 2; } }

        public int Parent;
        public int Generation;
        public float Radius;
        public Vector3 TargetPoint;
        public Vector3 TargetDir;
        public Rigidbody RigidBody;
        public ChunkImpacts Impacts;
        public Renderer Renderer;
        internal MaterialPropertyBlock Block;
    }

    public override void Initialize(Vector3 surfacePosition)
    {
        base.Initialize(surfacePosition);

        emptyBlock = new MaterialPropertyBlock();

        fingerTips = new FingerTip[fingerRigidBodies.Length];
        for (int i = 0; i < fingerRigidBodies.Length; i++)
        {
            FingerTip ft = new FingerTip();
            ft.ImpactAudio = fingerColliders[i].GetComponentInChildren<AudioSource>();
            ft.Renderer = fingers[i].GetComponentInChildren<Renderer>();
            ft.Block = new MaterialPropertyBlock();
            ft.ImpactIntensity = 0;
            ft.BaseColor = baseFingerColor;
            fingerTips[i] = ft;
        }

        largestRadius = 0;
        //await new WaitForBackgroundThread();

        // GEN 0
        List<Chunk> genList = new List<Chunk>();
        // Add one really big one in the middle
        Chunk core = new Chunk();
        core.Parent = -1;
        core.Generation = 0;
        core.Radius = coreRadius;
        core.TargetPoint = RandomInsideSphere(random) * SurfaceRadius * 0.1f;
        core.TargetDir = Vector3.up;

        genList.Add(core);

        // Then put a bunch around the core
        for (int i = 0; i < gen0Core; i++)
        {
            Chunk volume = new Chunk();
            volume.Parent = -1;
            volume.Generation = 0;
            volume.Radius = RandomRange(random, gen0RadiusMin, gen0RadiusMax);
            // Make sure the volume exists inside the sphere
            volume.TargetPoint = RandomInsideSphere(random) * (SurfaceRadius - volume.Radius * 1.25f);
            volume.TargetDir = volume.TargetPoint.normalized;

            genList.Add(volume);
        }

        // Then put a bunch around the edge
        for (int i = 0; i < gen0Surface; i++)
        {
            Chunk volume = new Chunk();
            volume.Parent = -1;
            volume.Generation = 0;
            volume.Radius = RandomRange(random, gen0RadiusMin, gen0RadiusMax);
            volume.TargetPoint = RandomOnSphere(random) * (SurfaceRadius - volume.Radius);
            volume.TargetDir = volume.TargetPoint.normalized;

            genList.Add(volume);
        }

        gen0 = genList.ToArray();
        genList.Clear();

        // GEN 1
        // Generate filler volumes around the big volumes
        // Pre-emptively cull volumes that go outside the sphere radius
        while (genList.Count < gen1Count)
        {
            for (int i = 0; i < gen0.Length; i++)
            {
                Chunk parent = gen0[i];

                Chunk volume = new Chunk();
                volume.Generation = 1;
                volume.Parent = i;
                volume.Radius = RandomRange(random, gen1RadiusMin, gen1RadiusMax);
                volume.TargetPoint = parent.TargetPoint + (RandomOnSphere(random) * (parent.Radius + volume.Radius));
                volume.TargetDir = parent.TargetPoint - volume.TargetPoint;
                // Now see if we're outside the sphere
                float outerEdgeDist = volume.TargetPoint.magnitude + volume.Diameter;
                // If we are, skip adding this volume
                if (outerEdgeDist > SurfaceRadius)
                    continue;

                genList.Add(volume);
            }
        }

        gen1 = genList.ToArray();
        genList.Clear();

        // GEN 2
        while (genList.Count < gen2Count)
        {
            for (int i = 0; i < gen1.Length; i++)
            {
                Chunk parent = gen1[i];

                Chunk volume = new Chunk();
                volume.Generation = 2;
                volume.Parent = i;
                volume.Radius = RandomRange(random, gen2RadiusMin, gen2RadiusMax);
                volume.TargetPoint = parent.TargetPoint + (RandomOnSphere(random)* (parent.Radius + volume.Radius));
                volume.TargetDir = parent.TargetPoint - volume.TargetPoint;
                // Now see if we're outside the sphere
                float outerEdgeDist = volume.TargetPoint.magnitude + volume.Diameter;
                // If we are, skip adding this volume
                if (outerEdgeDist > SurfaceRadius)
                    continue;

                genList.Add(volume);
            }
        }

        gen2 = genList.ToArray();
        genList.Clear();

        // GEN 3
        // This generation will live on the outside shell
        while (genList.Count < gen3Count)
        {
            for (int i = 0; i < gen2.Length; i++)
            {
                Chunk parent = gen2[i];

                Chunk volume = new Chunk();
                volume.Generation = 3;
                volume.Parent = i;
                volume.Radius = RandomRange(random, gen3RadiusMin, gen3RadiusMax);
                // Put on the outside shell
                volume.TargetPoint = parent.TargetPoint.normalized * (SurfaceRadius - volume.Radius);
                volume.TargetDir = parent.TargetPoint - volume.TargetPoint;

                genList.Add(volume);
            }
        }

        gen3 = genList.ToArray();
        genList.Clear();

        matrixes = new Matrix4x4[gen0.Length + gen1.Length + gen2.Length + gen3.Length];

        // Jitter volumes into place
        for (int i = 0; i < 5; i++)
        {
            JitterVolumes(gen0, null, 100, false);
            JitterVolumes(gen1, gen0, 25, false);
            JitterVolumes(gen2, gen1, 15, false);
            JitterVolumes(gen3, gen2, 100, true);
        }

        // Get back onto the main thread
        //await new WaitForEndOfFrame();

        // Create volume objects
        for (int i = 0; i < gen0.Length; i++)
            gen0[i] = CreateVolumeObject(gen0[i]);

        for (int i = 0; i < gen1.Length; i++)
            gen1[i] = CreateVolumeObject(gen1[i]);

        for (int i = 0; i < gen2.Length; i++)
            gen2[i] = CreateVolumeObject(gen2[i]);

        for (int i = 0; i < gen3.Length; i++)
            gen3[i] = CreateVolumeObject(gen3[i]);

        if (drawConnections)
        {
            // Create lines
            int numConnections = gen1.Length + gen2.Length + gen3.Length;
            lineRenderers = new LineRenderer[numConnections];
            for (int i = 0; i < numConnections; i++)
            {
                GameObject lineObject = GameObject.Instantiate(linePrefab, transform);
                LineRenderer lineRenderer = lineObject.GetComponent<LineRenderer>();
                lineRenderer.colorGradient = connectionColor;
                lineRenderers[i] = lineRenderer;
            }

            // Set connection width
            int lineIndex = 0;
            for (int i = 0; i < gen1.Length; i++)
            {
                Chunk v = gen1[i];
                lineRenderers[lineIndex].colorGradient = connectionGradientGen1;
                lineIndex++;
            }

            for (int i = 0; i < gen2.Length; i++)
            {
                Chunk v = gen2[i];
                lineRenderers[lineIndex].colorGradient = connectionGradientGen2;
                lineIndex++;
            }

            for (int i = 0; i < gen3.Length; i++)
            {
                Chunk v = gen3[i];
                lineRenderers[lineIndex].colorGradient = connectionGradientGen3;
                lineIndex++;
            }
        }

        centralParticles.gameObject.SetActive(true);
    }

    private Chunk CreateVolumeObject(Chunk v)
    {
        GameObject volumeGo = GameObject.Instantiate(volumePrefab, transform.TransformPoint(v.TargetPoint), Quaternion.LookRotation(v.TargetDir), transform);
        volumeGo.transform.localScale = Vector3.one * v.Diameter;
        v.RigidBody = volumeGo.GetComponent<Rigidbody>();
        v.Impacts = volumeGo.GetComponent<ChunkImpacts>();
        v.Impacts.Radius = v.Radius;
        v.RigidBody.mass = v.Radius;
        v.Renderer = volumeGo.GetComponent<Renderer>();
        //v.BaseColor = color;
        v.Block = new MaterialPropertyBlock();
        //v.Block.SetColor("_EmissiveColor", color);
        //v.Renderer.SetPropertyBlock(v.Block);

        v.Impacts.OnImpact += OnImpact;

        largestRadius = Mathf.Max(largestRadius, v.Radius);

        return v;
    }

    private void OnImpact(ChunkImpacts chunk, Vector3 impactPoint)
    {
        chunk.ImpactAudio.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
        chunk.ImpactAudio.clip = impactClips[UnityEngine.Random.Range(0, impactClips.Length)];
        chunk.ImpactAudio.volume = (chunk.LastImpactIntensity * impactVolume) * chunk.Radius / largestRadius;
        chunk.ImpactAudio.Play();

        // Create an impact event
        if (chunk.LastImpactIntensity > minImpactParticleIntensity)
        {
            for (int i = 0; i < impactParticles.Length; i++)
            {
                if (!impactParticles[i].gameObject.activeSelf)
                {
                    impactParticles[i].transform.position = impactPoint;
                    impactParticles[i].gameObject.SetActive(true);
                    break;
                }
            }
        }

        if (chunk.LastImpactType == ChunkImpacts.ImpactType.Finger)
        {
            // Find the finger we collided with
            for (int i = 0; i < fingerRigidBodies.Length; i++)
            {
                if (fingerRigidBodies[i].name == chunk.LastOtherCollider.name)
                {
                    FingerTip ft = fingerTips[i];
                    ft.ImpactIntensity = 1;
                    ft.ImpactAudio.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
                    ft.ImpactAudio.clip = fingerImpactClip;
                    ft.ImpactAudio.volume = chunk.LastImpactIntensity * impactVolume;
                    ft.ImpactAudio.Play();
                    fingerTips[i] = ft;
                    break;
                }
            }
        }
    }

    private void JitterVolumes(Chunk[] volumes, IEnumerable<Chunk> staticVolumes, int iterations, bool constrainToSurface)
    {
        for (int i = 0; i < iterations; i++)
        {
            // First check volumes against themselves
            for (int v1i = 0; v1i < volumes.Length; v1i++)
            {
                for (int v2i = 0; v2i < volumes.Length; v2i++)
                {
                    if (v1i == v2i)
                        continue;

                    volumes[v1i] = jitterVolume(volumes[v1i], volumes[v2i], constrainToSurface);
                }
            }

            if (staticVolumes != null)
            {
                // Then check volumes against static volumes (previous generations
                for (int vi = 0; vi < volumes.Length; vi++)
                {
                    foreach (Chunk v2 in staticVolumes)
                    {
                        volumes[vi] = jitterVolume(volumes[vi], v2, constrainToSurface);
                    }
                }
            }
        }
    }

    private Chunk jitterVolume(Chunk v1, Chunk v2, bool constrainToSurface)
    {
        float dist = (v1.TargetPoint - v2.TargetPoint).magnitude;
        float touchingDist = (v1.Radius + v2.Radius);

        // If they're touching, jitter the volume
        if (dist < touchingDist)
        {
            float overlap = touchingDist - dist;
            Vector3 dir = RandomOnSphere(random);
            v1.TargetPoint += dir * overlap;

            if (constrainToSurface)
                v1.TargetPoint = v1.TargetPoint.normalized * (SurfaceRadius - v1.Radius);
        }
        //If the volume has gone outside the sphere, move it back
        float outerEdgeDist = v1.TargetPoint.magnitude + v1.Radius;
        if (outerEdgeDist > SurfaceRadius)
        {
            float outerEdgeOverlap = outerEdgeDist - SurfaceRadius;
            v1.TargetPoint -= v1.TargetPoint.normalized * outerEdgeOverlap;
        }

        return v1;
    }

    private void Update()
    {
        if (!Initialized)
            return;

        if (drawConnections)
            DrawConnections();

        UpdateImpacts();
    }

    private void UpdateImpacts()
    {
        UpdateVolumeImpacts(gen0);
        UpdateVolumeImpacts(gen1);
        UpdateVolumeImpacts(gen2);
        UpdateVolumeImpacts(gen3);

        for (int i = 0; i < fingerTips.Length; i++)
        {
            FingerTip ft = fingerTips[i];

            if (fingers[i].gameObject.activeSelf)
            {
                Color impactColor = fingerImpactGradient.Evaluate(ft.ImpactIntensity);
                ft.Block.SetColor("_EmissiveColor", ft.BaseColor + impactColor);
                ft.Renderer.SetPropertyBlock(ft.Block);
                ft.ImpactIntensity = Mathf.Clamp01(ft.ImpactIntensity - (Time.deltaTime * fingerImpactFadeTime));
                fingerTips[i] = ft;
            }
        }
    }

    private void UpdateVolumeImpacts(Chunk[] volumes)
    {
        for (int i = 0; i < volumes.Length; i++)
        {
            Chunk c = volumes[i];
            c.Impacts.ChunkImpactIntensity = Mathf.Clamp01(c.Impacts.ChunkImpactIntensity - (Time.deltaTime * chunkImpactFadeTime));
            c.Impacts.FingerImpactIntensity = Mathf.Clamp01(c.Impacts.FingerImpactIntensity - (Time.deltaTime * fingerImpactFadeTime));

            /*if (c.Impacts.ChunkImpactIntensity + c.Impacts.FingerImpactIntensity > 0)
            {
                Color chunkImpactColor = volumeImpactGradient.Evaluate(c.Impacts.ChunkImpactIntensity);
                Color fingerImpactColor = fingerImpactGradient.Evaluate(c.Impacts.FingerImpactIntensity);
                Color finalColor = chunkImpactColor + fingerImpactColor;
                c.Block.SetColor("_EmissiveColor", finalColor);
                c.Renderer.SetPropertyBlock(c.Block);
            }
            else
            {
                c.Renderer.SetPropertyBlock(emptyBlock);
            }*/
        }
    }


    protected override void FixedUpdate()
    {
        if (!Initialized)
            return;

        base.FixedUpdate();

        ApplyVolumeDrift(gen0);
        ApplyVolumeDrift(gen1);
        ApplyVolumeDrift(gen2);
        ApplyVolumeDrift(gen3);

        ApplyVolumeSeek(gen0);
        ApplyVolumeSeek(gen1);
        ApplyVolumeSeek(gen2);
        ApplyVolumeSeek(gen3);
    }

    private void ApplyVolumeSeek(Chunk[] volumes)
    {
        for (int i = 0; i < volumes.Length; i++)
        {
            Chunk v = volumes[i];
            Vector3 dir = transform.TransformPoint(v.TargetPoint) - v.RigidBody.position;
            v.RigidBody.AddForce(dir * seekForce * Time.fixedDeltaTime, ForceMode.VelocityChange);
        }
    }

    private void ApplyVolumeDrift(Chunk[] volumes)
    {
        for (int i = 0; i < volumes.Length; i++)
        {
            Chunk v = volumes[i];
            v.RigidBody.AddForce(GetDrift(v.TargetPoint, i), ForceMode.Force);
        }
    }

    private Vector3 GetDrift(Vector3 point, int offset)
    {
        Vector3 drift = new Vector3();
        drift.x = (float)noise.Evaluate((point.x * driftScale) + offset, Time.time * driftTimeScale) * 0.01f;
        drift.y = (float)noise.Evaluate((point.y * driftScale) + offset, Time.time * driftTimeScale) * 0.01f;
        drift.z = (float)noise.Evaluate((point.z * driftScale) + offset, Time.time * driftTimeScale) * 0.01f;
        return drift * driftIntensity;
    }

    private void DrawConnections()
    {
        int lineIndex = 0;
        for (int i = 0; i < gen1.Length; i++)
        {
            Chunk v = gen1[i];
            //lineRenderers[lineIndex].widthCurve = connectionWidthGen1;
            lineRenderers[lineIndex].SetPosition(0, v.RigidBody.position);
            lineRenderers[lineIndex].SetPosition(1, gen0[v.Parent].RigidBody.position);
            lineIndex++;
        }

        for (int i = 0; i < gen2.Length; i++)
        {
            Chunk v = gen2[i];
            //lineRenderers[lineIndex].widthCurve = connectionWidthGen2;
            lineRenderers[lineIndex].SetPosition(0, v.RigidBody.position);
            lineRenderers[lineIndex].SetPosition(1, gen1[v.Parent].RigidBody.position);
            lineIndex++;
        }

        for (int i = 0; i < gen3.Length; i++)
        {
            Chunk v = gen3[i];
            //lineRenderers[lineIndex].widthCurve = connectionWidthGen3;
            lineRenderers[lineIndex].SetPosition(0, v.RigidBody.position);
            lineRenderers[lineIndex].SetPosition(1, gen2[v.Parent].RigidBody.position);
            lineIndex++;
        }
    }

    private static Vector3 RandomInsideSphere(System.Random random)
    {
        Vector3 value = Vector3.zero;
        value.x = RandomRange(random, -1f, 1f);
        value.y = RandomRange(random, -1f, 1f);
        value.z = RandomRange(random, -1f, 1f);
        return value;
    }

    private static Vector3 RandomOnSphere(System.Random random)
    {
        Vector3 value = Vector3.zero;
        value.x = RandomRange(random, -1f, 1f);
        value.y = RandomRange(random, -1f, 1f);
        value.z = RandomRange(random, -1f, 1f);
        return value.normalized;
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        double value = random.NextDouble();
        return Mathf.Lerp(min, max, (float)value);
    }
}
