using System.Collections.Generic;

namespace Spades.Core.State
{
    /// <summary>
    /// A scoring unit. In 4-player it holds two seats; in 2-player it holds one.
    /// Nothing else in the engine needs to know the difference, which is the payoff of not
    /// hard-coding North/South/East/West anywhere.
    /// </summary>
    public sealed class TeamState
    {
        private readonly List<Seat> _seats;

        public TeamState(int teamId)
        {
            TeamId = teamId;
            _seats = new List<Seat>(2);
        }

        public int TeamId { get; }
        public IReadOnlyList<Seat> Seats => _seats;
        public int Score { get; set; }
        public int Bags { get; set; }

        public void AddSeat(Seat seat) => _seats.Add(seat);
    }
}
