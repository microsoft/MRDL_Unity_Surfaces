// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using System;
using UnityEngine;

namespace Microsoft.MRDL
{
    [Serializable]
    public struct InnerBubble
    {
        public InnerBubble(Transform transform, float radius)
        {
            Active = false;
            Transform = transform;
            Radius = radius;
            Color = Color.white;
            UseColor = false;
            InnerPos = Vector3.zero;
            DistToCenter = 0;
            InsideBubble = false;
        }

        public bool Active;
        public Transform Transform;
        public float Radius;
        public Color Color;
        public bool UseColor;
        public Vector3 InnerPos;
        public float DistToCenter;
        public bool InsideBubble;
    }
}