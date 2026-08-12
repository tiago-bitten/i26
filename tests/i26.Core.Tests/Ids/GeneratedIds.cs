using i26.Core.Ids;

namespace i26.Core.Tests.Ids;

/// <summary>
/// The whole declaration of a typed id, when the generator writes the rest. Every one of these
/// compiling at all is most of the test.
/// </summary>
[TypedId("gen")]
public readonly partial record struct GeneratedId;

/// <summary>A prefix past three characters, which the attribute has to opt into.</summary>
[TypedId("generated", UsesExtendedPrefix = true)]
public readonly partial record struct GeneratedExtendedId;

/// <summary>Not a record, to show the generator follows the declaration it was given.</summary>
[TypedId("gns")]
public readonly partial struct GeneratedStructId;

/// <summary>Not public, and not readonly.</summary>
[TypedId("gin")]
internal partial record struct GeneratedInternalId;

/// <summary>Another service's prefix: parsed here, never minted here.</summary>
[TypedId("gex", Minted = false)]
public readonly partial record struct GeneratedExternalId;
