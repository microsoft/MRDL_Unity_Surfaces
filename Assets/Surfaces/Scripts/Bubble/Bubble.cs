// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using UnityEngine;

namespace Microsoft.MRDL
{
    public class Bubble : FingerSurface
    {
        private struct Fingertip
        {
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Block;
            public Color Color;
            public Vector3 Point;
            public Vector3 PrevPoint;
            public Vector3 Velocity;
            public AudioSource Audio;
            public float TimeIntersectedBubble;
        }

        [SerializeField]
        private float fingerForce;
        [SerializeField]
        private float bubbleDriftSpeed;
        [SerializeField]
        private float bubbleReturnSpeed;
        [SerializeField]
        private Transform bubbleTransform;
        [SerializeField]
        private BubbleSimple bubble;
        [SerializeField]
        private BubbleController controller;
        [SerializeField]
        private AudioClip[] enterBubbleClips;
        [SerializeField]
        private AnimationCurve touchBubbleCurve;
        [SerializeField]
        private ParticleSystem[] bubbleTouchParticles;

        private Vector3 bubbleVelocity;
        private Vector3 initialPosition;
        private Fingertip[] fingertips;

        public override void Initialize(Vector3 surfacePosition)
        {
            base.Initialize(surfacePosition);

            initialPosition = bubbleTransform.position;

            fingertips = new Fingertip[fingers.Length];
            for (int i = 0; i < fingers.Length; i++)
            {
                Fingertip fingertip = new Fingertip();
                fingertip.Block = new MaterialPropertyBlock();
                fingertip.Renderer = fingers[i].GetComponentInChildren<MeshRenderer>();
                fingertip.Color = bubble.InnerBubbles[i].Color;
                fingertip.Audio = fingers[i].GetComponentInChildren<AudioSource>();
                fingertips[i] = fingertip;
            }

            bubble.OnEnterBubble += OnEnterBubble;
            bubble.OnExitBubble += OnExitBubble;
        }

        private void OnEnterBubble(int innerBubbleIndex)
        {
            Fingertip f = fingertips[innerBubbleIndex];
            f.Audio.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
            f.Audio.clip = enterBubbleClips[UnityEngine.Random.Range(0, enterBubbleClips.Length)];
            f.Audio.Play();
            f.TimeIntersectedBubble = Time.time;
            fingertips[innerBubbleIndex] = f;

            for (int i = 0; i < bubbleTouchParticles.Length; i++)
            {
                if (!bubbleTouchParticles[i].gameObject.activeSelf)
                {
                    bubbleTouchParticles[i].transform.position = f.Point;
                    bubbleTouchParticles[i].gameObject.SetActive(true);
                    break;
                }
            }
        }

        private void OnExitBubble(int innerBubbleIndex)
        {
            Fingertip f = fingertips[innerBubbleIndex];
            f.Audio.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
            f.Audio.clip = enterBubbleClips[UnityEngine.Random.Range(0, enterBubbleClips.Length)];
            f.Audio.Play();
            f.TimeIntersectedBubble = Time.time;
            fingertips[innerBubbleIndex] = f;

            for (int i = 0; i < bubbleTouchParticles.Length; i++)
            {
                if (!bubbleTouchParticles[i].gameObject.activeSelf)
                {
                    bubbleTouchParticles[i].transform.position = f.Point;
                    bubbleTouchParticles[i].gameObject.SetActive(true);
                    break;
                }
            }
        }

        private void Update()
        {
            if (!Initialized)
                return;

            bubbleTransform.position = Vector3.Lerp(bubbleTransform.position, initialPosition, bubbleReturnSpeed * Time.deltaTime);

            bubbleVelocity = Vector3.Lerp(bubbleVelocity, Vector3.zero, Time.deltaTime);
            for (int i  = 0; i < fingertips.Length; i++)
            {
                Fingertip fingertip = fingertips[i];
                fingertip.Block.SetColor("_Color", fingertip.Color);
                fingertip.Renderer.SetPropertyBlock(fingertip.Block);

                fingertip.PrevPoint = fingertip.Point;
                fingertip.Point = fingers[i].position;
                fingertip.Velocity = (fingertip.Point - fingertip.PrevPoint);

                fingertip.Renderer.transform.localScale = Vector3.one * touchBubbleCurve.Evaluate(Time.time - fingertip.TimeIntersectedBubble);

                if (Vector3.Distance(fingertip.Point, bubbleTransform.position) < bubble.Radius)
                {
                    bubbleVelocity += fingertip.Velocity;
                }
                fingertips[i] = fingertip;
            }

            bubbleTransform.position += (bubbleVelocity * fingerForce);
            controller.RadiusMultiplier = bubbleVelocity.magnitude * fingerForce;
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            if (!Initialized)
                return;

            foreach (Fingertip ft in fingertips)
            {
                Gizmos.DrawLine(ft.Point, ft.Point + ft.Velocity * 1);
            }
        }
    }
}