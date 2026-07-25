#if !UNITY_DISABLE_MANAGED_COMPONENTS
using UnityEngine;

[assembly: Unity.Entities.RegisterGenericComponentType(typeof(Unity.Entities.CompanionComponent<Unity.Entities.Tests.CompanionComponentTestAuthoring>))]

namespace Unity.Entities.Tests
{
    // [ExecuteAlways] so OnEnable/OnDisable fire in edit mode — tests run as [Test] under the
    // editor test runner and need the MonoBehaviour activation callbacks to be observable.
    [ExecuteAlways]
    [AddComponentMenu("")]
    public class CompanionComponentTestAuthoring : MonoBehaviour
    {
        public int Value;

        // Counters for MonoBehaviour activation lifecycle. [NonSerialized] so Instantiate
        // does not carry them over from the source authoring instance — each clone starts at 0.
        [System.NonSerialized] public int OnEnableCount;
        [System.NonSerialized] public int OnDisableCount;

        void OnEnable() => OnEnableCount++;
        void OnDisable() => OnDisableCount++;
    }

    class CompanionComponentTestBaker : Baker<CompanionComponentTestAuthoring>
    {
        public override void Bake(CompanionComponentTestAuthoring authoring)
        {
            // This test might require transform components
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            AddComponentObject(entity, authoring);
            #pragma warning restore 0618
        }
    }

#if UNITY_EDITOR
    // Register the test type as a companion at assembly-load time, not from a test [SetUp].
    // Subscene baking runs in the AssetImportWorker process; a runtime AddAdditionalCompanionComponentType
    // call only mutates the static set in the main editor process and never reaches the worker.
    // [InitializeOnLoadMethod] runs during the worker's domain initialization, so the registration is
    // in place before BakingCompanionComponentSystem builds its companion query during import.
    static class CompanionComponentTestRegistration
    {
        [UnityEditor.InitializeOnLoadMethod]
        static void Register() =>
            BakingUtility.AddAdditionalCompanionComponentType(typeof(CompanionComponentTestAuthoring));
    }
#endif
}
#endif
