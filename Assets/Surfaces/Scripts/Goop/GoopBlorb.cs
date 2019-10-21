// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using UnityEngine;

public class GoopBlorb : MonoBehaviour
{
    public Renderer Renderer { get { return meshRenderer; } }
    public MaterialPropertyBlock PropertyBlock { get { return propertyBlock; } }

    [SerializeField]
    private MeshRenderer meshRenderer = null;

    private MaterialPropertyBlock propertyBlock;


    private void OnEnable()
    {
        propertyBlock = new MaterialPropertyBlock();
    }
}