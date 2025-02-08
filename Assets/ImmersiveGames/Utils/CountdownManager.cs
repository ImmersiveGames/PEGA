using System;
using System.Collections.Generic;

namespace ImmersiveGames.Utils
{
    public class CountdownManager
    {
        private class Countdown
        {
            public float InitialDuration;
            public float TimeRemaining;
            public bool IsRunning;
            public bool IsPaused; 
            public readonly Func<bool> Condition;
            private readonly Action _onComplete;
            private readonly Action<float> _onTick;
            public int ActivationCount;
            private readonly int _maxActivations;

            public Countdown(float duration, Func<bool> condition, Action onComplete, Action<float> onTick, int maxActivations = -1)
            {
                InitialDuration = duration;
                TimeRemaining = duration;
                Condition = condition;
                _onComplete = onComplete;
                _onTick = onTick;
                _maxActivations = maxActivations;
                ActivationCount = 0;
                IsRunning = condition == null;
                IsPaused = false;
            }

            public void Update(float deltaTime)
            {
                if (IsPaused || (_maxActivations > 0 && ActivationCount >= _maxActivations)) return;

                if (Condition != null)
                {
                    if (Condition.Invoke()) 
                    {
                        if (!IsRunning) 
                        {
                            TimeRemaining = InitialDuration; 
                            IsRunning = true;
                        }
                    }
                    else
                    {
                        IsRunning = false;
                    }
                }

                if (!IsRunning) return;

                TimeRemaining -= deltaTime;
                _onTick?.Invoke(TimeRemaining);

                if (!(TimeRemaining <= 0)) return;
                TimeRemaining = 0;
                IsRunning = false;
                ActivationCount++;

                _onComplete?.Invoke();

                // 🔹 Se for automático e tiver um limite de ativações, reinicia apenas se ainda puder ativar
                if (Condition == null || (_maxActivations >= 0 && ActivationCount >= _maxActivations)) return;
                TimeRemaining = InitialDuration;
                IsRunning = Condition.Invoke();
            }
        }

        private readonly Dictionary<string, Countdown> _countdowns = new();

        public void RegisterCountdown(string id, float duration, Action onComplete = null, Action<float> onTick = null)
        {
            _countdowns[id] = new Countdown(duration, null, onComplete, onTick);
        }

        public void RegisterAutoCountdown(string id, float duration, Func<bool> condition, Action onComplete = null, Action<float> onTick = null)
        {
            _countdowns[id] = new Countdown(duration, condition, onComplete, onTick);
        }

        public void RegisterCyclicCountdown(string id, float duration, Func<bool> condition, int maxActivations, Action onComplete = null, Action<float> onTick = null)
        {
            _countdowns[id] = new Countdown(duration, condition, onComplete, onTick, maxActivations);
        }

        public void Update(float deltaTime)
        {
            foreach (var kvp in _countdowns)
            {
                kvp.Value.Update(deltaTime);
            }
        }

        public void PauseCountdown(string id)
        {
            if (_countdowns.TryGetValue(id, out var countdown))
            {
                countdown.IsPaused = true;
            }
        }

        public void ResumeCountdown(string id)
        {
            if (_countdowns.TryGetValue(id, out var countdown))
            {
                countdown.IsPaused = false;
            }
        }

        public void ResetCountdown(string id, float newDuration)
        {
            if (!_countdowns.TryGetValue(id, out var countdown)) return;
            countdown.InitialDuration = newDuration;
            countdown.TimeRemaining = newDuration;
            countdown.IsRunning = countdown.Condition == null;
            countdown.IsPaused = false;
            countdown.ActivationCount = 0;
        }

        public int GetActivationCount(string id)
        {
            return _countdowns.TryGetValue(id, out var countdown) ? countdown.ActivationCount : -1;
        }

        public float GetTimeRemaining(string id)
        {
            return _countdowns.TryGetValue(id, out var countdown) ? countdown.TimeRemaining : -1f;
        }

        public bool IsCountdownRunning(string id)
        {
            return _countdowns.TryGetValue(id, out var countdown) && countdown.IsRunning && !countdown.IsPaused;
        }
    }
}
