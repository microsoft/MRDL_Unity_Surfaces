// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using System;
using UnityEngine;

namespace Microsoft.MRDL
{
    [Serializable]
    public struct BubbleTrail
    {
        public float Radius;
        public Vector3 BaseWorldPosition;
        public Vector3 FinalWorldPosition;
        public Vector3 InnerPosition;
    }
}