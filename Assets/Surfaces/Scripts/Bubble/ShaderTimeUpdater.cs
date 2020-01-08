// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using UnityEngine;

namespace Microsoft.MRDL
{
    [ExecuteInEditMode]
    public class ShaderTimeUpdater : MonoBehaviour
    {
        private void Update()
        {
            unloopedTime += Time.deltaTime;

            if (unloopedTime >= repeatInterval)
            {
                loopedTime = Mathf.Repeat(unloopedTime, repeatInterval);
                float blendAmount = repeatBlendCurve.Evaluate((unloopedTime - repeatInterval) / repeatBlendTime);
                if (blendAmount >= 1)
                {
                    unloopedTime = 0;
                }
                noiseTime = Mathf.Lerp(unloopedTime, loopedTime, blendAmount);
            }
            else
            {
                noiseTime = unloopedTime;
            }

            noiseTime = Mathf.Repeat(noiseTime, repeatInterval);

            Shader.SetGlobalFloat("_NoiseTime", noiseTime);
        }

        [SerializeField]
        private float repeatInterval = 600f;
        [SerializeField]
        private float repeatBlendTime = 20f;
        [SerializeField]
        private AnimationCurve repeatBlendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private float unloopedTime;
        private float loopedTime;
        private float noiseTime;
    }
}