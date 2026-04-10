using Project.Core.Events;
using UnityEngine;

namespace Project.Gameplay.Services
{
    /// <summary>하루 길이 표시 방식과 현재 날짜/시각, 누적 게임 시간을 관리하는 서비스이다.</summary>
    public class GameTimeService
    {
        private const float TwelveHourDayLength = 12f;      // 초반 가짜 하루 길이
        private const float TwentyFourHourDayLength = 24f;  // 진실 이후 실제 하루 길이

        private int currentDay;                     // 현재 Day
        private float currentHourOfDay;             // 현재 하루 내부 시각
        private float currentAbsoluteHours;         // 전체 누적 인게임 시간
        private float activeRealSecondsPerGameHour; // 인게임 1시간당 실제 액티브 시간(초)

        private GameDayLengthMode currentDayLengthMode; // 현재 하루 길이 모드
        private bool hasPendingDayLengthSwitch;         // 다음 경계에서 모드 전환 예약 여부
        private GameDayLengthMode pendingDayLengthMode; // 예약된 목표 모드

        /// <summary>현재 Day를 반환한다.</summary>
        public int CurrentDay => currentDay;

        /// <summary>현재 하루 내부 시각을 반환한다.</summary>
        public float CurrentHourOfDay => currentHourOfDay;

        /// <summary>현재 하루 길이를 반환한다.</summary>
        public float CurrentDayLengthHours => GetDayLengthHours(currentDayLengthMode);

        /// <summary>현재 하루 길이 모드를 반환한다.</summary>
        public GameDayLengthMode CurrentDayLengthMode => currentDayLengthMode;

        /// <summary>다음 경계에서 하루 길이 전환이 예약되어 있는지 반환한다.</summary>
        public bool HasPendingDayLengthSwitch => hasPendingDayLengthSwitch;

        /// <summary>현재 낮 구간 여부를 반환한다.</summary>
        public bool IsDaylight => currentHourOfDay < CurrentDayLengthHours * 0.5f;

        /// <summary>인게임 1시간당 실제 액티브 시간을 반환한다.</summary>
        public float ActiveRealSecondsPerGameHour => activeRealSecondsPerGameHour;

        /// <summary>누적 절대 인게임 시간을 반환한다. 기존 UI 호환용이다.</summary>
        public float CurrentAbsoluteHours => currentAbsoluteHours;

        /// <summary>게임 시간 서비스를 생성한다.</summary>
        public GameTimeService(
            int startDay = 1,
            float startHourOfDay = 9f,
            float activeRealSecondsPerGameHour = 180f,
            GameDayLengthMode initialDayLengthMode = GameDayLengthMode.TwelveHour)
        {
            currentDayLengthMode = initialDayLengthMode;
            currentDay = Mathf.Max(1, startDay);

            SetActiveRealSecondsPerGameHour(activeRealSecondsPerGameHour);
            SetDayAndHour(currentDay, startHourOfDay);
        }

        /// <summary>현재 Day와 시각을 직접 설정한다.</summary>
        public void SetDayAndHour(int day, float hourOfDay)
        {
            currentDay = Mathf.Max(1, day);
            currentHourOfDay = Mathf.Repeat(hourOfDay, CurrentDayLengthHours);

            currentAbsoluteHours = CalculateAbsoluteHours(currentDay, currentHourOfDay, currentDayLengthMode);
            PublishCurrentState();
        }

        /// <summary>인게임 1시간당 필요한 실제 액티브 시간을 설정한다.</summary>
        public void SetActiveRealSecondsPerGameHour(float seconds)
        {
            activeRealSecondsPerGameHour = seconds <= 0.01f ? 0.01f : seconds;
        }

        /// <summary>실제 액티브 시간을 인게임 시간으로 환산해 전진시킨다.</summary>
        public void AdvanceByActiveRealSeconds(float activeSeconds)
        {
            if (activeSeconds <= 0f)
                return;

            float deltaHours = activeSeconds / activeRealSecondsPerGameHour;
            AdvanceInGameHours(deltaHours);
        }

        /// <summary>인게임 시간을 지정 시간만큼 전진시킨다.</summary>
        public void AdvanceInGameHours(float hours)
        {
            if (hours <= 0f)
                return;

            float remainingHours = hours;
            currentAbsoluteHours += hours;

            while (remainingHours > 0f)
            {
                float currentDayLength = CurrentDayLengthHours;
                float hoursToBoundary = currentDayLength - currentHourOfDay;

                if (hoursToBoundary <= 0.0001f)
                {
                    MoveToNextDayBoundary();
                    continue;
                }

                if (remainingHours < hoursToBoundary)
                {
                    currentHourOfDay += remainingHours;
                    remainingHours = 0f;
                    continue;
                }

                currentHourOfDay = currentDayLength;
                remainingHours -= hoursToBoundary;

                MoveToNextDayBoundary();
            }

            PublishCurrentState();
        }

        /// <summary>진실 발견 후 24시간제로 전환을 요청한다.</summary>
        public void RequestTwentyFourHourDayReveal(bool applyAtNextDayBoundary = true)
        {
            RequestDayLengthModeChange(GameDayLengthMode.TwentyFourHour, applyAtNextDayBoundary);
        }

        /// <summary>필요 시 다시 12시간제로 전환을 요청한다.</summary>
        public void RequestTwelveHourDay(bool applyAtNextDayBoundary = true)
        {
            RequestDayLengthModeChange(GameDayLengthMode.TwelveHour, applyAtNextDayBoundary);
        }

        /// <summary>현재 시각을 HH:MM 문자열로 반환한다.</summary>
        public string GetFormattedTimeOfDay()
        {
            int hour = Mathf.FloorToInt(currentHourOfDay);
            int minute = Mathf.FloorToInt((currentHourOfDay - hour) * 60f);

            if (minute >= 60)
            {
                minute = 0;
                hour += 1;
            }

            if (hour >= CurrentDayLengthHours)
                hour = 0;

            return $"{hour:00}:{minute:00}";
        }

        /// <summary>현재 일자와 시각을 Day / HH:MM 형식 문자열로 반환한다.</summary>
        public string GetFormattedDayTime()
        {
            return $"Day {currentDay}, {GetFormattedTimeOfDay()}";
        }

        /// <summary>현재 하루 주기 진행률을 0~1로 반환한다.</summary>
        public float GetDayProgress01()
        {
            float dayLength = CurrentDayLengthHours;
            if (dayLength <= 0.001f)
                return 0f;

            return Mathf.Clamp01(currentHourOfDay / dayLength);
        }

        /// <summary>현재 상태를 이벤트로 재발행한다.</summary>
        public void PublishCurrentState()
        {
            EventBus.Publish(new GameTimeChangedEvent(
                currentDay,
                currentHourOfDay,
                CurrentDayLengthHours,
                IsDaylight,
                hasPendingDayLengthSwitch));
        }

        /// <summary>현재 모드의 하루 길이를 반환한다.</summary>
        private float GetDayLengthHours(GameDayLengthMode mode)
        {
            return mode == GameDayLengthMode.TwentyFourHour
                ? TwentyFourHourDayLength
                : TwelveHourDayLength;
        }

        /// <summary>다음 날짜 경계로 넘어가며 예약된 모드 전환을 처리한다.</summary>
        private void MoveToNextDayBoundary()
        {
            currentDay += 1;
            currentHourOfDay = 0f;

            if (!hasPendingDayLengthSwitch)
                return;

            hasPendingDayLengthSwitch = false;

            GameDayLengthMode previousMode = currentDayLengthMode;
            currentDayLengthMode = pendingDayLengthMode;

            EventBus.Publish(new GameDayLengthModeChangedEvent(previousMode, currentDayLengthMode));
        }

        /// <summary>하루 길이 모드 변경을 요청한다.</summary>
        private void RequestDayLengthModeChange(GameDayLengthMode targetMode, bool applyAtNextDayBoundary)
        {
            if (currentDayLengthMode == targetMode && !hasPendingDayLengthSwitch)
                return;

            if (applyAtNextDayBoundary)
            {
                hasPendingDayLengthSwitch = true;
                pendingDayLengthMode = targetMode;
                PublishCurrentState();
                return;
            }

            GameDayLengthMode previousMode = currentDayLengthMode;
            currentDayLengthMode = targetMode;

            currentHourOfDay = Mathf.Repeat(currentHourOfDay, CurrentDayLengthHours);
            hasPendingDayLengthSwitch = false;

            EventBus.Publish(new GameDayLengthModeChangedEvent(previousMode, currentDayLengthMode));
            PublishCurrentState();
        }

        /// <summary>현재 날짜/시각/모드를 기준으로 절대 누적 시간을 계산한다.</summary>
        private float CalculateAbsoluteHours(int day, float hourOfDay, GameDayLengthMode mode)
        {
            float dayLength = GetDayLengthHours(mode);
            return ((day - 1) * dayLength) + hourOfDay;
        }
    }
}
