internal static class BattleOutcomeRules
{
    public static bool ShouldFailFromBulletDepletion(
        GameFlowState currentState,
        bool battleCompleted)
    {
        return currentState == GameFlowState.Battle && !battleCompleted;
    }
}
