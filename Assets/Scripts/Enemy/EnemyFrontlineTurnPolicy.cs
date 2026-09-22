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
