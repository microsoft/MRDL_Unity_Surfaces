// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace Microsoft.MRDL
{
    /// <summary>
    /// Controls the scale a set of transforms under a target transform.
    /// Uses noise for variation.
    /// </summary>
    [ExecuteInEditMode]
    public class Growbies : MonoBehaviour
    {
        const float minGrowthCutoff = 0.001f;

        public enum CorkscrewAxisEnum
        {
            X,
            Y,
            Z,
        }

        public bool IsDirty { get; set; }

        [Range(0,1)]
        [Header("Animated Parameters")]
        public float Growth = 0f;

        [Header("Static Parameters")]
        [SerializeField]
        [Tooltip("Random values determined by seed")]
        [Range(0, 1)]
        public float growthRandomness = 0.5f;
        [Range(0, 1)]
        public float growthJitter = 0;
        [SerializeField]
        [Tooltip("Multiplier for scale of transforms in heirarchy from first to last")]
        private AnimationCurve growthScaleMultiplier = AnimationCurve.EaseInOut(0f, 1f, 1f, 1f);
        [Range(0, 1)]
        [Tooltip("How much the growbies rotate on their Y axis as they grow")]
        public float corkScrewAmount = 0;
        [Range(1, 20)]
        [Tooltip("How jittery to make the corkscrew effect")]
        public float corkscrewJitter = 5;
        [SerializeField]
        CorkscrewAxisEnum corkscrewAxis = CorkscrewAxisEnum.Y;
        [SerializeField]
        private int seed = 1234;

        [Header("Non-uniform scaling (set to linear for uniform scaling)")]
        [SerializeField]
        private AnimationCurve xAxisScale = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField]
        private AnimationCurve yAxisScale = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField]
        private AnimationCurve zAxisScale = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private Vector3 finalScale;
        private System.Random random;
        private FastSimplexNoise noise;
        private float growbieSizeLastFrame = 0;
        private int numTransformLastFrame = 0;
        private float[] randomGrowthValues;

        private void OnEnable()
        {
            // This will ensure we update at least once
            growbieSizeLastFrame = 1f - Growth;
            GenerateRandomGrowthValues();
        }

        private void Update()
        {
            Growth = Mathf.Clamp01(Growth);

            if (Application.isPlaying && Growth == growbieSizeLastFrame && !IsDirty)
            {
                // Nothing to do!
                return;
            }

            IsDirty = false;

            int numTransforms = transform.childCount;
            if (numTransformLastFrame != numTransforms)
            {
                GenerateRandomGrowthValues();
            }

            for (int i = 0; i < numTransforms; i++)
            {
                Transform growbie = transform.GetChild(i);

                if (Growth < minGrowthCutoff)
                {
                    growbie.gameObject.SetActive(false);
                    continue;
                }

                float scale = Growth * (growthScaleMultiplier.Evaluate((float)i / numTransforms));

                if (growthRandomness > 0)
                {
                    scale = scale * (1f - (growthRandomness * randomGrowthValues[i]));
                }

                if (corkScrewAmount > 0)
                {
                    float corkscrew = (float)noise.Evaluate(Growth, (scale + i) * corkscrewJitter);
                    Vector3 eulerAngles = growbie.localEulerAngles;
                    switch (corkscrewAxis)
                    {
                        case CorkscrewAxisEnum.X:
                            eulerAngles.x = corkscrew * 360 * corkScrewAmount;
                            break;

                        case CorkscrewAxisEnum.Y:
                            eulerAngles.y = corkscrew * 360 * corkScrewAmount;
                            break;

                        case CorkscrewAxisEnum.Z:
                            eulerAngles.z = corkscrew * 360 * corkScrewAmount;
                            break;
                    }
                    growbie.localEulerAngles = eulerAngles;
                }

                if (growthJitter > 0)
                {
                    float growthJitterMultiplier = (1 + growthJitter);
                    float jitter = (float)noise.Evaluate(Growth * 15 * growthJitterMultiplier, (i * 15 * growthJitterMultiplier));
                    scale += (jitter * (growthJitter * 0.1f));
                }

                if (scale < 0.001f)
                {
                    growbie.gameObject.SetActive(false);
                }
                else
                {
                    growbie.gameObject.SetActive(true);
                    finalScale = Vector3.one * scale;
                    // Apply non-uniform scaling
                    finalScale.x = xAxisScale.Evaluate(scale);
                    finalScale.y = yAxisScale.Evaluate(scale);
                    finalScale.z = zAxisScale.Evaluate(scale);
                    // Apply final scale
                    growbie.localScale = finalScale;
                }
            }

            growbieSizeLastFrame = Growth;
            numTransformLastFrame = numTransforms;
        }

        private void GenerateRandomGrowthValues()
        {
            random = new System.Random(seed);
            randomGrowthValues = new float[transform.childCount];
            for (int i = 0; i < randomGrowthValues.Length; i++)
            {
                randomGrowthValues[i] = (float)random.NextDouble();
            }

            if (noise == null)
            {
                noise = new FastSimplexNoise(seed);
            }
        }
    }
}