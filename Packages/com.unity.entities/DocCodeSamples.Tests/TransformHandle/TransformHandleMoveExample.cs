namespace Doc.CodeSamples.Tests
{
#region example
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class TransformHandleMoveExample : MonoBehaviour
{
    public int SpawnCount = 1000;
    // TransformHandle can be stored in NativeArray and accessed from Burst-compiled code
    NativeArray<TransformHandle> SpawnedTransforms;
    public Unity.Mathematics.Random Random;

    void Start()
    {
        Random = Unity.Mathematics.Random.CreateFromIndex(0);

        // Allocate native array of handles (no separate float3 buffer needed)
        SpawnedTransforms = new NativeArray<TransformHandle>(SpawnCount, Allocator.Persistent);

        // Create transforms and assign random start positions
        for (int i = 0; i < SpawnedTransforms.Length; i++)
        {
            TransformHandle transformHandle = new GameObject($"Transform{i}").transformHandle;
            SpawnedTransforms[i] = transformHandle;
            transformHandle.position = Random.NextFloat3(new float3(-100f), new float3(100f));
        }
    }

    void OnDestroy()
    {
        if (SpawnedTransforms.IsCreated)
        {
            SpawnedTransforms.Dispose();
        }
    }

    void Update()
    {
        // Single Burst-compiled call: reads positions, computes movement, and writes new positions. No manual copying needed.
        TransformHandleMoveExampleUtils.ComputeAndApplyRandomMovements(ref SpawnedTransforms, ref Random, 1f * Time.deltaTime);
    }
}


[BurstCompile]
public static class TransformHandleMoveExampleUtils
{
    [BurstCompile]
    public static void ComputeAndApplyRandomMovements(ref NativeArray<TransformHandle> transforms,
        ref Unity.Mathematics.Random random, float movementMagnitude)
    {
        for (int i = 0; i < transforms.Length; i++)
        {
            // TransformHandle positions can be read and written directly in Burst-compiled code
            TransformHandle transformHandle = transforms[i];
            float3 movement = math.normalizesafe(transforms[random.NextInt(0, transforms.Length)]
                .position - transformHandle.position) * movementMagnitude;
            transformHandle.position = transformHandle.position + (Vector3)movement;
        }
    }
}
#endregion
}
