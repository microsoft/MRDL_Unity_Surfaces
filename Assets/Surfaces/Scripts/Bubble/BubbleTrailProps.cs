// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using System;

namespace Microsoft.MRDL
{
    [Serializable]
    public struct BubbleTrailProps
    {
        public float Inertia;
        public float MaxDistance;
        public float BaseRadius;
        public float MaxRadius;
        public float RadiusNoiseMultiplier;
        public float PosNoiseMultiplier;
        public float NoiseSpeed;
    }
}