// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Extensions.SceneTransitions;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

public class Ephemeral : FingerSurface
{
    private struct Fingertip
    {
        public Color Color;
        public MaterialPropertyBlock Block;
        public MeshRenderer Mesh;
        public TrailRenderer Trail;
        public AudioSource PingAudio;
    }

    [Serializable]
    public struct NoiseSetting
    {
        public float Scale;
        public float Speed;
        public float Intensity;

        public static NoiseSetting Lerp(NoiseSetting n1, NoiseSetting n2, float t)
        {
            n1.Intensity = Mathf.Lerp(n1.Intensity, n2.Intensity, t);
            n1.Speed = Mathf.Lerp(n1.Speed, n2.Speed, t);
            n1.Scale = Mathf.Lerp(n1.Scale, n2.Scale, t);
            return n1;
        }
    }

    public struct RenderOrder
    {
        public int Index;
        public float Depth;
    }

    [Serializable]
    public struct Force
    {
        public bool Active;
        public float Radius;
        public Vector3 Point;
        public Vector3 PrevPoint;
        public Vector3 Velocity;
        public bool IsIntersecting;
        public bool WasIntersecting;
    }

    // Used when drawing quads
    public struct Quad
    {
        public Color Color;
        public Vector3 C1;
        public Vector3 C2;
        public Vector3 C3;
        public Vector3 C4;
        public Vector2 UV1;
        public Vector2 UV2;
        public Vector2 UV3;
        public Vector2 UV4;

        public static Quad FromWisp(Color c, Vector3 point, float size, Vector3 up, Vector3 right, Vector2 uv, float uvScale)
        {
            q.C1 = point + (up * size) - (right * size);
            q.C2 = point + (up * size) + (right * size);
            q.C3 = point - (up * size) + (right * size);
            q.C4 = point - (up * size) - (right * size);

            //C1 C2
            //C4 C3

            q.UV1 = new Vector2(uv.x,           uv.y);
            q.UV2 = new Vector2(uv.x,           uv.y + uvScale);
            q.UV3 = new Vector2(uv.x + uvScale, uv.y + uvScale);
            q.UV4 = new Vector2(uv.x + uvScale, uv.y);

            q.Color = c;
            return q;
        }

        private static Quad q;
    }

    // Used when calculating interactions
    public struct Wisp
    {
        public Vector3 Point;
        public Vector3 TargetPoint;
        public Vector3 Velocity;
        public float Radius;
        public float TargetRadius;
        public float Depth;
        public float Heat;
        public int TileOffset;
    }

    [Header("Heat")]
    [SerializeField]
    private float fingertipRadius = 0.035f;
    [SerializeField]
    private float heatDissipateSpeed = 0.05f;
    [SerializeField]
    private float heatGainSpeed = 5f;
    [SerializeField]
    private float heatRadiusMultiplier = 0.05f;

    [Header("Wisps")]
    [SerializeField]
    private int numWisps = 1024;
    [SerializeField]
    private float seekStrength = 0.1f;
    [SerializeField]
    private float velocityDampen = 0.75f;
    [SerializeField]
    private float initialVelocity = 0.1f;
    [SerializeField]
    private float offsetMultiplier = 0.01f;
    [SerializeField]
    private float colorCycleSpeed = 0.05f;
    [SerializeField]
    private Gradient baseWispColor;
    [SerializeField]
    private Gradient heatWispColor;
    [SerializeField]
    private Gradient fingertipColor;
    [SerializeField]
    private Material wispMat;
    [SerializeField]
    private Vector2 wispSize;
    [SerializeField]
    private NoiseSetting ambientNoise;
    [SerializeField]
    private NoiseSetting agitatedNoise;

    [Header("Fingertips")]
    [SerializeField]
    private float fingertipVelocityColor;

    [Header("Audio")]
    [SerializeField]
    private AudioSource wispAudio;
    [SerializeField]
    private AudioSource distortionAudio;
    [SerializeField]
    private AnimationCurve distortionVolume;
    [SerializeField]
    private AnimationCurve distortionPitch;
    [SerializeField]
    private AnimationCurve wispVolume;
    [SerializeField]
    private float distortionFadeSpeed = 3f;

    [Header("Texture tiling")]
    [SerializeField]
    private int numTiles = 45;
    [SerializeField]
    private int numColumns = 7;
    [SerializeField]
    private int numRows = 7;

    private Force[] forces;
    private Fingertip[] fingertips;
    private Quad[] quads;
    private Wisp[] wisps;
    private List<RenderOrder> renderOrder = new List<RenderOrder>();
    private Quad[] finalQuads;

    [SerializeField]
    private float tileScale;
    [SerializeField]
    private Vector2[] tileOffsets;
    [SerializeField]
    private int currentTileNum;

    private Vector3 cameraPos;
    private Vector3 cameraUp;
    private Vector3 cameraRht;
    private Vector3 cameraFwd;
    private float totalDistortion;
    private float totalDistortionTarget;
    private float totalHeat;
    private float totalHeatTarget;

    private Color transitionColor = Color.black;
    private bool fadeOut = false;

    private FastSimplexNoise noise;

    private bool updateWisps = false;
    private float time;
    private float deltaTime;
    private float timeLastUpdated;

    protected override void Awake()
    {
        base.Awake();

        wispAudio.volume = 0;
        distortionAudio.volume = 0;
    }

    public override void Initialize(Vector3 surfacePosition)
    {
        base.Initialize(surfacePosition);

        ISceneTransitionService transitionService;
        if (MixedRealityServiceRegistry.TryGetService<ISceneTransitionService>(out transitionService))
        {
            transitionService.OnTransitionStarted += OnTransitionStarted;
        }

        tileScale = 1f / numColumns;
        tileOffsets = new Vector2[numTiles];

        int tileNum = 0;
        for (int row = 0; row < numRows; row++)
        {
            for (int column = 0; column < numColumns; column++)
            {
                Vector2 uv = Vector2.zero;
                uv.x = tileScale * column;
                uv.y = (tileScale * -row) - tileScale;
                tileOffsets[tileNum] = uv;

                tileNum++;
                if (tileNum >= numTiles)
                    break;
            }

            if (tileNum >= numTiles)
                break;
        }

        noise = new FastSimplexNoise();

        transitionColor = Color.black;

        forces = new Force[fingers.Length];
        fingertips = new Fingertip[fingers.Length];
        wisps = new Wisp[numWisps];
        quads = new Quad[numWisps];
        finalQuads = new Quad[numWisps];
        renderOrder = new List<RenderOrder>(numWisps);

        for (int i = 0; i < numWisps; i++)
        {
            Wisp wisp = new Wisp();
            wisp.TargetPoint = SurfacePosition + (Random.insideUnitSphere * SurfaceRadius);
            wisp.Point = wisp.TargetPoint;
            wisp.Velocity = Random.insideUnitSphere * initialVelocity;
            wisp.TargetRadius = Mathf.Lerp(wispSize.x, wispSize.y, (float)noise.Evaluate(wisp.Point.x, wisp.Point.y, wisp.Point.z));
            wisp.Radius = wisp.TargetRadius;
            wisp.TileOffset = Random.Range(0, numTiles);
            wisps[i] = wisp;

            Quad quad = Quad.FromWisp(baseWispColor.Evaluate(Random.Range(0f, 1f)), wisp.Point, wisp.Radius, Vector3.up, Vector3.right, tileOffsets[wisp.TileOffset], tileScale);
            quad.Color = transitionColor;
            quads[i] = quad;
            finalQuads[i] = quads[i];

            RenderOrder r = new RenderOrder();
            renderOrder.Add(r);
        }

        for (int i = 0; i < fingers.Length; i++)
        {
            Transform finger = fingers[i];
            Fingertip fingertip = fingertips[i];
            fingertip.Block = new MaterialPropertyBlock();
            fingertip.Trail = finger.GetComponentInChildren<TrailRenderer>();
            fingertip.Mesh = finger.GetComponentInChildren<MeshRenderer>();
            fingertip.PingAudio = finger.GetComponentInChildren<AudioSource>();
            fingertips[i] = fingertip;
        }

        Camera.onPostRender += PostRender;

        updateWisps = true;
        Task task = UpdateWispsTask();
    }

    private async Task UpdateWispsTask()
    {
        while (updateWisps)
        {
            await new WaitForUpdate();

            FinalizeQuads();
            await Task.Yield();
            PrepareForUpdate();
            await UpdateWisps(time, deltaTime).ConfigureAwait(false);
        }
    }

    private void PrepareForUpdate()
    {
        // Gather all the info we can only get in the main thread
        cameraPos = CameraCache.Main.transform.position;
        cameraUp = CameraCache.Main.transform.up;
        cameraFwd = CameraCache.Main.transform.forward;
        cameraRht = CameraCache.Main.transform.right;

        totalDistortionTarget = 0;
        for (int i = 0; i < fingers.Length; i++)
        {
            Transform finger = fingers[i];
            Force force = forces[i];
            force.PrevPoint = force.Point;
            force.Radius = fingertipRadius;
            if (!finger.gameObject.activeSelf)
            {
                force.Active = false;
            }
            else
            {
                if (!force.Active)
                {
                    force.Velocity = Vector3.zero;
                }
                else
                {
                    force.Velocity = force.Point - finger.position;
                }
                force.Point = finger.position;
                force.Active = true;

                if (force.IsIntersecting)
                    totalDistortionTarget += force.Velocity.magnitude;
            }

            if (force.IsIntersecting)
            {
                if (!force.WasIntersecting)
                {
                    force.WasIntersecting = true;
                    fingertips[i].PingAudio.Play();
                }
            }
            else
            {
                force.WasIntersecting = false;
            }

            forces[i] = force;
        }

        if (totalDistortion < totalDistortionTarget)
            totalDistortion = totalDistortionTarget;
        else
            totalDistortion = Mathf.Lerp(totalDistortion, totalDistortionTarget, Time.deltaTime * distortionFadeSpeed);

        for (int i = 0; i < fingertips.Length; i++)
        {
            Force force = forces[i];
            Fingertip fingertip = fingertips[i];
            float velocity = force.Velocity.magnitude * fingertipVelocityColor;
            fingertip.Color = Color.Lerp(fingertip.Color, fingertipColor.Evaluate(velocity), Time.deltaTime);
            fingertip.Block.SetColor("_Color", fingertip.Color);
            fingertip.Mesh.SetPropertyBlock(fingertip.Block);
            fingertip.Trail.SetPropertyBlock(fingertip.Block);
            fingertips[i] = fingertip;
        }

        time = Time.time;
        deltaTime = Time.time - timeLastUpdated;
        timeLastUpdated = time;
    }

    private void FinalizeQuads()
    {
        // Do this on the main thread in one atomic operation
        for (int i = 0; i < numWisps; i++)
            finalQuads[i] = quads[renderOrder[i].Index];
    }

    private void OnDisable()
    {
        Camera.onPostRender -= PostRender;

        ISceneTransitionService transitionService;
        if (MixedRealityServiceRegistry.TryGetService<ISceneTransitionService>(out transitionService))
        {
            transitionService.OnTransitionStarted -= OnTransitionStarted;
        }

        updateWisps = false;
    }

    private void OnTransitionStarted()
    {
        fadeOut = true;
    }

    private void Update()
    { 
        if (!Initialized)
            return;

        currentTileNum++;
        if (currentTileNum >= numTiles)
            currentTileNum = 0;

        distortionAudio.pitch = distortionPitch.Evaluate(totalDistortion);
        distortionAudio.volume = distortionVolume.Evaluate(totalDistortion);
        totalHeat = Mathf.Lerp(totalHeat, totalHeatTarget, Time.deltaTime);
        wispAudio.volume = wispVolume.Evaluate(totalHeat);

        transitionColor.a = Mathf.Lerp(transitionColor.a, fadeOut ? 1 : 0, Time.deltaTime);
    }

    private void PostRender(Camera cam)
    {
        DrawWisps();
    }

    private async Task UpdateWisps(float time, float deltaTime)
    {
        await new WaitForBackgroundThread();

        for (int i = 0; i < forces.Length; i++)
        {
            Force force = forces[i];
            force.IsIntersecting = false;
            forces[i] = force;
        }

        // Update wisp forces
        totalHeatTarget = 0;
        for (int i = 0; i < numWisps; i++)
        {
            Wisp w1 = wisps[i];

            for (int forceIndex = 0; forceIndex < forces.Length; forceIndex++)
            {
                Force force = forces[forceIndex];
                float touchingDist = force.Radius + w1.Radius;
                float prevPointDistSqr = (w1.Point - force.PrevPoint).sqrMagnitude;
                float pointDistSqr = (w1.Point - force.Point).sqrMagnitude;
                if (pointDistSqr < (touchingDist * touchingDist) || prevPointDistSqr < (touchingDist * touchingDist))
                {
                    force.IsIntersecting = true;
                    w1.Heat = Mathf.Clamp01(w1.Heat + (deltaTime * heatGainSpeed));
                    w1.Velocity -= (force.Velocity * w1.Heat);
                    forces[forceIndex] = force;
                }
            }

            w1.Radius = AddNoise(w1.TargetRadius, ambientNoise, time, i);

            Vector3 point = w1.Point;
            point += (w1.Velocity * deltaTime);
            w1.Velocity = Vector3.Lerp(w1.Velocity, w1.Velocity * velocityDampen, deltaTime);
            point = Vector3.Lerp(point, w1.TargetPoint, seekStrength * deltaTime);
            point = AddNoise(point, NoiseSetting.Lerp(ambientNoise, agitatedNoise, w1.Heat), time, i);
            w1.Point = point;

            w1.Heat = Mathf.Clamp01(w1.Heat - (deltaTime * heatDissipateSpeed));
            totalHeatTarget += w1.Heat;

            wisps[i] = w1;
        }

        await Task.Yield();

        // Generate quads
        for (int i = 0; i < numWisps; i++)
        {
            Wisp wisp = wisps[i];
            Quad quad = quads[i];
            RenderOrder r = renderOrder[i];

            wisp.Depth = (wisp.Point - cameraPos).sqrMagnitude;

            Color color = baseWispColor.Evaluate(Mathf.Repeat((time + (i * offsetMultiplier)) * colorCycleSpeed, 1f));
            color += heatWispColor.Evaluate(wisp.Heat);
            color = Color.Lerp(color, transitionColor, transitionColor.a);

            wisps[i] = wisp;
            int tileNum = wisp.TileOffset + currentTileNum;
            if (tileNum >= numTiles)
                tileNum -= numTiles;

            quad = Quad.FromWisp(color, wisp.Point, wisp.Radius + (wisp.Heat * heatRadiusMultiplier), cameraUp, cameraRht, tileOffsets[tileNum], tileScale);
            quads[i] = quad;

            r.Index = i;
            r.Depth = wisp.Depth;
            renderOrder[i] = r;
        }

        renderOrder.Sort(delegate (RenderOrder r1, RenderOrder r2) { return r2.Depth.CompareTo(r1.Depth); });
    }

    private float AddNoise(float value, NoiseSetting setting, float time, int offset)
    {
        value += (float)noise.Evaluate(value * setting.Scale, time + offset * setting.Speed) * setting.Intensity;
        return value;
    }
    
    private Vector3 AddNoise(Vector3 point, NoiseSetting setting, float time, int offset)
    {
        point.x += (float)noise.Evaluate((offset * 0.92347) + point.x * setting.Scale, time * setting.Speed) * setting.Intensity;
        point.y += (float)noise.Evaluate((offset * 0.23474) + point.y * setting.Scale, time * setting.Speed) * setting.Intensity;
        point.z += (float)noise.Evaluate((offset * 0.34786) + point.z * setting.Scale, time * setting.Speed) * setting.Intensity;
        return point;
    }

    private void DrawWisps()
    {
        GL.PushMatrix();
        wispMat.SetPass(0);
        GL.Begin(GL.QUADS);

        for (int i = 0; i < numWisps; i++)
        {
            Quad quad = finalQuads[i];

            GL.Color(quad.Color);
            GL.TexCoord2(quad.UV1.x, quad.UV1.y);
            GL.Vertex(quad.C1);
            GL.TexCoord2(quad.UV2.x, quad.UV2.y);
            GL.Vertex(quad.C2);
            GL.TexCoord2(quad.UV3.x, quad.UV3.y);
            GL.Vertex(quad.C3);
            GL.TexCoord2(quad.UV4.x, quad.UV4.y);
            GL.Vertex(quad.C4);
        }

        GL.End();
        GL.PopMatrix();
    }
}
