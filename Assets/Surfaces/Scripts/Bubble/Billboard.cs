// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using UnityEngine;

namespace Microsoft.MRDL
{
    [ExecuteInEditMode]
    public class Billboard : MonoBehaviour
    {
        [SerializeField]
        private float zOffset = -1;
        [SerializeField]
        private bool rotate = false;
        [SerializeField]
        private bool scale = false;
        [SerializeField]
        private AnimationCurve scaleCurve;
        [SerializeField]
        private float rotateSpeed = 1f;
        [SerializeField]
        private float scaleSpeed = 1f;
        [SerializeField]
        private float rotateOffset;
        [SerializeField]
        private float scaleOffset;
        [SerializeField]
        private float randomScaleOffset;

        private void OnEnable()
        {
            randomScaleOffset = Random.Range(0, randomScaleOffset);
        }

        private void OnWillRenderObject()
        {
            transform.forward = -Camera.current.transform.forward;
            transform.localPosition = Vector3.forward * zOffset;

            if (rotate)
                transform.Rotate(0f, 0f, rotateOffset + Mathf.Repeat(Time.time * rotateSpeed, 360));

            if (scale)
                transform.localScale = Vector3.one * scaleCurve.Evaluate(scaleOffset + randomScaleOffset + Time.time * scaleSpeed);
        }
    }
}