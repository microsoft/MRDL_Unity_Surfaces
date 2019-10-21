// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class HandSurface : FingerSurface
{
    protected TrackedHandJoint[] handJointTypes = new TrackedHandJoint[]
    {
        TrackedHandJoint.ThumbDistalJoint,
        TrackedHandJoint.ThumbMetacarpalJoint,
        TrackedHandJoint.ThumbProximalJoint,

        TrackedHandJoint.IndexDistalJoint,
        TrackedHandJoint.IndexKnuckle,
        TrackedHandJoint.IndexMetacarpal,
        TrackedHandJoint.IndexMiddleJoint,

        TrackedHandJoint.MiddleDistalJoint,
        TrackedHandJoint.MiddleKnuckle,
        TrackedHandJoint.MiddleMetacarpal,
        TrackedHandJoint.MiddleMiddleJoint,

        TrackedHandJoint.RingDistalJoint,
        TrackedHandJoint.RingKnuckle,
        TrackedHandJoint.RingMetacarpal,
        TrackedHandJoint.RingMiddleJoint,

        TrackedHandJoint.PinkyDistalJoint,
        TrackedHandJoint.PinkyKnuckle,
        TrackedHandJoint.PinkyMetacarpal,
        TrackedHandJoint.PinkyMiddleJoint,
    };

    [Header("Joint objects")]
    [SerializeField]
    protected Transform[] handJoints;

    [SerializeField]
    protected Rigidbody[] handJointRigidBodies;
    [SerializeField]
    protected Collider[] handJointColliders;

    protected override void LateUpdate()
    {
        base.LateUpdate();

        if (!MixedRealityToolkit.IsInitialized)
            return;

        if (!Initialized)
            return;

        #region hand tracking
        IMixedRealityInputSystem inputSystem = MixedRealityToolkit.Instance.GetService<IMixedRealityInputSystem>();
        IMixedRealityHandJointService handJointService = (inputSystem as MixedRealityInputSystem).GetDataProvider<IMixedRealityHandJointService>();

        int jointIndex = 0;
        // Right hand
        foreach (TrackedHandJoint joint in handJointTypes)
        {
            if (handJointService.IsHandTracked(Handedness.Right))
            {
                Transform jointTransform = handJointService.RequestJointTransform(joint, Handedness.Right);
                handJoints[jointIndex].position = jointTransform.position;
                handJoints[jointIndex].rotation = jointTransform.rotation;
                handJoints[jointIndex].gameObject.SetActive(true);
            }
            else if (!Application.isEditor || disableInactiveFingersInEditor)
            {
                handJoints[jointIndex].gameObject.SetActive(false);
            }
            jointIndex++;
        }

        // Left hand
        foreach (TrackedHandJoint joint in handJointTypes)
        {
            if (handJointService.IsHandTracked(Handedness.Left))
            {
                Transform jointTransform = handJointService.RequestJointTransform(joint, Handedness.Left);
                handJoints[jointIndex].position = jointTransform.position;
                handJoints[jointIndex].rotation = jointTransform.rotation;
                handJoints[jointIndex].gameObject.SetActive(true);
            }
            else if (!Application.isEditor || disableInactiveFingersInEditor)
            {
                handJoints[jointIndex].gameObject.SetActive(false);
            }
            jointIndex++;
        }
        #endregion
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (!Initialized)
            return;

        #region physics
        // Disable finger colliders when menu is open to prevent them from interfering with menu operation
        if (menu != null)
        {
            for (int i = 0; i < handJointColliders.Length; i++)
            {
                handJointColliders[i].enabled = !menu.IsOpen;
            }
        }

        // Move rigid bodies to follow finger positions
        for (int i = 0; i < fingerRigidBodies.Length; i++)
        {
            if (handJoints[i].gameObject.activeSelf)
            {
                handJointRigidBodies[i].MovePosition(handJoints[i].position);
                handJointRigidBodies[i].MoveRotation(handJoints[i].rotation);
            }
        }
        #endregion
    }

#if UNITY_EDITOR
    [MenuItem("Surfaces/GenerateHandJoints")]
    private static void GenerateHandJoints()
    {
        HandSurface handSurface = GameObject.FindObjectOfType<HandSurface>();

        if (handSurface == null)
            return;

        List<Transform> handJointsList = new List<Transform>(handSurface.handJoints);
        List<Rigidbody> handJointRigidBodiesList = new List<Rigidbody>(handSurface.handJointRigidBodies);
        List<Collider> handJointCollidersList = new List<Collider>(handSurface.handJointColliders);

        foreach (TrackedHandJoint handJointType in handSurface.handJointTypes)
        {
            GameObject newJoint = new GameObject(handJointType.ToString());
            newJoint.transform.parent = handSurface.transform;

            GameObject newJointCollider = new GameObject(handJointType.ToString() + " Collider");
            newJointCollider.transform.parent = handSurface.transform;
            Rigidbody rb = newJointCollider.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            Collider c = newJointCollider.AddComponent<SphereCollider>();

            handJointsList.Add(newJoint.transform);
            handJointRigidBodiesList.Add(rb);
            handJointCollidersList.Add(c);
        }

        handSurface.handJoints = handJointsList.ToArray();
        handSurface.handJointRigidBodies = handJointRigidBodiesList.ToArray();
        handSurface.handJointColliders = handJointCollidersList.ToArray();
    }
#endif
}
