// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

#pragma warning disable 0649 // For serialized fields
using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LavaChunk : MonoBehaviour
{
    public MeshRenderer Renderer { get { return meshRenderer; } }

    public Action<Vector3,float> OnCollision { get; set; }

    public float SubmergedAmount { get; set; }

    public Rigidbody RigidBody
    {
        get
        {
            if (rigidBody == null)
                rigidBody = GetComponent<Rigidbody>();

            return rigidBody;
        }
    }

    public MeshCollider Collider
    {
        get
        {
            return meshCollider;
        }
    }

    public float Heat
    {
        set
        {
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(value * initialRateOverTime);
            submergeAudio.volume = value * initialVolume;
        }
    }

    [SerializeField]
    private Transform gravityTarget;
    [SerializeField]
    private ParticleSystem particles;
    [SerializeField]
    private GameObject particlesPrefab;

    private AudioSource submergeAudio;
    private ParticleSystem.EmissionModule emission;
    private float initialRateOverTime;
    private float initialVolume;
    private float adjustmentForce = 2.5f;
    private float adjustmentTorque = 20f;
    private float distToCenter;
    private Rigidbody rigidBody;
    private MeshCollider meshCollider;
    private MeshRenderer meshRenderer;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        meshCollider = GetComponentInChildren<MeshCollider>();
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        distToCenter = Vector3.Distance(transform.position, gravityTarget.position);
        emission = particles.emission;
        initialRateOverTime = emission.rateOverTime.constant;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0);
        submergeAudio = GetComponent<AudioSource>();
        initialVolume = 0.25f;
        submergeAudio.volume = 0;
        submergeAudio.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
    }

    private void FixedUpdate()
    {
        Vector3 idealPosition = gravityTarget.position + (transform.position - gravityTarget.position).normalized * distToCenter;
        float currentDistToCenter = Vector3.Distance(transform.position, gravityTarget.position);

        Vector3 force = idealPosition - transform.position;
        if (currentDistToCenter > distToCenter)
        {
            force *= 2;
            SubmergedAmount = 0;
        }
        else
        {
            SubmergedAmount = (distToCenter - currentDistToCenter);
        }
        rigidBody.AddForce(force * adjustmentForce, ForceMode.Acceleration);

        Vector3 upVector = (transform.position - gravityTarget.position).normalized;
        transform.up = Vector3.Lerp(transform.up, upVector, Time.fixedDeltaTime * adjustmentTorque);
    }

    public void OnCollisionEnter(Collision collision)
    {
        // Only collide with other lava chunks
        ContactPoint contact = collision.contacts[0];
        if (!contact.otherCollider.CompareTag("LavaChunk"))
            return;

        OnCollision?.Invoke(contact.point, collision.relativeVelocity.magnitude);
    }

#if UNITY_EDITOR

    private void CreateParticles()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        GameObject particleObject = (GameObject)PrefabUtility.InstantiatePrefab(particlesPrefab, meshRenderer.transform);
        particles = particleObject.GetComponent<ParticleSystem>();
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.meshRenderer = meshRenderer;
        UnityEditor.EditorUtility.SetDirty(particles);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private void Create()
    {
        GameObject renderObject = new GameObject("Renderer");
        renderObject.transform.position = transform.position;
        renderObject.transform.rotation = transform.rotation;
        MeshRenderer mr = renderObject.AddComponent<MeshRenderer>();
        MeshFilter mf = renderObject.AddComponent<MeshFilter>();

        GameObject meshObject = new GameObject("Collider");
        meshObject.transform.position = transform.position;
        meshObject.transform.rotation = transform.rotation;
        MeshCollider mc = meshObject.AddComponent<MeshCollider>();

        mr.sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
        mf.sharedMesh = GetComponent<MeshFilter>().sharedMesh;
        mc.sharedMesh = mf.sharedMesh;
        mc.convex = true;

        transform.up = (mc.bounds.center - gravityTarget.position).normalized;
        transform.position = mc.bounds.center;

        renderObject.transform.parent = transform;
        meshObject.transform.parent = transform;

        GameObject.DestroyImmediate(GetComponent<MeshRenderer>());
        GameObject.DestroyImmediate(GetComponent<MeshFilter>());
        GameObject.DestroyImmediate(GetComponent<MeshCollider>());

        CreateParticles();
    }

    [MenuItem("Surfaces/Create Lava Chunks")]
    static void CreateLavaChunks()
    {
        foreach (GameObject go in Selection.gameObjects)
            go.GetComponent<LavaChunk>().Create();
    }

    [CustomEditor(typeof(LavaChunk))]
    [CanEditMultipleObjects]
    public class LavaChunkEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            LavaChunk lc = (LavaChunk)target;

            if (GUILayout.Button("Create"))
            {
                lc.Create();
            }
        }
    }
#endif
}
