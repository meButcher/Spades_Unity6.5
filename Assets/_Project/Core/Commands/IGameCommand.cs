using Spades.Core.State;

namespace Spades.Core.Commands
{
    /// <summary>
    /// A request to change the game. Commands are the only way into GameState, so validation
    /// lives in exactly one place and there is no path that skips the rules.
    ///
    /// They are also the natural network payload: a ServerRpc carrying a command needs no
    /// change to anything below this interface.
    /// </summary>
    public interface IGameCommand
    {
        Seat Seat { get; }
    }
}
