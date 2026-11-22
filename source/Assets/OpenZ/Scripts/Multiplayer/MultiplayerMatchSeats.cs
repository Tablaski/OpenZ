using System;
using System.Collections.Generic;

namespace OpenZ.Multiplayer
{
    internal enum MatchSeatRole { Human, Bot, Observer }
    internal enum MatchStartBlocker { None, UnassignedPlayer, InvalidAssignment, EmptyStartingPositions, NoActiveHuman, UnequalTeams }

    // A negative position is reserved for observers and humans still waiting for a position.
    internal readonly struct MatchSeatAssignment
    {
        public readonly MatchSeatRole Role;
        public readonly int Position;

        public MatchSeatAssignment(MatchSeatRole role, int position)
        {
            Role = role;
            Position = position;
        }

        public bool MayUsePlayerCommands => Role == MatchSeatRole.Human && Position >= 0 && Position < 8;

        public bool MayFollow(MatchSeatAssignment target) => Role == MatchSeatRole.Observer && target.MayUsePlayerCommands;

        public bool SharesMatchChatWith(MatchSeatAssignment other) =>
            Role != MatchSeatRole.Bot && Role == other.Role;
    }

    internal static class MultiplayerMatchSeats
    {
        public static MatchStartBlocker Check(int startingPositions, IEnumerable<MatchSeatAssignment> assignments)
        {
            if (startingPositions < 2 || startingPositions > 8) throw new ArgumentOutOfRangeException(nameof(startingPositions));
            int occupied = 0;
            bool unassignedHuman = false;
            bool activeHuman = false;
            foreach (var entry in assignments)
            {
                if (entry.Role == MatchSeatRole.Observer)
                {
                    if (entry.Position != -1) return MatchStartBlocker.InvalidAssignment;
                    continue;
                }
                if (entry.Role != MatchSeatRole.Human && entry.Role != MatchSeatRole.Bot)
                    return MatchStartBlocker.InvalidAssignment;
                int position = entry.Position;
                if (entry.Role == MatchSeatRole.Human && (position == -1 || position >= startingPositions))
                {
                    unassignedHuman = true;
                    continue;
                }
                if (position < 0 || position >= startingPositions || (occupied & (1 << position)) != 0)
                    return MatchStartBlocker.InvalidAssignment;
                occupied |= 1 << position;
                activeHuman |= entry.Role == MatchSeatRole.Human;
            }
            if (unassignedHuman) return MatchStartBlocker.UnassignedPlayer;
            if (occupied != (1 << startingPositions) - 1) return MatchStartBlocker.EmptyStartingPositions;
            return activeHuman ? MatchStartBlocker.None : MatchStartBlocker.NoActiveHuman;
        }
    }
}
