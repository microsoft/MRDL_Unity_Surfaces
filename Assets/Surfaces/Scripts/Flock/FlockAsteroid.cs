// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace Microsoft.MRDL
{
    public class FlockAsteroid : MonoBehaviour
    {
        public enum StateEnum
        {
            Invisible,
            Visible,
            ReadyToCollide,
            Exploded,
        }

        public float TimeBecameVisible => timeBecameVisible;
        public float TimeExploded => timeExploded;

        public StateEnum State
        {
            get { return state; }
            set
            {
                if (value != state)
                {
                    switch (value)
                    {
                        case StateEnum.Exploded:
                            timeExploded = Time.time;
                            audioSource.PlayOneShot(impactClip, impactVolume);
                            ExplodedPosition = transform.position;
                            break;

                        case StateEnum.Visible:
                            timeBecameVisible = Time.time;
                            break;

                        case StateEnum.ReadyToCollide:
                            timeBecameReadyToCollide = Time.time;
                            audioSource.PlayOneShot(respawnClip, respawnVolume);
                            break;
                    }

                    state = value;
                }
            }
        }

        public Vector3 TargetPosition { get; set; }
        public Vector3 ExplodedPosition { get; protected set; }

        [SerializeField]
        private Renderer asteroidRenderer;
        [SerializeField]
        private HoverLight hoverLight;
        [SerializeField]
        private float trailTargetTime = 0.5f;
        [SerializeField]
        private float rotationSpeed = 25;
        [SerializeField]
        private Gradient hoverLightExplosionColor;
        [SerializeField]
        private Color hoverLightNormalColor;
        [SerializeField]
        private AnimationCurve growthCurve;
        [SerializeField]
        private AudioSource audioSource;
        [SerializeField]
        private AudioClip impactClip;
        [SerializeField]
        private AudioClip respawnClip;
        [SerializeField]
        private float impactVolume = 0.25f;
        [SerializeField]
        private float respawnVolume = 0.125f;
        [SerializeField]
        private Material despawnedMaterial;
        [SerializeField]
        private Material spawnedMaterial;

        private float timeExploded;
        private float timeBecameVisible;
        private float timeBecameReadyToCollide;
        private StateEnum state = StateEnum.Invisible;
        private Vector3 randomRotation;

        private void OnEnable()
        {
            randomRotation = Random.onUnitSphere;
            // Un-parent our hover light so we can move it around on the planet surface
            hoverLight.transform.parent = transform.parent;
        }

        private void LateUpdate()
        {
            switch (State)
            {
                case StateEnum.Invisible:
                    asteroidRenderer.enabled = false;
                    hoverLight.Color = Color.Lerp(hoverLight.Color, Color.black, 0.25f);
                    hoverLight.transform.position = TargetPosition;
                    transform.localScale = Vector3.one;
                    break;

                case StateEnum.Visible:
                    asteroidRenderer.enabled = true;
                    asteroidRenderer.sharedMaterial = despawnedMaterial;
                    transform.Rotate(randomRotation * rotationSpeed * Time.deltaTime);
                    transform.position = TargetPosition;                 
                    hoverLight.Color = Color.Lerp(hoverLight.Color, hoverLightNormalColor, 0.25f);
                    hoverLight.transform.position = TargetPosition;
                    transform.localScale = Vector3.one;
                    break;

                case StateEnum.ReadyToCollide:
                    asteroidRenderer.enabled = true;
                    asteroidRenderer.sharedMaterial = spawnedMaterial;
                    transform.Rotate(randomRotation * rotationSpeed * Time.deltaTime);
                    transform.position = TargetPosition;
                    hoverLight.transform.position = TargetPosition;
                    hoverLight.Color = Color.Lerp(hoverLight.Color, hoverLightNormalColor, 0.25f);
                    transform.localScale = Vector3.one * growthCurve.Evaluate(Time.time - timeBecameReadyToCollide);
                    break;

                case StateEnum.Exploded:
                    asteroidRenderer.enabled = true;
                    asteroidRenderer.sharedMaterial = despawnedMaterial;
                    transform.position = TargetPosition;
                    hoverLight.Color = hoverLightExplosionColor.Evaluate(Time.time - timeExploded);
                    transform.localScale = Vector3.one;
                    break;
            }
        }
    }
}
