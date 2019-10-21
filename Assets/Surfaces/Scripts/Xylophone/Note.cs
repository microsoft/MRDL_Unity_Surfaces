// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using System.Threading.Tasks;
using UnityEngine;

public class Note : MonoBehaviour
{
    public Vector3 Forward => forward;
    public Vector3 InitialPosition => initialPosition;
    public int NoteIndex => noteIndex;
    public AudioSource AudioSource => audio;
    public Renderer DomeRenderer => domeRenderer;
    public Renderer BurstRenderer => burstRenderer;
    public Renderer ConeRenderer => coneRenderer;
    public Renderer SoundWaveRenderer => soundWaveRenderer;
    public Animator Animator => animator;

    [SerializeField]
    private new AudioSource audio;
    [SerializeField]
    private Renderer domeRenderer;
    [SerializeField]
    private Renderer burstRenderer;
    [SerializeField]
    private Renderer coneRenderer;
    [SerializeField]
    private Renderer soundWaveRenderer;
    [SerializeField]
    private Animator animator;

    private Vector3 initialPosition;
    private Vector3 forward;
    private INoteHandler handler;
    private int noteIndex;
    
    public void Initialize(int noteIndex, INoteHandler handler)
    {
        this.noteIndex = noteIndex;
        this.handler = handler;

        forward = transform.up;
        initialPosition = transform.localPosition;
    }

    public void OnTriggerEnter(Collider other)
    {
        handler.HandleTriggerEnter(this, other);
    }

    public void OnTriggerExit(Collider other)
    {
        handler.HandleTriggerExit(this, other);
    }
}
