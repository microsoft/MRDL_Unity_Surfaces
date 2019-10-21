// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using UnityEngine;

namespace Microsoft.MRDL
{
    public class BubbleTrails : BubbleSimple
    {
        [SerializeField]
        private bool drawTrails = false;
        [SerializeField]
        private BubbleTrailProps[] trailProps = new BubbleTrailProps[0];
        [SerializeField]
        private BubbleTrail[] trails;
        [SerializeField]
        private float trailBubbleInertia = 3f;
        [SerializeField]
        private float trailBubbleForceMultiplier = 5f;

        protected Vector3[] trailBubbleForces;
                
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (drawTrails)
            {
                Gizmos.color = Color.Lerp(Color.gray, Color.clear, 0.85f);
                for (int i = 0; i < trails.Length; i++)
                {
                    Gizmos.DrawSphere(trails[i].FinalWorldPosition, trails[i].Radius);
                }
            }
        }

        protected override void UpdateForces()
        {
            UpdateTrails();

            for (int i = 0; i < numVertices; i++)
            {
                currentColors[i] = Color.Lerp(currentColors[i], defaultVertexColor, deltaTime);
                currentVertex = currentVertices[i];
                originalVertex = GetOriginalVertex(originalVertices[i]);

                CalculateRadialForces(currentVertex, originalVertex, i);
                CalculateAtomicForces(currentVertex, i, numVertices);
                CalculateInternalBubbleForces(currentVertex, i);
                CalculateTrailForces(currentVertex, i);

                currentVertices[i] = ApplyForces(currentVertex, originalVertex, i);
            }
        }

        protected override void PrepareForUpdate()
        {
            base.PrepareForUpdate();

            for (int i = 0; i < trails.Length; i++)
            {
                BubbleTrail t = trails[i];
                t.InnerPosition = transform.InverseTransformPoint(t.FinalWorldPosition);
                trails[i] = t;
            }
        }

        protected void UpdateTrails()
        {
            Vector3 newTrailTargetPos = bubbleWorldPos;
            Vector3 newTrailPosition = Vector3.zero;
            Vector3 trailTargetDirection = Vector3.zero;
            float noiseMultiplier = 0f;
            float maxDistance = 0f;
            float newRadius = 0f;

            for (int i = 0; i < trails.Length; i++)
            {
                newTrailPosition = trails[i].BaseWorldPosition;

                if (i > 0)
                {
                    newTrailTargetPos = trails[i - 1].BaseWorldPosition;
                }

                newTrailPosition = Vector3.Lerp(newTrailPosition, newTrailTargetPos, Mathf.Clamp01 (deltaTime * trailProps[i].Inertia));

                float distanceToTarget = Vector3.Distance(newTrailPosition, newTrailTargetPos);
                maxDistance = trailProps[i].MaxDistance * adjustedRadius;

                if (distanceToTarget > maxDistance)
                {
                    distanceToTarget = maxDistance;
                    trailTargetDirection = (newTrailPosition - newTrailTargetPos).normalized;
                    newTrailPosition = newTrailTargetPos + (trailTargetDirection * distanceToTarget);
                }

                trails[i].BaseWorldPosition = newTrailPosition;
                trails[i] = ConstrainTrailPosition(trails[i]);
                newTrailPosition = trails[i].BaseWorldPosition;

                noiseMultiplier = (distanceToTarget / maxDistance) * trailProps[i].PosNoiseMultiplier;
                newTrailPosition.x += (float)noise.Evaluate(newTrailPosition.x + i, time + i * trailProps[i].NoiseSpeed) * noiseMultiplier;
                newTrailPosition.y += (float)noise.Evaluate(newTrailPosition.y + i, time + i * trailProps[i].NoiseSpeed) * noiseMultiplier;
                newTrailPosition.z += (float)noise.Evaluate(newTrailPosition.z + i, time + i * trailProps[i].NoiseSpeed) * noiseMultiplier;

                trails[i].FinalWorldPosition = newTrailPosition;

                noiseMultiplier = (distanceToTarget / trailProps[i].MaxDistance) * trailProps[i].RadiusNoiseMultiplier;
                newRadius = adjustedRadius * Mathf.Lerp(trailProps[i].BaseRadius, trailProps[i].MaxRadius, distanceToTarget / trailProps[i].MaxDistance);
                newRadius += (float)noise.Evaluate(newTrailPosition.x + newTrailPosition.y + newTrailPosition.z * trailProps[i].NoiseSpeed, deltaTime + i * trailProps[i].NoiseSpeed) * noiseMultiplier;

                trails[i].Radius = Mathf.Lerp(trails[i].Radius, newRadius, deltaTime * trailProps[i].Inertia);
            }
        }

        protected virtual BubbleTrail ConstrainTrailPosition(BubbleTrail trail)
        {
            return trail;
        }

        protected void CalculateTrailForces(Vector3 currentVertex, int vertexIndex)
        {
            Vector3 trailBubblePos;
            Vector3 trailBubbleForce = trailBubbleForces[vertexIndex];
            Vector3 newTrailBubbleForce = Vector3.zero;
            Vector3 distanceVector;

            // Add trail distortion
            for (int j = 0; j < trails.Length; j++)
            {
                trailBubblePos = trails[j].InnerPosition;// transform.InverseTransformPoint(trails[j].FinalWorldPosition);

                distanceVector.x = currentVertex.x - trailBubblePos.x;
                distanceVector.y = currentVertex.y - trailBubblePos.y;
                distanceVector.z = currentVertex.z - trailBubblePos.z;

                float sqrDistToTrailBubble = (distanceVector.x * distanceVector.x + distanceVector.y * distanceVector.y + distanceVector.z * distanceVector.z);
                float normalizedDistToTrailBubble = sqrDistToTrailBubble / (trails[j].Radius * trails[j].Radius);
                if (normalizedDistToTrailBubble >= 1f)
                    continue;

                if (normalizedDistToTrailBubble < 0)
                    normalizedDistToTrailBubble = 0;

                // Make the force direction come from the inner-most point of the inner bubble
                trailBubblePos -= (trailBubblePos.normalized * trails[j].Radius);
                // Then re-calculate the direction from the bubble pos to the vertex
                distanceVector.x = currentVertex.x - trailBubblePos.x;
                distanceVector.y = currentVertex.y - trailBubblePos.y;
                distanceVector.z = currentVertex.z - trailBubblePos.z;

                float forceToInnerBubble = innerBubbleForceTable[(int)(normalizedDistToTrailBubble * (tableResolution - 1))] * trailBubbleForceMultiplier; //innerBubbleForceCurve.Evaluate(normalizedDistToInnerBubble) * trailBubbleForceMultiplier;

                newTrailBubbleForce.x += (distanceVector.x * forceToInnerBubble);
                newTrailBubbleForce.y += (distanceVector.y * forceToInnerBubble);
                newTrailBubbleForce.z += (distanceVector.z * forceToInnerBubble);
            }
            
            newTrailBubbleForce = Vector3.Lerp(trailBubbleForce, newTrailBubbleForce, deltaTime * trailBubbleInertia);
            trailBubbleForces[vertexIndex] = newTrailBubbleForce;
        }

        protected override Vector3 ApplyForces(Vector3 currentVertex, Vector3 originalVertex, int vertexIndex)
        {
            currentVertex = currentVertex + ((radialForces[vertexIndex] + atomicForces[vertexIndex] + innerBubbleForces[vertexIndex] + trailBubbleForces[vertexIndex]));

            // Last but not least, apply bubble solidity
            if (solidity > 0)
                currentVertex = Vector3.Lerp(currentVertex, originalVertex, solidity);

            return currentVertex;
        }

        protected override void ClearForces()
        {
            base.ClearForces();

            trailBubbleForces = new Vector3[originalVertices.Length];

            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].FinalWorldPosition = transform.position;
                trails[i].BaseWorldPosition = transform.position;
            }
        }
    }
}