using System;
using Spades.Core.Cards;
using Spades.Core.State;

namespace Spades.Core.Rules
{
    /// <summary>
    /// Immutable configuration for one game.
    ///
    /// 2-player and 4-player are two configurations of one engine rather than two code paths.
    /// Adding a 200-point game or a no-Nil variant is a new set of values, not a new branch.
    /// </summary>
    public sealed class GameRules
    {
        public GameRules(
            int playerCount,
            int handSize,
            int targetScore,
            int bagPenaltyThreshold,
            int bagPenaltyPoints,
            int nilBonus,
            bool allowNil,
            bool usesDrawPhase)
        {
            if (playerCount != 2 && playerCount != 4)
                throw new ArgumentOutOfRangeException(nameof(playerCount), "Spades supports 2 or 4 players.");
            if (handSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(handSize), "Hand size must be positive.");
            if (playerCount * handSize > Deck.StandardSize)
                throw new ArgumentOutOfRangeException(nameof(handSize), "Not enough cards in a standard deck.");
            if (targetScore <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetScore), "Target score must be positive.");
            if (bagPenaltyThreshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(bagPenaltyThreshold), "Bag threshold must be positive.");
            if (bagPenaltyPoints > 0)
                throw new ArgumentOutOfRangeException(nameof(bagPenaltyPoints), "Bag penalty is stored negative.");
            if (nilBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(nilBonus), "Nil bonus is stored positive.");

            PlayerCount = playerCount;
            HandSize = handSize;
            TargetScore = targetScore;
            BagPenaltyThreshold = bagPenaltyThreshold;
            BagPenaltyPoints = bagPenaltyPoints;
            NilBonus = nilBonus;
            AllowNil = allowNil;
            UsesDrawPhase = usesDrawPhase;
        }

        public int PlayerCount { get; }
        public int HandSize { get; }
        public int TargetScore { get; }
        public int BagPenaltyThreshold { get; }

        /// <summary>Stored negative (-100) so scoring code only ever adds, never subtracts.</summary>
        public int BagPenaltyPoints { get; }

        /// <summary>Stored positive (100). ScoreNil applies the sign.</summary>
        public int NilBonus { get; }

        public bool AllowNil { get; }
        public bool UsesDrawPhase { get; }

        public int TeamCount => 2;

        /// <summary>
        /// Seats 0..3 map to 0,1,0,1 (the partnerships 0/2 against 1/3).
        /// Seats 0..1 map to 0,1 (head to head).
        /// One expression produces both table layouts, which is why the engine has no
        /// player-count branch anywhere in it.
        /// </summary>
        public int TeamIdForSeat(Seat seat) => seat.Index % TeamCount;

        /// <summary>The seat that leads the first trick and bids first: left of the dealer.</summary>
        public Seat FirstToAct(Seat dealer) => dealer.Next(PlayerCount);

        public static GameRules Standard4Player()
        {
            return new GameRules(
                playerCount: 4,
                handSize: 13,
                targetScore: 500,
                bagPenaltyThreshold: 10,
                bagPenaltyPoints: -100,
                nilBonus: 100,
                allowNil: true,
                usesDrawPhase: false);
        }

        public static GameRules Standard2Player()
        {
            return new GameRules(
                playerCount: 2,
                handSize: 13,
                targetScore: 500,
                bagPenaltyThreshold: 10,
                bagPenaltyPoints: -100,
                nilBonus: 100,
                allowNil: true,
                usesDrawPhase: true);
        }
    }
}
