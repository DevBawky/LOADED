using System.Collections.Generic;
using NUnit.Framework;

public sealed class EnemySupportTargetSelectorTests
{
    [Test]
    public void HealCandidateTakesPriorityOverCloserShieldCandidate()
    {
        List<EnemySupportTargetCandidate<string>> candidates = new()
        {
            Candidate("shield", 100, 100, 0, 1, 1),
            Candidate("heal", 40, 100, 10, 4, 4)
        };

        bool selected = EnemySupportTargetSelector.TrySelect(
            candidates,
            0.5f,
            out string target,
            out EnemySupportType supportType);

        Assert.That(selected, Is.True);
        Assert.That(target, Is.EqualTo("heal"));
        Assert.That(supportType, Is.EqualTo(EnemySupportType.Heal));
    }

    [Test]
    public void HealUsesRatioThenDistanceThenTileIndex()
    {
        List<EnemySupportTargetCandidate<string>> candidates = new()
        {
            Candidate("higher-ratio", 40, 100, 0, 1, 1),
            Candidate("farther", 30, 100, 0, 4, 2),
            Candidate("higher-tile", 30, 100, 0, 2, 7),
            Candidate("winner", 30, 100, 0, 2, 3)
        };

        bool selected = EnemySupportTargetSelector.TrySelect(
            candidates,
            0.5f,
            out string target,
            out EnemySupportType supportType);

        Assert.That(selected, Is.True);
        Assert.That(target, Is.EqualTo("winner"));
        Assert.That(supportType, Is.EqualTo(EnemySupportType.Heal));
    }

    [Test]
    public void ShieldUsesDistanceThenTileIndexWhenNobodyNeedsHealing()
    {
        List<EnemySupportTargetCandidate<string>> candidates = new()
        {
            Candidate("farther", 90, 100, 0, 4, 1),
            Candidate("higher-tile", 80, 100, 0, 2, 8),
            Candidate("winner", 70, 100, 0, 2, 3)
        };

        bool selected = EnemySupportTargetSelector.TrySelect(
            candidates,
            0.5f,
            out string target,
            out EnemySupportType supportType);

        Assert.That(selected, Is.True);
        Assert.That(target, Is.EqualTo("winner"));
        Assert.That(supportType, Is.EqualTo(EnemySupportType.Shield));
    }

    [Test]
    public void IneligibleAndAlreadyShieldedCandidatesAreIgnored()
    {
        List<EnemySupportTargetCandidate<string>> candidates = new()
        {
            Candidate("ineligible-heal", 1, 100, 0, 1, 1, false),
            Candidate("shielded", 100, 100, 5, 1, 2)
        };

        bool selected = EnemySupportTargetSelector.TrySelect(
            candidates,
            0.5f,
            out string target,
            out EnemySupportType supportType);

        Assert.That(selected, Is.False);
        Assert.That(target, Is.Null);
        Assert.That(supportType, Is.EqualTo(EnemySupportType.None));
    }

    private static EnemySupportTargetCandidate<string> Candidate(
        string target,
        int currentHealth,
        int maxHealth,
        int currentShield,
        int playerDistance,
        int tileIndex,
        bool isEligible = true)
    {
        return new EnemySupportTargetCandidate<string>(
            target,
            isEligible,
            currentHealth,
            maxHealth,
            currentShield,
            playerDistance,
            tileIndex);
    }
}
