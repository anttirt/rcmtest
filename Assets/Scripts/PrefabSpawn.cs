using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Scenes;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

struct PrefabSpawn : IComponentData, IEnableableComponent
{
	public EntityPrefabReference prefab;

	public WeakObjectReference<Material> material;
	public WeakObjectReference<Mesh> mesh;
	public BatchMaterialID batchMaterialID;
	public BatchMeshID batchMeshID;
	public bool loadRequested;
}

partial struct PrefabSpawnSystem : ISystem
{
	void ISystem.OnUpdate(ref SystemState state)
	{
		var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

		foreach(var (ps, e) in SystemAPI.Query<RefRW<PrefabSpawn>>().WithNone<RequestEntityPrefabLoaded>().WithEntityAccess())
		{
			Debug.Log($"request load for entity prefab {ps.ValueRW.prefab.AssetGUID}");
			ecb.AddComponent(e, new RequestEntityPrefabLoaded { Prefab = ps.ValueRW.prefab });

			if(ps.ValueRW.material.IsReferenceValid && ps.ValueRW.mesh.IsReferenceValid)
			{
				ps.ValueRW.material.LoadAsync();
				ps.ValueRW.mesh.LoadAsync();
				ps.ValueRW.loadRequested = true;
			}
		}

		foreach(var (ps, ltw, lr, e) in SystemAPI.Query<RefRW<PrefabSpawn>, LocalToWorld, PrefabLoadResult>().WithEntityAccess())
		{
			if(ps.ValueRW.material.IsReferenceValid && ps.ValueRW.mesh.IsReferenceValid)
			{
				if(ps.ValueRW.material.LoadingStatus == ObjectLoadingStatus.Loading || ps.ValueRW.mesh.LoadingStatus == ObjectLoadingStatus.Loading)
					continue;

				ps.ValueRW.batchMaterialID = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>().RegisterMaterial(ps.ValueRW.material.Result);
				ps.ValueRW.batchMeshID = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>().RegisterMesh(ps.ValueRW.mesh.Result);
			}

			var instance = ecb.Instantiate(lr.PrefabRoot);
			if(state.EntityManager.HasComponent<LocalTransform>(lr.PrefabRoot))
				ecb.SetComponent(instance, LocalTransform.FromPositionRotationScale(ltw.Position, ltw.Rotation, math.cmax(ltw.Value.Scale())));

			ecb.SetComponent(instance, ltw);

			if(ps.ValueRW.material.IsReferenceValid && ps.ValueRW.mesh.IsReferenceValid && state.EntityManager.HasComponent<MaterialMeshInfo>(lr.PrefabRoot))
			{
				ecb.SetComponent(instance, new MaterialMeshInfo(ps.ValueRW.batchMaterialID, ps.ValueRW.batchMeshID));
			}

			ecb.SetComponentEnabled<PrefabSpawn>(e, false);
			ecb.RemoveComponent<RequestEntityPrefabLoaded>(e);
			ecb.RemoveComponent<PrefabLoadResult>(e);

			Debug.Log($"instantiated entity prefab {ps.ValueRW.prefab.AssetGUID} loaded as {lr.PrefabRoot}, instance {instance}");
		}

		ecb.Playback(state.EntityManager);

		ecb.Dispose();
	}

	void ISystem.OnDestroy(ref SystemState state)
	{
		foreach(var (_, e) in SystemAPI.Query<LocalToWorld>().WithPresent<PrefabSpawn>().WithEntityAccess())
		{
			var ps = SystemAPI.GetComponentRW<PrefabSpawn>(e);

			if(ps.ValueRW.loadRequested)
			{
				ps.ValueRW.material.Release();
				ps.ValueRW.mesh.Release();

				ps.ValueRW.loadRequested = false;
			}
		}
	}
}
