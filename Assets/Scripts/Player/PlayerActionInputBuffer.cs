internal sealed class PlayerActionInputBuffer
{
    private bool hasValue;
    private PlayerBehaviourAction action;
    private float expiresAt;

    public void Store(
        PlayerBehaviourAction inputAction,
        float currentTime,
        float duration)
    {
        hasValue = true;
        action = inputAction;
        expiresAt = currentTime + System.Math.Max(0f, duration);
    }

    public bool TryPeek(float currentTime, out PlayerBehaviourAction value)
    {
        if (!hasValue || currentTime > expiresAt)
        {
            Clear();
            value = default;
            return false;
        }

        value = action;
        return true;
    }

    public bool TryConsume(
        PlayerBehaviourAction expectedAction,
        float currentTime)
    {
        if (!TryPeek(currentTime, out PlayerBehaviourAction bufferedAction)
            || bufferedAction != expectedAction)
        {
            return false;
        }

        Clear();
        return true;
    }

    public void Clear()
    {
        hasValue = false;
        action = default;
        expiresAt = 0f;
    }
}
