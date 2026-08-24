using System;
using System.Collections.Generic;
using Spades.Core.Events;
using Spades.Core.Flow;
using Spades.Core.Players;
using Spades.Core.Rules;
using Spades.Core.State;
using Spades.Core.Util;

namespace Spades.Core.Tests
{
    /// <summary>
    /// Drives a GameLoop the way GamePresenter does, minus the animation, and records the event
    /// stream so tests can assert on what actually happened rather than on internal state.
    ///
    /// That this class exists and needs no scene, no GameObject and no renderer is the point of
    /// the whole architecture: a complete game of Spades is a plain C# object graph.
    /// </summary>
    internal sealed class GameHarness
    {
        public GameHarness(GameRules rules, int seed, IReadOnlyList<IPlayerController> controllers)
        {
            Rules = rules;
            Seed = seed;
            Events = new GameEventQueue();
            State = new GameState(rules, controllers);
            Loop = new GameLoop(State, new SeededRandomSource(seed), Events);
            Log = new List<IGameEvent>(4096);
        }

        public GameRules Rules { get; }
        public int Seed { get; }
        public GameState State { get; }
        public GameEventQueue Events { get; }
        public GameLoop Loop { get; }
        public List<IGameEvent> Log { get; }

        /// <summary>Builds an all-bot table of the given size.</summary>
        public static GameHarness AllBots(GameRules rules, int seed)
        {
            var controllers = new IPlayerController[rules.PlayerCount];
            for (int i = 0; i < controllers.Length; i++) controllers[i] = AiPlayerController.CreateDefault();
            return new GameHarness(rules, seed, controllers);
        }

        /// <summary>One step, then move whatever it emitted into the log.</summary>
        public void Step()
        {
            Loop.Advance();
            DrainEvents();
        }

        public void DrainEvents()
        {
            while (Events.TryDequeue(out IGameEvent e)) Log.Add(e);
        }

        /// <summary>
        /// Runs until the game ends. The cap is a hang detector, not a rule: a real game finishes
        /// in a few hundred steps, so hitting it means the machine stopped making progress.
        /// </summary>
        public void RunToCompletion(int maxSteps = 200000)
        {
            int steps = 0;
            while (!Loop.IsGameOver)
            {
                if (steps++ > maxSteps)
                    throw new InvalidOperationException("Game did not terminate within " + maxSteps + " steps (seed " + Seed + ").");

                if (Loop.IsAwaitingInput && !(State.SeatAt(Loop.AwaitingSeat).Controller is AiPlayerController))
                    throw new InvalidOperationException("Loop parked on a non-bot seat during a simulation (seed " + Seed + ").");

                Step();
            }
        }

        public int CountOf<T>() where T : IGameEvent
        {
            int count = 0;
            for (int i = 0; i < Log.Count; i++)
            {
                if (Log[i] is T) count++;
            }
            return count;
        }

        public List<T> AllOf<T>() where T : IGameEvent
        {
            var found = new List<T>();
            for (int i = 0; i < Log.Count; i++)
            {
                if (Log[i] is T typed) found.Add(typed);
            }
            return found;
        }

        /// <summary>
        /// Checks the invariants that must hold for every hand of every game, no matter the seed:
        /// every hand plays exactly HandSize tricks, every card in play is accounted for, and the
        /// tricks won across the table add up to the number of tricks that existed.
        /// Returns null when everything holds, or a description of the first violation.
        /// </summary>
        public string ValidateEventStream()
        {
            int handsSeen = 0;
            int cardsThisHand = 0;
            int tricksThisHand = 0;
            var tricksBySeat = new int[Rules.PlayerCount];
            bool inHand = false;

            for (int i = 0; i < Log.Count; i++)
            {
                switch (Log[i])
                {
                    case HandStarted _:
                        if (inHand)
                        {
                            string unfinished = CheckHandTotals(handsSeen, cardsThisHand, tricksThisHand, tricksBySeat);
                            if (unfinished != null) return unfinished;
                        }

                        handsSeen++;
                        inHand = true;
                        cardsThisHand = 0;
                        tricksThisHand = 0;
                        Array.Clear(tricksBySeat, 0, tricksBySeat.Length);
                        break;

                    case CardPlayed _:
                        cardsThisHand++;
                        break;

                    case TrickWon won:
                        tricksThisHand++;
                        tricksBySeat[won.Winner.Index]++;
                        if (won.Cards.Count != Rules.PlayerCount)
                            return "Hand " + handsSeen + ": a trick contained " + won.Cards.Count + " cards.";
                        break;

                    case HandScored _:
                        string totals = CheckHandTotals(handsSeen, cardsThisHand, tricksThisHand, tricksBySeat);
                        if (totals != null) return totals;
                        inHand = false;
                        break;
                }
            }

            if (handsSeen == 0) return "No hands were played.";
            if (CountOf<GameEnded>() != 1) return "Expected exactly one GameEnded event.";

            return null;
        }

        private string CheckHandTotals(int handNumber, int cards, int tricks, int[] tricksBySeat)
        {
            if (tricks != Rules.HandSize)
                return "Hand " + handNumber + " played " + tricks + " tricks, expected " + Rules.HandSize + ".";

            int expectedCards = Rules.HandSize * Rules.PlayerCount;
            if (cards != expectedCards)
                return "Hand " + handNumber + " played " + cards + " cards, expected " + expectedCards + ".";

            int sum = 0;
            for (int i = 0; i < tricksBySeat.Length; i++) sum += tricksBySeat[i];
            if (sum != Rules.HandSize)
                return "Hand " + handNumber + ": trick counts sum to " + sum + ", expected " + Rules.HandSize + ".";

            return null;
        }
    }
}
