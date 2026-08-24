using Spades.Unity.Bootstrap;
using UnityEngine;

namespace Spades.Unity.Views
{
    /// <summary>Where a seat sits on screen. The human is always at the bottom.</summary>
    public enum TablePosition
    {
        Bottom,
        Left,
        Top,
        Right
    }

    public static class TablePositions
    {
        /// <summary>
        /// Maps a seat index to a screen position relative to the human seat, so the player is
        /// always at the bottom whichever seat the engine gave them. Four seats go round the
        /// table clockwise; two seats face each other.
        /// </summary>
        public static TablePosition For(int seatIndex, int humanSeatIndex, int playerCount)
        {
            int offset = ((seatIndex - humanSeatIndex) % playerCount + playerCount) % playerCount;

            if (playerCount == 2) return offset == 0 ? TablePosition.Bottom : TablePosition.Top;

            switch (offset)
            {
                case 0: return TablePosition.Bottom;
                case 1: return TablePosition.Left;
                case 2: return TablePosition.Top;
                default: return TablePosition.Right;
            }
        }

        /// <summary>Where a seat's hand is drawn.</summary>
        public static Vector2 HandAnchor(TablePosition position, LayoutSettings layout)
        {
            switch (position)
            {
                case TablePosition.Bottom: return new Vector2(0f, -layout.SeatRadiusY);
                case TablePosition.Top: return new Vector2(0f, layout.SeatRadiusY);
                case TablePosition.Left: return new Vector2(-layout.SeatRadiusX, 20f);
                default: return new Vector2(layout.SeatRadiusX, 20f);
            }
        }

        /// <summary>Where a seat's name plate and bid chip are drawn.</summary>
        public static Vector2 LabelAnchor(TablePosition position, LayoutSettings layout)
        {
            switch (position)
            {
                case TablePosition.Bottom: return new Vector2(-layout.SeatRadiusX + 60f, -layout.SeatRadiusY + 10f);
                case TablePosition.Top: return new Vector2(0f, layout.SeatRadiusY - 130f);
                case TablePosition.Left: return new Vector2(-layout.SeatRadiusX + 20f, -160f);
                default: return new Vector2(layout.SeatRadiusX - 20f, -160f);
            }
        }

        /// <summary>Where a card played by this seat lands in the middle of the table.</summary>
        public static Vector2 TrickSlot(TablePosition position, LayoutSettings layout)
        {
            switch (position)
            {
                case TablePosition.Bottom: return new Vector2(0f, -layout.TrickRadius);
                case TablePosition.Top: return new Vector2(0f, layout.TrickRadius);
                case TablePosition.Left: return new Vector2(-layout.TrickRadius * 1.35f, 8f);
                default: return new Vector2(layout.TrickRadius * 1.35f, 8f);
            }
        }

        /// <summary>A small deterministic tilt, so a completed trick looks thrown rather than placed.</summary>
        public static float TrickRotation(TablePosition position)
        {
            switch (position)
            {
                case TablePosition.Bottom: return -4f;
                case TablePosition.Top: return 5f;
                case TablePosition.Left: return 8f;
                default: return -7f;
            }
        }

        public static bool IsVertical(TablePosition position)
        {
            return position == TablePosition.Left || position == TablePosition.Right;
        }
    }
}
