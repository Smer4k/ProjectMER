namespace ProjectMER.Features.Serializable.Schematics;

public class SchematicObjectDataList
{
	public string Path;

	public long RootObjectId { get; set; }

	public List<SchematicBlockData> Blocks { get; set; } = new();
}
