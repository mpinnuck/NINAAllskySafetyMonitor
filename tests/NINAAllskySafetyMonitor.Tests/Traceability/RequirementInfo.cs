namespace NINAAllskySafetyMonitor.Tests.Traceability;

/// <summary>One requirement parsed from requirements.md.</summary>
/// <param name="Id">Identifier, e.g. "REQ-23".</param>
/// <param name="Text">Requirement text without markdown emphasis.</param>
public sealed record RequirementInfo(string Id, string Text);
