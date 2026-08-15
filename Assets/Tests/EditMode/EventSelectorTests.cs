using NUnit.Framework;
using UnityEngine;

public sealed class EventSelectorTests
{
    [Test]
    public void Select_ExcludesCompletedEvent_EvenWhenRepeatable()
    {
        EventDefinition completed = CreateEvent("completed", false, 100f);
        EventDefinition available = CreateEvent("available", true, 1f);

        try
        {
            EventDefinition selected = EventSelector.Select(
                new[] { completed, available },
                default,
                new[] { completed.StableId });

            Assert.That(selected, Is.SameAs(available));
        }
        finally
        {
            Object.DestroyImmediate(completed);
            Object.DestroyImmediate(available);
        }
    }

    private static EventDefinition CreateEvent(
        string id,
        bool oncePerRun,
        float weight)
    {
        EventDefinition definition =
            ScriptableObject.CreateInstance<EventDefinition>();
        definition.eventId = id;
        definition.oncePerRun = oncePerRun;
        definition.baseWeight = weight;
        return definition;
    }
}
