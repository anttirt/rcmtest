using System.Collections.Generic;
using Unity.Collections;
using Unity.Properties;

namespace Unity.Entities.Editor
{
    unsafe struct SystemProxy : System.IEquatable<SystemProxy>
    {
        public readonly WorldProxy WorldProxy;
        public readonly int SystemIndex;

        public SystemProxy(WorldProxy worldProxy, int systemIndex, World world)
        {
            WorldProxy = worldProxy;
            SystemIndex = systemIndex;
            World = world;
        }

        public SystemProxy(ComponentSystemBase b, WorldProxy worldProxy)
        {
            WorldProxy = worldProxy;
            SystemIndex = WorldProxy.FindSystemIndexFor(b);
            World = b.World;
        }

        public SystemProxy(SystemHandle h, World w, WorldProxy worldProxy)
        {
            WorldProxy = worldProxy;
            SystemIndex = WorldProxy.FindSystemIndexFor(h);
            World = w;
        }

        ScheduledSystemData ScheduledSystemData
        {
            get
            {
                if (!Valid || SystemIndex >= WorldProxy.AllSystemData.Count || SystemIndex < 0)
                    return default;

                return WorldProxy.AllSystemData[SystemIndex];
            }
        }

        SystemFrameData FrameData
        {
            get
            {
                if (!Valid || SystemIndex >= WorldProxy.AllFrameData.Count || SystemIndex < 0)
                    return default;

                return WorldProxy.AllFrameData[SystemIndex];
            }
        }

        public SystemCategory Category
        {
            get
            {
                if (!Valid || SystemIndex >= WorldProxy.AllSystemData.Count || SystemIndex < 0)
                    return SystemCategory.Unknown;

                return WorldProxy.AllSystemData[SystemIndex].Category;
            }
        }

        public bool Valid => WorldProxy != null;

        public SystemProxy Parent => ScheduledSystemData.ParentIndex == -1 ? default : WorldProxy.AllSystems[ScheduledSystemData.ParentIndex];

        public int ChildCount => ScheduledSystemData.ChildCount;
        public int FirstChildIndexInWorld => ScheduledSystemData.ChildIndex;

        public string NicifiedDisplayName => ScheduledSystemData.NicifiedDisplayName;
        public string TypeName => ScheduledSystemData.TypeName;
        public string TypeFullName => ScheduledSystemData.FullName;
        public string Namespace => ScheduledSystemData.Namespace;

        public void SetEnabled(bool value)
        {
            WorldProxy.SetSystemEnabledState(SystemIndex, value, World);
        }

        public bool Enabled => FrameData.Enabled;

        public bool IsRunning => FrameData.IsRunning;

        public int TotalEntityMatches => FrameData.EntityCount;

        public float RunTimeMillisecondsForDisplay => FrameData.LastFrameRuntimeMilliseconds;

        public IReadOnlyList<SystemProxy> UpdateBeforeSet => WorldProxy.GetUpdateBeforeSet(this);

        public IReadOnlyList<SystemProxy> UpdateAfterSet => WorldProxy.GetUpdateAfterSet(this);

        public IReadOnlyList<SystemProxy> UpdateBeforeReverseSet => WorldProxy.GetUpdateBeforeReverseSet(this);

        public IReadOnlyList<SystemProxy> UpdateAfterReverseSet => WorldProxy.GetUpdateAfterReverseSet(this);

        public string[] GetComponentTypesUsedByQueries()
        {
            if (World == null || !World.IsCreated || !Valid)
                return System.Array.Empty<string>();

            var ptr = StatePointer;
            if (ptr == null || ptr->EntityQueries.Length <= 0)
                return System.Array.Empty<string>();

            var componentNames = new NativeHashSet<FixedString128Bytes>(1, Allocator.Temp);
            var queries = ptr->EntityQueries;
            for (var i = 0; i < queries.Length; i++)
            {
                var queryTypes = queries[i].GetQueryTypes();
                foreach (var queryType in queryTypes)
                {
                    var typeName = TypeUtility.GetTypeDisplayName(TypeManager.GetType(queryType.TypeIndex));
                    componentNames.TryAdd(typeName);
                }
            }

            var nameArr = new string[componentNames.Count];
            var index = 0;
            foreach (var name in componentNames)
            {
                nameArr[index] = name.Value;
                index++;
            }
            componentNames.Dispose();

            return nameArr;
        }

        public bool Equals(SystemProxy other)
        {
            if (!other.Valid)
                return false;

            return WorldProxy.SequenceNumber.Equals(other.WorldProxy.SequenceNumber) && SystemIndex == other.SystemIndex;
        }

        public override int GetHashCode()
        {
            var hash = WorldProxy.GetHashCode();
            hash = hash * 31 + SystemIndex;
            return hash;
        }

        public void FillListWithJobDependencyForReadingSystems(List<ComponentViewData> dependencies)
        {
            if (World == null || !World.IsCreated || !Valid)
                return;

            var ptr = StatePointer;
            if (ptr != null && ptr->m_JobDependencyForReadingSystems.Length > 0)
            {
                var jobDependencies = ptr->m_JobDependencyForReadingSystems;
                for (var i = 0; i < jobDependencies.Length; i++)
                {
                    var componentType = ComponentType.FromTypeIndex(jobDependencies[i]);
                    var type = componentType.GetManagedType();
                    var name = TypeUtility.GetTypeDisplayName(type);
                    dependencies.Add(new ComponentViewData(type, name, ComponentType.AccessMode.ReadOnly, ComponentsUtility.GetComponentKind(componentType)));
                }
            }
        }

        public void FillListWithJobDependencyForWritingSystems(List<ComponentViewData> dependencies)
        {
            if (World == null || !World.IsCreated || !Valid)
                return;

            var ptr = StatePointer;
            if (ptr != null && ptr->m_JobDependencyForWritingSystems.Length > 0)
            {
                var jobDependencies = ptr->m_JobDependencyForWritingSystems;
                for (var i = 0; i < jobDependencies.Length; i++)
                {
                    var componentType = ComponentType.FromTypeIndex(jobDependencies[i]);
                    var type = componentType.GetManagedType();
                    var name = TypeUtility.GetTypeDisplayName(type);
                    dependencies.Add(new ComponentViewData(type, name, ComponentType.AccessMode.ReadWrite, ComponentsUtility.GetComponentKind(componentType)));
                }
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is SystemProxy sel)
            {
                return Equals(sel);
            }

            return false;
        }

        public static bool operator ==(SystemProxy lhs, SystemProxy rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(SystemProxy lhs, SystemProxy rhs)
        {
            return !lhs.Equals(rhs);
        }

        public override string ToString()
        {
            return TypeFullName;
        }

        public World World { get; }

        public SystemState* StatePointerForQueryResults => StatePointer;

        SystemState* StatePointer
        {
            get
            {
                if (World == null || !World.IsCreated)
                    return null;

                if ((Category & SystemCategory.Unmanaged) == 0)
                    return ScheduledSystemData.Managed.m_StatePtr;

                var world = World;
                if (world != null && world.IsCreated)
                    return world.Unmanaged.ResolveSystemState(ScheduledSystemData.WorldSystemHandle);

                return null;
            }
        }

        internal static void BuildSystemDependencyMap(SystemProxy systemProxy, Dictionary<string, string[]> dependencyMap)
        {
            var keyString = systemProxy.TypeName;

            // TODO: Find better solution to be able to uniquely identify each system.
            // At the moment, we are using system name to identify each system, which is not reliable
            // because there can be multiple systems with the same name in a world. This is only a
            // temporary solution to avoid the error of adding the same key into the map. We need to
            // find a proper solution to be able to uniquely identify each system.
            if (!dependencyMap.ContainsKey(keyString))
            {
                var handle = systemProxy;

                var beforeSet = handle.UpdateBeforeSet;
                var afterSet = handle.UpdateAfterSet;
                var dependenciesList = new List<string>();
                foreach (var s in beforeSet)
                    dependenciesList.Add(s.TypeName);
                foreach (var s in afterSet)
                    dependenciesList.Add(s.TypeName);
                var dependencies = dependenciesList.ToArray();

                dependencyMap.Add(keyString, dependencies);
            }
        }
    }
}

