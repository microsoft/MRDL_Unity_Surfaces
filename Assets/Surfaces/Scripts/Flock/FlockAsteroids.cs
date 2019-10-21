// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using Microsoft.MRDL;
using UnityEngine;

public class FlockAsteroids : MonoBehaviour
{
    public FlockAsteroid[] Asteroids => asteroids;

    [SerializeField]
    private Flock flock;
    [SerializeField]
    private FlockAsteroid[] asteroids;
    [SerializeField]
    private GameObject impactPrefab;
    [SerializeField]
    private float explosionRespawnDelay = 5f;
    [SerializeField]
    private float collisionTimeoutDelay = 3f;
    [SerializeField]
    private Material despawnedMaterial;
    [SerializeField]
    private Gradient despawnedGradient;

    public void Update()
    {
        if (!flock.Initialized)
            return;

        for (int i = 0; i < flock.Fingers.Length; i++)
        {
            FlockAsteroid asteroid = asteroids[i];
            Flock.Force force = flock.Forces[i];
            Transform finger = flock.Fingers[i];

            if (force.Enabled)
            {
                asteroid.TargetPosition = finger.position;

                switch (asteroid.State)
                {
                    case FlockAsteroid.StateEnum.Invisible:
                        force.Active = false;
                        asteroid.State = FlockAsteroid.StateEnum.Visible;
                        break;

                    case FlockAsteroid.StateEnum.Visible:
                        force.Active = false;
                        if (Time.time > asteroid.TimeExploded + collisionTimeoutDelay)
                            asteroid.State = FlockAsteroid.StateEnum.ReadyToCollide;
                        break;

                    case FlockAsteroid.StateEnum.ReadyToCollide:
                        force.Active = true;
                        if (Vector3.Distance(flock.SurfacePosition, finger.position) < flock.SurfaceRadius)
                        {
                            asteroid.State = FlockAsteroid.StateEnum.Exploded;
                            GameObject.Instantiate(
                                impactPrefab,
                                finger.position,
                                Quaternion.LookRotation(finger.position - flock.SurfacePosition),
                                flock.SurfaceTransform);

                            flock.PingCrowdNoises();
                        }
                        break;

                    case FlockAsteroid.StateEnum.Exploded:
                        // Boids flee the exploded asteroid
                        force.Active = true;
                        // Move the finger to follow the asteroid
                        finger.position = asteroid.ExplodedPosition;
                        if (Time.time > asteroid.TimeExploded + explosionRespawnDelay)
                            asteroid.State = FlockAsteroid.StateEnum.Visible;
                        break;
                }
            }
            else
            {
                asteroid.State = FlockAsteroid.StateEnum.Invisible;
                force.Active = false;
            }

            despawnedMaterial.color = despawnedGradient.Evaluate(Mathf.Repeat(Time.time, 1));

            flock.Forces[i] = force;
        }
    }
}
