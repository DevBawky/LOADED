internal static class BattleOutcomeRules
{
    public static bool ShouldShowBulletDepletionGameOver(
        GameFlowState currentState,
        bool battleCompleted)
    {
        // StateManager may process the same depletion event first and already
        // commit RunFailed. Presentation must accept either subscriber order.
        return currentState == GameFlowState.RunFailed
            || ShouldFailFromBulletDepletion(currentState, battleCompleted);
    }

    public static bool ShouldFailFromBulletDepletion(
        GameFlowState currentState,
        bool battleCompleted)
    {
        return currentState == GameFlowState.Battle && !battleCompleted;
    }
}
