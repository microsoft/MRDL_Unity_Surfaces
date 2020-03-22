// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using System.Runtime.CompilerServices;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Extensions.SceneTransitions;
using Microsoft.MixedReality.Toolkit.Utilities;
using MRDL;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonLoadContentScene : MonoBehaviour
{
    [SerializeField]
    private LoadSceneMode loadSceneMode = LoadSceneMode.Single;
    [SerializeField]
    private string contentName;
    [SerializeField]
    private bool loadOnKeyPress = false;
    [SerializeField]
    private KeyCode keyCode;

    private OVROverlay ovrOverlay = null;

    private void OnEnable()
    {
        ovrOverlay = FindObjectOfType<OVROverlay>();
        if (ovrOverlay != null)
        {
            ovrOverlay.enabled = false;
        }
    }

    private void Update()
    {
        UpdateOverlayPos();

        if (loadOnKeyPress && Input.GetKeyDown(keyCode))
            LoadContent();
    }

    private void UpdateOverlayPos()
    {
        if (ovrOverlay != null && ovrOverlay.isActiveAndEnabled)
        {
            Vector3 projectedForward = Vector3.ProjectOnPlane(CameraCache.Main.transform.forward, Vector3.up);
            ovrOverlay.transform.position = CameraCache.Main.transform.position + projectedForward - Vector3.up * 0.5f;
            ovrOverlay.transform.rotation = Quaternion.LookRotation(projectedForward, Vector3.up);
            ovrOverlay.transform.localScale = Vector3.one * 2f;
        }
    }

    public void LoadContent()
    {

        if (CoreServices.SceneSystem.SceneOperationInProgress)
            return;

        var tutorial = FindObjectOfType<Tutorial>();
        if (tutorial != null)
        {
            tutorial.gameObject.SetActive(false);
        }

        if (ovrOverlay != null)
        {
            UpdateOverlayPos();
            ovrOverlay.enabled = true;
        }

        ISceneTransitionService transitions = MixedRealityToolkit.Instance.GetService<ISceneTransitionService>();
        transitions.DoSceneTransition(
            () => CoreServices.SceneSystem.LoadContent(contentName, loadSceneMode),
            () =>
            {
                if (ovrOverlay != null)
                {
                    ovrOverlay.enabled = false;
                }
                return SurfacePlacement.Instance.PlaceNewSurface();
            });
    }
}
