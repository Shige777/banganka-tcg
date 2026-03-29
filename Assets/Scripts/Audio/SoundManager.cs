using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Banganka.Audio
{
    /// <summary>
    /// SE優先度 (SOUND_DESIGN_SPEC §3.1-3.2)
    /// 最高=0, 高=1, 中=2, 低=3
    /// </summary>
    public enum SEPriority
    {
        Critical = 0,
        High = 1,
        Normal = 2,
        Low = 3
    }

    /// <summary>
    /// バトルBGMの状態 (SOUND_DESIGN_SPEC §2.3)
    /// </summary>
    enum BattleMusicState
    {
        None,
        Normal,         // HP 40-60%: BATTLE_MAIN 通常再生
        Tension,        // HP 20-80% (outside center): BATTLE_MAIN + テンションレイヤー
        Climax,         // HP < 20%: BATTLE_CLIMAX
        ClimaxStrings   // HP < 10%: BATTLE_CLIMAX + ストリングス
    }

    /// <summary>
    /// アクティブSEの追跡情報
    /// </summary>
    class ActiveSEEntry
    {
        public AudioSource Source;
        public SEPriority Priority;
        public float StartTime;
        public float Duration;
        public string SeId;

        public bool IsFinished => Time.time - StartTime >= Duration;
    }

    /// <summary>
    /// サウンド管理 (SOUND_DESIGN_SPEC.md)
    /// BGM 11曲 + SE 48種
    /// BGMクロスフェード、ダイナミックBGM、SE優先度システム対応
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("BGM Sources")]
        [SerializeField] AudioSource bgmSource;
        [SerializeField] AudioSource _bgmSourceB;

        [Header("Layer Sources (Dynamic BGM)")]
        [SerializeField] AudioSource _tensionLayer;
        [SerializeField] AudioSource _stringsLayer;

        [Header("SE Sources")]
        [SerializeField] AudioSource seSource;
        [SerializeField] AudioSource uiSeSource;

        [Header("Crossfade Settings")]
        [SerializeField] float defaultCrossfadeDuration = 1.5f;

        // BGM clips (loaded from Resources/Audio/)
        readonly Dictionary<string, AudioClip> _bgmCache = new();
        readonly Dictionary<string, AudioClip> _seCache = new();

        float _bgmVolume = 0.7f;
        float _seVolume = 1.0f;
        bool _muted;

        // ====================================================================
        // BGM Crossfade state (SOUND_DESIGN_SPEC §2.2)
        // ====================================================================
        bool _bgmSourceAIsActive = true;
        Coroutine _crossfadeCoroutine;

        AudioSource ActiveBGMSource => _bgmSourceAIsActive ? bgmSource : _bgmSourceB;
        AudioSource InactiveBGMSource => _bgmSourceAIsActive ? _bgmSourceB : bgmSource;

        // ====================================================================
        // Dynamic BGM state (SOUND_DESIGN_SPEC §2.3)
        // ====================================================================
        BattleMusicState _currentBattleMusicState = BattleMusicState.None;

        // ====================================================================
        // SE Priority state (SOUND_DESIGN_SPEC §3.1-3.2)
        // ====================================================================
        const int MaxConcurrentVoices = 8;
        const float VolumeDuckMultiplier = 0.5f;
        const float VolumeDuckDuration = 0.3f;

        readonly List<ActiveSEEntry> _activeSEEntries = new();
        readonly List<AudioSource> _seSourcePool = new();
        Coroutine _volumeDuckCoroutine;

        // SE ID → 優先度マッピング (SOUND_DESIGN_SPEC §3.1)
        static readonly Dictionary<string, SEPriority> SEPriorityMap = new()
        {
            // 最高 (Critical)
            { "se_attack_hit", SEPriority.Critical },
            { "se_direct_hit", SEPriority.Critical },
            { "se_law_set", SEPriority.Critical },
            { "se_law_open", SEPriority.Critical },
            { "se_law_overwrite", SEPriority.Critical },
            { "se_level_up", SEPriority.Critical },
            { "se_leader_skill_lv3", SEPriority.Critical },
            { "se_victory", SEPriority.Critical },
            { "se_shachihoko_win", SEPriority.Critical },
            { "se_nuri_win", SEPriority.Critical },
            { "se_defeat", SEPriority.Critical },
            { "se_final_state", SEPriority.Critical },
            { "se_card_flip_ssr", SEPriority.Critical },
            { "se_leader_cutin", SEPriority.Critical },
            { "se_leader_levelup", SEPriority.Critical },
            { "se_damage_critical", SEPriority.Critical },
            // 高 (High)
            { "se_card_play", SEPriority.High },
            { "se_spell_cast", SEPriority.High },
            { "se_attack_declare", SEPriority.High },
            { "se_block", SEPriority.High },
            { "se_destroy", SEPriority.High },
            { "se_hp_damage", SEPriority.High },
            { "se_threshold_cross", SEPriority.High },
            { "se_wish_trigger", SEPriority.High },
            { "se_leader_skill_lv2", SEPriority.High },
            { "se_skill_unlock", SEPriority.High },
            { "se_law_set_facedown", SEPriority.High },
            { "se_pack_open", SEPriority.High },
            { "se_card_flip_sr", SEPriority.High },
            { "se_timer_warning", SEPriority.High },
            { "se_summon_sora", SEPriority.High },
            { "se_summon_akebono", SEPriority.High },
            { "se_summon_odayaka", SEPriority.High },
            { "se_summon_ayakashi", SEPriority.High },
            { "se_summon_asobi", SEPriority.High },
            { "se_summon_kuro", SEPriority.High },
            { "se_damage_large", SEPriority.High },
            { "se_damage_medium", SEPriority.High },
            { "se_unit_exit", SEPriority.High },
            // 中 (Normal)
            { "se_card_lift", SEPriority.Normal },
            { "se_turn_start", SEPriority.Normal },
            { "se_turn_end", SEPriority.Normal },
            { "se_card_flip", SEPriority.Normal },
            { "se_turn_start_my", SEPriority.Normal },
            { "se_turn_start_enemy", SEPriority.Normal },
            { "se_mulligan_select", SEPriority.Normal },
            { "se_mulligan_confirm", SEPriority.Normal },
            { "se_match_start", SEPriority.Normal },
            // 低 (Low)
            { "se_draw", SEPriority.Low },
            { "se_card_draw", SEPriority.Low },
            { "se_button_tap", SEPriority.Low },
            { "se_button_cancel", SEPriority.Low },
            { "se_tab_switch", SEPriority.Low },
            { "se_navigation", SEPriority.Low },
            { "se_emote", SEPriority.Low },
            { "se_notification", SEPriority.Low },
        };

        // ====================================================================
        // Screen BGM routing (自動画面BGM切替)
        // ====================================================================

        static readonly Dictionary<string, string> ScreenBGMMap = new()
        {
            { "Home", "bgm_home" },
            { "Battle", "bgm_battle_normal" },
            { "Cards", "bgm_home" },
            { "Story", "bgm_story_calm" },
            { "Shop", "bgm_shop" },
            { "Tutorial", "bgm_tutorial" },
            { "Title", "bgm_title" },
        };

        string _currentScreen;

        /// <summary>
        /// 画面遷移時にBGMを自動切替する。ScreenManagerから呼ぶ。
        /// Battle画面ではSetBattleHPStateが動的BGMを制御するため、初回のみ設定。
        /// </summary>
        public void OnScreenChanged(string screenName)
        {
            if (screenName == _currentScreen) return;
            _currentScreen = screenName;

            // バトル画面では動的BGMシステムが管理するため直接設定しない
            if (screenName == "Battle")
            {
                _currentBattleMusicState = BattleMusicState.None;
                PlayBGM("bgm_battle_normal");
                return;
            }

            // 非バトル画面ではレイヤーを停止
            if (_tensionLayer.isPlaying) StartCoroutine(FadeLayerCoroutine(_tensionLayer, 0f, 0.5f));
            if (_stringsLayer.isPlaying) StartCoroutine(FadeLayerCoroutine(_stringsLayer, 0f, 0.5f));
            _currentBattleMusicState = BattleMusicState.None;

            if (ScreenBGMMap.TryGetValue(screenName, out var bgmId))
                PlayBGM(bgmId);
        }

        // ====================================================================
        // Ambient loop system (環境音)
        // ====================================================================

        AudioSource _ambientSource;
        string _currentAmbientId;

        /// <summary>環境音ループを再生 (バトルフィールド音、ショップ雰囲気等)</summary>
        public void PlayAmbient(string ambientId, float volume = 0.3f)
        {
            if (_muted) return;
            if (ambientId == _currentAmbientId && _ambientSource != null && _ambientSource.isPlaying) return;

            EnsureAmbientSource();

            var clip = LoadClip("Audio/Ambient/" + ambientId);
            if (clip == null) clip = LoadClip("Audio/" + ambientId);
            if (clip == null) return;

            _currentAmbientId = ambientId;
            _ambientSource.clip = clip;
            _ambientSource.volume = 0f;
            _ambientSource.loop = true;
            _ambientSource.Play();
            StartCoroutine(FadeLayerCoroutine(_ambientSource, volume, 1.0f));
        }

        /// <summary>環境音をフェードアウトして停止</summary>
        public void StopAmbient(float fadeOut = 1.0f)
        {
            if (_ambientSource == null || !_ambientSource.isPlaying) return;
            _currentAmbientId = null;
            StartCoroutine(FadeLayerCoroutine(_ambientSource, 0f, fadeOut));
        }

        void EnsureAmbientSource()
        {
            if (_ambientSource != null) return;
            _ambientSource = gameObject.AddComponent<AudioSource>();
            _ambientSource.loop = true;
            _ambientSource.playOnAwake = false;
            _ambientSource.volume = 0f;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // BGM Source A (primary)
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
            }

            // BGM Source B (crossfade partner)
            if (_bgmSourceB == null)
            {
                _bgmSourceB = gameObject.AddComponent<AudioSource>();
                _bgmSourceB.loop = true;
                _bgmSourceB.playOnAwake = false;
                _bgmSourceB.volume = 0f;
            }

            // Tension layer (パーカッション強化)
            if (_tensionLayer == null)
            {
                _tensionLayer = gameObject.AddComponent<AudioSource>();
                _tensionLayer.loop = true;
                _tensionLayer.playOnAwake = false;
                _tensionLayer.volume = 0f;
            }

            // Strings layer (ストリングス上昇)
            if (_stringsLayer == null)
            {
                _stringsLayer = gameObject.AddComponent<AudioSource>();
                _stringsLayer.loop = true;
                _stringsLayer.playOnAwake = false;
                _stringsLayer.volume = 0f;
            }

            // SE sources
            if (seSource == null)
            {
                seSource = gameObject.AddComponent<AudioSource>();
                seSource.playOnAwake = false;
            }
            if (uiSeSource == null)
            {
                uiSeSource = gameObject.AddComponent<AudioSource>();
                uiSeSource.playOnAwake = false;
            }
        }

        void Update()
        {
            CleanupFinishedSE();
        }

        // ====================================================================
        // BGM (SOUND_DESIGN_SPEC.md §2)
        // ====================================================================

        // BGM IDs:
        // bgm_title, bgm_home, bgm_battle_normal, bgm_battle_intense,
        // bgm_battle_climax, bgm_victory, bgm_defeat, bgm_shop,
        // bgm_story_calm, bgm_story_tension, bgm_tutorial

        /// <summary>
        /// BGM再生 (クロスフェード対応)
        /// SOUND_DESIGN_SPEC §2.2: 現在のBGMから新しいBGMへクロスフェード遷移
        /// </summary>
        public void PlayBGM(string bgmId, float fadeDuration = -1f)
        {
            if (_muted) return;
            var clip = LoadClip("Audio/BGM/" + bgmId);
            if (clip == null) clip = LoadClip("Audio/" + bgmId);
            if (clip == null) { Debug.LogWarning($"[SoundManager] BGM not found: {bgmId}"); return; }

            // 同じ曲が再生中なら何もしない
            if (ActiveBGMSource.clip == clip && ActiveBGMSource.isPlaying) return;

            float duration = fadeDuration >= 0f ? fadeDuration : defaultCrossfadeDuration;

            // 現在BGMが再生中ならクロスフェード、そうでなければ直接再生
            if (ActiveBGMSource.isPlaying)
            {
                if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
                _crossfadeCoroutine = StartCoroutine(CrossfadeBGMCoroutine(clip, duration));
            }
            else
            {
                // 何も再生していない場合はフェードインで開始
                ActiveBGMSource.clip = clip;
                ActiveBGMSource.volume = 0f;
                ActiveBGMSource.Play();
                if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
                _crossfadeCoroutine = StartCoroutine(FadeInBGMCoroutine(ActiveBGMSource, duration));
            }
        }

        /// <summary>
        /// BGM停止 (フェードアウト対応)
        /// </summary>
        public void StopBGM(float fadeOut = 0.5f)
        {
            if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);

            if (fadeOut > 0f)
            {
                _crossfadeCoroutine = StartCoroutine(FadeOutAndStopCoroutine(fadeOut));
            }
            else
            {
                bgmSource.Stop();
                _bgmSourceB.Stop();
                _tensionLayer.Stop();
                _stringsLayer.Stop();
            }

            _currentBattleMusicState = BattleMusicState.None;
        }

        /// <summary>
        /// BGMクロスフェード (SOUND_DESIGN_SPEC §2.2)
        /// SourceA/SourceBを交互に使い、スムーズに遷移する
        /// </summary>
        IEnumerator CrossfadeBGMCoroutine(AudioClip newClip, float duration)
        {
            var outgoing = ActiveBGMSource;
            var incoming = InactiveBGMSource;

            // 新しいソースを準備
            incoming.clip = newClip;
            incoming.volume = 0f;
            incoming.Play();

            float elapsed = 0f;
            float outStartVolume = outgoing.volume;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                outgoing.volume = Mathf.Lerp(outStartVolume, 0f, t);
                incoming.volume = Mathf.Lerp(0f, _bgmVolume, t);

                yield return null;
            }

            outgoing.Stop();
            outgoing.volume = 0f;
            incoming.volume = _bgmVolume;

            // アクティブソースを切り替え
            _bgmSourceAIsActive = !_bgmSourceAIsActive;
            _crossfadeCoroutine = null;
        }

        IEnumerator FadeInBGMCoroutine(AudioSource source, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(0f, _bgmVolume, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            source.volume = _bgmVolume;
            _crossfadeCoroutine = null;
        }

        IEnumerator FadeOutAndStopCoroutine(float duration)
        {
            var sourceA = bgmSource;
            var sourceB = _bgmSourceB;
            float startA = sourceA.volume;
            float startB = sourceB.volume;
            float startTension = _tensionLayer.volume;
            float startStrings = _stringsLayer.volume;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                sourceA.volume = Mathf.Lerp(startA, 0f, t);
                sourceB.volume = Mathf.Lerp(startB, 0f, t);
                _tensionLayer.volume = Mathf.Lerp(startTension, 0f, t);
                _stringsLayer.volume = Mathf.Lerp(startStrings, 0f, t);
                yield return null;
            }

            sourceA.Stop();
            sourceB.Stop();
            _tensionLayer.Stop();
            _stringsLayer.Stop();
            _crossfadeCoroutine = null;
        }

        // ====================================================================
        // Dynamic/Adaptive BGM (SOUND_DESIGN_SPEC §2.3)
        // ====================================================================

        /// <summary>
        /// バトル中HP状態に応じてBGMを動的に変化させる (SOUND_DESIGN_SPEC §2.3)
        /// HP残量パーセンテージ(低い方)を基準に判定:
        ///   40-60%: BATTLE_MAIN 通常
        ///   20-80% (outside center): BATTLE_MAIN + テンションレイヤー
        ///   &lt; 20%: BATTLE_CLIMAX
        ///   &lt; 10%: BATTLE_CLIMAX + ストリングス
        /// </summary>
        public void SetBattleHPState(int playerHp, int opponentHp, int maxHp)
        {
            if (maxHp <= 0) return;

            float playerPct = (float)playerHp / maxHp * 100f;
            float opponentPct = (float)opponentHp / maxHp * 100f;
            float minPct = Mathf.Min(playerPct, opponentPct);

            BattleMusicState newState;

            if (minPct < 10f)
            {
                newState = BattleMusicState.ClimaxStrings;
            }
            else if (minPct < 20f)
            {
                newState = BattleMusicState.Climax;
            }
            else if (minPct < 40f || minPct > 60f)
            {
                newState = BattleMusicState.Tension;
            }
            else
            {
                newState = BattleMusicState.Normal;
            }

            if (newState == _currentBattleMusicState) return;

            ApplyBattleMusicState(newState);
            _currentBattleMusicState = newState;
        }

        void ApplyBattleMusicState(BattleMusicState state)
        {
            switch (state)
            {
                case BattleMusicState.Normal:
                    // BATTLE_MAIN 通常再生、レイヤーOFF
                    EnsureBattleMainPlaying();
                    StartCoroutine(FadeLayerCoroutine(_tensionLayer, 0f, 1.0f));
                    StartCoroutine(FadeLayerCoroutine(_stringsLayer, 0f, 1.0f));
                    break;

                case BattleMusicState.Tension:
                    // BATTLE_MAIN + テンションレイヤー (パーカッション)
                    EnsureBattleMainPlaying();
                    EnsureLayerPlaying(_tensionLayer, "Audio/BGM/bgm_battle_tension_layer");
                    StartCoroutine(FadeLayerCoroutine(_tensionLayer, _bgmVolume, 1.0f));
                    StartCoroutine(FadeLayerCoroutine(_stringsLayer, 0f, 1.0f));
                    break;

                case BattleMusicState.Climax:
                    // BATTLE_CLIMAX に遷移、レイヤーOFF
                    PlayBGM("bgm_battle_climax", 2.0f);
                    StartCoroutine(FadeLayerCoroutine(_tensionLayer, 0f, 1.0f));
                    StartCoroutine(FadeLayerCoroutine(_stringsLayer, 0f, 1.0f));
                    break;

                case BattleMusicState.ClimaxStrings:
                    // BATTLE_CLIMAX + ストリングス
                    PlayBGM("bgm_battle_climax", 2.0f);
                    StartCoroutine(FadeLayerCoroutine(_tensionLayer, 0f, 1.0f));
                    EnsureLayerPlaying(_stringsLayer, "Audio/BGM/bgm_battle_strings_layer");
                    StartCoroutine(FadeLayerCoroutine(_stringsLayer, _bgmVolume, 1.5f));
                    break;
            }
        }

        void EnsureBattleMainPlaying()
        {
            var clip = LoadClip("Audio/BGM/bgm_battle_main");
            if (clip == null) clip = LoadClip("Audio/BGM/bgm_battle_normal");
            if (clip == null) return;

            // 現在のアクティブソースがBATTLE_MAINでなければクロスフェード
            if (ActiveBGMSource.clip != clip || !ActiveBGMSource.isPlaying)
            {
                PlayBGM("bgm_battle_main", 2.0f);
            }
        }

        void EnsureLayerPlaying(AudioSource layer, string clipPath)
        {
            if (layer.isPlaying) return;
            var clip = LoadClip(clipPath);
            if (clip == null) return;
            layer.clip = clip;
            layer.volume = 0f;
            layer.Play();

            // メインBGMと同期するために再生位置を合わせる
            layer.timeSamples = ActiveBGMSource.timeSamples % (layer.clip.samples > 0 ? layer.clip.samples : 1);
        }

        IEnumerator FadeLayerCoroutine(AudioSource layer, float targetVolume, float duration)
        {
            float startVolume = layer.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                layer.volume = Mathf.Lerp(startVolume, targetVolume, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            layer.volume = targetVolume;

            // ボリューム0になったら停止
            if (targetVolume <= 0f && layer.isPlaying)
            {
                layer.Stop();
            }
        }

        // ====================================================================
        // SE (SOUND_DESIGN_SPEC.md §3)
        // ====================================================================

        // Core SE IDs:
        // se_card_play, se_card_draw, se_attack_declare, se_attack_hit,
        // se_unit_exit, se_direct_hit, se_wish_trigger, se_leader_levelup,
        // se_leader_skill, se_blocker, se_spell_cast, se_algorithm_set,
        // se_turn_start, se_turn_end, se_victory, se_defeat,
        // se_button_tap, se_button_cancel, se_tab_switch, se_notification,
        // se_pack_open, se_pack_reveal_c, se_pack_reveal_r, se_pack_reveal_sr, se_pack_reveal_ssr,
        // se_craft, se_dismantle, se_purchase, se_mission_complete,
        // se_mulligan_select, se_mulligan_confirm, se_countdown_tick,
        // se_emote_1..se_emote_6, se_final_state, se_match_start

        /// <summary>
        /// SE再生 (優先度システム対応)
        /// SOUND_DESIGN_SPEC §3.1-3.2: 最大同時8ボイス、優先度に基づく制御
        /// </summary>
        public void PlaySE(string seId)
        {
            if (_muted) return;

            var priority = GetSEPriority(seId);
            PlaySEWithPriority(seId, priority, _seVolume);
        }

        /// <summary>
        /// SE再生 (優先度明示)
        /// </summary>
        public void PlaySE(string seId, SEPriority priority)
        {
            if (_muted) return;
            PlaySEWithPriority(seId, priority, _seVolume);
        }

        public void PlayUISE(string seId)
        {
            if (_muted) return;
            var clip = LoadClip("Audio/SE/" + seId);
            if (clip == null) clip = LoadClip("Audio/" + seId);
            if (clip == null) return;

            // UI SEは専用ソースを使用 (音量 0.8x)
            uiSeSource.volume = _seVolume * 0.8f;
            uiSeSource.PlayOneShot(clip);
        }

        void PlaySEWithPriority(string seId, SEPriority priority, float volume)
        {
            var clip = LoadClip("Audio/SE/" + seId);
            if (clip == null) clip = LoadClip("Audio/" + seId);
            if (clip == null) { Debug.LogWarning($"[SoundManager] SE not found: {seId}"); return; }

            // エントリ数がMaxに達している場合、最低優先度のSEを停止
            CleanupFinishedSE();

            if (_activeSEEntries.Count >= MaxConcurrentVoices)
            {
                if (!EvictLowestPrioritySE(priority))
                {
                    // 全てのアクティブSEが同等以上の優先度なので再生しない
                    return;
                }
            }

            // AudioSourceを取得して再生
            var source = GetAvailableSESource();
            source.volume = volume;
            source.PlayOneShot(clip);

            _activeSEEntries.Add(new ActiveSEEntry
            {
                Source = source,
                Priority = priority,
                StartTime = Time.time,
                Duration = clip.length,
                SeId = seId
            });

            // Critical SEのボリュームダッキング
            if (priority == SEPriority.Critical)
            {
                if (_volumeDuckCoroutine != null) StopCoroutine(_volumeDuckCoroutine);
                _volumeDuckCoroutine = StartCoroutine(VolumeDuckCoroutine());
            }
        }

        /// <summary>
        /// 最低優先度のSEを退場させる。新しいSEより優先度が低い場合のみ成功。
        /// </summary>
        bool EvictLowestPrioritySE(SEPriority newPriority)
        {
            int worstIndex = -1;
            SEPriority worstPriority = SEPriority.Critical;

            for (int i = 0; i < _activeSEEntries.Count; i++)
            {
                if (_activeSEEntries[i].Priority > worstPriority)
                {
                    worstPriority = _activeSEEntries[i].Priority;
                    worstIndex = i;
                }
            }

            // 退場候補が新しいSEより優先度が高いか同等なら退場させない
            if (worstIndex < 0 || worstPriority < newPriority) return false;

            var entry = _activeSEEntries[worstIndex];
            entry.Source.Stop();
            ReturnSESource(entry.Source);
            _activeSEEntries.RemoveAt(worstIndex);
            return true;
        }

        /// <summary>
        /// Critical SE再生時、他のSEボリュームを50%に0.3秒間ダッキング
        /// </summary>
        IEnumerator VolumeDuckCoroutine()
        {
            // 他のアクティブSEのボリュームを一時的に下げる
            float duckVolume = _seVolume * VolumeDuckMultiplier;

            foreach (var entry in _activeSEEntries)
            {
                if (entry.Priority != SEPriority.Critical)
                {
                    entry.Source.volume = duckVolume;
                }
            }

            yield return new WaitForSeconds(VolumeDuckDuration);

            // ボリュームを復元
            foreach (var entry in _activeSEEntries)
            {
                if (entry.Priority != SEPriority.Critical && !entry.IsFinished)
                {
                    entry.Source.volume = _seVolume;
                }
            }

            _volumeDuckCoroutine = null;
        }

        /// <summary>
        /// 終了したSEエントリをクリーンアップ
        /// </summary>
        void CleanupFinishedSE()
        {
            for (int i = _activeSEEntries.Count - 1; i >= 0; i--)
            {
                if (_activeSEEntries[i].IsFinished)
                {
                    ReturnSESource(_activeSEEntries[i].Source);
                    _activeSEEntries.RemoveAt(i);
                }
            }
        }

        AudioSource GetAvailableSESource()
        {
            // プールから未使用のソースを取得
            for (int i = 0; i < _seSourcePool.Count; i++)
            {
                if (!_seSourcePool[i].isPlaying)
                {
                    return _seSourcePool[i];
                }
            }

            // プールに空きがなければ新規作成 (上限MaxConcurrentVoices)
            if (_seSourcePool.Count < MaxConcurrentVoices)
            {
                var newSource = gameObject.AddComponent<AudioSource>();
                newSource.playOnAwake = false;
                _seSourcePool.Add(newSource);
                return newSource;
            }

            // フォールバック: メインseSourceを使用
            return seSource;
        }

        void ReturnSESource(AudioSource source)
        {
            // プールに含まれていればそのまま (再利用可能)
            // 含まれていなければ何もしない (seSource等のフォールバック)
        }

        /// <summary>
        /// SE IDから優先度を取得。マッピングにない場合はNormal。
        /// </summary>
        static SEPriority GetSEPriority(string seId)
        {
            if (SEPriorityMap.TryGetValue(seId, out var priority)) return priority;
            return SEPriority.Normal;
        }

        // ====================================================================
        // Settings
        // ====================================================================

        public void SetBGMVolume(float vol)
        {
            _bgmVolume = Mathf.Clamp01(vol);
            // アクティブなBGMソースに即座に反映
            if (ActiveBGMSource.isPlaying) ActiveBGMSource.volume = _bgmVolume;
            // レイヤーも反映
            if (_tensionLayer.isPlaying && _tensionLayer.volume > 0f) _tensionLayer.volume = _bgmVolume;
            if (_stringsLayer.isPlaying && _stringsLayer.volume > 0f) _stringsLayer.volume = _bgmVolume;
        }

        public void SetSEVolume(float vol)
        {
            _seVolume = Mathf.Clamp01(vol);
        }

        public void SetMuted(bool muted)
        {
            _muted = muted;
            if (muted)
            {
                bgmSource.Pause();
                _bgmSourceB.Pause();
                _tensionLayer.Pause();
                _stringsLayer.Pause();
            }
            else
            {
                bgmSource.UnPause();
                _bgmSourceB.UnPause();
                _tensionLayer.UnPause();
                _stringsLayer.UnPause();
            }
        }

        AudioClip LoadClip(string path)
        {
            if (_bgmCache.TryGetValue(path, out var cached)) return cached;
            var clip = Resources.Load<AudioClip>(path);
            if (clip != null) _bgmCache[path] = clip;
            return clip;
        }
    }
}
