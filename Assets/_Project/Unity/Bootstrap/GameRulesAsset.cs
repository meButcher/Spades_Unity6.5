using Spades.Core.Rules;
using UnityEngine;

namespace Spades.Unity.Bootstrap
{
    /// <summary>
    /// An inspector-authorable rule variant.
    ///
    /// Note what it is not: it is not the GameRules the engine uses. Spades.Core has engine
    /// references switched off and so cannot so much as name ScriptableObject. The asset is a
    /// Unity-side description that produces the plain object the core understands.
    ///
    /// That boundary is enforced by the compiler rather than by discipline, which is the point:
    /// a two-hundred-point variant becomes a duplicated asset instead of a branch in the engine.
    /// </summary>
    [CreateAssetMenu(fileName = "GameRules", menuName = "Spades/Game Rules")]
    public sealed class GameRulesAsset : ScriptableObject
    {
        [Range(2, 4)] public int PlayerCount = 4;
        [Min(1)] public int HandSize = 13;
        [Min(1)] public int TargetScore = 500;
        [Min(1)] public int BagPenaltyThreshold = 10;
        public int BagPenaltyPoints = -100;
        [Min(0)] public int NilBonus = 100;
        public bool AllowNil = true;
        public bool UsesDrawPhase;

        public GameRules ToGameRules()
        {
            return new GameRules(
                PlayerCount,
                HandSize,
                TargetScore,
                BagPenaltyThreshold,
                BagPenaltyPoints,
                NilBonus,
                AllowNil,
                UsesDrawPhase);
        }
    }
}
