// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using UnityEngine;

namespace Microsoft.MRDL
{
    public class GrowbieParticles : MonoBehaviour
    {
        public float SeasonBlend;

        [Header("Particles")]
        [SerializeField]
        private ParticleSystem neutralParticles;
        [SerializeField]
        private ParticleSystem winterParticles;
        [SerializeField]
        private ParticleSystem summerParticles;
        [SerializeField]
        private float emissionRate = 0.8f;
        [SerializeField]
        private AnimationCurve neutralEmissionCurve;
        [SerializeField]
        private AnimationCurve winterEmissionCurve;
        [SerializeField]
        private AnimationCurve summerEmissionCurve;

        private ParticleSystem.EmissionModule neutralEmit;
        private ParticleSystem.EmissionModule summerEmit;
        private ParticleSystem.EmissionModule winterEmit;
        private ParticleSystem.MinMaxCurve rate;
        private float winterBurst = 0f;
        private float summerBurst = 0f;

        private void OnEnable()
        {
            neutralEmit = neutralParticles.emission;
            summerEmit = summerParticles.emission;
            winterEmit = winterParticles.emission;
        }

        private void Update()
        {
            float winterEmission = winterEmissionCurve.Evaluate(SeasonBlend) * emissionRate + winterBurst;
            float summerEmission = summerEmissionCurve.Evaluate(SeasonBlend) * emissionRate + summerBurst;
            float neutralEmission = neutralEmissionCurve.Evaluate(SeasonBlend) * emissionRate;

            rate.constant = neutralEmission;
            neutralEmit.rateOverTime = rate;

            rate.constant = summerEmission;
            summerEmit.rateOverTime = rate;

            rate.constant = winterEmission;
            winterEmit.rateOverTime = rate;

            summerBurst = Mathf.Lerp(summerBurst, 0, Time.deltaTime);
            winterBurst = Mathf.Lerp(winterBurst, 0, Time.deltaTime);
        }
    }
}
