// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using UnityEngine;
using Random = UnityEngine.Random;

public class Flock : FingerSurface
{
    [System.Serializable]
    public struct Boid
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 Acceleration;
        public float Rotation;
    }

    [System.Serializable]
    public struct SurfaceBoid
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Normal;
        public float Offset;
        internal float Agitation;
    }

    [System.Serializable]
    public struct Force
    {
        public bool Enabled;
        public bool Active;
        public Vector2 Position;
        public float Radius;
    }

    public override float SurfaceRadius
    {
        get { return oceanCollider.radius; }
    }

    public Transform[] Fingers => fingers;

    public Force[] Forces => forces;

    public Transform SurfaceTransform => surfaceTransform;

    [SerializeField]
    private MeshSampler sampler;
    [SerializeField]
    private SphereCollider oceanCollider;

    [SerializeField]
    private int numBoids = 512;
    [SerializeField]
    private float coherenceAmount = 0.5f;
    [SerializeField]
    private float alignmentAmount = 0.5f;
    [SerializeField]
    private float separationAmount = 0.5f;
    [SerializeField]
    private float fleeAmount = 1f;
    [SerializeField]
    private float maxSpeed = 1f;
    [SerializeField]
    private float maxForce = 0.035f;
    [SerializeField]
    private float minDistance = 0.05f;
    [SerializeField]
    private float boidRespawnTime = 1f;
    [SerializeField]
    private float surfaceBounce = 0.015f;
    [SerializeField]
    private float surfaceBounceSpeed = 10;
    [SerializeField]
    private float agitationMultiplier = 0.15f;
    [SerializeField]
    private Vector2 forceRadius = Vector2.one;
    [SerializeField]
    private GameObject splashPrefab;
    [SerializeField]
    private AnimationCurve bounceCurve;
    [SerializeField]
    private Vector3 flockRotate;

    [Header("Audio")]
    [SerializeField]
    private AudioSource normalAudio;
    [SerializeField]
    private AudioSource fleeingAudio;
    [SerializeField]
    private float masterVolume = 0.25f;
    [SerializeField]
    private float audioChangeSpeed = 0.25f;
    [SerializeField]
    private int numBoidsFleeingMaxVolume = 100;

    [SerializeField]
    private Mesh boidMesh;
    [SerializeField]
    private Material boidMat;
    [SerializeField]
    private float boidScale;

    private float crowdNoisePing;
    private int numBoidsFleeing = 0;

    [SerializeField]
    private Texture2D positionMap;
    private Bounds meshBounds;
    private Force[] forces;
    private Boid[] boids;
    private SurfaceBoid[] surfaceBoids;
    private Matrix4x4[] boidMatrices;

    public void PingCrowdNoises()
    {
        crowdNoisePing = 1;
    }

    protected override void Awake()
    {
        base.Awake();

        normalAudio.volume = 0;
        fleeingAudio.volume = 0;
    }

    public override void Initialize(Vector3 surfacePosition)
    {
        base.Initialize(surfacePosition);

        sampler.SampleMesh(false);

        meshBounds = sampler.Bounds;

        boids = new Boid[numBoids];
        surfaceBoids = new SurfaceBoid[numBoids];
        boidMatrices = new Matrix4x4[numBoids];
        for (int i = 0; i < numBoids; i++)
        {
            Boid b = new Boid();
            MeshSample s = sampler.Samples[Random.Range(0, sampler.Samples.Length)];
            b.Position = s.UV;
            b.Rotation = Random.Range(0, 360);
            boids[i] = b;

            SurfaceBoid sb = new SurfaceBoid();
            sb.Offset = Random.value;
            surfaceBoids[i] = sb;
        }

        forces = new Force[fingers.Length];
    }

    private void Update()
    {
        if (!Initialized)
            return;

        surfaceTransform.Rotate(flockRotate * Time.deltaTime);

        for (int i = 0; i < fingers.Length; i++)
        {
            Force f = forces[i];

            if (!fingers[i].gameObject.activeSelf)
            {
                f.Enabled = false;
                forces[i] = f;
                continue;
            }

            f.Enabled = true;

            float distToSphere = Vector3.Distance(fingers[i].position, surfaceTransform.position);
            if (distToSphere > SurfaceRadius)
            {
                float distToSurface = 1f - Mathf.Clamp01(forceRadius.x / (distToSphere - SurfaceRadius));
                f.Radius = Mathf.Lerp(forceRadius.x, forceRadius.y, distToSurface);
            }
            else
            {
                float distToSurface = Mathf.Clamp01(forceRadius.x / (distToSphere - SurfaceRadius));
                f.Radius = Mathf.Lerp(forceRadius.x, forceRadius.y, distToSurface);
            }

            f.Position = ProjectFingerPosition(fingers[i].position);

            forces[i] = f;
        }

        UpdateBoids(Time.time, Time.deltaTime);
        DrawSurfaceBoids();
        UpdateAudio();
    }

    private void UpdateAudio()
    {
        float normalizedBoidsFleeing = Mathf.Clamp01((float)numBoidsFleeing / numBoidsFleeingMaxVolume);
        normalAudio.volume = Mathf.Lerp(normalAudio.volume, (1f - normalizedBoidsFleeing) * masterVolume, 0.5f);
        float fleeingAudioVolume = Mathf.Clamp01((normalizedBoidsFleeing + crowdNoisePing) * masterVolume);
        crowdNoisePing = Mathf.Clamp01(crowdNoisePing - Time.deltaTime * 5);
        if (fleeingAudioVolume > fleeingAudio.volume)
        {
            fleeingAudio.volume = fleeingAudioVolume;
        }
        else
        {
            fleeingAudio.volume = Mathf.Lerp(fleeingAudio.volume, fleeingAudioVolume, Time.deltaTime * audioChangeSpeed);
        }
    }

    private void DrawSurfaceBoids()
    {
        Vector3 scale = Vector3.one * boidScale;
        for (int i = 0; i < numBoids; i++)
        {
            SurfaceBoid b = surfaceBoids[i];
            float bounce = bounceCurve.Evaluate(Mathf.Repeat((b.Offset + Time.time) * (surfaceBounceSpeed * b.Agitation), 1f)) * surfaceBounce;
            boidMatrices[i] = Matrix4x4.TRS(b.Position + (b.Normal * bounce), b.Rotation, scale);
        }

        Graphics.DrawMeshInstanced(boidMesh, 0, boidMat, boidMatrices);
    }

    private Vector2 ProjectFingerPosition(Vector3 position)
    {
        MeshSample sample = sampler.ClosestSample(surfaceTransform.InverseTransformPoint(position));
        return sample.UV;
    }

    private void UpdateBoids(float time, float deltaTime)
    {
        Vector2 sumPositions = Vector2.zero;
        Vector2 sumVelocities = Vector2.zero;
        Vector2 averagePosition = Vector2.zero;
        Vector2 averageVelocity = Vector2.zero;
        int newNumBoidFleeing = 0;

        for (int i = 0; i < numBoids; i++)
        {
            Boid b = boids[i];
            sumPositions += b.Position;
            sumVelocities += b.Velocity;
        }
        averagePosition = sumPositions / numBoids;
        averageVelocity = sumVelocities / numBoids;

        for (int b1i = 0; b1i < numBoids; b1i++)
        {
            Boid b1 = boids[b1i];

            Vector2 desiredAverageDirection = (averagePosition - b1.Position).normalized;
            Vector2 separationDirection = Vector2.zero;
            Vector2 fleeDirection = Vector2.zero;
            Vector2 forceAveragePosition = Vector2.zero;
            Vector2 difference = Vector2.zero;

            int numBoidsInRange = 0;
            for (int b2i = 0; b2i < numBoids; b2i++)
            {
                if (b1i == b2i)
                    continue;

                Boid b2 = boids[b2i];

                float dist = DistanceBetween(b1.Position, b2.Position, ref difference);
                if (dist < minDistance)
                {
                    separationDirection += (difference.normalized / dist);
                    numBoidsInRange++;
                }
            }

            int numForcesInRange = 0;
            for (int fi = 0; fi < forces.Length; fi++)
            {
                Force f = forces[fi];
                if (!f.Enabled || !f.Active)
                    continue;

                float dist = DistanceBetween(b1.Position, f.Position, ref difference);
                if (dist < f.Radius)
                {
                    fleeDirection += (difference.normalized / dist);
                    forceAveragePosition += f.Position;
                    numForcesInRange++;
                    newNumBoidFleeing++;
                }
            }

            // Alignment and coherence happen regardless of proximity
            Vector2 alignment = Steer(b1.Velocity, averageVelocity);
            Vector2 coherence = Steer(b1.Velocity, desiredAverageDirection);
            Vector2 newVelocity = (alignmentAmount * alignment) + (coherenceAmount * coherence);

            if (numBoidsInRange > 0)
            {
                // Separation happens if any boids were in range
                separationDirection = (separationDirection / numBoidsInRange).normalized;
                Vector2 separation = Steer(b1.Velocity, separationDirection * maxSpeed);
                newVelocity += (separationAmount * separation);
            }

            // Do a smooth lerp for alignment, coherence and separation
            b1.Velocity = Vector2.Lerp(b1.Velocity, LimitMagnitude(newVelocity, maxSpeed), deltaTime);

            if (numForcesInRange > 0)
            {
                // Flee is more disruptive
                // Use disance to force center to determine flee amount
                forceAveragePosition = (forceAveragePosition / numForcesInRange);
                fleeDirection = (fleeDirection / numForcesInRange).normalized;
                float distToForceCenter = DistanceBetween(b1.Position, forceAveragePosition, ref difference);
                float normalizedFleeForce = Mathf.Clamp01(distToForceCenter / forceRadius.y);
                Vector2 flee = Steer(b1.Velocity, fleeDirection * fleeAmount * normalizedFleeForce);
                // Don't limit flee velocity
                b1.Velocity = b1.Velocity + flee;
            }

            // Make sure boids don't go out of range
            Vector2 position = b1.Position + (b1.Velocity * deltaTime);
            position.x = Mathf.Repeat(position.x, 1);
            position.y = Mathf.Repeat(position.y, 1);

            b1.Position = position;
            b1.Rotation = Mathf.Lerp(b1.Rotation, Mathf.Atan2(b1.Velocity.y, b1.Velocity.x) * Mathf.Rad2Deg, deltaTime);

            boids[b1i] = b1;
        }

        numBoidsFleeing = newNumBoidFleeing;
        Vector3 origin = surfaceTransform.position;

        for (int i = 0; i < numBoids; i++)
        {
            Boid b = boids[i];
            SurfaceBoid sb = surfaceBoids[i];
            sb.Agitation = 1f + (b.Velocity.magnitude * agitationMultiplier);
            sb.Position = SampleSurface(b.Position);
            sb.Normal = (sb.Position - origin).normalized;
            Vector3 up = Quaternion.AngleAxis(b.Rotation, Vector3.forward) * Vector3.right;
            sb.Rotation = Quaternion.LookRotation(sb.Normal, up);

            surfaceBoids[i] = sb;
        }
    }

    private Vector3 SampleSurface(Vector2 position)
    {
        Color c = positionMap.GetPixelBilinear(position.x, position.y);
        Vector3 pos = Vector3.zero;
        pos.x = c.r * meshBounds.size.x;
        pos.y = c.g * meshBounds.size.y;
        pos.z = c.b * meshBounds.size.z;
        pos = pos - meshBounds.extents - meshBounds.center;
        return surfaceTransform.TransformPoint(pos);
    }

    private Vector2 Steer(Vector2 current, Vector2 desired)
    {
        return LimitMagnitude(desired - current, maxForce);
    }

    private float DistanceBetween(Vector2 p1, Vector2 p2, ref Vector2 diff)
    {
        diff.x = p1.x - p2.x;
        diff.y = p1.y - p2.y;
        return diff.magnitude;
    }

    private Vector2 LimitMagnitude(Vector2 v, float max)
    {
        if (v.sqrMagnitude > max * max)
            v = v.normalized * max;

        return v;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        foreach (Transform finger in fingers)
            Gizmos.DrawWireSphere(finger.position, 0.05f);

        if (!Application.isPlaying)
            return;

        Gizmos.color = Color.Lerp(Color.yellow, Color.clear, 0.75f);
        foreach (Force f in forces)
        {
            if (!f.Enabled)
                continue;

            Vector3 position = Quaternion.Euler(-90f, 0f, 0f) * f.Position;
            Gizmos.DrawSphere(position, f.Radius);
        }

        foreach (Boid b in boids)
        {
            Gizmos.color = Color.magenta;
            Vector3 forward = Vector3.forward;
            Vector3 position = Quaternion.Euler(-90f, 0f, 0f) * b.Position;
            Gizmos.DrawCube(position, Vector3.one * minDistance * 0.5f);
            if (!Mathf.Approximately(b.Rotation, 0))
            {
                forward = Quaternion.Euler(0f, b.Rotation, 0f) * Vector3.forward;
            }
            Gizmos.DrawLine(position, position + (forward * 0.05f));
            Gizmos.color = Color.Lerp(Color.magenta, Color.clear, 0.65f);
            Gizmos.DrawWireSphere(position, minDistance);
        }
    }
}
