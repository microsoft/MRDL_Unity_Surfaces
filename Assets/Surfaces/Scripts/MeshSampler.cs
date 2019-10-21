// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

public struct MeshSample
{
    public int Index;
    public Vector3 Point;
    public Vector3 Normal;
    public Vector2 UV;
    public int TriangleIndex;
    public Vector3 BarycentricCoordinate;
}

public struct DispSample
{
    public bool Empty;
    public Vector3 Point;
    public Vector3 Normal;
    public Vector2 UV;

    public static DispSample Lerp(DispSample s1, DispSample s2, float t)
    {
        s1.Point = Vector3.Lerp(s1.Point, s2.Point, t);
        s1.Normal = Vector3.Lerp(s2.Normal, s2.Normal, t);
        s1.UV = Vector2.Lerp(s1.UV, s2.UV, t);
        return s1;
    }

    internal static DispSample Blend(DispSample s1, DispSample s2, DispSample s3, Vector3 bary)
    {
        s1.Point = ((bary.x * s1.Point) + (bary.y * s2.Point) + (bary.z * s3.Point));
        s1.Normal = ((bary.x * s1.Normal) + (bary.y * s2.Normal) + (bary.z * s3.Normal));
        return s1;
    }
}

public class MeshSampler : MonoBehaviour
{
    const int processedWeightMultiplier = 1000;

    public MeshSample[] Samples => samples;

    public Bounds Bounds => mesh.bounds;

    [SerializeField]
    private Mesh mesh;
    [SerializeField]
    private int numPointsToSample = 10;
    [SerializeField]
    private int dispMapResX = 128;
    [SerializeField]
    private int dispMapResY = 128;
    [SerializeField]
    private Vector2 dispMapUvTest;
    [SerializeField]
    private int dispMapTriIndexTest;
    [SerializeField]
    [Range(1,8)]
    private int uvChannel = 1;

    private MeshSample[] samples = new MeshSample[0];
    private int[] triWeights;
    private int totalTriWeight;
    private Vector3[] verts;
    private Vector3[] normals;
    private Vector2[] uvs;
    private int[] tris;
    private DispSample[,] dispMapSphere;
    private bool meshDigested;

    public void SampleMesh(bool transformPoints = false)
    {
        DigestMesh();
        List<MeshSample> samples = new List<MeshSample>();
        for (int i = 0; i < numPointsToSample; i++)
        {
            samples.Add(SampleRandomPoint(i, transformPoints));
        }
        this.samples = samples.ToArray();
    }

    MeshSample SampleRandomPoint(int index, bool transformPoint = false)
    {
        MeshSample sample = default(MeshSample);

        sample.Index = index;
        sample.TriangleIndex = GetRandomTriangleIndex();
        Vector3 BC = Random.insideUnitSphere.normalized;
        BC.x = Random.value;
        BC.y = 1f - BC.x;
        BC.z = 1f - BC.x - BC.z;
        sample.BarycentricCoordinate = BC;        

        Vector3 P1 = verts[tris[sample.TriangleIndex + 0]];
        Vector3 P2 = verts[tris[sample.TriangleIndex + 1]];
        Vector3 P3 = verts[tris[sample.TriangleIndex + 2]];

        Vector2 UV1 = uvs[tris[sample.TriangleIndex + 0]];
        Vector2 UV2 = uvs[tris[sample.TriangleIndex + 0]];
        Vector2 UV3 = uvs[tris[sample.TriangleIndex + 0]];

        UV1 = Vector2.Lerp(UV1, UV2, BC.x);
        UV2 = Vector2.Lerp(UV2, UV3, BC.y);

        P1 = Vector3.Lerp(P1, P2, BC.x);
        P1 = Vector3.Lerp(P1, P3, BC.y);
        sample.Point = P1;
        sample.UV = UV1;

        if (transformPoint)
            sample.Point = transform.TransformPoint(sample.Point);

        // Interpolated vertex normal
        Vector3 N1 = normals[tris[sample.TriangleIndex + 0]];
        Vector3 N2 = normals[tris[sample.TriangleIndex + 1]];
        Vector3 N3 = normals[tris[sample.TriangleIndex + 2]];
        N1 = Vector3.Lerp(N1, N2, BC.x);
        N1 = Vector3.Lerp(N1, N3, BC.y);
        sample.Normal = N1;
        return sample;
    }

    public Vector3 SampleSphereDispMap(Vector2 uv)
    {
        uv.x = Mathf.Repeat(uv.x, 1);
        uv.y = Mathf.Repeat(uv.y, 1);

        return Vector3.zero;
    }

    public DispSample SampleRandomSphereDistMap()
    {
        DispSample sample = default(DispSample);

        int maxIterations = 500;
        for (int i = 0; i < maxIterations; i++)
        {
            int randomX = Random.Range(0, dispMapResX);
            int randomY = Random.Range(0, dispMapResY);
            sample = dispMapSphere[randomX, randomY];

            if (!sample.Empty)
                break;
        }

        return sample;
    }

    public void CreateSphereDispMap(bool transformPoint = false)
    {
        DigestMesh();

        int xRes = dispMapResX;
        int yRes = dispMapResY;
        DispSample sample = new DispSample();

        dispMapSphere = new DispSample[xRes, yRes];
        for (int x = 0; x < xRes; x++)
        {
            for (int y = 0; y < yRes; y++)
            {
                Vector2 uv = new Vector2(1f / xRes * x, 1f / yRes * y);
                Vector3 meshPos = Vector3.zero;

                sample.Empty = true;
                sample.UV = uv;

                int numTris = tris.Length / 3;
                for (int triIndex = 0; triIndex < numTris; triIndex++)
                {
                    if (GetMeshPointFromUV(uv, triIndex, out meshPos))
                    {
                        sample.Empty = false;
                        sample.Point = transformPoint ? transform.TransformPoint(meshPos) : meshPos;
                        sample.Normal = meshPos.normalized;
                        break;
                    }
                }

                dispMapSphere[x, y] = sample;
            }
        }
    }

    private int GetRandomTriangleIndex()
    {
        int randomValue = Random.Range(0, totalTriWeight);
        for (int i = 0; i < triWeights.Length; i++)
        {
            if (randomValue <= triWeights[i])
                return i * 3;

            randomValue -= triWeights[i];
        }
        return 0;
    }

    private void DigestMesh()
    {
        if (meshDigested)
            return;

        tris = mesh.triangles;
        verts = mesh.vertices;
        normals = mesh.normals;

        switch (uvChannel)
        {
            case 1: uvs = mesh.uv; break;
            case 2: uvs = mesh.uv2; break;
            case 3: uvs = mesh.uv3; break;
            case 4: uvs = mesh.uv4; break;
            case 5: uvs = mesh.uv5; break;
            case 6: uvs = mesh.uv6; break;
            case 7: uvs = mesh.uv7; break;
            case 8: uvs = mesh.uv8; break;
        }

        float[] triWeightsUnprocessed = new float[tris.Length / 3];

        int weightIndex = 0;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = verts[tris[i + 0]];
            Vector3 b = verts[tris[i + 1]];
            Vector3 c = verts[tris[i + 2]];

            Vector3 u = a - b;
            Vector3 v = c - b;

            float weight = Vector3.Cross(u, v).magnitude / 2;
            triWeightsUnprocessed[weightIndex] = weight;
            weightIndex++;
        }

        // Normalize weights
        totalTriWeight = 0;
        triWeights = new int[triWeightsUnprocessed.Length];
        for (int i = 0; i < triWeightsUnprocessed.Length; i++)
        {
            triWeights[i] = Mathf.CeilToInt(triWeightsUnprocessed[i] * processedWeightMultiplier);
            totalTriWeight += triWeights[i];
        }

        meshDigested = true;
    }

    public MeshSample RandomSample()
    {
        return Samples[Random.Range(0, Samples.Length)];
    }

    internal MeshSample RandomSample(Vector3 point, float searchRadius)
    {
        float sqrSearchRadius = searchRadius * searchRadius;
        List<int> samplesInRange = new List<int>();
        for (int i = 0; i < samples.Length; i++)
        {
            MeshSample sample = samples[i];
            float sqrDist = (sample.Point - point).sqrMagnitude;
            if (sqrDist < sqrSearchRadius)
                samplesInRange.Add(i);
        }
        return samples[samplesInRange[Random.Range(0, samplesInRange.Count)]];
    }

    internal MeshSample ClosestSample(Vector3 point)
    {
        float closestDistSoFar = Mathf.Infinity;
        int closestIndex = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            MeshSample sample = samples[i];
            float sqrDist = (sample.Point - point).sqrMagnitude;
            if (sqrDist < closestDistSoFar)
            {
                closestDistSoFar = sqrDist;
                closestIndex = i;
            }
        }
        return samples[closestIndex];
    }

    internal DispSample GetSphereDispMapSample(Vector2 uv)
    {
        uv.x = Mathf.Clamp01(uv.x);
        uv.y = Mathf.Clamp01(uv.y);

        int x = Mathf.FloorToInt(uv.x * dispMapResX);
        int y = Mathf.FloorToInt(uv.y * dispMapResY);
        return dispMapSphere[x, y];
    }

    internal DispSample GetSphereDispMapSampleSmooth(Vector2 uv)
    {
        uv.x = Mathf.Clamp01(uv.x);
        uv.y = Mathf.Clamp01(uv.y);

        int xFloor = Mathf.Clamp(Mathf.FloorToInt(uv.x * dispMapResX - 1), 0, dispMapResX - 1);
        int yFloor = Mathf.Clamp(Mathf.FloorToInt(uv.y * dispMapResY - 1), 0, dispMapResY - 1);
        int xCeil = Mathf.Clamp(Mathf.CeilToInt(uv.x * dispMapResX - 1), 0, dispMapResX - 1);
        int yCeil = Mathf.Clamp(Mathf.CeilToInt(uv.y * dispMapResY - 1), 0, dispMapResY - 1);

        DispSample s1 = dispMapSphere[xFloor, yFloor];
        DispSample s2 = dispMapSphere[xCeil, yFloor];
        DispSample s3 = dispMapSphere[xCeil, yCeil];
        DispSample s4 = dispMapSphere[xFloor, yCeil];

        float xBlockSize = 1f / dispMapResX;
        float yBlockSize = 1f / dispMapResY;
        float xCeilNorm = (float)xCeil / dispMapResX;
        float yCeilNorm = (float)yCeil / dispMapResY;

        float xDiffFloor = (uv.x - xCeilNorm) / xBlockSize;
        float yDiffFloor = (uv.y - yCeilNorm) / yBlockSize;
        float xDiffCeil = 1f - xDiffFloor;
        float yDiffCeil = 1f - xDiffFloor;

        Debug.Log(xDiffFloor + " : " + yDiffFloor);

        float s2Weight = (xDiffCeil + yDiffFloor) / 2;
        float s3Weight = (xDiffCeil + yDiffCeil) / 2;
        float s4Weight = (xDiffFloor + yDiffCeil) / 2;

        s1 = DispSample.Lerp(s1, s2, s2Weight);
        s1 = DispSample.Lerp(s1, s3, s3Weight);
        s1 = DispSample.Lerp(s1, s4, s4Weight);

        return s1;
    }

    private bool GetMeshPointFromUV(Vector2 uv, int triIndex, out Vector3 localPos)
    {
        localPos = Vector3.zero;

        // Getting the UV coordinates of the triangle
        Vector2 UV1 = uvs[tris[triIndex * 3 + 0]];
        Vector2 UV2 = uvs[tris[triIndex * 3 + 1]];
        Vector2 UV3 = uvs[tris[triIndex * 3 + 2]];

        // Getting the Vertex positions of the triangle
        Vector3 Vert1 = verts[tris[triIndex * 3 + 0]];
        Vector3 Vert2 = verts[tris[triIndex * 3 + 1]];
        Vector3 Vert3 = verts[tris[triIndex * 3 + 2]];

        // Get the barycentric values
        Vector3 bary = GetBary(UV1, UV2, UV3, uv);

        if (InTriangle(bary))
        {
            localPos = (bary.x * Vert1) + (bary.y * Vert2) + (bary.z * Vert3);
            return true;
        }

        return false;
    }
    
    Vector3 GetBary(Vector2 v1, Vector2 v2, Vector2 v3, Vector2 p)
    {
        Vector3 B = new Vector3();
        B.x = ((v2.y - v3.y) * (p.x - v3.x) + (v3.x - v2.x) * (p.y - v3.y)) / ((v2.y - v3.y) * (v1.x - v3.x) + (v3.x - v2.x) * (v1.y - v3.y));
        B.y = ((v3.y - v1.y) * (p.x - v3.x) + (v1.x - v3.x) * (p.y - v3.y)) / ((v3.y - v1.y) * (v2.x - v3.x) + (v1.x - v3.x) * (v2.y - v3.y));
        B.z = 1 - B.x - B.y;
        return B;
    }

    bool InTriangle(Vector3 barycentric)
    {
        return (barycentric.x >= 0.0f) && (barycentric.x <= 1.0f)
             && (barycentric.y >= 0.0f) && (barycentric.y <= 1.0f)
             && (barycentric.z >= 0.0f); //(barycentric.z <= 1.0f)
    }

    private void OnDrawGizmos()
    {
        if (dispMapSphere != null)
        {
            Gizmos.color = Color.red;
            for (int x = 0; x < dispMapSphere.GetLength(0); x++)
            {
                for (int y = 0; y < dispMapSphere.GetLength(1); y++)
                {
                    DispSample sample = dispMapSphere[x, y];
                    if (sample.Empty)
                        continue;

                    Gizmos.DrawLine(sample.Point, sample.Point + (sample.Normal * 0.01f));
                }
            }
        }

        foreach (MeshSample currentSample in samples)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(currentSample.Point, 0.01f);
            Gizmos.DrawLine(currentSample.Point, currentSample.Point + currentSample.Normal * 0.2f);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(MeshSampler))]
    public class MeshSamplerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            MeshSampler ms = (MeshSampler)target;

            if (GUILayout.Button("Digest Mesh"))
            {
                ms.DigestMesh();
            }
            if (GUILayout.Button("Sample Random Points"))
            {
                ms.SampleMesh();
            }
            if (GUILayout.Button("Create Spherical Disp Map"))
            {
                ms.CreateSphereDispMap(true);
            }
        }
    }
#endif
}