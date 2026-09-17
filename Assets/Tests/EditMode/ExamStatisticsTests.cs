using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ExamStatisticsTests
{
    private const string ModelsNamespace = "FireValveSimulator.Statistics.";

    [Test]
    public void AddAndTrimKeepsNewestRecords()
    {
        Type databaseType = GameType("ExamStatisticsDatabase");
        Type sessionType = GameType("ExamSessionStatistics");
        Type utilityType = GameType("ExamStatisticsUtility");
        object database = Activator.CreateInstance(databaseType);
        MethodInfo addAndTrim = utilityType.GetMethod(
            "AddAndTrim",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(addAndTrim, Is.Not.Null);

        for (int index = 1; index <= 5; index++)
        {
            object session = Activator.CreateInstance(sessionType);
            SetField(session, "sessionId", index.ToString());
            addAndTrim.Invoke(null, new[] { database, session, 3 });
        }

        IList sessions = GetListField(database, "sessions");
        Assert.That(sessions, Has.Count.EqualTo(3));
        Assert.That(GetField<string>(sessions[0], "sessionId"), Is.EqualTo("3"));
        Assert.That(GetField<string>(sessions[2], "sessionId"), Is.EqualTo("5"));
    }

    [Test]
    public void BuildOverviewAggregatesWrongCountsAndAverageTime()
    {
        Type sessionType = GameType("ExamSessionStatistics");
        Type utilityType = GameType("ExamStatisticsUtility");
        IList sessions = CreateGenericList(sessionType);

        sessions.Add(Session(
            Step("step-a", 0, "Open valve", 10f, 0),
            Step("step-b", 1, "Start pump", 30f, 2)));
        sessions.Add(Session(
            Step("step-a", 0, "Open valve", 20f, 3),
            Step("step-b", 1, "Start pump", 10f, 1)));

        MethodInfo buildOverview = utilityType.GetMethod(
            "BuildOverview",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(buildOverview, Is.Not.Null);

        object overview = buildOverview.Invoke(null, new object[] { sessions });
        Assert.That(GetProperty<int>(overview, "SessionCount"), Is.EqualTo(2));

        IList mostWrong = GetListProperty(overview, "MostWrongSteps");
        Assert.That(GetProperty<string>(mostWrong[0], "StepId"), Is.EqualTo("step-a"));
        Assert.That(GetProperty<int>(mostWrong[0], "WrongCount"), Is.EqualTo(3));

        IList mostTime = GetListProperty(overview, "MostTimeUsedSteps");
        Assert.That(GetProperty<string>(mostTime[0], "StepId"), Is.EqualTo("step-b"));
        Assert.That(GetProperty<float>(mostTime[0], "AverageTimeSeconds"), Is.EqualTo(20f));
    }

    [Test]
    public void DatabaseSurvivesJsonRoundTrip()
    {
        Type databaseType = GameType("ExamStatisticsDatabase");
        object original = Activator.CreateInstance(databaseType);
        SetField(original, "nextUserNumber", 8);

        object session = Session(Step("step-a", 0, "Open valve", 12.5f, 1));
        SetField(session, "sessionId", "session-1");
        SetField(session, "userName", "User7");
        GetListField(original, "sessions").Add(session);

        string json = JsonUtility.ToJson(original);
        object restored = JsonUtility.FromJson(json, databaseType);
        IList restoredSessions = GetListField(restored, "sessions");
        IList restoredSteps = GetListField(restoredSessions[0], "steps");

        Assert.That(GetField<int>(restored, "nextUserNumber"), Is.EqualTo(8));
        Assert.That(restoredSessions, Has.Count.EqualTo(1));
        Assert.That(GetField<string>(restoredSessions[0], "userName"), Is.EqualTo("User7"));
        Assert.That(GetField<float>(restoredSteps[0], "timeSeconds"), Is.EqualTo(12.5f));
    }

    private static object Session(params object[] steps)
    {
        object session = Activator.CreateInstance(GameType("ExamSessionStatistics"));
        IList stepList = GetListField(session, "steps");
        foreach (object step in steps)
            stepList.Add(step);
        return session;
    }

    private static object Step(
        string id,
        int index,
        string name,
        float duration,
        int wrongCount)
    {
        object step = Activator.CreateInstance(GameType("ExamStepStatistics"));
        SetField(step, "stepId", id);
        SetField(step, "stepIndex", index);
        SetField(step, "stepName", name);
        SetField(step, "timeSeconds", duration);
        SetField(step, "correctCount", 1);
        SetField(step, "wrongCount", wrongCount);

        Type outcomeType = GameType("ExamStepOutcome");
        object outcome = Enum.Parse(
            outcomeType,
            wrongCount > 0 ? "CompletedAfterMistake" : "Correct");
        SetField(step, "outcome", outcome);
        return step;
    }

    private static Type GameType(string typeName)
    {
        Type type = Type.GetType($"{ModelsNamespace}{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Could not find runtime type {typeName}.");
        return type;
    }

    private static IList CreateGenericList(Type itemType)
    {
        Type listType = typeof(List<>).MakeGenericType(itemType);
        return (IList)Activator.CreateInstance(listType);
    }

    private static IList GetListField(object target, string fieldName)
    {
        return (IList)GetRequiredField(target, fieldName).GetValue(target);
    }

    private static IList GetListProperty(object target, string propertyName)
    {
        return (IList)GetRequiredProperty(target, propertyName).GetValue(target);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        return (T)GetRequiredField(target, fieldName).GetValue(target);
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        return (T)GetRequiredProperty(target, propertyName).GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        GetRequiredField(target, fieldName).SetValue(target, value);
    }

    private static FieldInfo GetRequiredField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, $"Could not find field {fieldName}.");
        return field;
    }

    private static PropertyInfo GetRequiredProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, $"Could not find property {propertyName}.");
        return property;
    }
}
