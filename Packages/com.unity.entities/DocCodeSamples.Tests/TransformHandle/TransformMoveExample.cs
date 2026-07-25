using System;

namespace Doc.CodeSamples.Tests
{
#region example
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class TransformMoveExample : MonoBehaviour
{
    public int SpawnCount = 1000;
    // Transform API cannot be accessed from Burst-compiled code
    public Transform[] SpawnedTransforms;
    // Separate data buffer required to connect Transform and Burst
    NativeArray<float3> Positions;
    public Unity.Mathematics.Random Random;

    void Start()
    {
        Random = Unity.Mathematics.Random.CreateFromIndex(0);

        // Initialize managed Transform array and native float3 array
        // (extra overhead compared to TransformHandle)
        SpawnedTransforms = new Transform[SpawnCount];
        Positions = new NativeArray<float3>(SpawnCount, Allocator.Persistent);

        // Create transforms and assign random start positions
        for (int i = 0; i < SpawnedTransforms.Length; i++)
        {
            SpawnedTransforms[i] = new GameObject($"Transform{i}").transform;
            SpawnedTransforms[i].position = Random.NextFloat3(new float3(-100f), new float3(100f));
        }
    }

    void OnDestroy()
    {
        if (Positions.IsCreated)
        {
            Positions.Dispose();
        }
    }

    void Update()
    {
        // 1) Copy Transform positions into NativeArray (Transform cannot be accessed in Burst).
        for (int i = 0; i < SpawnedTransforms.Length; i++)
        {
            Positions[i] = SpawnedTransforms[i].position;
        }

        // 2) Burst-compiled compute on the copied data.
        TransformMoveExampleUtils.ComputeRandomMovements(ref Positions, ref Random, 1f * Time.deltaTime);

        // 3) Copy results back to each Transform to update scene state.
        for (int i = 0; i < SpawnedTransforms.Length; i++)
        {
            SpawnedTransforms[i].position = Positions[i];
        }
    }
}

[BurstCompile]
public static class TransformMoveExampleUtils
{
    [BurstCompile]
    public static void ComputeRandomMovements(ref NativeArray<float3> positions, ref Unity.Mathematics.Random random, float movementMagnitude)
    {
        for (int i = 0; i < positions.Length; i++)
        {
            // Calculate movement from copied position data
            float3 movement = math.normalizesafe(positions[random.NextInt(0, positions.Length)] - positions[i]) * movementMagnitude;
            // Updated positions will be copied back to Transforms
            positions[i] += movement;
        }
    }
}
#endregion
}
