// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Threading.Tasks;
using UnityEngine;

namespace MRDL
{
    /// <summary>
    /// Uses spatial mesh to place the surface in a reasonable position.
    /// </summary>
    public class SurfacePlacement : MonoBehaviour
    {
        public static SurfacePlacement Instance { get { return instance; } }

        private static SurfacePlacement instance;

        [SerializeField]
        private Vector3 defaultOffset = new Vector3(0, -0.1f, 1.15f);
        [SerializeField]
        private float maxRepositionDistance = 2.5f;
        [SerializeField]
        private bool useSpatialUnderstanding = false;
        [SerializeField]
        private float moveIncrement = 0.05f;
        [SerializeField]
        private float timeOut = 3f;
        [SerializeField]
        private LayerMask spatialAwarenessMask;

        private Transform placementTransform;
        private Vector3 currentSurfacePosition;

        private bool placedOnce = false;
        private float timeStarted;

        private void Awake()
        {
            instance = this;
            placementTransform = new GameObject("PlacementTransform").transform;
            placementTransform.parent = transform;
        }

        public async Task PlaceNewSurface()
        {
            await Task.Yield();

            // Find our finger surface
            FingerSurface fingerSurface = GameObject.FindObjectOfType<FingerSurface>();        
            float fingerSurfaceRadius = fingerSurface.SurfaceRadius;

            // Use our camera to place the surface
            placementTransform.position = CameraCache.Main.transform.position;
            placementTransform.rotation = CameraCache.Main.transform.rotation;
            // Only rotate on y axis
            Vector3 eulerAngles = placementTransform.eulerAngles;
            eulerAngles.x = 0;
            eulerAngles.z = 0;
            placementTransform.eulerAngles = eulerAngles;

            Vector3 targetSurfacePosition = placementTransform.TransformPoint(defaultOffset);

            if (fingerSurface == null)
            {
                Debug.LogError("No surface found in surface placement.");
                return;
            }

            if (placedOnce)
            {   // If we've placed it once already, the current placement might still be valid.
                if (Vector3.Distance(CameraCache.Main.transform.position, currentSurfacePosition) < maxRepositionDistance)
                {
                    targetSurfacePosition = currentSurfacePosition;
                }
            }
            else if (useSpatialUnderstanding)
            {

                IMixedRealitySpatialAwarenessSystem spatial;
                if (!MixedRealityServiceRegistry.TryGetService<IMixedRealitySpatialAwarenessSystem>(out spatial))
                {
                    Debug.LogError("This component requires a IMixedRealitySpatialAwarenessSystem to be enabled.");
                    return;
                }

                // Start the observers creating meshes again
                spatial.ResumeObservers();

                // Wait until our spatial understanding has delivered some meshes
                timeStarted = Time.time;
                while (spatial.SpatialAwarenessObjectParent.transform.childCount == 0)
                {
                    if ((Time.time - timeStarted > timeOut))
                        break;

                    await Task.Yield();
                }

                // While our placement sphere overlaps with spatial awareness objects, move it progressively closer to the user
                bool collidesWithWalls = true;
                timeStarted = Time.time;
                while (collidesWithWalls)
                {
                    if (Time.time - timeStarted > timeOut)
                        break;

                    Collider[] colliders = Physics.OverlapSphere(targetSurfacePosition, fingerSurfaceRadius, spatialAwarenessMask);
                    if (colliders.Length == 0)
                    {
                        collidesWithWalls = false;
                    }
                    else
                    {
                        targetSurfacePosition = Vector3.MoveTowards(targetSurfacePosition, CameraCache.Main.transform.position, moveIncrement);
                    }

                    await Task.Yield();
                }

                // Suspeend observers now that we're all set
                spatial.SuspendObservers();
            }

            currentSurfacePosition = targetSurfacePosition;
            placedOnce = true;

            // Tell the surface to initialize
            fingerSurface.Initialize(targetSurfacePosition);
        }
    }
}