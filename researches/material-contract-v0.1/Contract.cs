namespace Gravia.MaterialContract.Reference;

// Proposal specimen, not the application API. The host must own/freeze backing
// buffers and dictionaries before publishing: records alone do not ensure this.
public enum CaseStatus { Success, Failed }
public enum OriginKind { Model, Transform, Correlation, Passport, Assumption }

public sealed record Origin(
    OriginKind Kind, string SourceId, string VersionOrHash,
    string? MethodId, string? ValidityDescription,
    IReadOnlyList<string> Dependencies);

public sealed record PropertyDefinition(
    string Id, string Version, string SiUnit, string Basis,
    string MeasurandDefinition);

public sealed record PropertyColumn(
    string DefinitionId, ReadOnlyMemory<double> Values,
    ReadOnlyMemory<bool> Present, IReadOnlyList<string> OriginIds);

// Complete composition only. PIONA descriptors may be partial and are properties.
public sealed record CompositionBlock(
    string SchemaId, IReadOnlyList<string> ComponentIds,
    ReadOnlyMemory<double> MassFractionsRowMajor,
    ReadOnlyMemory<bool> Present, IReadOnlyList<string> OriginIds);

public sealed record CaseIssue(string CaseId, string NodeId, string Code, string Detail);

public sealed record MaterialBatch(
    string ContractVersion, string RunId, string StreamId, string MaterialId,
    IReadOnlyList<string> CaseIds, ReadOnlyMemory<CaseStatus> Status,
    ReadOnlyMemory<double> MassFlowKgS,
    IReadOnlyList<CompositionBlock> Compositions,
    IReadOnlyList<PropertyColumn> Properties,
    IReadOnlyDictionary<string, Origin> Origins,
    IReadOnlyList<CaseIssue> Issues);

// Fixed before the run. Resolution errors must not silently select another source.
public sealed record PropertyBinding(string PropertyId, OriginKind Kind, string SourceId);
public sealed record InputRequirement(
    IReadOnlyList<string> RequiredPropertyIds, string? RequiredCompositionSchemaId);
