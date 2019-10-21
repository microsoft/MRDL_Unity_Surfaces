// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class Goop : HandMeshSurface
{
    [System.Serializable]
    private struct FingerTip
    {
        public Renderer Renderer;
        public float SlimeIntensity;
    }

    [System.Serializable]
    private struct Blorb
    {
        public Vector3 Point;
        public Vector3 Dir;
        public float Rotation;
        public float RotationSpeed;
        public float Radius;
        public float Agitation;
        public float Bloat;
        public GoopBlorb Goop;
        public bool Popped;
        public float TimePopped;
    }

    [SerializeField]
    private MeshSampler sampler;
    [SerializeField]
    private float minRadius = 0.01f;
    [SerializeField]
    private float maxRadius = 0.1f;
    [SerializeField]
    private float agitationForce = 0.1f;
    [SerializeField]
    private float radiusChangeSpeed = 5f;
    [SerializeField]
    private float gravity = 0.5f;

    [Header("Goop Center")]
    [SerializeField]
    private AnimationCurve[] blendShapeCurves;
    [SerializeField]
    private SkinnedMeshRenderer goopCenter;
    [SerializeField]
    private Gradient centerGlowGradient;
    [SerializeField]
    private Gradient centerPoppedGradient;
    [SerializeField]
    private AnimationCurve centerGlowCurve;
    [SerializeField]
    private AnimationCurve centerPoppedCurve;
    [SerializeField]
    private AnimationCurve centerSquirmCurve;

    [Header("Goop Blorbs")]
    [SerializeField]
    private GameObject[] goopBlorbPrefabs;
    [SerializeField]
    private Gradient bloatGradientEmission;
    [SerializeField]
    private Gradient bloatGradientColor;
    [SerializeField]
    private Gradient poppedGradientColor;
    [SerializeField]
    private Gradient baseGradientColor;
    [SerializeField]
    private float maxDistToFinger = 0.5f;
    [SerializeField]
    private float maxBloatBeforePop = 25f;
    [SerializeField]
    private float respawnTime = 2.5f;
    [SerializeField]
    private AnimationCurve fingerAgitationCurve;
    [SerializeField]
    private AnimationCurve popRecoveryCurve;
    [SerializeField]
    private AnimationCurve popRecoveryScaleCurve;
    [SerializeField]
    private ParticleSystem[] popParticles;
    [SerializeField]
    private string emissionColorPropName = "_EmissiveColor";

    [Header("Fingertips")]
    [SerializeField]
    private FingerTip[] fingerTips;
    [SerializeField]
    private float fingerTipSlimeFadeSpeed = 0.25f;
    [SerializeField]
    private Color fingerTipSlimeColor;

    [Header("Goop Slime")]
    [SerializeField]
    private float goopDistance = 0.05f;
    [SerializeField]
    private GoopSlime[] goopSlimes;
    [SerializeField]
    private float timeLastPopped = 0;
    [SerializeField]
    private AudioSource growthChaosAudio;
    [SerializeField]
    private AnimationCurve growthChaosVolume;
    [SerializeField]
    private AnimationCurve growthChaosPitch;
    [SerializeField]
    private float rotationSpeed = 25f;

    private Blorb[] blorbs = new Blorb[0];
    private Matrix4x4[] matrixes = new Matrix4x4[0];
    private Vector3[] fingerPositions;
    private Queue<Blorb> popEvents = new Queue<Blorb>();
    private System.Random random = new System.Random();

    private float timeSinceLastUpdate;
    private float totalAgitation;
    private float centerAgitation;

    private bool updateBlorbs;
    private float time;
    private float deltaTime;
    private float mainLoopDeltaTime;
    private float goopCenterTimeElapsed;
    private MaterialPropertyBlock emptyBlock;

    protected override void Awake()
    {
        base.Awake();

        growthChaosAudio.volume = 0;

        fingerTips = new FingerTip[fingers.Length];
        for (int i = 0; i < fingers.Length; i++)
        {
            FingerTip fingerTip = new FingerTip();
            fingerTip.Renderer = fingers[i].GetComponentInChildren<Renderer>();
            fingerTips[i] = fingerTip;
        }
    }

    public override void Initialize(Vector3 surfacePosition)
    {
        base.Initialize(surfacePosition);

        emptyBlock = new MaterialPropertyBlock();

        sampler.SampleMesh();

        blorbs = new Blorb[sampler.Samples.Length];
        matrixes = new Matrix4x4[sampler.Samples.Length];
        fingerPositions = new Vector3[fingers.Length];

        for (int i = 0; i < sampler.Samples.Length; i++)
        {
            MeshSample sample = sampler.Samples[i];
            Blorb e = new Blorb();
            e.Point = sample.Point;
            e.Dir = e.Point.normalized;
            e.Radius = Random.Range(minRadius, maxRadius);
            e.Agitation = Random.value;
            e.Goop = GameObject.Instantiate(goopBlorbPrefabs[Random.Range(0, goopBlorbPrefabs.Length)], transform).GetComponent<GoopBlorb>();
            e.Rotation = Random.Range(0, 360);
            e.RotationSpeed = Random.Range(-rotationSpeed, rotationSpeed);
            blorbs[i] = e;
        }

        timeLastPopped = -100;

        updateBlorbs = true;
        Task updateTask = UpdateBlorbsLoop();
    }

    private void OnDisable()
    {
        updateBlorbs = false;
    }

    // Update is called once per frame
    private void Update()
    {
        if (!Initialized)
            return;

        time = Time.time;
        mainLoopDeltaTime = Time.deltaTime;

        for (int i = 0; i < fingers.Length; i++)
            fingerPositions[i] = fingers[i].localPosition;

        DrawBlorbs();
        UpdateGoopCenter();
        UpdateGoopSlime();
        UpdateFingerTips();
        UpdateAudio();
    }

    private void UpdateFingerTips()
    {
        for (int i = 0; i < fingerTips.Length; i++)
        {
            FingerTip fingerTip = fingerTips[i];
            fingerTipSlimeColor.a = fingerTip.SlimeIntensity;
            fingerTip.SlimeIntensity = goopSlimes[i].isActiveAndEnabled ? 1 : Mathf.Clamp01(fingerTip.SlimeIntensity - Time.deltaTime * fingerTipSlimeFadeSpeed);
            fingerTip.Renderer.materials[1].color = fingerTipSlimeColor;
            fingerTips[i] = fingerTip;
        }
    }

    private void UpdateGoopCenter()
    {
        float squirm = centerSquirmCurve.Evaluate(Time.time - timeLastPopped);
        goopCenterTimeElapsed += Time.deltaTime * (1f + totalAgitation + squirm);
        for (int i = 0; i < blendShapeCurves.Length; i++)
            goopCenter.SetBlendShapeWeight(i, blendShapeCurves[i].Evaluate(goopCenterTimeElapsed) * 100);

        centerAgitation += Time.deltaTime * totalAgitation;
        Color goopColor = centerGlowGradient.Evaluate(centerGlowCurve.Evaluate(centerAgitation));
        Color poppedColor = centerPoppedGradient.Evaluate(centerPoppedCurve.Evaluate(Time.time - timeLastPopped));
        goopCenter.material.SetColor(emissionColorPropName, goopColor + poppedColor);
    }

    private async Task UpdateBlorbsLoop()
    {
        while (updateBlorbs)
        {
            deltaTime = time - timeSinceLastUpdate;
            timeSinceLastUpdate = time;
            await UpdateBlorbs().ConfigureAwait(false);
            await Task.Yield();
        }
    }

    private async Task UpdateBlorbs()
    {
        await new WaitForBackgroundThread();

        float timeSincePopped = time - timeLastPopped;
        float popRecovery = popRecoveryCurve.Evaluate(timeSincePopped);
        int numBlorbs = blorbs.Length;

        //Gravity
        for (int i = 0; i < numBlorbs; i++)
        {
            Blorb e = blorbs[i];
            Vector3 idealPos = (e.Point.normalized * SurfaceRadius);
            e.Point = Vector3.Lerp(e.Point, idealPos, gravity);
            blorbs[i] = e;
        }

        // Agitation / Rotation
        for (int i = 0; i < numBlorbs; i++)
        {
            Blorb e = blorbs[i];
            Vector3 randomPoint = RandomInsideSphere(random) * ((e.Agitation + e.Bloat) * agitationForce * deltaTime);
            e.Point = Vector3.Lerp(e.Point, e.Point + randomPoint, deltaTime);
            float newRadius = Mathf.Clamp(e.Radius + (RandomRange(random, -1f, 1f) * (e.Agitation + e.Bloat) * deltaTime), minRadius, maxRadius);
            e.Radius = Mathf.Lerp(e.Radius, newRadius, radiusChangeSpeed * deltaTime);
            e.Bloat = Mathf.Lerp(e.Bloat, 0, deltaTime * radiusChangeSpeed);
            e.Rotation += deltaTime * e.RotationSpeed;
            blorbs[i] = e;
        }

        // Fingers
        totalAgitation = 0;
        for (int f = 0; f < fingerPositions.Length; f++)
        {
            Vector3 fingerPos = fingerPositions[f];

            for (int i = 0; i < numBlorbs; i++)
            {
                Blorb e = blorbs[i];
                float distToFinger = Vector3.Distance(fingerPos, e.Point);
                if (distToFinger > maxDistToFinger)
                    continue;

                float fingerAgitation = fingerAgitationCurve.Evaluate(distToFinger / maxDistToFinger) * popRecovery;
                totalAgitation += fingerAgitation;
                e.Bloat = Mathf.Lerp(e.Bloat, Mathf.Clamp(e.Bloat + fingerAgitation, 0, Mathf.Infinity), deltaTime * radiusChangeSpeed);
                blorbs[i] = e;
            }
        }

        // Forces
        for (int i = 0; i < numBlorbs; i++)
        {
            for (int j = 0; j < numBlorbs; j++)
            {
                if (i == j)
                    continue;

                Blorb e1 = blorbs[i];
                Blorb e2 = blorbs[j];

                if (e1.Popped || e2.Popped)
                    continue;

                float dist = Vector3.Distance(e1.Point, e2.Point);
                float touchingDist = (e1.Radius + e1.Bloat) + (e2.Radius + e2.Bloat);

                if (dist > touchingDist)
                    continue;

                float overlap = touchingDist - dist;
                Vector3 dir = (e1.Point - e2.Point).normalized;

                if (e1.Radius > e2.Radius)
                {
                    e2.Point -= dir * (overlap * deltaTime);
                    blorbs[j] = e2;
                }
                else
                {
                    e1.Point += dir * (overlap * deltaTime);
                    blorbs[i] = e1;
                }
            }
        }

        // Direction
        for (int i = 0; i < numBlorbs; i++)
        {
            Blorb e1 = blorbs[i];
            e1.Dir = e1.Point.normalized;
            blorbs[i] = e1;
        }

        for (int i = 0; i < blorbs.Length; i++)
        {
            Blorb e = blorbs[i];
            if (e.Popped)
            {
                if (time > e.TimePopped + respawnTime)
                {
                    e.Popped = false;
                    blorbs[i] = e;
                }
            }
            else
            {
                float normalizedBloat = (e.Bloat / maxBloatBeforePop);
                if (normalizedBloat >= 1)
                {
                    e.Popped = true;
                    e.TimePopped = time;
                    timeLastPopped = time;
                    popEvents.Enqueue(e);
                }
            }
        }
    }

    private void UpdateGoopSlime()
    {
        for (int i = 0; i < fingers.Length; i++)
        {
            if (!fingers[i].gameObject.activeSelf)
            {
                goopSlimes[i].gameObject.SetActive(false);
                continue;
            }

            if (Vector3.Distance(fingers[i].position, transform.position) < SurfaceRadius + goopDistance)
            {
                MeshSample closestSample = sampler.ClosestSample(fingers[i].localPosition);
                goopSlimes[i].SetGoop(closestSample, fingers[i]);
            }
        }
    }

    private void DrawBlorbs()
    {
        Color centerPoppedColor = centerPoppedGradient.Evaluate(centerPoppedCurve.Evaluate(Time.time - timeLastPopped));
        bool applyPoppedColor = centerPoppedColor.r > 0;
        float popScale = popRecoveryScaleCurve.Evaluate(Time.time - timeLastPopped);

        for (int i = 0; i < blorbs.Length; i++)
        {
            Blorb e = blorbs[i];
            if (e.Popped)
            {
                float poppedTime = (Time.time - e.TimePopped) / respawnTime;
                e.Goop.Renderer.SetPropertyBlock(emptyBlock);
                e.Goop.transform.localScale = Vector3.one * Mathf.Lerp(minRadius, e.Radius + e.Bloat, poppedTime);
            }
            else
            {
                float normalizedBloat = Mathf.Clamp01(e.Bloat / maxBloatBeforePop);
                e.Goop.transform.localPosition = e.Point;
                e.Goop.transform.forward = e.Dir;
                e.Goop.transform.Rotate(0f, 0f, e.Rotation, Space.Self);
                e.Goop.transform.localScale = Vector3.one * (e.Radius + e.Bloat) * popScale;

                if (normalizedBloat > 0.01f || applyPoppedColor)
                {
                    Color bloatColor = bloatGradientEmission.Evaluate(normalizedBloat);
                    bloatColor += centerPoppedColor;
                    e.Goop.PropertyBlock.SetColor(emissionColorPropName, bloatColor);
                    e.Goop.Renderer.SetPropertyBlock(e.Goop.PropertyBlock);
                }
                else
                {
                    e.Goop.Renderer.SetPropertyBlock(emptyBlock);
                }
            }
        }

        if (popEvents.Count > 0)
        { 
            Blorb popEvent = popEvents.Dequeue();

            // Try getting a random one first
            int randomIndex = Random.Range(0, popParticles.Length);
            if (!popParticles[randomIndex].gameObject.activeSelf)
            {
                popParticles[randomIndex].transform.localPosition = popEvent.Point;
                popParticles[randomIndex].transform.forward = popEvent.Dir;
                popParticles[randomIndex].gameObject.SetActive(true);
                return;
            }

            // If that didn't work just go through the list
            for (int i = 0; i < popParticles.Length; i++)
            {
                if (popParticles[i].gameObject.activeSelf)
                    continue;

                popParticles[i].transform.localPosition = popEvent.Point;
                popParticles[i].transform.forward = popEvent.Dir;
                popParticles[i].gameObject.SetActive(true);
                break;
            }

            // Slime all fingers nearby
            for (int i = 0; i < fingerTips.Length; i++)
            {
                if (Vector3.Distance(popEvent.Point, fingers[i].position) < maxDistToFinger)
                {
                    FingerTip fingerTip = fingerTips[i];
                    fingerTip.SlimeIntensity = 1;
                    fingerTips[i] = fingerTip;
                }
            }
        }

        popEvents.Clear();
    }

    private void UpdateAudio()
    {
        float timeSincePopped = Time.time - timeLastPopped;
        float popRecovery = popRecoveryCurve.Evaluate(timeSincePopped);
        growthChaosAudio.volume = growthChaosVolume.Evaluate(totalAgitation * popRecovery);
        growthChaosAudio.pitch = growthChaosPitch.Evaluate(totalAgitation * popRecovery);
    }

    private void OnDrawGizmos()
    {
        foreach (Transform finger in fingers)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(finger.position, maxDistToFinger);
        }
    }

    private static Vector3 RandomInsideSphere(System.Random random)
    {
        Vector3 value = Vector3.zero;
        value.x = RandomRange(random, - 1f, 1f);
        value.y = RandomRange(random, -1f, 1f);
        value.z = RandomRange(random, -1f, 1f);
        return value;
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        double value = random.NextDouble();
        return Mathf.Lerp(min, max, (float)value);
    }
}
