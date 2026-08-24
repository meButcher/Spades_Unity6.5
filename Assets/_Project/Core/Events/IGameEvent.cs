namespace Spades.Core.Events
{
    /// <summary>
    /// Something that happened, stated as a value object. The core emits these and forgets;
    /// it holds no reference to the view and calls nothing on it.
    ///
    /// Three consequences worth knowing:
    ///  - the event stream is a log, so a test can assert on the exact sequence a hand produced;
    ///  - it can be serialised down a wire unchanged, which is the multiplayer story;
    ///  - the view can consume it at animation speed while the core has already moved on.
    /// </summary>
    public interface IGameEvent
    {
    }
}
