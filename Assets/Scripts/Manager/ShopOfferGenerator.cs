using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates unique shop offers while preserving the project's grade-first
/// weighted selection and Unity random call order.
/// </summary>
internal static class ShopOfferGenerator
{
    public static void GenerateBullets(
        IReadOnlyList<BulletData> pool,
        IReadOnlyList<BulletGradeWeightData> gradeWeights,
        int maximumOffers,
        List<BulletData> destination)
    {
        destination.Clear();
        List<BulletData> candidates = BuildBulletCandidates(
            pool,
            gradeWeights);
        int offerCount = Mathf.Min(
            Mathf.Max(0, maximumOffers),
            candidates.Count);

        for (int slotIndex = 0; slotIndex < offerCount; slotIndex++)
        {
            int candidateIndex = SelectWeightedCandidateIndex(
                candidates,
                gradeWeights);

            if (candidateIndex < 0)
            {
                break;
            }

            destination.Add(candidates[candidateIndex]);
            candidates.RemoveAt(candidateIndex);
        }
    }

    public static void GenerateItems(
        IReadOnlyList<ItemData> pool,
        int maximumOffers,
        List<ItemData> destination)
    {
        List<ItemData> candidates = new List<ItemData>();

        if (pool != null)
        {
            foreach (ItemData itemData in pool)
            {
                if (itemData != null && !candidates.Contains(itemData))
                {
                    candidates.Add(itemData);
                }
            }
        }

        destination.Clear();
        int offerCount = Mathf.Min(
            Mathf.Max(0, maximumOffers),
            candidates.Count);

        for (int slotIndex = 0; slotIndex < offerCount; slotIndex++)
        {
            int candidateIndex = Random.Range(0, candidates.Count);
            destination.Add(candidates[candidateIndex]);
            candidates.RemoveAt(candidateIndex);
        }
    }

    private static List<BulletData> BuildBulletCandidates(
        IReadOnlyList<BulletData> pool,
        IReadOnlyList<BulletGradeWeightData> gradeWeights)
    {
        List<BulletData> candidates = new List<BulletData>();

        if (pool == null)
        {
            return candidates;
        }

        foreach (BulletData bulletData in pool)
        {
            if (bulletData != null && !candidates.Contains(bulletData)
                && GetGradeWeight(bulletData.Grade, gradeWeights) > 0f)
            {
                candidates.Add(bulletData);
            }
        }

        return candidates;
    }

    private static int SelectWeightedCandidateIndex(
        IReadOnlyList<BulletData> candidates,
        IReadOnlyList<BulletGradeWeightData> gradeWeights)
    {
        List<BulletGrade> availableGrades = new List<BulletGrade>();

        foreach (BulletData candidate in candidates)
        {
            if (!availableGrades.Contains(candidate.Grade)
                && GetGradeWeight(candidate.Grade, gradeWeights) > 0f)
            {
                availableGrades.Add(candidate.Grade);
            }
        }

        float totalWeight = 0f;

        foreach (BulletGrade grade in availableGrades)
        {
            totalWeight += GetGradeWeight(grade, gradeWeights);
        }

        if (totalWeight <= 0f)
        {
            return -1;
        }

        float roll = Random.Range(0f, totalWeight);
        BulletGrade selectedGrade = availableGrades[
            availableGrades.Count - 1];

        foreach (BulletGrade grade in availableGrades)
        {
            roll -= GetGradeWeight(grade, gradeWeights);

            if (roll <= 0f)
            {
                selectedGrade = grade;
                break;
            }
        }

        List<int> gradeCandidateIndices = new List<int>();

        for (int candidateIndex = 0;
             candidateIndex < candidates.Count;
             candidateIndex++)
        {
            if (candidates[candidateIndex].Grade == selectedGrade)
            {
                gradeCandidateIndices.Add(candidateIndex);
            }
        }

        return gradeCandidateIndices.Count == 0
            ? -1
            : gradeCandidateIndices[Random.Range(
                0,
                gradeCandidateIndices.Count)];
    }

    private static float GetGradeWeight(
        BulletGrade grade,
        IReadOnlyList<BulletGradeWeightData> gradeWeights)
    {
        if (gradeWeights != null)
        {
            foreach (BulletGradeWeightData gradeWeight in gradeWeights)
            {
                if (gradeWeight != null && gradeWeight.Grade == grade)
                {
                    return gradeWeight.AppearanceWeight;
                }
            }
        }

        return grade switch
        {
            BulletGrade.Normal => 100f,
            BulletGrade.Rare => 85f,
            BulletGrade.Ace => 10f,
            BulletGrade.Legendary => 3f,
            _ => 0f
        };
    }
}
