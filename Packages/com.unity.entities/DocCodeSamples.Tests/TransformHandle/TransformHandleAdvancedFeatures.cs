namespace Doc.CodeSamples.Tests
{
#region example
using UnityEngine;

// Demonstrates advanced features of the TransformHandle API, including
// state management, batch operations, and manual hierarchy iteration.
public class TransformHandleAdvancedFeatures : MonoBehaviour
{
    // Assign a prefab in the Inspector.
    public GameObject childPrefab;

    void Start()
    {
        // Each method demonstrates a different set of features.
        DemonstrateHandleStateAndManagement();
        DemonstrateBatchOperations();
        DemonstrateManualIteration();
    }

    // Covers checking handle validity, parent status, and un-parenting.
    private void DemonstrateHandleStateAndManagement()
    {
        Debug.Log("--- Part 1: Hierarchy management ---");

        if (childPrefab == null)
        {
            Debug.LogWarning("Child prefab not assigned. Skipping Part 1.");
            return;
        }

        // Create a child object to work with.
        GameObject childGo = Instantiate(childPrefab, transform);
        TransformHandle childHandle = childGo.transformHandle;

        // 1. Use HasParent() for a direct boolean check.
        Debug.Log($"Does child have a parent? {childHandle.HasParent()}");

        // 2. Un-parent the object by setting its parent to TransformHandle.None.
        Debug.Log("Un-parenting the child...");
        childHandle.parent = TransformHandle.None;
        Debug.Log($"Child's parent is now None: " +
            $"{childHandle.parent == TransformHandle.None}");

        // 3. Use IsValid() to check if a handle points to a valid object.
        Debug.Log($"Is the handle valid before destroying? {childHandle.IsValid()}");

        // Destroy the GameObject the handle was pointing to.
        Destroy(childGo);

        // NOTE: IsValid() will return false for this handle in the next frame,
        // as object destruction is delayed until the end of the current frame.
        // This check demonstrates the intended use case for preventing errors.
        if (!childHandle.IsValid())
        {
            Debug.Log("Handle is no longer valid after object was destroyed.");
        }
    }

    // Shows how to use batch operations for improved performance.
    private void DemonstrateBatchOperations()
    {
        Debug.Log("\n--- Part 2: Batch operations ---");

        TransformHandle handle = transformHandle;
        var localPoints = new Vector3[] { Vector3.up, Vector3.right, Vector3.forward };

        Debug.Log($"Local point [0] before TransformPoints: {localPoints[0]}");

        // TransformPoints modifies the array in-place, which is more efficient
        // than transforming each point individually in a loop.
        handle.TransformPoints(localPoints);

        Debug.Log($"World point [0] after TransformPoints: {localPoints[0]}");
    }

    // Demonstrates how to manually iterate through children with an enumerator.
    private void DemonstrateManualIteration()
    {
        Debug.Log("\n--- Part 3: Custom iteration ---");

        // Setup: Create some temporary children for the iteration demo.
        var child1 = new GameObject("Child_A");
        var child2 = new GameObject("Child_B");
        child1.transform.SetParent(transform);
        child2.transform.SetParent(transform);

        // GetDirectChildrenEnumerator provides fine-grained control over iteration.
        Debug.Log("Iterating with GetDirectChildrenEnumerator:");
        var enumerator = transformHandle.GetDirectChildrenEnumerator();
        while (enumerator.MoveNext())
        {
            TransformHandle currentChild = enumerator.Current;
            Debug.Log($"Found child via enumerator.");
        }

        // Clean up the temporary objects created for this demonstration.
        Destroy(child1);
        Destroy(child2);
    }
}
#endregion
}