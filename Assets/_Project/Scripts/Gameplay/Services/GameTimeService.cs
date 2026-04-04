namespace Project.Gameplay.Services
{
    /// <summary>인게임 누적 시간과 일자 계산을 담당하는 서비스이다.</summary>
    public class GameTimeService
    {
        private float currentInGameHours; // 현재 누적 인게임 시간

        /// <summary>현재 누적 인게임 시간을 시간 단위로 반환한다.</summary>
        public float CurrentInGameHours => currentInGameHours;

        /// <summary>현재 인게임 Day를 반환한다. (1일부터 시작)</summary>
        public int CurrentDay => (int)(currentInGameHours / 24f) + 1;

        /// <summary>현재 Day 내부 시각(0~24)을 반환한다.</summary>
        public float CurrentHourOfDay => currentInGameHours % 24f;

        /// <summary>게임 시간 서비스를 생성한다.</summary>
        public GameTimeService(float startInGameHours = 0f)
        {
            currentInGameHours = startInGameHours < 0f ? 0f : startInGameHours;
        }

        /// <summary>현재 인게임 시간을 지정 값으로 설정한다.</summary>
        public void SetCurrentInGameHours(float newInGameHours)
        {
            currentInGameHours = newInGameHours < 0f ? 0f : newInGameHours;
        }

        /// <summary>인게임 시간을 지정 시간만큼 전진시킨다.</summary>
        public void AdvanceInGameHours(float hours)
        {
            if (hours <= 0f)
                return;

            currentInGameHours += hours;
        }

        /// <summary>현재 시각을 HH:MM 형식 문자열로 반환한다.</summary>
        public string GetFormattedTimeOfDay()
        {
            int hour = (int)CurrentHourOfDay;
            int minute = (int)((CurrentHourOfDay - hour) * 60f);
            return $"{hour:00}:{minute:00}";
        }
    }
}
