// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Extensions.SceneTransitions;
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

    private void Update()
    {
        if (loadOnKeyPress && Input.GetKeyDown(keyCode))
            LoadContent();
    }

    public void LoadContent()
    {

        if (CoreServices.SceneSystem.SceneOperationInProgress)
            return;

        ISceneTransitionService transitions = MixedRealityToolkit.Instance.GetService<ISceneTransitionService>();
        transitions.DoSceneTransition(
            () => CoreServices.SceneSystem.LoadContent(contentName, loadSceneMode),
            () => SurfacePlacement.Instance.PlaceNewSurface());
    }
}
