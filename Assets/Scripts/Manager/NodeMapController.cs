public sealed class NodeMapController : NodeMapControllerDefinition
{
    private void Start()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        ClipPathsToViewport();
    }

    private void OnDestroy()
    {
        ReleaseRuntimeResources();
    }
}
