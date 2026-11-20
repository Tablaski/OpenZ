namespace OpenZ.Sim
{
    public sealed partial class ZWorld
    {
        int[] skirmishAlliances;

        internal void SetSkirmishAlliances(int[] alliances)
        {
            skirmishAlliances = (int[])alliances.Clone();
        }

        public bool AreAllied(Team left, Team right)
        {
            if (left == right) return true;
            int leftSeat = (int)left - 1;
            int rightSeat = (int)right - 1;
            return skirmishAlliances != null &&
                (uint)leftSeat < (uint)skirmishAlliances.Length &&
                (uint)rightSeat < (uint)skirmishAlliances.Length &&
                skirmishAlliances[leftSeat] == skirmishAlliances[rightSeat];
        }

        public bool IsWinningSeat(Team team) =>
            GameOver && Winner != Team.Neutral && AreAllied(team, Winner);
    }
}
