// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MRDL
{    public class BubbleSimple : MonoBehaviour, IBubbleSimple
    {
        public List<InnerBubble> InnerBubbles { get { return innerBubbles; } }

        public Action<int> OnEnterBubble { get; set; }
        public Action<int> OnExitBubble { get; set; }

        public float MinInnerBubbleRadius { get { return minInnerBubbleRadius; } }

        public float Solidity { get { return solidity; } set { solidity = Mathf.Clamp01(value); } }

        public float TurbulenceMultiplier
        {
            get { return turbulenceMultiplier; }
            set { turbulenceMultiplier = value; }
        }

        public float TurbulenceSpeed
        {
            get { return turbulenceSpeed; }
            set { turbulenceSpeed = value; }
        }

        public float Radius
        {
            get { return radius; }
            set { radius = Mathf.Clamp(value, 0.001f, float.MaxValue); }
        }

        public float RadiusMultiplier
        {
            get { return radiusMultiplier; }
            set { radiusMultiplier = value; }
        }

        public Material MaterialInstance
        {
            get { return meshRenderer.material; }
            set { meshRenderer.material = value; }
        }

        public int RecursionLevel
        {
            get
            {
                return recursionLevel;
            }
            set
            {
                if (recursionLevel != value)
                {
                    recursionLevel = value;
                    ClearMesh();
                }
            }
        }

        [Header("Main Settings")]
        [SerializeField]
        protected float radius;
        [SerializeField]
        protected float radiusMultiplier = 0f;
        [SerializeField]
        private bool useVertexNoise = false;
        [SerializeField]
        protected bool useVertexColors = false;
        [SerializeField]
        protected Color defaultVertexColor = Color.white;

        [Header("Rendering")]
        [SerializeField]
        protected MeshFilter meshFilter;
        [SerializeField]
        protected MeshRenderer meshRenderer;
        [SerializeField]
        protected int recursionLevel = 3;
        [SerializeField]
        protected float normalAngle = 60f;

        [Header("Forces")]
        [SerializeField]
        protected AnimationCurve atomicForceCurve;
        [SerializeField]
        protected AnimationCurve radialForceCurve;
        [SerializeField]
        protected AnimationCurve innerBubbleForceCurve;
        [SerializeField]
        protected float atomicForceMultiplier = 1f;
        [SerializeField]
        protected float radialForceMultiplier = 1f;
        [SerializeField]
        protected float innerBubbleForceMultiplier = 1f;
        [SerializeField]
        protected float radialForceInertia = 2f;
        [SerializeField]
        protected float atomicForceInertia = 2f;
        [SerializeField]
        protected float innerBubbleForceInertia = 2f;

        [Header("Turbulence")]
        [SerializeField]
        protected float turbulenceMultiplier;
        [SerializeField]
        protected float turbulenceSpeed;
        [SerializeField]
        protected float turbulenceScale;

        [Header("Inner bubbles")]
        [SerializeField]
        protected List<InnerBubble> innerBubbles = new List<InnerBubble>();
        [SerializeField]
        protected float minInnerBubbleRadius = 0.5f;
        [SerializeField]
        protected float solidity = 0f;

        [Header("Gizmos")]
        [SerializeField]
        protected bool drawForces = true;
        [SerializeField]
        protected int forceDrawSkipInterval = 3;

        protected Mesh sphereMesh;
        protected Vector3[] radialForces = new Vector3[0];
        protected Vector3[] atomicForces = new Vector3[0];
        protected Vector3[] innerBubbleForces = new Vector3[0];
        protected Vector3[] currentVertices = new Vector3[0];
        protected Vector3[] targetVertices = new Vector3[0];
        protected Vector3[] originalVertices = new Vector3[0];
        protected Color[] currentColors = new Color[0];
        protected FastSimplexNoise noise;
        protected int numVertices;
        protected int numInnerBubbles;
        protected Vector3 bubbleWorldPos;
        protected Vector3 currentVertex;
        protected Vector3 originalVertex;
        protected Vector3 zero;
        protected Color vertexColor;

        protected float timeLastUpdated;
        protected float deltaTime;
        protected float time;
        protected float adjustedRadius;
        protected bool updatingBubble;

        protected const int tableResolution = 1024;
        protected float[] atomicForceTable;
        protected float[] radialForceTable;
        protected float[] innerBubbleForceTable;

        protected virtual void PrepareForUpdate()
        {
            // Set our main thread stuff
            numVertices = originalVertices.Length;
            numInnerBubbles = InnerBubbles.Count;
            time = Time.time;
            deltaTime = time - timeLastUpdated;
            timeLastUpdated = time;
            adjustedRadius = radius * (1 + radiusMultiplier);
            bubbleWorldPos = transform.position;

            for (int i = 0; i < innerBubbles.Count; i++)
            {
                InnerBubble b = innerBubbles[i];
                b.Active = b.Transform != null && b.Transform.gameObject.activeSelf;
                b.InnerPos = transform.InverseTransformPoint(b.Transform.position);
                b.DistToCenter = Vector3.Distance(b.Transform.position, transform.position);
                if (b.DistToCenter < radius - b.Radius)
                {
                    if (!b.InsideBubble)
                        OnEnterBubble?.Invoke(i);

                    b.InsideBubble = true;
                }
                else
                {
                    if (b.InsideBubble)
                        OnExitBubble?.Invoke(i);

                    b.InsideBubble = false;
                }
                innerBubbles[i] = b;
            }
        }

        protected virtual async Task UpdateBubbleAsync()
        {
            while (updatingBubble)
            {
                // Get back onto the main thread before doing the rest
                await new WaitForUpdate();

                PrepareForUpdate();

                radiusMultiplier = Mathf.Lerp(radiusMultiplier, 0f, deltaTime);

                if (useVertexColors)
                    sphereMesh.SetColors(currentColors);

                sphereMesh.SetVertices(currentVertices);
                sphereMesh.RecalculateBounds();
                sphereMesh.RecalculateNormals();
                sphereMesh.RecalculateTangents();

                // Do the update force work on a background thread
                await new WaitForBackgroundThread();

                UpdateForces();
            }
        }

        private void OnEnable()
        {
            if (noise == null)
                noise = new FastSimplexNoise();

            CheckMesh();
            GenerateTables();
            updatingBubble = true;
            Task task = UpdateBubbleAsync();
            task.ConfigureAwait(false);
        }

        private void OnDisable()
        {
            ClearMesh();
            updatingBubble = false;
        }

        protected virtual void OnDrawGizmos()
        {
            if (drawForces)
            {
                for (int i = 0; i < currentVertices.Length; i += forceDrawSkipInterval)
                {
                    if (innerBubbleForces[i].magnitude > 0.002f)
                    {
                        Gizmos.color = Color.cyan;
                    }
                    else
                    {
                        Gizmos.color = Color.yellow;
                    }
                    Gizmos.DrawWireSphere(transform.TransformPoint(currentVertices[i]), radius * 0.02f);
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(transform.TransformPoint(currentVertices[i]), transform.TransformPoint(currentVertices[i] + radialForces[i]));
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(transform.TransformPoint(currentVertices[i]), transform.TransformPoint(currentVertices[i] + atomicForces[i]));
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(transform.TransformPoint(currentVertices[i]), transform.TransformPoint(currentVertices[i] + innerBubbleForces[i]));
                }
            }

            Gizmos.color = Color.Lerp(Color.red, Color.clear, 0.65f);
            for (int i = 0; i < innerBubbles.Count; i++)
            {
                float innerBubbleRadius = Mathf.Max(minInnerBubbleRadius * radius, innerBubbles[i].Radius);
                Gizmos.DrawSphere(innerBubbles[i].Transform.position, innerBubbleRadius);
            }
        }

        protected void CheckMesh()
        {
            if (sphereMesh == null && gameObject.activeSelf)
            {
                sphereMesh = new Mesh();
                IcoSphere.Create(sphereMesh, 1f, recursionLevel);
                meshFilter.sharedMesh = sphereMesh;
                originalVertices = sphereMesh.vertices;

                ClearForces();
            }
        }

        protected virtual void ClearForces()
        {
            radialForces = new Vector3[originalVertices.Length];
            atomicForces = new Vector3[originalVertices.Length];
            innerBubbleForces = new Vector3[originalVertices.Length];
            targetVertices = new Vector3[originalVertices.Length];
            currentVertices = new Vector3[originalVertices.Length];
            currentColors = new Color[originalVertices.Length];
            for (int i = 0; i < originalVertices.Length; i++)
            {
                targetVertices[i] = (originalVertices[i] * radius);
                currentVertices[i] = (originalVertices[i] * radius);
                currentColors[i] = defaultVertexColor;
            }
        }

        protected virtual void GenerateTables()
        {
            atomicForceTable = new float[tableResolution];
            radialForceTable = new float[tableResolution];
            innerBubbleForceTable = new float[tableResolution];

            for (int i = 0; i < tableResolution; i++)
            {
                atomicForceTable[i] = atomicForceCurve.Evaluate((float)i / tableResolution);
                radialForceTable[i] = radialForceCurve.Evaluate((float)i / tableResolution);
                innerBubbleForceTable[i] = innerBubbleForceCurve.Evaluate((float)i / tableResolution);
            }
        }

        protected virtual void UpdateForces()
        {
            for (int i = 0; i < numVertices; i++)
            {
                currentColors[i] = defaultVertexColor;
                currentVertex = currentVertices[i];
                originalVertex = GetOriginalVertex(originalVertices[i]);

                CalculateRadialForces(currentVertex, originalVertex, i);
                CalculateAtomicForces(currentVertex, i, numVertices);
                CalculateInternalBubbleForces(currentVertex, i);

                currentVertices[i] = ApplyForces(currentVertex, originalVertex, i);
            }
        }

        protected virtual Vector3 GetOriginalVertex(Vector3 originalVertex)
        {
            // Modify original vertex by adjustedRadius and add local offset
            // This simulates 'volume'
            originalVertex.x *= adjustedRadius;
            originalVertex.y *= adjustedRadius;
            originalVertex.z *= adjustedRadius;

            return originalVertex;
        }

        protected virtual Vector3 ApplyForces(Vector3 currentVertex, Vector3 originalVertex, int vertexIndex)
        {
            currentVertex = currentVertex + ((radialForces[vertexIndex] + atomicForces[vertexIndex] + innerBubbleForces[vertexIndex]));

            // Last but not least, apply bubble solidity
            if (solidity > 0)
            {
                currentVertex = Vector3.Lerp(currentVertex, originalVertex, solidity);
            }

            return currentVertex;
        }

        protected void CalculateRadialForces(Vector3 currentVertex, Vector3 originalVertex, int vertexIndex)
        {
            Vector3 currentRadialForce = radialForces[vertexIndex];
            Vector3 newRadialForce = zero; //Vector3.zero;
            Vector3 distanceVector = zero; //Vector3.zero;

            distanceVector.x = originalVertex.x - currentVertex.x;
            distanceVector.y = originalVertex.y - currentVertex.y;
            distanceVector.z = originalVertex.z - currentVertex.z;

            float sqrDistanceToTarget = (distanceVector.x * distanceVector.x + distanceVector.y * distanceVector.y + distanceVector.z * distanceVector.z);
            float normalizedDistToTarget = sqrDistanceToTarget / (adjustedRadius * adjustedRadius);

            if (normalizedDistToTarget > 1)
                normalizedDistToTarget = 1;
            if (normalizedDistToTarget < 0)
                normalizedDistToTarget = 0;

            float radialForce = radialForceTable[(int)(normalizedDistToTarget * (tableResolution - 1))] * radialForceMultiplier;//radialForceCurve.Evaluate(normalizedDistToTarget) * radialForceMultiplier;

            newRadialForce = distanceVector;
            newRadialForce.x *= radialForce;
            newRadialForce.y *= radialForce;
            newRadialForce.z *= radialForce;

            if (useVertexNoise)
            {
                // Add random noise to force
                newRadialForce.x += ((float)noise.Evaluate(currentVertex.x * turbulenceScale, time * turbulenceSpeed) * turbulenceMultiplier);
                newRadialForce.y += ((float)noise.Evaluate(currentVertex.y * turbulenceScale, time * turbulenceSpeed) * turbulenceMultiplier);
                newRadialForce.z += ((float)noise.Evaluate(currentVertex.z * turbulenceScale, time * turbulenceSpeed) * turbulenceMultiplier);
            }

            newRadialForce = Vector3.Lerp(currentRadialForce, newRadialForce, deltaTime * radialForceInertia);
            radialForces[vertexIndex] = newRadialForce;
        }

        protected void CalculateAtomicForces(Vector3 currentVertex, int vertexIndex, int numVertices)
        {
            Vector3 otherVertex = zero;// Vector3.zero;
            Vector3 distanceVector = zero;//Vector3.zero;
            Vector3 newAtomicForce = zero;//Vector3.zero;
            Vector3 currentAtomicForce = atomicForces[vertexIndex];

            for (int j = 0; j < numVertices; j++)
            {
                if (vertexIndex == j)
                    continue;

                otherVertex = currentVertices[j];

                distanceVector.x = currentVertex.x - otherVertex.x;
                distanceVector.y = currentVertex.y - otherVertex.y;
                distanceVector.z = currentVertex.z - otherVertex.z;

                float sqrDistanceToOtherVertex = (distanceVector.x * distanceVector.x + distanceVector.y * distanceVector.y + distanceVector.z * distanceVector.z);
                float normalizedDistToOtherVertex = sqrDistanceToOtherVertex / (adjustedRadius * adjustedRadius);
                if (normalizedDistToOtherVertex > 1)
                    normalizedDistToOtherVertex = 1;
                if (normalizedDistToOtherVertex < 0)
                    normalizedDistToOtherVertex = 0;

                float forceToOtherVertex = atomicForceTable[(int)(normalizedDistToOtherVertex * (tableResolution - 1))] * atomicForceMultiplier;//atomicForceCurve.Evaluate(normalizedDistToOtherVertex) * atomicForceMultiplier;

                if (forceToOtherVertex <= 0)
                    continue;

                newAtomicForce.x += (distanceVector.x * forceToOtherVertex);
                newAtomicForce.y += (distanceVector.y * forceToOtherVertex);
                newAtomicForce.z += (distanceVector.z * forceToOtherVertex);
            }

            newAtomicForce = Vector3.Lerp(currentAtomicForce, newAtomicForce, deltaTime * atomicForceInertia);
            atomicForces[vertexIndex] = newAtomicForce;
        }

        protected void CalculateInternalBubbleForces(Vector3 currentVertex, int vertexIndex)
        {
            Vector3 innerBubblePos = zero;//Vector3.zero;
            Vector3 vertexDistVector = zero;//Vector3.zero;

            Vector3 currentInnerBubbleForce = innerBubbleForces[vertexIndex];
            Vector3 newInnerBubbleForce = zero;//Vector3.zero;

            for (int i = 0; i < numInnerBubbles; i++)
            {
                InnerBubble innerBubble = innerBubbles[i];
                if (!innerBubble.Active)
                    continue;

                innerBubblePos = innerBubble.InnerPos;

                vertexDistVector.x = currentVertex.x - innerBubblePos.x;
                vertexDistVector.y = currentVertex.y - innerBubblePos.y;
                vertexDistVector.z = currentVertex.z - innerBubblePos.z;

                float innerBubbleRadius = Mathf.Max(minInnerBubbleRadius * adjustedRadius, innerBubble.Radius);
                float sqrDistToInnerBubble = (vertexDistVector.x * vertexDistVector.x + vertexDistVector.y * vertexDistVector.y + vertexDistVector.z * vertexDistVector.z);
                float normalizedDistToInnerBubble = sqrDistToInnerBubble / (innerBubbleRadius * innerBubbleRadius);
                if (normalizedDistToInnerBubble >= 1f)
                    continue;

                if (normalizedDistToInnerBubble < 0)
                    normalizedDistToInnerBubble = 0;

                // Make the force direction come from the inner-most point of the inner bubble
                innerBubblePos -= (innerBubblePos.normalized * innerBubbleRadius);
                // Then re-calculate the direction from the bubble pos to the vertex
                vertexDistVector.x = currentVertex.x - innerBubblePos.x;
                vertexDistVector.y = currentVertex.y - innerBubblePos.y;
                vertexDistVector.z = currentVertex.z - innerBubblePos.z;

                // If the inner bubble is outside the radius, push the surface in
                // Otherwise, push the surface out
                if (innerBubble.DistToCenter > (radius - innerBubble.Radius))
                {
                    vertexDistVector.x = -vertexDistVector.x;
                    vertexDistVector.y = -vertexDistVector.y;
                    vertexDistVector.z = -vertexDistVector.z;
                }

                float forceToInnerBubble = innerBubbleForceTable[(int)(normalizedDistToInnerBubble * (tableResolution - 1))] * innerBubbleForceMultiplier;//innerBubbleForceCurve.Evaluate(normalizedDistToInnerBubble) * innerBubbleForceMultiplier;

                newInnerBubbleForce.x += (vertexDistVector.x * forceToInnerBubble);
                newInnerBubbleForce.y += (vertexDistVector.y * forceToInnerBubble);
                newInnerBubbleForce.z += (vertexDistVector.z * forceToInnerBubble);

                if (useVertexColors && innerBubble.UseColor)
                {
                    vertexColor = currentColors[vertexIndex];
                    float alpha = Mathf.Max(vertexColor.a, normalizedDistToInnerBubble * normalizedDistToInnerBubble);
                    vertexColor = Color.Lerp(vertexColor, innerBubble.Color, normalizedDistToInnerBubble);
                    vertexColor.a = alpha;
                    currentColors[vertexIndex] = vertexColor;
                }
            }

            newInnerBubbleForce = Vector3.Lerp(currentInnerBubbleForce, newInnerBubbleForce, deltaTime * innerBubbleForceInertia);
            innerBubbleForces[vertexIndex] = newInnerBubbleForce;
        }

        protected void ClearMesh()
        {
            if (sphereMesh != null)
            {
                GameObject.Destroy(sphereMesh);
            }

            ClearForces();
        }
    }
}
