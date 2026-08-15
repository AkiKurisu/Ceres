using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Unity.CodeEditor;
using BoolChangeEvent = UnityEngine.UIElements.ChangeEvent<bool>;
using StringChangeEvent = UnityEngine.UIElements.ChangeEvent<string>;
using Newtonsoft.Json.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Ceres.Events.Editor
{
    internal class EventsDebuggerWindow : EditorWindow
    {
        [SerializeField]
        private EventsDebuggerImpl m_DebuggerImpl;

        [MenuItem("Tools/Ceres/Debug/Event Debugger", false, 1)]
        public static void ShowEventDebugger()
        {
            var window = GetWindow<EventsDebuggerWindow>();
            window.minSize = new Vector2(640, 480);
            window.titleContent = EditorGUIUtility.TrTextContent("Event Debugger");
            window.m_DebuggerImpl.ClearLogs();
        }

        private void OnEnable()
        {
            m_DebuggerImpl ??= new EventsDebuggerImpl();
            m_DebuggerImpl.Initialize(this, rootVisualElement);
        }

        private void OnDisable()
        {
            m_DebuggerImpl.OnDisable();
        }
    }
    
    internal class CodeLine : Label
    {
        private string m_FileName;

        private int m_LineNumber;

        public int HashCode { get; private set; }

        public void Init(string textName, string fileName, int lineNumber, int lineHashCode)
        {
            text = textName;
            m_FileName = fileName;
            m_LineNumber = lineNumber;
            HashCode = lineHashCode;
        }

        public void GotoCode()
        {
            CodeEditor.Editor.CurrentCodeEditor.OpenProject(m_FileName, m_LineNumber);
        }

        public override string ToString()
        {
            return $"{m_FileName} ({m_LineNumber})";
        }
    }

    [Serializable]
    internal class EventsDebuggerImpl : CoordinatorDebugger
    {
        private const string k_EventsContainerName = "eventsHistogramContainer";
        private const string k_EventsLabelName = "eventsHistogramEntry";
        private const string k_EventsDurationName = "eventsHistogramDuration";
        private const string k_EventsDurationLabelName = "eventsHistogramDurationLabel";
        private const string k_EventsDurationLengthName = "eventsHistogramDurationLength";
        private const int k_DefaultMaxLogLines = 5000;
        private const string k_RegisteredEventCallbacksPrefix = "Registered Event Callbacks for ";

        public enum HistogramDurationMode
        {
            // Average duration spent handling each event type
            AverageTime,
            // Total duration spent handling each event type
            TotalTime
        }

        // Event playback speed, divide by 10f before using
        public readonly List<string> m_PlaybackSpeeds = new()

        {
            "0.1x", // 0.1x (slowest)
            "0.2x",
            "0.5x",
            "1x", // 1x (normal)
            "2x",
            "5x",
            "10x" // 10x (fastest)
        };
        private Label m_EventPropagationPaths;
        private Label m_EventBaseInfo;
        private ListView m_EventsLog;
        private ListView m_EventRegistrationsListView;
        private ScrollView m_EventCallbacksScrollView;
        private EventLog m_Log;
        private int m_StartIndex;
        private ScrollView m_EventsHistogramScrollView;

        private long m_ModificationCount;
        [SerializeField]
        private bool m_AutoScroll;
        [SerializeField]
        private bool m_MaxLogLines;
        [SerializeField]
        private int m_MaxLogLineCount;
        [SerializeField]
        private HistogramDurationMode m_DisplayHistogramDurationMode;
        [SerializeField]
        private float m_PlaybackSpeed;

        [Serializable]
        private struct EventTypeFilterStateStruct
        {
            public long key;
            public bool value;
        }

        public bool GetStateValue(long key, bool defaultValue)
        {
            if (m_StateList == null)
                return false;

            if (m_StateList.Exists(x => x.key == key))
                return m_StateList.Find(x => x.key == key).value;
            return defaultValue;
        }

        [SerializeField]
        private List<EventTypeFilterStateStruct> m_StateList;

        private EventTypeSearchField m_EventTypeFilter;
        private ToolbarSearchField m_CallbackTypeFilter;
        private Label m_LogCountLabel;
        private Label m_SelectionCountLabel;
        private List<EventLogLine> m_SelectedEvents;
        private IntegerField m_MaxLogLinesField;
        private ToolbarMenu m_SettingsMenu;
        private ToolbarToggle m_SuspendListeningToggle;
        private readonly List<IRegisteredCallbackLine> m_RegisteredEventCallbacksDataSource = new();

        private ToolbarToggle m_TogglePlayback;
        private ToolbarButton m_DecreasePlaybackSpeedButton;
        private ToolbarButton m_IncreasePlaybackSpeedButton;
        private ToolbarButton m_SaveReplayButton;
        private ToolbarButton m_LoadReplayButton;
        private ToolbarButton m_StartPlaybackButton;
        private ToolbarButton m_StopPlaybackButton;
        private Label m_PlaybackLabel;
        private EnumField m_DisplayHistogramAverageEnum;
        private DropdownField m_PlaybackSpeedDropdown;
        private Label m_EventRegistrationTitle;

        private readonly Dictionary<ulong, long> m_EventTimestampDictionary = new();
        private VisualElement rootVisualElement;
        private readonly EventDebugger m_Debugger = new();

        private void DisplayHistogram()
        {
            if (m_EventsHistogramScrollView == null)
                return;

            // Clear the ScrollView
            m_EventsHistogramScrollView.Clear();

            if (CoordinatorDebug == null)
                return;

            var childrenList = m_EventsHistogramScrollView.Children().ToList();
            foreach (var child in childrenList)
                child.RemoveFromHierarchy();

            var histogramValue = m_Debugger.ComputeHistogram(m_SelectedEvents?.Select(x => x.EventBase).ToList() ??
                m_Log.lines.Select(x => x.EventBase).ToList());
            if (histogramValue == null)
                return;

            long maxDuration = 0;
            float maxAverageDuration = 0f;
            foreach (var key in histogramValue.Keys)
            {
                if (maxDuration < histogramValue[key].duration)
                    maxDuration = histogramValue[key].duration;
                if (maxAverageDuration < histogramValue[key].duration / (float)histogramValue[key].count)
                    maxAverageDuration = histogramValue[key].duration / (float)histogramValue[key].count;
            }

            foreach (var key in histogramValue.Keys)
            {
                float adjustedDuration, adjustedPercentDuration;
                if (m_DisplayHistogramDurationMode == HistogramDurationMode.AverageTime)
                {
                    adjustedDuration = histogramValue[key].duration / (float)histogramValue[key].count;
                    adjustedPercentDuration = adjustedDuration / maxAverageDuration;
                }
                else
                {
                    adjustedDuration = histogramValue[key].duration;
                    adjustedPercentDuration = adjustedDuration / maxDuration;
                }

                AddHistogramEntry(m_EventsHistogramScrollView, key, adjustedDuration, adjustedPercentDuration * 100f);
            }

            var eventsHistogramTitleHeader = rootVisualElement.MandatoryQ("eventsHistogramTitleHeader");
            var eventsHistogramTotal = eventsHistogramTitleHeader.MandatoryQ<Label>("eventsHistogramTotal");
            eventsHistogramTotal.text = $"{histogramValue.Count} event type{(histogramValue.Count > 1 ? "s" : "")}";
        }

        private static void AddHistogramEntry(VisualElement root, string name, float duration, float percent)
        {
            var container = new VisualElement() { name = k_EventsContainerName };
            var labelName = new Label(name) { name = k_EventsLabelName };
            var durationGraph = new VisualElement() { name = k_EventsDurationName };
            float durationLength = duration / 1000f;
            var labelNameDuration = new Label(name) { name = k_EventsDurationLabelName, text = durationLength.ToString("0.#####") + "ms" };
            var durationGraphLength = new VisualElement() { name = k_EventsDurationLengthName };
            durationGraphLength.StretchToParentSize();
            durationGraph.Add(durationGraphLength);
            durationGraph.Add(labelNameDuration);

            container.style.flexDirection = FlexDirection.Row;
            container.Add(labelName);
            container.Add(durationGraph);
            root.Add(container);

            durationGraphLength.style.top = 1.0f;
            durationGraphLength.style.left = 0.0f;
            durationGraphLength.style.width = Length.Percent(percent);
        }

        private void InitializeRegisteredCallbacksBinding()
        {
            m_EventRegistrationsListView.fixedItemHeight = 20;
            m_EventRegistrationsListView.makeItem += () =>
            {
                var lineContainer = new VisualElement { pickingMode = PickingMode.Position };
                lineContainer.AddToClassList("line-container");

                // Title items
                var titleLine = new Label { pickingMode = PickingMode.Ignore };
                titleLine.AddToClassList("callback-list-element");
                titleLine.AddToClassList("visual-element");
                lineContainer.Add(titleLine);

                // Callback items
                var callbackLine = new Label { pickingMode = PickingMode.Ignore };
                callbackLine.AddToClassList("callback-list-element");
                callbackLine.AddToClassList("event-type");
                lineContainer.Add(callbackLine);

                // Code line items
                var codeLineContainer = new VisualElement();
                codeLineContainer.AddToClassList("code-line-container");
                var line = new CodeLine { pickingMode = PickingMode.Ignore };
                line.AddToClassList("callback-list-element");
                line.AddToClassList("callback");
                codeLineContainer.Add(line);
                var openSourceFileButton = new Button();
                openSourceFileButton.AddToClassList("open-source-file-button");
                openSourceFileButton.clickable.clicked += line.GotoCode;
                openSourceFileButton.tooltip = $"Click to go to event registration point in code:\n{line}";
                codeLineContainer.Add(openSourceFileButton);
                lineContainer.Add(codeLineContainer);

                return lineContainer;
            };
            m_EventRegistrationsListView.bindItem += (element, i) =>
            {
                var codeLineContainer = element[2];
                var codeLine = codeLineContainer?.Q<CodeLine>();

                if (element[0] is not Label titleLine || element[1] is not Label callbackLine || codeLine == null)
                    return;

                var data = m_RegisteredEventCallbacksDataSource[i];
                titleLine.style.display = data.Type == LineType.Title ? DisplayStyle.Flex : DisplayStyle.None;
                callbackLine.style.display = data.Type == LineType.Callback ? DisplayStyle.Flex : DisplayStyle.None;
                codeLineContainer.style.display = data.Type == LineType.CodeLine ? DisplayStyle.Flex : DisplayStyle.None;

                element.userData = data.CallbackHandler;

                titleLine.text = data.Text;
                callbackLine.text = data.Text;

                if (data.Type == LineType.CodeLine)
                {
                    if (data is not CodeLineInfo codeLineData)
                        return;

                    codeLine.Init(codeLineData.Text, codeLineData.FileName, codeLineData.LineNumber, codeLineData.LineHashCode);
                    codeLine.RemoveFromClassList("highlighted");

                    if (codeLineData.Highlighted)
                    {
                        codeLine.AddToClassList("highlighted");
                    }
                }
            };
        }

        private long GetTypeId(Type type)
        {
            var getTypeId = type.GetMethod("TypeId", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (getTypeId == null)
                return -1;

            return (long)getTypeId.Invoke(null, null);
        }

        private void DisplayRegisteredEventCallbacks()
        {
            var listView = m_EventRegistrationsListView;
            var filter = m_CallbackTypeFilter.value;
            if (CoordinatorDebug == null || listView == null)
                return;

            m_RegisteredEventCallbacksDataSource.Clear();

            if (!GlobalCallbackRegistry.IsEventDebuggerConnected)
            {
                listView.Rebuild();
                return;
            }

            bool IsFilteredOut(Type type)
            {
                var id = GetTypeId(type);
                return m_EventTypeFilter.State.TryGetValue(id, out var isEnabled) && !isEnabled;
            }

            GlobalCallbackRegistry.CleanListeners();

            var listeners = GlobalCallbackRegistry.s_Listeners.ToList();
            var nbListeners = 0;
            var nbCallbacks = 0;
            foreach (var eventRegistrationListener in listeners)
            {
                var key = eventRegistrationListener.Key; // VE that sends events

                var text = EventDebugger.GetObjectDisplayName(key);

                if (!string.IsNullOrEmpty(filter) && !text.ToLower().Contains(filter.ToLower()))
                    continue;

                var events = eventRegistrationListener.Value;
                if (events.All(e => IsFilteredOut(e.Key)))
                    continue;

                m_RegisteredEventCallbacksDataSource.Add(new TitleInfo(text, key));

                foreach (var evt in events)
                {
                    var evtType = evt.Key;
                    text = EventDebugger.GetTypeDisplayName(evtType);

                    if (IsFilteredOut(evtType))
                        continue;

                    m_RegisteredEventCallbacksDataSource.Add(new CallbackInfo(text, key));

                    var evtCallbacks = evt.Value;
                    foreach (var evtCallback in evtCallbacks)
                    {
                        m_RegisteredEventCallbacksDataSource.Add(new CodeLineInfo(evtCallback.name, key, evtCallback.fileName, evtCallback.lineNumber, evtCallback.hashCode));
                        nbCallbacks++;
                    }
                }

                nbListeners++;
            }

            listView.itemsSource = m_RegisteredEventCallbacksDataSource;

            var choiceCount = m_EventTypeFilter.GetSelectedCount();
            var choiceCountString = $"{choiceCount} event type{(choiceCount > 1 ? "s" : "")}";

            m_EventRegistrationTitle.text = k_RegisteredEventCallbacksPrefix + choiceCountString + (CoordinatorDebug == null ? " - [No Coordinator Selected]" : "");

            var nbEvents = m_EventTypeFilter.State.Count(s => s.Key > 0);
            var nbFilteredEvents = m_EventTypeFilter.State.Count(s => s.Key > 0 && s.Value);
            var eventsRegistrationSearchContainer = rootVisualElement.MandatoryQ("eventsRegistrationSearchContainer");
            var eventsRegistrationTotals = eventsRegistrationSearchContainer.MandatoryQ<Label>("eventsRegistrationTotals");
            eventsRegistrationTotals.text =
                $"{nbListeners} listener{(nbListeners > 1 ? "s" : "")}, {nbCallbacks} callback{(nbCallbacks > 1 ? "s" : "")}" +
                (nbFilteredEvents < nbEvents ? $" (filter: {nbFilteredEvents} event{(nbFilteredEvents > 1 ? "s" : "")})" : string.Empty);
        }

        public void Initialize(EditorWindow debuggerWindow, VisualElement root)
        {
            rootVisualElement = root;

            VisualTreeAsset template = Resources.Load<VisualTreeAsset>("EventsDebugger");
            if (template != null)
                template.CloneTree(rootVisualElement);
            var st = rootVisualElement.Q<VisualElement>("searchToolbar");
            var toolbar = rootVisualElement.MandatoryQ<Toolbar>("searchToolbar");
            m_Toolbar = toolbar;

            Initialize(debuggerWindow);

            rootVisualElement.styleSheets.Add(Resources.Load<StyleSheet>("EventsDebugger"));
            var eventsDebugger = rootVisualElement.MandatoryQ("eventsDebugger");
            eventsDebugger.StretchToParentSize();

            m_EventCallbacksScrollView = (ScrollView)rootVisualElement.MandatoryQ("eventCallbacksScrollView");

            m_EventTypeFilter = toolbar.MandatoryQ<EventTypeSearchField>("filter-event-type");
            m_EventTypeFilter.RegisterCallback<StringChangeEvent>(OnFilterChange);
            m_SuspendListeningToggle = rootVisualElement.MandatoryQ<ToolbarToggle>("suspend");
            m_SuspendListeningToggle.RegisterValueChangedCallback(SuspendListening);
            var clearLogsButton = rootVisualElement.MandatoryQ<ToolbarButton>("clear-logs");
            clearLogsButton.clickable.clicked += ClearLogs;

            var eventReplayToolbar = rootVisualElement.MandatoryQ<Toolbar>("eventReplayToolbar");
            var eventFileToolbar = rootVisualElement.MandatoryQ<Toolbar>("eventFileToolbar");
            m_DecreasePlaybackSpeedButton = eventReplayToolbar.MandatoryQ<ToolbarButton>("decrease-playback-speed");
            m_DecreasePlaybackSpeedButton.clickable.clicked += DecreasePlaybackSpeed;
            m_IncreasePlaybackSpeedButton = eventReplayToolbar.MandatoryQ<ToolbarButton>("increase-playback-speed");
            m_IncreasePlaybackSpeedButton.clickable.clicked += IncreasePlaybackSpeed;
            m_TogglePlayback = eventReplayToolbar.MandatoryQ<ToolbarToggle>("pause-resume-playback");
            m_TogglePlayback.RegisterValueChangedCallback(TogglePlayback);
            m_PlaybackLabel = eventReplayToolbar.MandatoryQ<Label>("replay-selected-events");
            m_PlaybackLabel.text = "";
            m_StartPlaybackButton = eventReplayToolbar.MandatoryQ<ToolbarButton>("start-playback");
            m_StartPlaybackButton.clickable.clicked += OnReplayStart;
            m_StopPlaybackButton = eventReplayToolbar.MandatoryQ<ToolbarButton>("stop-playback");
            m_StopPlaybackButton.clickable.clicked += OnReplayCompleted;
            m_SaveReplayButton = eventFileToolbar.MandatoryQ<ToolbarButton>("save-replay");
            m_SaveReplayButton.clickable.clicked += SaveReplaySessionFromSelection;
            m_LoadReplayButton = eventFileToolbar.MandatoryQ<ToolbarButton>("load-replay");
            m_LoadReplayButton.clickable.clicked += LoadReplaySession;
            UpdatePlaybackButtons();

            var infoContainer = rootVisualElement.MandatoryQ("eventInfoContainer");
            var playbackContainer = rootVisualElement.MandatoryQ("eventPlaybackContainer");
            m_LogCountLabel = infoContainer.MandatoryQ<Label>("log-count");
            m_SelectionCountLabel = infoContainer.MandatoryQ<Label>("selection-count");

            m_MaxLogLinesField = playbackContainer.MandatoryQ<IntegerField>("maxLogLinesField");
            m_MaxLogLinesField.RegisterValueChangedCallback(e =>
            {
                // Minimum 1 line if max log lines is enabled
                m_MaxLogLineCount = Math.Max(1, e.newValue);
                m_MaxLogLinesField.value = m_MaxLogLineCount;
                DoMaxLogLines();
            });

            m_SettingsMenu = playbackContainer.Q<ToolbarMenu>("settings-menu");
            SetupSettingsMenu();

            m_EventPropagationPaths = (Label)rootVisualElement.MandatoryQ("eventPropagationPaths");
            m_EventBaseInfo = (Label)rootVisualElement.MandatoryQ("eventbaseInfo");

            m_EventsLog = (ListView)rootVisualElement.MandatoryQ("eventsLog");
            m_EventsLog.focusable = true;
            m_EventsLog.selectionType = SelectionType.Multiple;
            m_EventsLog.selectionChanged += OnEventsLogSelectionChanged;

            m_DisplayHistogramAverageEnum = rootVisualElement.MandatoryQ<EnumField>("eventsHistogramDurationType");
            m_DisplayHistogramAverageEnum.Init(HistogramDurationMode.AverageTime);
            m_DisplayHistogramAverageEnum.RegisterValueChangedCallback(e =>
            {
                m_DisplayHistogramDurationMode = (HistogramDurationMode)e.newValue;
                DisplayHistogram();
            });

            m_PlaybackSpeedDropdown = eventReplayToolbar.MandatoryQ<DropdownField>("playback-speed-dropdown");
            m_PlaybackSpeedDropdown.choices = m_PlaybackSpeeds;
            m_PlaybackSpeedDropdown.RegisterValueChangedCallback(e =>
            {
                m_PlaybackSpeed = float.Parse(e.newValue.Trim('x'));
                UpdatePlaybackSpeed();
            });

            m_Log = new EventLog();

            m_ModificationCount = 0;

            var eventCallbacksScrollView = (ScrollView)rootVisualElement.MandatoryQ("eventCallbacksScrollView");
            eventCallbacksScrollView.StretchToParentSize();

            var eventPropagationPathsScrollView = (ScrollView)rootVisualElement.MandatoryQ("eventPropagationPathsScrollView");
            eventPropagationPathsScrollView.StretchToParentSize();

            var eventBaseInfoScrollView = (ScrollView)rootVisualElement.MandatoryQ("eventbaseInfoScrollView");
            eventBaseInfoScrollView.StretchToParentSize();

            m_CallbackTypeFilter = rootVisualElement.MandatoryQ<ToolbarSearchField>("filter-registered-callback");
            m_CallbackTypeFilter.RegisterCallback<StringChangeEvent>(OnRegisteredCallbackFilterChange);
            m_CallbackTypeFilter.tooltip = "Type in element name, type or id to filter callbacks.";

            m_EventRegistrationsListView = rootVisualElement.MandatoryQ<ListView>("eventsRegistrationsListView");
            m_EventRegistrationsListView.StretchToParentSize();
            InitializeRegisteredCallbacksBinding();
            DisplayRegisteredEventCallbacks();

            m_EventsHistogramScrollView = (ScrollView)rootVisualElement.MandatoryQ("eventsHistogramScrollView");
            m_EventsHistogramScrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            m_EventsHistogramScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            m_EventsHistogramScrollView.StretchToParentSize();
            DisplayHistogram();

            m_PlaybackSpeed = 1f;
            UpdatePlaybackSpeed();

            m_AutoScroll = true;
            m_MaxLogLines = false;
            m_MaxLogLineCount = k_DefaultMaxLogLines;
            m_MaxLogLinesField.value = m_MaxLogLineCount;

            m_EventRegistrationTitle = rootVisualElement.MandatoryQ<Label>("eventsRegistrationTitle");

            DoMaxLogLines();

            var isProSkin = EditorGUIUtility.isProSkin;

            var eventsTitle = rootVisualElement.MandatoryQ("eventsTitle");
            var eventCallbacksTitle = rootVisualElement.MandatoryQ("eventCallbacksTitle");
            var eventPropagationPathsTitle = rootVisualElement.MandatoryQ("eventPropagationPathsTitle");
            var eventbaseInfoTitle = rootVisualElement.MandatoryQ("eventbaseInfoTitle");
            var eventsRegistrationTitleContainer = rootVisualElement.MandatoryQ("eventsRegistrationTitleContainer");
            var eventsRegistrationSearchContainer = rootVisualElement.MandatoryQ("eventsRegistrationSearchContainer");
            var eventsHistogramTitleContainer = rootVisualElement.MandatoryQ("eventsHistogramTitleContainer");

            eventsTitle.EnableInClassList("light", !isProSkin);
            eventCallbacksTitle.EnableInClassList("light", !isProSkin);
            eventPropagationPathsTitle.EnableInClassList("light", !isProSkin);
            eventbaseInfoTitle.EnableInClassList("light", !isProSkin);
            eventsRegistrationTitleContainer.EnableInClassList("light", !isProSkin);
            eventsRegistrationSearchContainer.EnableInClassList("light", !isProSkin);
            eventsHistogramTitleContainer.EnableInClassList("light", !isProSkin);

            GlobalCallbackRegistry.IsEventDebuggerConnected = true;

            EditorApplication.update += EditorUpdate;

            if (m_StateList != null && m_StateList.Count > 0)
                m_EventTypeFilter.SetState(m_StateList
                    .ToDictionary(c => c.key, c => c.value));
        }

        private void SuspendListening(BoolChangeEvent evt)
        {
            m_Debugger.Suspended = evt.newValue;
            m_SuspendListeningToggle.text = m_Debugger.Suspended ? "Record" : "Suspend";
            Refresh();
        }

        private void SetupSettingsMenu()
        {
            m_SettingsMenu.menu.AppendAction(
                "Autoscroll",
                a =>
                {
                    m_AutoScroll = !m_AutoScroll;
                    DoAutoScroll();
                },
                a => m_AutoScroll ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            m_SettingsMenu.menu.AppendAction(
                "Max Log Lines",
                a =>
                {
                    m_MaxLogLines = !m_MaxLogLines;
                    DoMaxLogLines();
                },
                a => m_MaxLogLines ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        private void DoMaxLogLines()
        {
            m_MaxLogLinesField.SetEnabled(m_MaxLogLines);
            m_MaxLogLineCount = m_MaxLogLinesField.value;
            BuildEventsLog();
        }

        public new void OnDisable()
        {
            base.OnDisable();

            EditorApplication.update -= EditorUpdate;
        }

        public override bool InterceptEvent(EventBase evt)
        {
            evt.EventLogger = m_Debugger;
            m_EventTimestampDictionary[evt.EventId] = (long)(Time.realtimeSinceStartup * 1000.0f);
            m_Debugger.BeginProcessEvent(evt);
            return false;
        }

        public override void PostProcessEvent(EventBase evt)
        {
            if (evt.Log)
            {
                var now = (long)(Time.realtimeSinceStartup * 1000.0f);
                var start = m_EventTimestampDictionary[evt.EventId];
                m_EventTimestampDictionary.Remove(evt.EventId);
                m_Debugger.EndProcessEvent(evt, now - start);
                evt.EventLogger = null;
            }
        }


        private void DecreasePlaybackSpeed()
        {
            var i = m_PlaybackSpeeds.Count - 1;
            for (; i >= 0; i--)
            {
                var playbackSpeed = float.Parse(m_PlaybackSpeeds[i].Trim('x'));
                if (playbackSpeed < m_Debugger.PlaybackSpeed)
                {
                    UpdatePlaybackSpeed(playbackSpeed);
                    break;
                }
            }
        }

        private void IncreasePlaybackSpeed()
        {
            var i = 0;
            for (; i < m_PlaybackSpeeds.Count; i++)
            {
                var playbackSpeed = float.Parse(m_PlaybackSpeeds[i].Trim('x'));
                if (playbackSpeed > m_Debugger.PlaybackSpeed)
                {
                    UpdatePlaybackSpeed(playbackSpeed);
                    break;
                }
            }
        }

        private IEnumerator _replayEnumerator;

        private void OnReplayStart()
        {
            if (m_SelectedEvents == null)
                return;

            ReplayEvents(m_SelectedEvents.Select(x => x.EventBase));
        }

        private void ReplayEvents(IEnumerable<EventDebuggerEventRecord> events)
        {
            if (!m_Debugger.IsReplaying)
            {
                _replayEnumerator = m_Debugger.ReplayEvents(events, RefreshFromReplay);

                if (_replayEnumerator == null || !_replayEnumerator.MoveNext())
                    return;

                UpdatePlaybackButtons();
            }
        }

        private void EditorUpdate()
        {
            if (_replayEnumerator != null && !_replayEnumerator.MoveNext())
            {
                OnReplayCompleted();
                _replayEnumerator = null;
            }
        }

        private void TogglePlayback(BoolChangeEvent evt)
        {
            if (!m_Debugger.IsReplaying)
                return;

            m_Debugger.IsReplaying = evt.newValue;
            m_PlaybackLabel.text = m_Debugger.IsPlaybackPaused ?
                m_PlaybackLabel.text.Replace("Event", "Paused") :
                m_PlaybackLabel.text.Replace("Paused", "Event");
        }

        private void RefreshFromReplay(int i, int count)
        {
            m_PlaybackLabel.text = $"{(m_Debugger.IsPlaybackPaused ? "Paused" : "Event")}: {i} / {count}...";
            Refresh();
        }

        private void UpdatePlaybackSpeed()
        {
            UpdatePlaybackSpeed(m_PlaybackSpeed);
        }

        private void UpdatePlaybackSpeed(float playbackSpeed)
        {
            var slowest = float.Parse(m_PlaybackSpeeds[0].Trim('x'));
            var fastest = float.Parse(m_PlaybackSpeeds[^1].Trim('x'));

            m_Debugger.PlaybackSpeed = playbackSpeed;
            m_PlaybackSpeedDropdown.SetValueWithoutNotify(m_Debugger.PlaybackSpeed + "x");
            m_DecreasePlaybackSpeedButton.SetEnabled(playbackSpeed > slowest);
            m_IncreasePlaybackSpeedButton.SetEnabled(playbackSpeed < fastest);
        }

        private void SaveReplaySessionFromSelection()
        {
            var path = EditorUtility.SaveFilePanel("Save Replay File", Application.dataPath, "ReplayData.json", "json");
            m_Debugger.SaveReplaySessionFromSelection(path, m_SelectedEvents.Select(x => x.EventBase).ToList());
        }

        private void LoadReplaySession()
        {
            var path = EditorUtility.OpenFilePanel("Select Replay File", "", "json");
            var savedRecord = m_Debugger.LoadReplaySession(path);
            if (savedRecord == null)
                return;

            ReplayEvents(savedRecord.eventList);
        }

        private void HighlightCodeLine(int hashcode)
        {
            var matchingIndex = -1;
            for (var i = 0; i < m_RegisteredEventCallbacksDataSource.Count; i++)
            {
                var data = m_RegisteredEventCallbacksDataSource[i];
                if (data.Type == LineType.CodeLine && data is CodeLineInfo codeLineData)
                {
                    var matchesHashcode = codeLineData.LineHashCode == hashcode;
                    if (matchesHashcode)
                    {
                        matchingIndex = i;
                        codeLineData.Highlighted = true;
                    }
                }
            }

            if (matchingIndex >= 0)
            {
                m_EventRegistrationsListView.Rebuild();
                m_EventRegistrationsListView.ScrollToItem(matchingIndex);
            }
        }

        private void UpdateEventsLog()
        {
            if (m_Log == null)
                return;

            m_Log.Clear();

            var activeEventTypes = new List<long>();

            foreach (var s in m_EventTypeFilter.State)
            {
                if (s.Value)
                {
                    activeEventTypes.Add(s.Key);
                }
            }

            bool allActive = activeEventTypes.Count == m_EventTypeFilter.State.Count;
            bool allInactive = activeEventTypes.Count == 0;
            if (CoordinatorDebug == null)
            {
                m_EventsLog.itemsSource = ToList();
                m_EventsLog.Rebuild();
                return;
            }

            var calls = m_Debugger.GetBeginEndProcessedEvents(CoordinatorDebug);
            if (calls == null)
            {
                m_EventsLog.itemsSource = ToList();
                m_EventsLog.Rebuild();
                return;
            }

            if (!allInactive)
            {
                for (var lineIndex = 0; lineIndex < calls.Count; lineIndex++)
                {
                    var eventBase = calls[lineIndex].EventBase;
                    if (allActive || activeEventTypes.Contains(eventBase.EventTypeId))
                    {
                        var eventDateTimeStr = eventBase.TimestampString() + " #" + eventBase.EventId;
                        string handler = eventBase.EventBaseName;
                        string targetName = eventBase.Target != null
                            ? EventDebugger.GetObjectDisplayName(eventBase.Target)
                            : "<null>";
                        var line = new EventLogLine(lineIndex + 1, "[" + eventDateTimeStr + "]", handler, targetName, eventBase);
                        m_Log.AddLine(line);
                    }
                }
            }

            UpdateLogCount();
            BuildEventsLog();
        }


        private void OnEventsLogSelectionChanged(IEnumerable<object> obj)
        {
            m_SelectedEvents ??= new List<EventLogLine>();
            m_SelectedEvents.Clear();

            if (obj != null)
            {
                foreach (EventLogLine listItem in obj.Cast<EventLogLine>())
                {
                    if (listItem != null)
                        m_SelectedEvents.Add(listItem);
                }
            }

            EventDebuggerEventRecord eventBase = null;
            if (m_SelectedEvents.Any())
            {
                var line = m_SelectedEvents[0];
                var calls = m_Debugger.GetBeginEndProcessedEvents(CoordinatorDebug);
                eventBase = line != null ? calls?[line.LineNumber - 1].EventBase : null;
            }

            UpdateSelectionCount();
            UpdatePlaybackButtons();

            if (m_SelectedEvents.Count == 1)
            {
                UpdateEventCallbacks(eventBase);
                UpdateEventPropagationPaths(eventBase);
                UpdateEventBaseInfo(eventBase);
            }
            else
            {
                ClearEventCallbacks();
                ClearEventPropagationPaths();
                ClearEventBaseInfo();
            }

            DisplayHistogram();
        }

        private void ClearEventBaseInfo()
        {
            m_EventBaseInfo.text = "";
        }


        private void OnFilterChange(StringChangeEvent e)
        {
            if (e.newValue != null)
                return;

            m_StateList = m_EventTypeFilter.State.Select(pair => new EventTypeFilterStateStruct { key = pair.Key, value = pair.Value }).ToList();

            m_Debugger.UpdateModificationCount();
            Refresh();
            BuildEventsLog();
            DisplayRegisteredEventCallbacks();
        }

        private void OnRegisteredCallbackFilterChange(StringChangeEvent _)
        {
            DisplayRegisteredEventCallbacks();
        }

        private void ClearEventCallbacks()
        {
            foreach (var data in m_RegisteredEventCallbacksDataSource)
            {
                if (data.Type == LineType.CodeLine && data is CodeLineInfo codeLineData)
                {
                    codeLineData.Highlighted = false;
                }
            }

            m_EventRegistrationsListView.Rebuild();
            m_EventCallbacksScrollView.Clear();
        }

        private void UpdateEventCallbacks(EventDebuggerEventRecord eventBase)
        {
            ClearEventCallbacks();

            if (eventBase == null)
                return;

            var callbacks = m_Debugger.GetCalls(CoordinatorDebug, eventBase);
            if (callbacks != null)
            {
                foreach (EventDebuggerCallTrace callback in callbacks)
                {
                    VisualElement container = new() { name = "line-container" };

                    Label timeStamp = new();
                    timeStamp.AddToClassList("timestamp");
                    Label handler = new();
                    handler.AddToClassList("handler");
                    Label phaseDurationContainer = new() { name = "phaseDurationContainer" };
                    Label phase = new();
                    phase.AddToClassList("phase");
                    Label duration = new();
                    duration.AddToClassList("duration");

                    var isProSkin = EditorGUIUtility.isProSkin;
                    phase.EnableInClassList("light", !isProSkin);
                    duration.EnableInClassList("light", !isProSkin);

                    timeStamp.AddToClassList("log-line-item");
                    handler.AddToClassList("log-line-item");
                    phaseDurationContainer.AddToClassList("log-line-item");

                    timeStamp.text = "[" + eventBase.TimestampString() + "]";
                    handler.text = callback.CallbackName;
                    if (callback.ImmediatePropagationHasStopped)
                        handler.text += " Immediately Stopped Propagation";
                    else if (callback.PropagationHasStopped)
                        handler.text += " Stopped Propagation";
                    if (callback.DefaultHasBeenPrevented)
                        handler.text += " (Default Prevented)";

                    phase.text = callback.EventBase.PropagationPhase.ToString();
                    duration.text = callback.Duration / 1000f + "ms";

                    container.Add(timeStamp);
                    container.Add(handler);
                    phaseDurationContainer.Add(phase);
                    phaseDurationContainer.Add(duration);
                    container.Add(phaseDurationContainer);

                    m_EventCallbacksScrollView.Add(container);

                    var hash = callback.CallbackHashCode;
                    HighlightCodeLine(hash);
                }
            }

            var defaultActions = m_Debugger.GetDefaultActions(CoordinatorDebug, eventBase);
            if (defaultActions == null)
                return;

            foreach (EventDebuggerDefaultActionTrace defaultAction in defaultActions)
            {
                VisualElement container = new() { name = "line-container" };

                Label timeStamp = new();
                timeStamp.AddToClassList("timestamp");
                Label handler = new();
                handler.AddToClassList("handler");
                Label phaseDurationContainer = new() { name = "phaseDurationContainer" };
                Label phase = new();
                phase.AddToClassList("phase");
                Label duration = new();
                duration.AddToClassList("duration");

                var isProSkin = EditorGUIUtility.isProSkin;
                phase.EnableInClassList("light", !isProSkin);
                duration.EnableInClassList("light", !isProSkin);

                timeStamp.AddToClassList("log-line-item");
                handler.AddToClassList("log-line-item");
                phaseDurationContainer.AddToClassList("log-line-item");

                timeStamp.text = "[" + eventBase.TimestampString() + "]";
                handler.text = defaultAction.TargetName + "." +
                    (defaultAction.Phase == PropagationPhase.AtTarget
                        ? "ExecuteDefaultActionAtTarget"
                        : "ExecuteDefaultAction");

                duration.text = defaultAction.Duration / 1000f + "ms";

                container.Add(timeStamp);
                container.Add(handler);
                phaseDurationContainer.Add(phase);
                phaseDurationContainer.Add(duration);
                container.Add(phaseDurationContainer);

                m_EventCallbacksScrollView.Add(container);
            }
        }
        private void ClearEventPropagationPaths()
        {
            m_EventPropagationPaths.text = "";
        }
        private void UpdateEventPropagationPaths(EventDebuggerEventRecord eventBase)
        {
            ClearEventPropagationPaths();

            if (eventBase == null)
                return;

            var propagationPaths = m_Debugger.GetPropagationPaths(CoordinatorDebug, eventBase);
            if (propagationPaths == null)
                return;

            foreach (EventDebuggerPathTrace propagationPath in propagationPaths)
            {
                if (propagationPath?.Paths == null)
                    continue;

                m_EventPropagationPaths.text += "Trickle Down Path:\n";
                var pathsTrickleDownPath = propagationPath.Paths?.TrickleDownPath;
                if (pathsTrickleDownPath != null && pathsTrickleDownPath.Any())
                {
                    foreach (var trickleDownPathElement in pathsTrickleDownPath)
                    {
                        // var trickleDownPathName = trickleDownPathElement.name;
                        // if (string.IsNullOrEmpty(trickleDownPathName))
                        var trickleDownPathName = trickleDownPathElement.GetType().Name;
                        m_EventPropagationPaths.text += "    " + trickleDownPathName + "\n";
                    }
                }
                else
                {
                    m_EventPropagationPaths.text += "    <empty>\n";
                }

                m_EventPropagationPaths.text += "Target list:\n";
                var targets = propagationPath.Paths.TargetElements;
                if (targets != null && targets.Any())
                {
                    foreach (var t in targets)
                    {
                        // var targetName = t.name;
                        // if (string.IsNullOrEmpty(targetName))
                        var targetName = t.GetType().Name;
                        m_EventPropagationPaths.text += "    " + targetName + "\n";
                    }
                }
                else
                {
                    m_EventPropagationPaths.text += "    <empty>\n";
                }

                m_EventPropagationPaths.text += "Bubble Up Path:\n";
                var pathsBubblePath = propagationPath.Paths.BubbleUpPath;
                if (pathsBubblePath != null && pathsBubblePath.Any())
                {
                    foreach (var bubblePathElement in pathsBubblePath)
                    {
                        // var bubblePathName = bubblePathElement.name;
                        // if (string.IsNullOrEmpty(bubblePathName))
                        var bubblePathName = bubblePathElement.GetType().Name;
                        m_EventPropagationPaths.text += "    " + bubblePathName + "\n";
                    }
                }
                else
                {
                    m_EventPropagationPaths.text += "    <empty>\n";
                }
            }
        }

        private void BuildEventsLog()
        {
            if (m_MaxLogLines)
            {
                m_StartIndex = Math.Max(0, m_Log.lines.Count - m_MaxLogLineCount);
                m_EventsLog.itemsSource = m_Log.lines.Skip(m_StartIndex).Take(m_MaxLogLineCount).ToList();
            }
            else
            {
                m_StartIndex = 0;
                m_EventsLog.itemsSource = ToList();
            }

            m_EventsLog.fixedItemHeight = 15;
            m_EventsLog.bindItem = (target, index) =>
            {
                var line = m_Log.lines[index + m_StartIndex];

                // Add text
                VisualElement lineText = target.MandatoryQ<VisualElement>("log-line");
                if (lineText[0] is Label theLabel)
                {
                    theLabel.text = line.Timestamp;
                    theLabel = lineText[1] as Label;
                    if (theLabel != null)
                        theLabel.text = line.EventName;
                    theLabel = lineText[2] as Label;
                    if (theLabel != null)
                        theLabel.text = line.Target;
                }
            };
            m_EventsLog.makeItem = () =>
            {
                VisualElement container = new() { name = "log-line" };
                Label timeStamp = new();
                timeStamp.AddToClassList("timestamp");
                Label eventLabel = new();
                eventLabel.AddToClassList("event");
                Label target = new();
                target.AddToClassList("target");

                timeStamp.AddToClassList("log-line-item");
                eventLabel.AddToClassList("log-line-item");
                target.AddToClassList("log-line-item");

                container.Add(timeStamp);
                container.Add(eventLabel);
                container.Add(target);

                return container;
            };

            m_EventsLog.Rebuild();
            DoAutoScroll();
        }

        private void DoAutoScroll()
        {
            if (m_AutoScroll)
                rootVisualElement.schedule.Execute(() => m_EventsLog.ScrollToItem(-1));
        }

        private IList ToList()
        {
            return m_Log.lines.ToList();
        }

        public void ClearLogs()
        {
            m_Debugger.ClearLogs();
            m_SelectedEvents?.Clear();
            Refresh();
            OnReplayCompleted();
        }


        private void OnReplayCompleted()
        {
            m_Debugger.StopPlayback();

            if (m_TogglePlayback != null)
                m_TogglePlayback.value = false;
            if (m_PlaybackLabel != null)
                m_PlaybackLabel.text = "";
            UpdatePlaybackButtons();
        }

        protected override void OnSelectCoordinateDebug(IEventCoordinator selectedCoordinateDebug)
        {
            if (selectedCoordinateDebug == m_Debugger.Coordinator)
                return;
            if (selectedCoordinateDebug != null)
            {
                m_Debugger.CoordinatorDebug = selectedCoordinateDebug;
            }
            m_EventTypeFilter.SetEventLog(m_Debugger.EventTypeProcessedCount);

            DisplayRegisteredEventCallbacks();
            Refresh();
        }

        public override void Refresh()
        {
            var eventDebuggerModificationCount = m_Debugger.GetModificationCount(CoordinatorDebug);
            if (eventDebuggerModificationCount == m_ModificationCount)
                return;

            m_ModificationCount = eventDebuggerModificationCount;
            UpdateEventsLog();
            UpdateLogCount();
            UpdateSelectionCount();
            UpdatePlaybackButtons();
            DisplayHistogram();
        }

        private void UpdateLogCount()
        {
            if (m_LogCountLabel == null || m_Log?.lines == null)
                return;

            m_LogCountLabel.text = m_Log.lines.Count + " event" + (m_Log.lines.Count > 1 ? "s" : "");
        }

        private void UpdateSelectionCount()
        {
            if (m_SelectionCountLabel == null)
                return;

            m_SelectionCountLabel.text =
                "(" + (m_SelectedEvents != null ? m_SelectedEvents.Count.ToString() : "0") + " selected)";
        }

        private void UpdatePlaybackButtons()
        {
            var isProSkin = EditorGUIUtility.isProSkin;
            m_DecreasePlaybackSpeedButton.EnableInClassList("light", !isProSkin);
            m_IncreasePlaybackSpeedButton.EnableInClassList("light", !isProSkin);
            m_SaveReplayButton.EnableInClassList("light", !isProSkin);
            m_LoadReplayButton.EnableInClassList("light", !isProSkin);

            var anySelected = m_SelectedEvents != null && m_SelectedEvents.Any();
            m_TogglePlayback?.SetEnabled(m_Debugger.IsReplaying);
            m_StopPlaybackButton?.SetEnabled(m_Debugger.IsReplaying);
            m_SaveReplayButton?.SetEnabled(anySelected);
            m_StartPlaybackButton?.SetEnabled(anySelected && !m_Debugger.IsReplaying);
        }
        
        private void UpdateEventBaseInfo(EventDebuggerEventRecord eventBase)
        {
            ClearEventBaseInfo();
            if (eventBase == null)
                return;
            m_EventBaseInfo.text += FormatJson(eventBase.JsonData);
        }
        
        /// <summary>
        /// Only format for indent level 0
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static string FormatJson(string json)
        {
            StringBuilder stringBuilder = new();
            JObject obj = JsonConvert.DeserializeObject<JObject>(json);
            foreach (var property in obj.Properties())
            {
                stringBuilder.AppendLine(property.Name + ": " + property.Value.ToString());
            }
            return stringBuilder.ToString();
        }
    }
}
