namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// Provenance for a <see cref="GameDataSnapshot"/>: which game build it represents and
/// how it was loaded. Surfaced to operators so a version mismatch between the shipped
/// snapshot and a live game is diagnosable rather than silent.
/// </summary>
/// <param name="SnapshotVersion">
/// Our snapshot schema/asset version, e.g. <c>v1</c>. Bumped when the snapshot format
/// or the game release it tracks changes.
/// </param>
/// <param name="GameBuildId">
/// The Satisfactory Steam build id the data was generated from (e.g. <c>23300430</c>),
/// or a descriptive marker when loaded from a user-supplied Docs.json.
/// </param>
/// <param name="Source">
/// Where the data came from: the shipped embedded snapshot, or the path of a
/// user-supplied Docs.json override.
/// </param>
public sealed record GameDataMetadata(
    string SnapshotVersion,
    string GameBuildId,
    string Source);
