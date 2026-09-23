using System.Collections.Generic;

internal static class EnemyFrontlineTurnPolicy
{
    public static bool CanTakeTurn(
        bool requiresFrontline,
        bool hasBoardContext,
        int selfTileIndex,
        int playerTileIndex,
        IReadOnlyList<int> otherLivingEnemyTileIndices)
    {
        if (!requiresFrontline)
        {
            return true;
        }

        if (!hasBoardContext)
        {
            return false;
        }

        int selfOffset = selfTileIndex - playerTileIndex;

        if (selfOffset == 0 || otherLivingEnemyTileIndices == null)
        {
            return true;
        }

        foreach (int otherTileIndex in otherLivingEnemyTileIndices)
        {
            int otherOffset = otherTileIndex - playerTileIndex;
            bool isOnSameSide = selfOffset * otherOffset > 0;
            bool isCloserToPlayer =
                System.Math.Abs(otherOffset) < System.Math.Abs(selfOffset);

            if (isOnSameSide && isCloserToPlayer)
            {
                return false;
            }
        }

        return true;
    }
}

internal readonly struct EnemyLanePursuitCandidate
{
    public EnemyLanePursuitCandidate(
        int identity,
        int tileIndex,
        int laneIndex)
    {
        Identity = identity;
        TileIndex = tileIndex;
        LaneIndex = laneIndex;
    }

    public int Identity { get; }
    public int TileIndex { get; }
    public int LaneIndex { get; }
}

internal static class EnemyLanePursuitPolicy
{
    public static bool ShouldPursueLane(
        int selfIdentity,
        int playerTileIndex,
        int playerLaneIndex,
        IReadOnlyList<EnemyLanePursuitCandidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return false;
        }

        int selfCandidateIndex = -1;

        for (int index = 0; index < candidates.Count; index++)
        {
            if (candidates[index].Identity == selfIdentity)
            {
                selfCandidateIndex = index;
                break;
            }
        }

        if (selfCandidateIndex < 0)
        {
            return false;
        }

        EnemyLanePursuitCandidate self = candidates[selfCandidateIndex];
        int selfOffset = self.TileIndex - playerTileIndex;
        int selfSide = System.Math.Sign(selfOffset);
        int selectedIndex = selfCandidateIndex;

        for (int index = 0; index < candidates.Count; index++)
        {
            EnemyLanePursuitCandidate candidate = candidates[index];
            int candidateOffset = candidate.TileIndex - playerTileIndex;

            if (System.Math.Sign(candidateOffset) != selfSide
                || !HasHigherPriority(
                    candidate,
                    index,
                    candidates[selectedIndex],
                    selectedIndex,
                    playerTileIndex,
                    playerLaneIndex))
            {
                continue;
            }

            selectedIndex = index;
        }

        return selectedIndex == selfCandidateIndex;
    }

    private static bool HasHigherPriority(
        EnemyLanePursuitCandidate candidate,
        int candidateIndex,
        EnemyLanePursuitCandidate selected,
        int selectedIndex,
        int playerTileIndex,
        int playerLaneIndex)
    {
        int candidateDistance = System.Math.Abs(
            candidate.TileIndex - playerTileIndex);
        int selectedDistance = System.Math.Abs(
            selected.TileIndex - playerTileIndex);

        if (candidateDistance != selectedDistance)
        {
            return candidateDistance < selectedDistance;
        }

        bool candidateSharesPlayerLane =
            candidate.LaneIndex == playerLaneIndex;
        bool selectedSharesPlayerLane = selected.LaneIndex == playerLaneIndex;

        if (candidateSharesPlayerLane != selectedSharesPlayerLane)
        {
            return candidateSharesPlayerLane;
        }

        return candidateIndex < selectedIndex;
    }
}
