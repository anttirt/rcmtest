// Documentation code examples for the "Transforms comparison" documentation page.

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Doc.CodeSamples.Tests.TransformsComparison
{
    // Marker used by the multithreaded examples so each IJobEntity has a
    // query constraint instead of iterating every entity in the world.
    public struct ExampleTag : IComponentData {}

    // Hosts the single-threaded helpers and a (non-documented) ScheduleAll
    // method that exists purely so the multithreaded snippets compile against
    // SystemAPI.
    public partial struct TransformsComparisonExamples : ISystem
    {
        // ---------- Properties ----------

        #region childCount
        int ChildCount(ref SystemState state, Entity e)
        {
            // Entities without children don't have a Child buffer, so a check is
            // needed to match Transform.childCount returning 0 in that case.
            if (SystemAPI.HasBuffer<Child>(e))
                return SystemAPI.GetBuffer<Child>(e).Length;
            return 0;
        }
        #endregion

        #region forward
        float3 Forward(ref SystemState state, Entity e)
        {
            return math.normalize(SystemAPI.GetComponent<LocalToWorld>(e).Forward);
        }
        #endregion

        #region localPosition
        float3 LocalPosition(ref SystemState state, Entity e)
        {
            return SystemAPI.GetComponent<LocalTransform>(e).Position;
        }
        #endregion

        #region localPosition-set
        void SetLocalPosition(ref SystemState state, Entity e, float3 localPosition)
        {
            // WithPosition preserves the existing Scale and Rotation.
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            SystemAPI.SetComponent(e, transform.WithPosition(localPosition));
        }
        #endregion

        #region localRotation
        quaternion LocalRotation(ref SystemState state, Entity e)
        {
            return SystemAPI.GetComponent<LocalTransform>(e).Rotation;
        }
        #endregion

        #region localRotation-set
        void SetLocalRotation(ref SystemState state, Entity e, quaternion localRotation)
        {
            // WithRotation preserves the existing Position and Scale.
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            SystemAPI.SetComponent(e, transform.WithRotation(localRotation));
        }
        #endregion

        #region localScale
        float3 LocalScale(ref SystemState state, Entity e)
        {
            // Examples assume uniform scale. LocalTransform.Scale is a single scalar.
            return new float3(SystemAPI.GetComponent<LocalTransform>(e).Scale);
        }
        #endregion

        #region localScale-set
        void SetLocalScale(ref SystemState state, Entity e, float scale)
        {
            // These examples assume uniform scale, so WithScale takes a single
            // scalar and preserves the existing Position and Rotation.
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            SystemAPI.SetComponent(e, transform.WithScale(scale));
        }
        #endregion

        #region localToWorldMatrix
        float4x4 LocalToWorldMatrix(ref SystemState state, Entity e)
        {
            return SystemAPI.GetComponent<LocalToWorld>(e).Value;
        }
        #endregion

        #region lossyScale
        float3 LossyScale(ref SystemState state, Entity e)
        {
            return SystemAPI.GetComponent<LocalToWorld>(e).Value.Scale();
        }
        #endregion

        #region parent
        Entity Parent(ref SystemState state, Entity e)
        {
            // Root entities have no Parent component, so return Entity.Null to match
            // Transform.parent returning null in that case.
            return SystemAPI.TryGetComponent<Parent>(e, out var parent)
                ? parent.Value
                : Entity.Null;
        }
        #endregion

        #region position
        float3 Position(ref SystemState state, Entity e)
        {
            return SystemAPI.GetComponent<LocalToWorld>(e).Position;
        }
        #endregion

        #region position-set
        void SetPosition(ref SystemState state, Entity e, float3 worldPosition)
        {
            // If the entity has a parent, convert the world-space position into
            // the local space of the parent entity before assigning it.
            float3 localPosition = worldPosition;
            if (SystemAPI.HasComponent<Parent>(e))
            {
                Entity parent = SystemAPI.GetComponent<Parent>(e).Value;
                float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
                localPosition = parentL2W.InverseTransformPoint(worldPosition);
            }
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            SystemAPI.SetComponent(e, transform.WithPosition(localPosition));
        }
        #endregion

        #region right
        float3 Right(ref SystemState state, Entity e)
        {
            return math.normalize(SystemAPI.GetComponent<LocalToWorld>(e).Right);
        }
        #endregion

        #region root
        Entity Root(ref SystemState state, Entity e)
        {
            while (SystemAPI.TryGetComponent<Parent>(e, out var parent))
                e = parent.Value;
            return e;
        }
        #endregion

        #region rotation
        quaternion Rotation(ref SystemState state, Entity e)
        {
            return SystemAPI.GetComponent<LocalToWorld>(e).Value.Rotation();
        }
        #endregion

        #region rotation-set
        void SetRotation(ref SystemState state, Entity e, quaternion worldRotation)
        {
            // If the entity has a parent, convert the world-space rotation into
            // the local space of the parent entity before assigning it.
            quaternion localRotation = worldRotation;
            if (SystemAPI.HasComponent<Parent>(e))
            {
                Entity parent = SystemAPI.GetComponent<Parent>(e).Value;
                float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
                localRotation = parentL2W.InverseTransformRotation(worldRotation);
            }
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            SystemAPI.SetComponent(e, transform.WithRotation(localRotation));
        }
        #endregion

        #region up
        float3 Up(ref SystemState state, Entity e)
        {
            return math.normalize(SystemAPI.GetComponent<LocalToWorld>(e).Up);
        }
        #endregion

        #region worldToLocalMatrix
        float4x4 WorldToLocalMatrix(ref SystemState state, Entity e)
        {
            return math.inverse(SystemAPI.GetComponent<LocalToWorld>(e).Value);
        }
        #endregion

        // ---------- Methods ----------

        #region GetChild
        Entity GetChild(ref SystemState state, Entity e, int index)
        {
            if (SystemAPI.HasBuffer<Child>(e))
                return SystemAPI.GetBuffer<Child>(e)[index].Value;
            return Entity.Null;
        }
        #endregion

        #region GetLocalPositionAndRotation
        void GetLocalPositionAndRotation(ref SystemState state,
            Entity e, out float3 localPosition,
            out quaternion localRotation)
        {
            LocalTransform transform =
                    SystemAPI.GetComponent<LocalTransform>(e);
            localPosition = transform.Position;
            localRotation = transform.Rotation;
        }
        #endregion

        #region GetPositionAndRotation
        void GetPositionAndRotation(ref SystemState state, Entity e,
            out float3 position, out quaternion rotation)
        {
            float4x4 l2w =
                SystemAPI.GetComponent<LocalToWorld>(e).Value;
            position = l2w.Translation();
            rotation = l2w.Rotation();
        }
        #endregion

        #region InverseTransformDirection
        float3 InverseTransformDirection(ref SystemState state,
                                         Entity e, float3 direction)
        {
            // Transform.InverseTransformDirection ignores scale and preserves
            // the length of the direction argument, so apply only the inverse
            // of the rotation extracted from the LocalToWorld.Value matrix.
            quaternion worldRotation =
                SystemAPI.GetComponent<LocalToWorld>(e).Value.Rotation();
            return math.mul(math.inverse(worldRotation), direction);
        }
        #endregion

        #region InverseTransformPoint
        float3 InverseTransformPoint(ref SystemState state, Entity e, float3 position)
        {
            return SystemAPI.GetComponent<LocalToWorld>(e).Value.InverseTransformPoint(position);
        }
        #endregion

        #region InverseTransformVector
        float3 InverseTransformVector(ref SystemState state, Entity e, float3 vector)
        {
            return SystemAPI.GetComponent<LocalToWorld>(e).Value.InverseTransformDirection(vector);
        }
        #endregion

        #region IsChildOf
        bool IsChildOf(ref SystemState state, Entity e, Entity parent)
        {
            // Transform.IsChildOf returns true when the candidate is the entity
            // itself, its parent entity, or any ancestor. The loop follows the
            // chain of parent references and compares each entity to the candidate.
            while (true)
            {
                if (e == parent)
                    return true;
                if (!SystemAPI.TryGetComponent<Parent>(e, out var parentComp))
                    return false;
                e = parentComp.Value;
            }
        }
        #endregion

        #region LookAt
        void LookAt(ref SystemState state, Entity e, float3 target, float3 worldUp)
        {
            // Compute the rotation in world space.
            float3 eyeWorld = SystemAPI.GetComponent<LocalToWorld>(e).Position;
            quaternion worldRotation = TransformHelpers.LookAtRotation(eyeWorld, target, worldUp);

            // If the entity has a parent, convert the rotation into the local
            // space of the parent entity.
            quaternion localRotation = worldRotation;
            if (SystemAPI.HasComponent<Parent>(e))
            {
                Entity parent = SystemAPI.GetComponent<Parent>(e).Value;
                float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
                localRotation = parentL2W.InverseTransformRotation(worldRotation);
            }

            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            SystemAPI.SetComponent(e, transform.WithRotation(localRotation));
        }
        #endregion

        #region Rotate-self
        // For Space.Self, or if the entity has no parent.
        void RotateSelf(ref SystemState state, Entity e, quaternion rotation)
        {
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            SystemAPI.SetComponent(e, transform.Rotate(rotation));
        }
        #endregion

        #region Rotate-world
        // For Space.World when the entity might have a parent.
        void RotateWorld(ref SystemState state, Entity e, quaternion rotation)
        {
            // The `rotation` argument is in world space. If the entity has a
            // parent, convert it into the local space of the parent entity
            // before applying it.
            quaternion localRotation = rotation;
            if (SystemAPI.HasComponent<Parent>(e))
            {
                Entity parent = SystemAPI.GetComponent<Parent>(e).Value;
                float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
                localRotation = parentL2W.InverseTransformRotation(rotation);
            }
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            // Apply localRotation by pre-multiplication, not with transform.Rotate
            // (which applies in the entity's own local space, post-multiplied).
            SystemAPI.SetComponent(e, transform.WithRotation(math.mul(localRotation, transform.Rotation)));
        }
        #endregion

        #region RotateAround
        void RotateAround(ref SystemState state, Entity e, float3 point,
            float3 axis, float angleDegrees)
        {
            // quaternion.AxisAngle expects a unit-length axis and an angle in radians.
            axis = math.normalize(axis);
            float angleRadians = math.radians(angleDegrees);

            // The `point` and `axis` arguments are in world space. If the
            // entity has a parent, convert them into the local space of the
            // parent entity before applying them.
            float3 localPoint = point;
            float3 localAxis = axis;
            if (SystemAPI.HasComponent<Parent>(e))
            {
                Entity parent = SystemAPI.GetComponent<Parent>(e).Value;
                float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
                localPoint = parentL2W.InverseTransformPoint(point);
                // Direction transforms scale the axis under uniform scale, so
                // renormalize to recover a unit-length axis for quaternion.AxisAngle.
                localAxis = math.normalize(parentL2W.InverseTransformDirection(axis));
            }
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            quaternion q = quaternion.AxisAngle(localAxis, angleRadians);
            transform.Position = localPoint + math.mul(q, transform.Position - localPoint);
            transform.Rotation = math.mul(q, transform.Rotation);
            SystemAPI.SetComponent(e, transform);
        }
        #endregion

        #region SetLocalPositionAndRotation
        void SetLocalPositionAndRotation(ref SystemState state, Entity e,
            float3 localPosition, quaternion localRotation)
        {
            // WithPosition / WithRotation preserve the existing Scale.
            // LocalTransform.FromPositionRotation would reset Scale to 1.
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            SystemAPI.SetComponent(e,
                transform.WithPosition(localPosition).WithRotation(localRotation));
        }
        #endregion

        #region SetPositionAndRotation-no-parent
        // If the entity has no parent.
        void SetPositionAndRotationNoParent(ref SystemState state, Entity e,
            float3 position, quaternion rotation)
        {
            // WithPosition / WithRotation preserve the existing Scale.
            // LocalTransform.FromPositionRotation would reset Scale to 1.
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            SystemAPI.SetComponent(e,
                transform.WithPosition(position).WithRotation(rotation));
        }
        #endregion

        #region SetPositionAndRotation-parent
        // If the entity might have a parent.
        void SetPositionAndRotation(ref SystemState state, Entity e,
            float3 position, quaternion rotation)
        {
            // The `position` and `rotation` arguments are in world space. If
            // the entity has a parent, convert them into the local space of
            // the parent entity before assigning them.
            float3 localPosition = position;
            quaternion localRotation = rotation;
            if (SystemAPI.HasComponent<Parent>(e))
            {
                Entity parent = SystemAPI.GetComponent<Parent>(e).Value;
                float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
                localPosition = parentL2W.InverseTransformPoint(position);
                localRotation = parentL2W.InverseTransformRotation(rotation);
            }
            // WithPosition / WithRotation preserve the existing Scale.
            // LocalTransform.FromPositionRotation would reset Scale to 1.
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            SystemAPI.SetComponent(e,
                transform.WithPosition(localPosition).WithRotation(localRotation));
        }
        #endregion

        #region TransformDirection
        float3 TransformDirection(ref SystemState state, Entity e, float3 direction)
        {
            // Transform.TransformDirection ignores scale and preserves the length
            // of the direction argument, so apply only the rotation extracted
            // from the LocalToWorld.Value matrix.
            quaternion worldRotation =
                SystemAPI.GetComponent<LocalToWorld>(e).Value.Rotation();
            return math.mul(worldRotation, direction);
        }
        #endregion

        #region TransformPoint
        float3 TransformPoint(ref SystemState state, Entity e, float3 position)
        {
            return SystemAPI.GetComponent<LocalToWorld>(e).Value.TransformPoint(position);
        }
        #endregion

        #region TransformVector
        float3 TransformVector(ref SystemState state, Entity e, float3 vector)
        {
            return SystemAPI.GetComponent<LocalToWorld>(e).Value.TransformDirection(vector);
        }
        #endregion

        #region Translate-self
        // For Space.Self.
        void TranslateSelf(ref SystemState state, Entity e, float3 translation)
        {
            // Applying LocalTransform.Rotation to the translation argument
            // produces the correct local-space translation without a parent
            // entity lookup.
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            SystemAPI.SetComponent(e,
                transform.Translate(math.mul(transform.Rotation, translation)));
        }
        #endregion

        #region Translate-world
        // For Space.World when the entity might have a parent.
        void TranslateWorld(ref SystemState state, Entity e, float3 translation)
        {
            // The `translation` argument is in world space. If the entity has
            // a parent, convert it into the local space of the parent entity
            // before applying it.
            float3 localTranslation = translation;
            if (SystemAPI.HasComponent<Parent>(e))
            {
                Entity parent = SystemAPI.GetComponent<Parent>(e).Value;
                float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
                localTranslation = parentL2W.InverseTransformDirection(translation);
            }
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            SystemAPI.SetComponent(e, transform.Translate(localTranslation));
        }
        #endregion

        // Compile-checks that the multithreaded snippets below schedule correctly
        // with current SystemAPI. Not referenced from the documentation.
        public void OnUpdate(ref SystemState state)
        {
            new ChildCountJob
            {
                ChildLookup = SystemAPI.GetBufferLookup<Child>(true)
            }.ScheduleParallel();

            new LocalPositionJob().ScheduleParallel();

            new PositionJob().ScheduleParallel();

            new ParentJob
            {
                ParentLookup = SystemAPI.GetComponentLookup<Parent>(true)
            }.ScheduleParallel();

            new TranslateJob
            {
                Translation = new float3(0, 1, 0)
            }.ScheduleParallel();
        }
    }

    // ---------- Multithreaded examples ----------

    #region childCount-job
    // Burst-compiled parallel job that reads the Child buffer length of the
    // entity that the job processes. The Child buffer is optional, so use a
    // BufferLookup field with TryGetBuffer to match Transform.childCount
    // returning 0 for childless entities.
    [BurstCompile]
    public partial struct ChildCountJob : IJobEntity
    {
        [ReadOnly] public BufferLookup<Child> ChildLookup;

        void Execute(Entity entity, in ExampleTag tag)
        {
            int childCount = ChildLookup.TryGetBuffer(entity, out var children)
                ? children.Length
                : 0;
        }
    }
    #endregion

    #region localPosition-job
    // Burst-compiled parallel job that reads the entity's LocalTransform
    // component as an Execute parameter.
    [BurstCompile]
    public partial struct LocalPositionJob : IJobEntity
    {
        void Execute(in LocalTransform transform, in ExampleTag tag)
        {
            float3 localPosition = transform.Position;
        }
    }
    #endregion

    #region position-job
    // Burst-compiled parallel job that reads the entity's LocalToWorld
    // component as an Execute parameter.
    [BurstCompile]
    public partial struct PositionJob : IJobEntity
    {
        void Execute(in LocalToWorld localToWorld, in ExampleTag tag)
        {
            float3 worldPosition = localToWorld.Position;
        }
    }
    #endregion

    #region parent-job
    // Burst-compiled parallel job that reads the Parent component of the
    // entity that the job processes. The Parent component is optional, so
    // use a ComponentLookup field with TryGetComponent to match
    // Transform.parent returning null for root entities.
    [BurstCompile]
    public partial struct ParentJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<Parent> ParentLookup;

        void Execute(Entity entity, in ExampleTag tag)
        {
            Entity parent = ParentLookup.TryGetComponent(entity, out var p)
                ? p.Value
                : Entity.Null;
        }
    }
    #endregion

    #region Translate-job
    // Multithreaded equivalent of TranslateSelf: rotate the job's Translation
    // field by the entity's own LocalTransform.Rotation before adding it to
    // LocalTransform.Position. The job doesn't need a ComponentLookup<LocalTransform>
    // because it only modifies the entity that it processes.
    [BurstCompile]
    public partial struct TranslateJob : IJobEntity
    {
        public float3 Translation;

        void Execute(ref LocalTransform transform, in ExampleTag tag)
        {
            transform.Position += math.mul(transform.Rotation, Translation);
        }
    }
    #endregion

}
