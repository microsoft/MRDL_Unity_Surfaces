// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MRDL;
using UnityEngine;

public class FingerSurface : MonoBehaviour
{
    public static FingerSurface ActiveSurface { get { return activeSurface; } }
    private static FingerSurface activeSurface;

    public bool Initialized { get; private set; }

    public virtual float SurfaceRadius { get { return 0.125f; } }

    public Vector3 SurfacePosition { get; protected set; }

    [Header("Finger objects")]
    [SerializeField]
    protected Transform[] fingers;
    [SerializeField]
    protected Transform[] palms;
    [SerializeField]
    protected bool disableInactiveFingersInEditor = true;

    [Header("Finger Physics")]
    [SerializeField]
    protected Rigidbody[] fingerRigidBodies;
    [SerializeField]
    protected Collider[] fingerColliders;
    [SerializeField]
    protected Rigidbody[] palmRigidBodies;
    [SerializeField]
    protected Collider[] palmColliders;

    [Header("Surface")]
    [SerializeField]
    protected Transform surfaceTransform;

    protected ContextualHandMenu menu;

    protected TrackedHandJoint[] fingerJointTypes = new TrackedHandJoint[]
    {
        TrackedHandJoint.ThumbTip,
        TrackedHandJoint.IndexTip,
        TrackedHandJoint.MiddleTip,
        TrackedHandJoint.RingTip,
        TrackedHandJoint.PinkyTip,
    };

    protected virtual void Awake()
    {
        menu = FindObjectOfType<ContextualHandMenu>();
        activeSurface = this;
        Initialized = false;

        SurfacePosition = surfaceTransform.position;
    }

    protected virtual void OnDestroy()
    {
        if (activeSurface == this)
            activeSurface = null;
    }

    protected virtual void LateUpdate()
    {
        if (!MixedRealityToolkit.IsInitialized)
            return;

        if (!Initialized)
            return;

        #region hand tracking
        IMixedRealityInputSystem inputSystem = MixedRealityToolkit.Instance.GetService<IMixedRealityInputSystem>();
        IMixedRealityHandJointService handJointService = (inputSystem as MixedRealityInputSystem).GetDataProvider<IMixedRealityHandJointService>();

        int fingerIndex = 0;
        // Right hand
        foreach (TrackedHandJoint joint in fingerJointTypes)
        {
            if (handJointService.IsHandTracked(Handedness.Right))
            {
                Transform jointTransform = handJointService.RequestJointTransform(joint, Handedness.Right);
                fingers[fingerIndex].position = jointTransform.position;
                fingers[fingerIndex].rotation = jointTransform.rotation;
                fingers[fingerIndex].gameObject.SetActive(true);
            }
            else if (!Application.isEditor || disableInactiveFingersInEditor)
            {
                fingers[fingerIndex].gameObject.SetActive(false);
            }
            fingerIndex++;
        }

        // Left hand
        foreach (TrackedHandJoint joint in fingerJointTypes)
        {
            if (handJointService.IsHandTracked(Handedness.Left))
            {
                Transform jointTransform = handJointService.RequestJointTransform(joint, Handedness.Left);
                fingers[fingerIndex].position = jointTransform.position;
                fingers[fingerIndex].rotation = jointTransform.rotation;
                fingers[fingerIndex].gameObject.SetActive(true);
            }
            else if (!Application.isEditor || disableInactiveFingersInEditor)
            {
                fingers[fingerIndex].gameObject.SetActive(false);
            }
            fingerIndex++;
        }

        if (palms.Length == 2)
        {
            if (handJointService.IsHandTracked(Handedness.Right))
            {
                Transform palmTransform = handJointService.RequestJointTransform(TrackedHandJoint.Palm, Handedness.Right);
                palms[0].gameObject.SetActive(true);
                palms[0].position = palmTransform.position;
                palms[0].rotation = palmTransform.rotation;
            }
            else if (!Application.isEditor || disableInactiveFingersInEditor)
            {
                palms[0].gameObject.SetActive(false);
            }

            if (handJointService.IsHandTracked(Handedness.Left))
            {
                Transform palmTransform = handJointService.RequestJointTransform(TrackedHandJoint.Palm, Handedness.Left);
                palms[1].gameObject.SetActive(true);
                palms[1].position = palmTransform.position;
                palms[1].rotation = palmTransform.rotation;
            }
            else if (!Application.isEditor || disableInactiveFingersInEditor)
            {
                palms[1].gameObject.SetActive(false);
            }
        }
        #endregion

        SurfacePosition = surfaceTransform.position;
    }

    public virtual void Initialize(Vector3 surfacePosition)
    {
        SurfacePosition = surfacePosition;
        surfaceTransform.position = surfacePosition;
        Initialized = true;
    }

    protected virtual void FixedUpdate()
    {
        if (!Initialized)
            return;

        #region physics
        // Disable finger colliders when menu is open to prevent them from interfering with menu operation
        if (menu != null)
        {
            for (int i = 0; i < fingerColliders.Length; i++)
            {
                fingerColliders[i].enabled = !menu.IsOpen;
            }

            for (int i = 0; i < palmColliders.Length; i++)
            {
                palmColliders[i].enabled = !menu.IsOpen;
            }
        }

        // Move rigid bodies to follow finger positions
        for (int i = 0; i < fingerRigidBodies.Length; i++)
        {
            if (fingers[i].gameObject.activeSelf)
            {
                fingerRigidBodies[i].MovePosition(fingers[i].position);
                fingerRigidBodies[i].MoveRotation(fingers[i].rotation);
            }
        }

        for (int i = 0; i < palmRigidBodies.Length; i++)
        {
            if (palms[i].gameObject.activeSelf)
            {
                palmRigidBodies[i].MovePosition(palms[i].position);
                palmRigidBodies[i].MoveRotation(palms[i].rotation);
            }
        }
        #endregion
    }
}