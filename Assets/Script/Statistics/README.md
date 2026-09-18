# Offline Exam Statistics Setup

The statistics system stores the newest 30 exam records in
`Application.persistentDataPath/exam-statistics.json`. Change the limit on the
`OfflineStatisticsRepository` component when needed.

## Scene setup

1. Create a persistent `StatisticsSystem` GameObject in `Scene_MainScene`.
2. Add `OfflineStatisticsRepository` and `ExamStatisticsCollector`.
3. Assign the existing `ActionOrderManager`, `ExamManager`, and
   `SimulatorModeManager` to the collector. References also resolve
   automatically when left empty.
4. Add `StatisticsPanelController` to the statistics UI root and assign the
   repository, tab objects, labels, and row prefab/content references.
   Assign the Personal and Overview `Toggle` components to one `ToggleGroup`;
   the controller disables `Allow Switch Off` so exactly one tab stays active.
5. Connect the main-menu Statistics button to
   `StatisticsPanelController.Open`.

## Scroll views

Use a `ScrollRect` with `RectMask2D` for both record and step lists. Put a
`VerticalLayoutGroup` and `ContentSizeFitter` on each Content object. The panel
controller pools row instances and only rebinds them when data changes or the
panel opens.

Add a `ToggleGroup` to the record-list Content object and keep
`Allow Switch Off` disabled. Assign it to the panel controller's Record Toggle
Group field so one record is always selected.

Create three lightweight row prefabs:

- `StatisticsRecordRowView`: one selectable User/session row. Its root uses a
  `Toggle`, not a `Button`; the Toggle's Checkmark is the selected
  border/background. Configure separate Completed, Failed, and Abandoned
  indicator objects with their labels inside; exactly one is shown for every
  record.
- `StatisticsStepRowView`: one personal step-result row.
- `StatisticsOverviewRowView`: one ranked overview row. Configure its bar Image
  as `Filled / Horizontal` if a fill bar is used.

Optional labels and status indicators may be left unassigned. The associated
data will still be collected and saved.
