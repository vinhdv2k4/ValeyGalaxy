using System;
using System.Linq;
using DailyEvents;
using FarmCutscenes;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[CustomEditor(typeof(EventQaPreview))]
public sealed class EventQaPreviewEditor : Editor
{
    private const string PrefabPath = "Assets/Prefabs/QA/Event QA Preview.prefab";

    [MenuItem("GameObject/QA/Event Preview", false, 30)]
    private static void CreatePreview()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "GamePlay")
        {
            Debug.LogWarning("Mở trực tiếp scene GamePlay trước khi tạo Event QA Preview.");
            return;
        }
        var existing = FindAnyObjectByType<EventQaPreview>(FindObjectsInactive.Include);
        if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(instance, "Create Event QA Preview");
        Selection.activeGameObject = instance;
    }

    public override VisualElement CreateInspectorGUI()
    {
        var qa = (EventQaPreview)target;
        var root = new VisualElement();
        var help = new HelpBox(
            "Mở trực tiếp GamePlay và bấm Play. J mở nhật ký hoặc hoạt động lễ hội. K bỏ qua cutscene.",
            HelpBoxMessageType.Info);
        root.Add(help);
        var status = new Label { style = { whiteSpace = WhiteSpace.Normal } };
        root.Add(status);
        var controls = new VisualElement();
        root.Add(controls);

        var script = Read<EventScriptFile>("Cutscenes/Milestones");
        var cutscenes = new Foldout { text = "Cutscene cột mốc", value = true };
        foreach (var entry in script.events)
            cutscenes.Add(new Button(() => qa.PreviewCutscene(entry.id)) { text = entry.title });
        controls.Add(cutscenes);

        var calendar = new DailyEventCalendar(Read<DailyEventFile>("DailyEvents/Calendar"));
        var daily = new Foldout { text = "Hiện tượng và sự kiện theo ngày", value = true };
        var day = new IntegerField("Ngày trong lịch") { value = 1 };
        var all = new Toggle("Hiện toàn bộ sự kiện") { value = true };
        var dateLabel = new Label();
        var selection = new DropdownField("Sự kiện");
        DailyEventDefinition[] visible = Array.Empty<DailyEventDefinition>();
        var preview = new Button(() =>
        {
            if (selection.index >= 0 && selection.index < visible.Length)
                qa.PreviewDaily(visible[selection.index].id, day.value);
        }) { text = "Mở sự kiện" };

        daily.Add(day);
        daily.Add(dateLabel);
        daily.Add(new Button(() => qa.PreviewDay(day.value)) { text = "Xem bối cảnh ngày" });
        daily.Add(all);
        daily.Add(selection);
        daily.Add(preview);
        controls.Add(daily);
        var delivery = new Button(qa.PreviewDelivery) { text = "Xem bước trả nhiệm vụ" };
        var stop = new Button(qa.StopPreview) { text = "Dừng preview" };
        root.Add(delivery);
        root.Add(stop);

        day.RegisterValueChangedCallback(change =>
        {
            day.SetValueWithoutNotify(Mathf.Clamp(change.newValue, 1, 10_000));
            FilterEvents();
        });
        all.RegisterValueChangedCallback(_ => FilterEvents());
        FilterEvents();
        Refresh();
        root.schedule.Execute(Refresh).Every(300);
        return root;

        void FilterEvents()
        {
            var date = new FarmDate(day.value);
            dateLabel.text = $"{date.Season} {date.DayOfSeason} · Năm {date.Year}";
            visible = (all.value ? calendar.Events : calendar.On(date)).ToArray();
            selection.choices = visible.Select(entry => $"Ngày {entry.day} · {entry.title}").ToList();
            selection.index = visible.Length > 0 ? 0 : -1;
            preview.SetEnabled(visible.Length > 0);
        }

        void Refresh()
        {
            if (qa == null) return;
            bool available = qa.CanPreview;
            controls.SetEnabled(available && !qa.IsTransitioning && !qa.IsPlayingCutscene);
            delivery.SetEnabled(available && qa.CanPreviewDelivery);
            stop.SetEnabled(qa.IsActive);
            status.text = available ? qa.Status : Application.isPlaying
                ? "Cần scene GamePlay chạy trực tiếp với người chơi offline."
                : "Các nút sẽ mở trong Play Mode.";
        }
    }

    private static T Read<T>(string path)
    {
        var asset = Resources.Load<TextAsset>(path);
        if (asset == null) throw new InvalidOperationException("Thiếu tài nguyên " + path);
        return JsonConvert.DeserializeObject<T>(asset.text);
    }
}
