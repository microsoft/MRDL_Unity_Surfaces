// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MRDL
{
    public interface IBubbleSimple
    {
        bool enabled { get; set; }
        Material MaterialInstance { get; set; }
        float TurbulenceMultiplier { get; set; }
        float TurbulenceSpeed { get; set; }
        float Solidity { get; set; }
        float RadiusMultiplier { get; set; }
        List<InnerBubble> InnerBubbles { get; }
    }
}