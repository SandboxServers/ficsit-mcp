namespace FicsitMcp.Domain.DedicatedServer.Settings;

/// <summary>
/// The result of reading server options: the API distinguishes the options that are <b>currently
/// applied</b> (live now) from any that are <b>pending</b> (accepted but not yet in effect — typically
/// taking effect after a restart). Both are surfaced as typed <see cref="ServerOptions"/> so a caller
/// can see, for each option, what is live versus what is queued.
/// </summary>
/// <param name="Applied">
/// The currently-applied options (the live values).
/// </param>
/// <param name="Pending">
/// Options that have been accepted but are not yet in effect (e.g. awaiting a restart). When the server
/// reports no pending changes this is an all-null <see cref="ServerOptions"/> with an empty passthrough,
/// not null — "no pending changes" is a normal, queryable state.
/// </param>
/// <param name="HasPendingChanges">
/// Whether the server reported any pending option at all (true when the raw pending map was non-empty).
/// A quick "is a restart needed to apply queued changes?" signal without diffing the two views.
/// </param>
public sealed record ServerOptionsView(
    ServerOptions Applied,
    ServerOptions Pending,
    bool HasPendingChanges);
