namespace FireValveSimulator.Statistics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public enum ExamSessionOutcome
    {
        Completed,
        Failed,
        Abandoned
    }

    public enum ExamStepOutcome
    {
        NotAttempted,
        Correct,
        CompletedAfterMistake,
        Incomplete,
        Skipped
    }

    [Serializable]
    public sealed class ExamStepStatistics
    {
        public string stepId;
        public int stepIndex;
        public string stepName;
        public float timeSeconds;
        public int correctCount;
        public int wrongCount;
        public ExamStepOutcome outcome;
    }

    [Serializable]
    public sealed class ExamSessionStatistics
    {
        public string sessionId;
        public string userName;
        public string startedAtUtc;
        public string endedAtUtc;
        public float totalTimeSeconds;
        public int correctCount;
        public int wrongCount;
        public ExamSessionOutcome outcome;
        public List<ExamStepStatistics> steps = new List<ExamStepStatistics>();
    }

    [Serializable]
    public sealed class ExamStatisticsDatabase
    {
        public int schemaVersion = CurrentSchemaVersion;
        public int nextUserNumber = 1;
        public List<ExamSessionStatistics> sessions = new List<ExamSessionStatistics>();

        public const int CurrentSchemaVersion = 1;
    }

    public sealed class StepAggregateStatistics
    {
        public string StepId { get; internal set; }
        public int StepIndex { get; internal set; }
        public string StepName { get; internal set; }
        public int AttemptCount { get; internal set; }
        public int CorrectCount { get; internal set; }
        public int WrongCount { get; internal set; }
        public float TotalTimeSeconds { get; internal set; }
        public float AverageTimeSeconds => AttemptCount > 0 ? TotalTimeSeconds / AttemptCount : 0f;
    }

    public sealed class StatisticsOverview
    {
        public int SessionCount { get; internal set; }
        public IReadOnlyList<StepAggregateStatistics> MostWrongSteps { get; internal set; }
        public IReadOnlyList<StepAggregateStatistics> MostTimeUsedSteps { get; internal set; }
    }

    public static class ExamStatisticsUtility
    {
        public static void Normalize(ExamStatisticsDatabase database)
        {
            if (database == null)
                return;

            database.schemaVersion = Math.Max(database.schemaVersion, ExamStatisticsDatabase.CurrentSchemaVersion);
            database.nextUserNumber = Math.Max(1, database.nextUserNumber);
            database.sessions ??= new List<ExamSessionStatistics>();

            database.sessions.RemoveAll(session => session == null);
            foreach (ExamSessionStatistics session in database.sessions)
                session.steps ??= new List<ExamStepStatistics>();
        }

        public static void AddAndTrim(
            ExamStatisticsDatabase database,
            ExamSessionStatistics session,
            int maximumRecords)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            Normalize(database);
            database.sessions.Add(session);

            int recordLimit = Math.Max(1, maximumRecords);
            int excess = database.sessions.Count - recordLimit;
            if (excess > 0)
                database.sessions.RemoveRange(0, excess);
        }

        public static StatisticsOverview BuildOverview(IEnumerable<ExamSessionStatistics> sessions)
        {
            List<ExamSessionStatistics> validSessions = sessions?
                .Where(session => session != null)
                .ToList() ?? new List<ExamSessionStatistics>();

            Dictionary<string, StepAggregateStatistics> aggregates =
                new Dictionary<string, StepAggregateStatistics>(StringComparer.Ordinal);

            foreach (ExamSessionStatistics session in validSessions)
            {
                if (session.steps == null)
                    continue;

                foreach (ExamStepStatistics step in session.steps)
                {
                    if (step == null || step.outcome == ExamStepOutcome.NotAttempted)
                        continue;

                    string key = GetAggregateKey(step);
                    if (!aggregates.TryGetValue(key, out StepAggregateStatistics aggregate))
                    {
                        aggregate = new StepAggregateStatistics
                        {
                            StepId = step.stepId,
                            StepIndex = step.stepIndex,
                            StepName = step.stepName
                        };
                        aggregates.Add(key, aggregate);
                    }

                    aggregate.AttemptCount++;
                    aggregate.CorrectCount += Math.Max(0, step.correctCount);
                    aggregate.WrongCount += Math.Max(0, step.wrongCount);
                    aggregate.TotalTimeSeconds += Math.Max(0f, step.timeSeconds);
                }
            }

            List<StepAggregateStatistics> values = aggregates.Values.ToList();
            return new StatisticsOverview
            {
                SessionCount = validSessions.Count,
                MostWrongSteps = values
                    .OrderByDescending(step => step.WrongCount)
                    .ThenBy(step => step.StepIndex)
                    .ToList(),
                MostTimeUsedSteps = values
                    .OrderByDescending(step => step.AverageTimeSeconds)
                    .ThenBy(step => step.StepIndex)
                    .ToList()
            };
        }

        private static string GetAggregateKey(ExamStepStatistics step)
        {
            if (!string.IsNullOrWhiteSpace(step.stepId))
                return step.stepId;

            return $"legacy:{step.stepIndex}:{step.stepName}";
        }
    }
}
