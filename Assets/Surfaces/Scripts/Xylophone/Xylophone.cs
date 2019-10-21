// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

public interface INoteHandler
{
    void HandleTriggerEnter(Note note, Collider other);
    void HandleTriggerExit(Note note, Collider other);
}

public class Xylophone : FingerSurface, INoteHandler
{
    [Serializable]
    public struct NoteData
    {
        public Color Color;
        public Color DomeColor;
        public AudioClip Clip;
        public AudioClip PressClip;
        public float Volume;
        public float PressVolume;
    }

    public enum StateEnum
    {
        Pressing,
        Releasing,
        Resetting,
    }

    public enum NoteStateEnum
    {
        Reset,
        Pressed,
        Releasing
    }

    [Serializable]
    public struct NoteState
    {
        public NoteStateEnum PressState;
        public bool PressedNow;
        public float TimePressed;
        public float TimeReleased;
        public int NoteDataIndex;
        public int NotePlaybackIndex;
        public HashSet<Collider> ActiveColliders;
        public MaterialPropertyBlock ConePropertyBlock;
        public MaterialPropertyBlock DomePropertyBlock;
        public MaterialPropertyBlock BurstPropertyBlock;
    }

    [SerializeField]
    private Transform[] notesTransforms;
    [SerializeField]
    private Note[] notes;
    [SerializeField]
    private GameObject notePrefab;
    [SerializeField]
    private Transform noteParent;

    [Header("Note Data")]
    [SerializeField]
    private NoteData[] noteData;

    [Header("Note Animation")]
    [SerializeField]
    private Gradient pressedGradient;
    [SerializeField]
    private Gradient releasedGradient;
    [SerializeField]
    private AnimationCurve pressedPosCurve;
    [SerializeField]
    private AnimationCurve releasedPosCurve;
    [SerializeField]
    private float pressedOffset = 0.1f;
    [SerializeField]
    private float releasedOffset = 0.25f;
    [SerializeField]
    private float releaseAnimationDuration = 1f;
    [SerializeField]
    private float timeBetweenNoteReleases = 0.25f;
    [SerializeField]
    private string emissiveColorName = "_EmissiveColor";
    [SerializeField]
    private string emissiveColorNameBurst = "_EmissionColor";
    [SerializeField]
    private string albedorColorName = "_Color";

    private StateEnum xyloState = StateEnum.Pressing;

    private Queue<int> releaseIndexQueue = new Queue<int>();
    private float timeAllPressed = 0;
    private float timeLastReleased = 0;
    private float timeAllReleased = 0;
    private NoteState[] noteStates;


    protected override void Awake()
    {
        base.Awake();

        List<int> noteDataIndexes = new List<int>();
        for (int i = 0; i < notes.Length; i++)
            noteDataIndexes.Add(i % noteData.Length);

        int n = noteDataIndexes.Count;
        while (n > 1)
        {
            n--;
            int k = UnityEngine.Random.Range(0, n + 1);
            int value = noteDataIndexes[k];
            noteDataIndexes[k] = noteDataIndexes[n];
            noteDataIndexes[n] = value;
        }

        noteStates = new NoteState[notes.Length];
        for (int i = 0; i < notes.Length; i++)
        {
            NoteState state = new NoteState();
            state.ActiveColliders = new HashSet<Collider>();
            state.ConePropertyBlock = new MaterialPropertyBlock();
            state.DomePropertyBlock = new MaterialPropertyBlock();
            state.BurstPropertyBlock = new MaterialPropertyBlock();
            state.NoteDataIndex = noteDataIndexes[i];
            state.NotePlaybackIndex = i;
            state.TimePressed = -100;
            state.TimeReleased = -100;

            noteStates[i] = state;

            notes[i].Initialize(i, this);

            NoteData data = noteData[state.NoteDataIndex];
            Note note = notes[i];
            state.BurstPropertyBlock.SetColor(emissiveColorNameBurst, data.Color);
            state.DomePropertyBlock.SetColor(emissiveColorName, Color.black);
            state.DomePropertyBlock.SetColor(albedorColorName, data.DomeColor);
            state.ConePropertyBlock.SetColor(emissiveColorName, Color.black);

            note.DomeRenderer.SetPropertyBlock(state.DomePropertyBlock);
            note.BurstRenderer.SetPropertyBlock(state.BurstPropertyBlock);
            note.ConeRenderer.SetPropertyBlock(state.ConePropertyBlock);
        }
    }

    public override void Initialize(Vector3 surfacePosition)
    {
        base.Initialize(surfacePosition);

        xyloState = StateEnum.Pressing;
    }

    public void HandleTriggerEnter(Note note, Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        NoteState state = noteStates[note.NoteIndex];
        state.ActiveColliders.Add(other);
        state.PressedNow = true;
        noteStates[note.NoteIndex] = state;
    }

    public void HandleTriggerExit(Note note, Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        NoteState state = noteStates[note.NoteIndex];
        state.ActiveColliders.Remove(other);
        state.PressedNow = state.ActiveColliders.Count > 0;
        noteStates[note.NoteIndex] = state;
    }

    private void Update()
    {
        if (!Initialized)
            return;

        switch (xyloState)
        {
            case StateEnum.Pressing:
                UpdatePressing();
                break;

            case StateEnum.Releasing:
                UpdateReleasing();
                break;

            case StateEnum.Resetting:
                UpdateResetting();
                break;
        }
    }

    private void UpdateResetting()
    {
        for (int i = 0; i < noteStates.Length; i++)
        {
            NoteState state = noteStates[i];
            Note note = notes[i];
            NoteData data = noteData[state.NoteDataIndex];

            note.transform.localPosition = Vector3.Lerp(note.transform.localPosition, note.InitialPosition, Time.time - timeAllReleased);

            state.ConePropertyBlock.SetColor(emissiveColorName, Color.black);
            state.DomePropertyBlock.SetColor(emissiveColorName, Color.black);
            state.DomePropertyBlock.SetColor(albedorColorName, data.DomeColor);

            note.DomeRenderer.SetPropertyBlock(state.DomePropertyBlock);
            note.ConeRenderer.SetPropertyBlock(state.ConePropertyBlock);

            if (Time.time > timeAllReleased + 1)
            {
                xyloState = StateEnum.Pressing;
            }
        }
    }

    private void UpdateReleasing()
    {
        bool allReset = true;

        if (releaseIndexQueue.Count > 0 && Time.time > timeLastReleased + timeBetweenNoteReleases)
        {
            timeLastReleased = Time.time;
            int releasedNoteIndex = releaseIndexQueue.Dequeue();
            NoteState releasedNoteState = noteStates[releasedNoteIndex];
            releasedNoteState.PressState = NoteStateEnum.Releasing;
            releasedNoteState.TimeReleased = Time.time;

            Note releasedNote = notes[releasedNoteIndex];
            NoteData releasedNoteData = noteData[releasedNoteState.NoteDataIndex];
            releasedNote.AudioSource.PlayOneShot(releasedNoteData.Clip, releasedNoteData.Volume);
            releasedNote.AudioSource.PlayOneShot(releasedNoteData.PressClip, releasedNoteData.PressVolume);

            releasedNote.Animator.SetTrigger("Burst");
            releasedNote.Animator.SetTrigger("Transparency");

            noteStates[releasedNoteIndex] = releasedNoteState;
        }

        for (int i = 0; i < noteStates.Length; i++)
        {
            NoteState state = noteStates[i];
            Note note = notes[i];
            NoteData data = noteData[state.NoteDataIndex];

            switch (state.PressState)
            {
                default:
                case NoteStateEnum.Reset:
                    break;

                case NoteStateEnum.Pressed:
                    Color pressedColor = pressedGradient.Evaluate(Time.time - state.TimePressed) * data.Color;
                    state.ConePropertyBlock.SetColor(emissiveColorName, Color.black);
                    state.DomePropertyBlock.SetColor(emissiveColorName, pressedColor);
                    state.DomePropertyBlock.SetColor(albedorColorName, data.DomeColor);

                    note.DomeRenderer.SetPropertyBlock(state.DomePropertyBlock);
                    note.ConeRenderer.SetPropertyBlock(state.ConePropertyBlock);

                    float pressedPos = pressedPosCurve.Evaluate(Time.time - state.TimePressed) * pressedOffset;
                    note.transform.localPosition = note.InitialPosition + (note.Forward * pressedPos);
                    allReset = false;
                    break;

                case NoteStateEnum.Releasing:
                    allReset = false;
                    Color releasedColor = releasedGradient.Evaluate(Time.time - state.TimeReleased) * data.Color;
                    state.ConePropertyBlock.SetColor(emissiveColorName, Color.black);
                    state.DomePropertyBlock.SetColor(emissiveColorName, releasedColor);
                    state.DomePropertyBlock.SetColor(albedorColorName, data.DomeColor);

                    note.DomeRenderer.SetPropertyBlock(state.DomePropertyBlock);
                    note.ConeRenderer.SetPropertyBlock(state.ConePropertyBlock);

                    float releasedPos = releasedPosCurve.Evaluate(Time.time - state.TimeReleased) * releasedOffset;
                    note.transform.localPosition = note.InitialPosition + (note.Forward * releasedPos);

                    if (Time.time > state.TimeReleased + releaseAnimationDuration)
                    {
                        state.PressState = NoteStateEnum.Reset;
                    }
                    break;
            }
            noteStates[i] = state;
        }

        if (allReset)
        {
            xyloState = StateEnum.Resetting;
            timeAllReleased = Time.time;
        }
    }

    private void UpdatePressing()
    {
        bool allPressed = true;

        for (int i = 0; i < noteStates.Length; i++)
        {
            NoteState state = noteStates[i];
            Note note = notes[i];
            NoteData data = noteData[state.NoteDataIndex];

            switch (state.PressState)
            {
                case NoteStateEnum.Pressed:
                    Color pressedColor = pressedGradient.Evaluate(Time.time - state.TimePressed) * data.Color;
                    state.ConePropertyBlock.SetColor(emissiveColorName, Color.black);
                    state.ConePropertyBlock.SetColor(emissiveColorNameBurst, Color.black);
                    state.DomePropertyBlock.SetColor(emissiveColorName, pressedColor);
                    state.DomePropertyBlock.SetColor(albedorColorName, data.DomeColor);

                    note.DomeRenderer.SetPropertyBlock(state.DomePropertyBlock);
                    note.ConeRenderer.SetPropertyBlock(state.ConePropertyBlock);

                    float pressedPos = pressedPosCurve.Evaluate(Time.time - state.TimePressed) * pressedOffset;
                    note.transform.localPosition = note.InitialPosition + (note.Forward * pressedPos);
                    break;

                case NoteStateEnum.Reset:
                case NoteStateEnum.Releasing:
                default:
                    state.ConePropertyBlock.SetColor(emissiveColorName, Color.black);
                    state.ConePropertyBlock.SetColor(emissiveColorNameBurst, Color.black);
                    state.DomePropertyBlock.SetColor(emissiveColorName, Color.black);
                    state.DomePropertyBlock.SetColor(albedorColorName, data.DomeColor);

                    note.DomeRenderer.SetPropertyBlock(state.DomePropertyBlock);
                    note.ConeRenderer.SetPropertyBlock(state.ConePropertyBlock);

                    if (state.PressedNow)
                    {
                        note.Animator.SetTrigger("Burst");
                        note.Animator.SetTrigger("Transparency");
                        state.PressState = NoteStateEnum.Pressed;
                        state.TimePressed = Time.time;
                        note.AudioSource.PlayOneShot(data.Clip, data.Volume);
                        note.AudioSource.PlayOneShot(data.PressClip, data.PressVolume);
                        releaseIndexQueue.Enqueue(i);
                    }
                    allPressed = false;
                    break;
            }

            noteStates[i] = state;
        }

        if (allPressed)
        {
            xyloState = StateEnum.Releasing;
            timeAllPressed = Time.time;
        }
    }

#if UNITY_EDITOR
    [MenuItem("Surfaces/Randomize Xylophone Notes")]
    public static void RandomizeXylophoneNotes()
    {
        Xylophone xylophone = FindObjectOfType<Xylophone>();
    }

    [MenuItem("Surfaces/Create Xylophone Notes")]
    public static void CreateNotes()
    {
        Xylophone xylophone = FindObjectOfType<Xylophone>();

        List<Note> notes = new List<Note>();
        List<GameObject> objectsToDestroy = new List<GameObject>();
        foreach (Note existingNote in xylophone.notes)
            objectsToDestroy.Add(existingNote.gameObject);

        foreach (Transform noteTransform in xylophone.notesTransforms)
        {
            GameObject newNoteGo = (GameObject)PrefabUtility.InstantiatePrefab(xylophone.notePrefab);
            newNoteGo.transform.parent = xylophone.noteParent;
            newNoteGo.transform.position = noteTransform.position;
            newNoteGo.transform.rotation = noteTransform.rotation;
            newNoteGo.name = "Note " + notes.Count.ToString();
            Note note = newNoteGo.GetComponent<Note>();
            notes.Add(note);
        }

        foreach (GameObject objectToDestroy in objectsToDestroy)
            GameObject.DestroyImmediate(objectToDestroy);

        xylophone.notes = notes.ToArray();
    }
#endif
}
