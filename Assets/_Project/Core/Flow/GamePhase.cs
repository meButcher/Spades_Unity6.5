namespace Spades.Core.Flow
{
    public enum GamePhase
    {
        /// <summary>Shuffle and hand out cards, or fill the stock in the 2-player game.</summary>
        Dealing,

        /// <summary>2-player only: players build their hands from the stock one card at a time.</summary>
        Drawing,

        Bidding,
        Playing,

        /// <summary>All thirteen tricks are played and the hand is waiting to be scored.</summary>
        HandComplete,

        GameOver
    }
}
