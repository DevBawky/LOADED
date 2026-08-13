using System;

[Serializable]
public sealed class NodeResult
{
    public bool succeeded = true;
    public int goldDelta;
    public int healthDelta;
    public string grantedBulletAssetName;
    public string grantedItemAssetName;
}
