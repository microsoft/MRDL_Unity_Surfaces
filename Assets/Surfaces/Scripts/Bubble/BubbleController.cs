// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MRDL
{
    public class BubbleController : MonoBehaviour
    {
        [Range(-0.9f, 4f)]
        public float RadiusMultiplier;
        [Range(0f, 0.1f)]
        public float TurbulenceMultiplier;
        [Range(0f, 10f)]
        public float TurbulenceSpeed;
        [Range(0f, 1f)]
        public float Transparency = 0f;
        [Range(0f, 1f)]
        public float Gaze = 0f;
        [Range(0f, 1f)]
        public float Highlight = 0f;
        [Range(0f, 1f)]
        public float Freeze;
        public Color RimColor;
        public Color MainColor;
        public Material BubbleMaterial;

        private float radiusMultiplierLastFrame = 0f;
        private Material materialInstance;

        public void ClearInnerBubbles()
        {
            bubble.InnerBubbles.Clear();
        }

        public void AddInnerBubble(Transform t, float size)
        {
            bubble.InnerBubbles.Add(new InnerBubble(t, size));
        }

        private void LateUpdate()
        {
            bubble.enabled = (Transparency > 0);
            bubbleRenderer.enabled = (Transparency > 0);

            if (bubble.RadiusMultiplier > RadiusMultiplier)
            {
                RadiusMultiplier = bubble.RadiusMultiplier;
            }

            if (RadiusMultiplier != radiusMultiplierLastFrame)
            {
                bubble.RadiusMultiplier = RadiusMultiplier;
                radiusMultiplierLastFrame = RadiusMultiplier;
            }

            bubble.TurbulenceMultiplier = TurbulenceMultiplier;
            bubble.TurbulenceSpeed = TurbulenceSpeed;
            bubble.Solidity = Freeze;

            if (materialInstance == null)
            {
                materialInstance = new Material(BubbleMaterial);
            }

            materialInstance.SetColor("_MainColor", MainColor);
            materialInstance.SetColor("_CubeColor", MainColor);
            materialInstance.SetColor("_RimColor", RimColor);
            materialInstance.SetFloat("_Freeze", Freeze);
            materialInstance.SetFloat("_InnerTransparency", MainColor.a);
            materialInstance.SetFloat("_Transparency", Transparency);
            materialInstance.SetFloat("_Gaze", Gaze);
            materialInstance.SetFloat("_Highlight", Highlight);
            bubble.MaterialInstance = materialInstance;
        }

        private void OnEnable()
        {
            bubble = gameObject.GetComponent(typeof(IBubbleSimple)) as IBubbleSimple;
            bubbleRenderer = gameObject.GetComponent<MeshRenderer>();
        }

        private void OnDisable()
        {
            if (materialInstance != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(materialInstance);
                }
                else
                {
                    DestroyImmediate(materialInstance);
                }
            }
        }

        private IBubbleSimple bubble;
        private MeshRenderer bubbleRenderer;
    }
}