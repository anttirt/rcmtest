using Unity.Entities;

struct StreamingAssetsCatalogEntry
{
	// content hash of the file
	public Hash128 ContentHash;

	// relative path of the file under StreamingAssets
	public BlobString FilePath;
}

struct StreamingAssetsCatalogData
{
	public const int kVersion = 1;
	public const string kFilename = "streaming_assets.catalog";

	public BlobArray<StreamingAssetsCatalogEntry> Entries;
}
