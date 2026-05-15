using Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ProjectMER.Features;

public static class YamlParser
{
	public static ISerializer Serializer { get; } = new SerializerBuilder()
		.WithEmissionPhaseObjectGraphVisitor(visitor => new CommentsObjectGraphVisitor(visitor.InnerVisitor))
		.WithTypeInspector(typeInspector => new CommentGatheringTypeInspector(typeInspector))
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.DisableAliases()
		.IgnoreFields()
		.WithTypeConverter(new VectorConverter())
		.Build();


	public static IDeserializer Deserializer { get; } = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.IgnoreFields()
		.WithTypeConverter(new VectorConverter())
		.Build();
}
