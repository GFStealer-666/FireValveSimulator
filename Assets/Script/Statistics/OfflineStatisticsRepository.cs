namespace FireValveSimulator.Statistics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using UnityEngine;

    [DefaultExecutionOrder(-200)]
    public sealed class OfflineStatisticsRepository : MonoBehaviour
    {
        private const string DefaultFileName = "exam-statistics.json";

        [SerializeField, Min(1)] private int maximumRecords = 30;
        [SerializeField] private string fileName = DefaultFileName;

        private ExamStatisticsDatabase database;
        private bool isLoaded;

        public event Action DataChanged;

        public int MaximumRecords => Mathf.Max(1, maximumRecords);
        public IReadOnlyList<ExamSessionStatistics> Sessions
        {
            get
            {
                EnsureLoaded();
                return database.sessions;
            }
        }

        public string StoragePath => Path.Combine(
            Application.persistentDataPath,
            string.IsNullOrWhiteSpace(fileName) ? DefaultFileName : fileName.Trim());

        private void Awake()
        {
            EnsureLoaded();
        }

        private void OnValidate()
        {
            maximumRecords = Mathf.Max(1, maximumRecords);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = DefaultFileName;
        }

        public string ReserveNextUserName()
        {
            EnsureLoaded();

            int userNumber = Mathf.Max(1, database.nextUserNumber);
            database.nextUserNumber = userNumber + 1;
            SaveDatabase();
            return $"User{userNumber}";
        }

        public void AddSession(ExamSessionStatistics session)
        {
            EnsureLoaded();
            ExamStatisticsUtility.AddAndTrim(database, session, MaximumRecords);
            SaveDatabase();
            DataChanged?.Invoke();
        }

        [ContextMenu("Statistics/Clear All Saved Records")]
        public void ClearAll()
        {
            database = new ExamStatisticsDatabase();
            isLoaded = true;
            SaveDatabase();
            DataChanged?.Invoke();
        }

        [ContextMenu("Statistics/Reload From Disk")]
        public void Reload()
        {
            isLoaded = false;
            EnsureLoaded();
            DataChanged?.Invoke();
        }

        private void EnsureLoaded()
        {
            if (isLoaded)
                return;

            isLoaded = true;
            database = LoadDatabase(StoragePath);
            ExamStatisticsUtility.Normalize(database);

            int excess = database.sessions.Count - MaximumRecords;
            if (excess > 0)
            {
                database.sessions.RemoveRange(0, excess);
                SaveDatabase();
            }
        }

        private static ExamStatisticsDatabase LoadDatabase(string path)
        {
            if (!File.Exists(path))
                return new ExamStatisticsDatabase();

            try
            {
                string json = File.ReadAllText(path);
                ExamStatisticsDatabase loaded = JsonUtility.FromJson<ExamStatisticsDatabase>(json);
                return loaded ?? new ExamStatisticsDatabase();
            }
            catch (Exception exception)
            {
                TryBackUpUnreadableFile(path);
                Debug.LogWarning($"Could not load offline exam statistics. A new database will be used. {exception.Message}");
                return new ExamStatisticsDatabase();
            }
        }

        private void SaveDatabase()
        {
            ExamStatisticsUtility.Normalize(database);

            string path = StoragePath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = path + ".tmp";
            string backupPath = path + ".bak";
            string json = JsonUtility.ToJson(database, true);

            try
            {
                File.WriteAllText(temporaryPath, json);

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temporaryPath, path, backupPath);
                    }
                    catch (Exception)
                    {
                        File.Copy(temporaryPath, path, true);
                        File.Delete(temporaryPath);
                    }
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not save offline exam statistics to '{path}'. {exception.Message}");
            }
        }

        private static void TryBackUpUnreadableFile(string path)
        {
            try
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                File.Copy(path, $"{path}.corrupt-{timestamp}", true);
            }
            catch (Exception)
            {
                // The original file remains untouched when a backup cannot be created.
            }
        }
    }
}
