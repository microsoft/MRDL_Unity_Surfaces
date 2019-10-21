// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using UnityEngine;

namespace Microsoft.MRDL
{
    [ExecuteInEditMode]
    public class GrowbieSeasons : MonoBehaviour
    {
        public Vector3 InfluenceCenter { get { return influenceCenter.position; } }
        public float InfluenceRange { get { return influenceRange; } }

        [Range(0f, 1f)]
        [Header("Animated Fields")]
        public float Season = 0.5f;

        [Range(1f, 10f)]
        public float ChangeSpeed = 5f;

        [SerializeField]
        private Transform influenceCenter;

        [Header("Normal Growbies (visible at all times)")]
        [SerializeField]
        private Growbies[] winterGrowbies;
        [SerializeField]
        private Growbies[] neutralGrowbies;
        [SerializeField]
        private Growbies[] summerGrowbies;

        [Header("Growth curves")]
        [SerializeField]
        private AnimationCurve winterGrowthCurve;
        [SerializeField]
        private AnimationCurve neutralGrowthCurve;
        [SerializeField]
        private AnimationCurve summerGrowthCurve;

        [Header("Audio")]
        [SerializeField]
        private AudioClip[] winterClips;
        [SerializeField]
        private AudioClip[] summerClips;
        [SerializeField]
        private AudioSource audioSource;
        [SerializeField]
        private float volumeMultiplier = 0.25f;

        private float influenceRange;
        private float prevWinterGrowthTarget;
        private float prevSummerGrowthTarget;

        private void OnEnable()
        {
            foreach (Growbies g in winterGrowbies)
                g.IsDirty = true;

            foreach (Growbies g in neutralGrowbies)
                g.IsDirty = true;

            foreach (Growbies g in summerGrowbies)
                g.IsDirty = true;

            influenceRange = influenceCenter.lossyScale.x;

            float prevWinterGrowthTarget = winterGrowthCurve.Evaluate(Season);
            float prevSummerGrowthTarget = summerGrowthCurve.Evaluate(Season);
        }

        private void Update()
        {
            Season = Mathf.Clamp01(Season);

            float winterGrowthTarget = winterGrowthCurve.Evaluate(Season);
            float neutralGrowthTarget = neutralGrowthCurve.Evaluate(Season);
            float summerGrowthTarget = summerGrowthCurve.Evaluate(Season);

            UpdateGrowth(winterGrowbies, winterGrowthTarget);
            UpdateGrowth(neutralGrowbies, neutralGrowthTarget);
            UpdateGrowth(summerGrowbies, summerGrowthTarget);

            if (!audioSource.isPlaying && winterGrowthTarget > 0 && winterGrowthTarget > prevWinterGrowthTarget)
            {
                audioSource.clip = winterClips[Random.Range(0, winterClips.Length)];
                audioSource.pitch = Random.Range(0.8f, 1.2f);
                audioSource.volume = Random.Range(0.5f * volumeMultiplier, volumeMultiplier);
                audioSource.Play();
            }

            if (!audioSource.isPlaying && summerGrowthTarget > 0 && summerGrowthTarget > prevSummerGrowthTarget)
            {
                audioSource.clip = summerClips[Random.Range(0, summerClips.Length)];
                audioSource.pitch = Random.Range(0.8f, 1.2f);
                audioSource.volume = Random.Range(0.5f * volumeMultiplier, volumeMultiplier);
                audioSource.Play();
            }

            prevWinterGrowthTarget = winterGrowthTarget;
            prevSummerGrowthTarget = summerGrowthTarget;
        }

        private void UpdateGrowth(Growbies[] growbies, float targetValue)
        {
            for (int i = 0; i < growbies.Length; i++)
            {
                float val = Mathf.Lerp(growbies[i].Growth, targetValue, Time.unscaledDeltaTime * ChangeSpeed);
                growbies[i].Growth = val;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.Lerp(Color.white, Color.clear, 0.25f);
            Gizmos.DrawSphere(InfluenceCenter, InfluenceRange);
        }

#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(GrowbieSeasons))]
        public class GrowbieSeasonsEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                UnityEditor.EditorUtility.SetDirty(target);
            }

            private void OnSceneGUI()
            {
                Repaint();
            }
        }
#endif
    }
}