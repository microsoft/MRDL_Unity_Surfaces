// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using Microsoft.MixedReality.Toolkit;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadFirstScene : MonoBehaviour
{
    [SerializeField]
    private string contentName = string.Empty;

    void Start()
    {
        CoreServices.SceneSystem.LoadContent(contentName, LoadSceneMode.Single);
    }
}
