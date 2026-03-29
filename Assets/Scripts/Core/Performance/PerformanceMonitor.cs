using UnityEngine;
using Banganka.Core.Analytics;

namespace Banganka.Core.Performance
{
    /// <summary>
    /// パフォーマンス監視 (PERFORMANCE_SPEC.md)
    /// FPS / メモリ / サーマル状態 — 目標: 60fps, 800MB, iPhone SE2基準
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        public static PerformanceMonitor Instance { get; private set; }

        [Header("Targets")]
        [SerializeField] int targetFps = 60;
        [SerializeField] int minAcceptableFps = 45;
        [SerializeField] float memoryBudgetMB = 800f;
        [SerializeField] float measureInterval = 2f;

        [Header("Debug")]
        [SerializeField] bool showDebugOverlay;

        // FPS tracking
        float _fpsTimer;
        int _frameCount;
        float _currentFps;
        int _lowFpsStreak;

        // Memory tracking
        float _lastMemoryCheckTime;
        float _currentMemoryMB;

        // Thermal tracking
        int _qualityLevel = 2; // 0=low, 1=mid, 2=high

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = targetFps;
        }

        void Update()
        {
            TrackFps();

            if (Time.time - _lastMemoryCheckTime > 10f)
            {
                _lastMemoryCheckTime = Time.time;
                CheckMemory();
                CheckThermalState();
            }
        }

        // ====================================================================
        // FPS
        // ====================================================================

        void TrackFps()
        {
            _frameCount++;
            _fpsTimer += Time.unscaledDeltaTime;

            if (_fpsTimer >= measureInterval)
            {
                _currentFps = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0;

                if (_currentFps < minAcceptableFps)
                {
                    _lowFpsStreak++;
                    if (_lowFpsStreak >= 3) // 3 consecutive low measurements
                    {
                        OnPersistentLowFps();
                        _lowFpsStreak = 0;
                    }
                }
                else
                {
                    _lowFpsStreak = 0;
                }
            }
        }

        void OnPersistentLowFps()
        {
            Debug.LogWarning($"[Perf] Low FPS detected: {_currentFps:F0}");
            AnalyticsService.LogFpsDrop(_currentFps, GetCurrentScreenName());

            // Auto-reduce quality
            if (_qualityLevel > 0)
            {
                _qualityLevel--;
                ApplyQualityLevel();
            }
        }

        // ====================================================================
        // Memory (800MB budget)
        // ====================================================================

        void CheckMemory()
        {
            // Unity provides total reserved memory
            _currentMemoryMB = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f);

            if (_currentMemoryMB > memoryBudgetMB * 0.9f) // 90% warning
            {
                Debug.LogWarning($"[Perf] Memory warning: {_currentMemoryMB:F0}MB / {memoryBudgetMB:F0}MB");
                AnalyticsService.LogMemoryWarning(_currentMemoryMB);

                // Force GC and unload unused assets
                Resources.UnloadUnusedAssets();
                System.GC.Collect();
            }
        }

        // ====================================================================
        // Thermal State (iOS)
        // ====================================================================

        void CheckThermalState()
        {
            // NOTE: On iOS, check ProcessInfo.thermalState
            // Unity doesn't expose this directly — use native plugin or
            // approximate via sustained low FPS + high memory

            // Approximate: if FPS is consistently low, reduce quality
            if (_currentFps < minAcceptableFps && _qualityLevel > 0)
            {
                _qualityLevel--;
                ApplyQualityLevel();
            }
            else if (_currentFps > targetFps - 5 && _qualityLevel < 2)
            {
                _qualityLevel++;
                ApplyQualityLevel();
            }
        }

        // ====================================================================
        // Quality Levels
        // ====================================================================

        void ApplyQualityLevel()
        {
            switch (_qualityLevel)
            {
                case 0: // Low — iPhone SE2で安定動作
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = 30;
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.antiAliasing = 0;
                    Debug.Log("[Perf] Quality: LOW (30fps, no shadows/AA)");
                    break;

                case 1: // Mid
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = 60;
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.antiAliasing = 2;
                    Debug.Log("[Perf] Quality: MID (60fps, no shadows, 2x AA)");
                    break;

                case 2: // High
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = 60;
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.antiAliasing = 4;
                    Debug.Log("[Perf] Quality: HIGH (60fps, hard shadows, 4x AA)");
                    break;
            }
        }

        // ====================================================================
        // Public API
        // ====================================================================

        public float CurrentFps => _currentFps;
        public float CurrentMemoryMB => _currentMemoryMB;
        public int QualityLevel => _qualityLevel;

        public void ForceQuality(int level)
        {
            _qualityLevel = Mathf.Clamp(level, 0, 2);
            ApplyQualityLevel();
        }

        static string GetCurrentScreenName()
        {
            var gm = Banganka.Game.GameManager.Instance;
            return gm != null ? gm.CurrentScreen.ToString() : "Unknown";
        }

        // ====================================================================
        // Debug Overlay
        // ====================================================================

        void OnGUI()
        {
            if (!showDebugOverlay) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            style.normal.textColor = _currentFps >= minAcceptableFps ? Color.green : Color.red;

            GUI.Label(new Rect(10, 10, 300, 30),
                $"FPS: {_currentFps:F0} | MEM: {_currentMemoryMB:F0}MB | Q:{_qualityLevel}", style);
        }
    }
}
