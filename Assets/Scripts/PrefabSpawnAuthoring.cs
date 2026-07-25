#if UNITY_EDITOR
using Unity.Entities;
using UnityEngine;

class PrefabSpawnAuthoring : MonoBehaviour
{
	public GameObject prefab;
	public Material material;
	public Mesh mesh;

	class Baker : Baker<PrefabSpawnAuthoring>
	{
		public override void Bake(PrefabSpawnAuthoring authoring)
		{
			var entity = GetEntity(authoring, TransformUsageFlags.Renderable);

			AddComponent(entity, new PrefabSpawn
			{
				prefab = new(authoring.prefab),
				material = authoring.material != null ? new(authoring.material) : default,
				mesh = authoring.mesh != null ? new(authoring.mesh) : default,
			});
		}
	}
}
#endif
