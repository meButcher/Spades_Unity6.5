using Spades.Unity.Views;

namespace Spades.Unity.Presentation
{
    /// <summary>
    /// Human-readable seat and team names.
    ///
    /// It lives in the Unity layer on purpose: Spades.Core prints seats as S0..S3 because it has
    /// no idea which of them a person is sitting in. Names are a presentation fact.
    /// </summary>
    public sealed class SeatNaming
    {
        private readonly string[] _seatNames;
        private readonly string[] _teamNames;
        private readonly int _humanTeam;

        public SeatNaming(int playerCount, int humanSeat, int humanTeam)
        {
            _humanTeam = humanTeam;
            _seatNames = new string[playerCount];

            for (int seat = 0; seat < playerCount; seat++)
            {
                TablePosition position = TablePositions.For(seat, humanSeat, playerCount);

                if (seat == humanSeat) _seatNames[seat] = "You";
                else if (playerCount == 2) _seatNames[seat] = "Opponent";
                else if (position == TablePosition.Left) _seatNames[seat] = "West";
                else if (position == TablePosition.Top) _seatNames[seat] = "North";
                else _seatNames[seat] = "East";
            }

            _teamNames = playerCount == 2
                ? new[] { "You", "Opponent" }
                : new[] { "Your team", "Opponents" };

            // Team ids are seat index modulo two, so the human is not guaranteed to be team zero.
            if (humanTeam != 0)
            {
                string swap = _teamNames[0];
                _teamNames[0] = _teamNames[1];
                _teamNames[1] = swap;
            }
        }

        public string SeatName(int seatIndex) => _seatNames[seatIndex];

        public string TeamName(int teamId) => _teamNames[teamId];

        public bool IsHumanTeam(int teamId) => teamId == _humanTeam;
    }
}
