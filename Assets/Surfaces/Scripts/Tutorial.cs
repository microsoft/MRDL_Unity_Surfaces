// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using Microsoft.MRDL;
using UnityEngine;

public class Tutorial : MonoBehaviour
{
    [SerializeField]
    private GameObject holdUpPalmText = null;

    [SerializeField]
    private ContextualHandMenu menu = null;

    private float startTime = 0;
    private float initialDelay = 2;


    private void Awake()
    {
        holdUpPalmText.SetActive(false);
        startTime = Time.time;
    }

    private void OnDisable()
    {
        holdUpPalmText.SetActive(false);
    }

    private void Update()
    {
        if (Time.time < startTime + initialDelay)
        {
            holdUpPalmText.SetActive(false);
            return;
        }

        if (menu.ActivatedOnce)
        {
            enabled = false;
            return;
        }

        holdUpPalmText.SetActive(true);
    }
}