﻿using System.Collections;
using System.Linq;
using IPA.Utilities;
using TrickSaber.Configuration;
using UnityEngine;
using Zenject;

namespace TrickSaber
{
    internal class GlobalTrickManager
    {
        public AudioTimeSyncController AudioTimeSyncController;

        public AudioSource AudioSource
        {
            get
            {
                if(_audioSource==null)
                    _audioSource = AudioTimeSyncController.GetField<AudioSource, AudioTimeSyncController>("_audioSource");
                return _audioSource;
            }
        }

        public BeatmapObjectManager BeatmapObjectManager;

        public SaberTrickManager LeftSaberSaberTrickManager;
        public SaberTrickManager RightSaberSaberTrickManager;

        public bool Enabled
        {
            get
            {
                if (!LeftSaberSaberTrickManager || !RightSaberSaberTrickManager) return false;
                return LeftSaberSaberTrickManager.Enabled || RightSaberSaberTrickManager.Enabled;
            }
            set
            {
                if (!LeftSaberSaberTrickManager || !RightSaberSaberTrickManager) return;
                LeftSaberSaberTrickManager.Enabled = value;
                RightSaberSaberTrickManager.Enabled = value;
            }
        }

        public bool SaberClashCheckerEnabled = true;

        private readonly PluginConfig _config;
        private readonly IDifficultyBeatmap _iDifficultyBeatmap;

        private readonly float _slowmoStepAmount;

        private Coroutine _applySlowmoCoroutine;
        private Coroutine _endSlowmoCoroutine;

        private bool _slowmoApplied;
        private float _endSlowmoTarget;
        private float _originalTimeScale;
        private AudioSource _audioSource;
        private float _timeSinceLastNote;

        private GlobalTrickManager(PluginConfig config, AudioTimeSyncController audioTimeSyncController, GameplayCoreSceneSetupData gameplayCoreSceneSetup)
        {
            _config = config;
            AudioTimeSyncController = audioTimeSyncController;

            _iDifficultyBeatmap = gameplayCoreSceneSetup.difficultyBeatmap;

            _slowmoStepAmount = _config.SlowmoStepAmount;
        }

        private void Awake()
        {
            //var scoreController = FindObjectsOfType<ScoreController>().FirstOrDefault();
            //BeatmapObjectManager = scoreController.GetField<BeatmapObjectManager, ScoreController>("_beatmapObjectManager");

            //BeatmapObjectManager.noteWasSpawnedEvent += OnNoteWasSpawned;
            //if (PluginConfig.Instance.DisableIfNotesOnScreen) StartCoroutine(NoteSpawnTimer());
        }

        public void OnTrickStarted(TrickAction trickAction)
        {
            SaberClashCheckerEnabled = false;
            if (trickAction == TrickAction.Throw && _config.SlowmoDuringThrow && !_slowmoApplied)
            {
                var timeScale = AudioTimeSyncController.timeScale;
                if (_endSlowmoCoroutine != null)
                {
                    SharedCoroutineStarter.instance.StopCoroutine(_endSlowmoCoroutine);
                    timeScale = _endSlowmoTarget;
                }
                _applySlowmoCoroutine = SharedCoroutineStarter.instance.StartCoroutine(ApplySlowmoSmooth(_config.SlowmoAmount, timeScale));
                _slowmoApplied = true;
            }
        }

        public void OnTrickEndRequested(TrickAction trickAction)
        {
            if (trickAction == TrickAction.Throw)
                if (_config.SlowmoDuringThrow &&
                    !IsTrickInState(trickAction, TrickState.Started) && _slowmoApplied)
                {
                    if(_applySlowmoCoroutine!=null)SharedCoroutineStarter.instance.StopCoroutine(_applySlowmoCoroutine);
                    _endSlowmoCoroutine = SharedCoroutineStarter.instance.StartCoroutine(EndSlowmoSmooth());
                    _slowmoApplied = false;
                }
        }

        public void OnTrickEnded(TrickAction trickAction)
        {
            if(!IsDoingTrick()) SaberClashCheckerEnabled = true;
        }

        private IEnumerator ApplySlowmoSmooth(float amount, float originalTimescale)
        {
            float timeScale = AudioTimeSyncController.timeScale;
            _originalTimeScale = originalTimescale;
            float targetTimeScale = _originalTimeScale - amount;
            if (targetTimeScale < 0.1f) targetTimeScale = 0.1f;
            while (timeScale > targetTimeScale)
            {
                timeScale -= _slowmoStepAmount;
                SetTimescale(timeScale);
                yield return new WaitForFixedUpdate();
            }

            SetTimescale(targetTimeScale);
        }

        private IEnumerator EndSlowmoSmooth()
        {
            float timeScale = AudioTimeSyncController.timeScale;
            float targetTimeScale = _originalTimeScale;
            _endSlowmoTarget = targetTimeScale;
            while (timeScale < targetTimeScale)
            {
                timeScale += _slowmoStepAmount;
                SetTimescale(timeScale);
                yield return new WaitForFixedUpdate();
            }

            SetTimescale(targetTimeScale);
        }

        void SetTimescale(float timescale)
        {
            AudioTimeSyncController.SetField("_timeScale", timescale);
            AudioSource.pitch = timescale;
        }

        IEnumerator NoteSpawnTimer()
        {
            while (true)
            {
                _timeSinceLastNote += Time.deltaTime;
                yield return null;
            }
        }

        public bool CanDoTrick()
        {
            if (!_config.DisableIfNotesOnScreen) return true;
            if (_timeSinceLastNote > 20/_iDifficultyBeatmap.noteJumpMovementSpeed) return true;
            return false;
        }

        void OnNoteWasSpawned(NoteController noteController)
        {
            _timeSinceLastNote = 0;
        }

        public bool IsTrickInState(TrickAction trickAction, TrickState state)
        {
            return LeftSaberSaberTrickManager.IsTrickInState(trickAction, state) ||
                   RightSaberSaberTrickManager.IsTrickInState(trickAction, state);
        }

        public bool IsDoingTrick()
        {
            return LeftSaberSaberTrickManager.IsDoingTrick() || RightSaberSaberTrickManager.IsDoingTrick();
        }
    }
}