using System;

namespace Project.Data.World
{
    /// <summary>월드맵 리전의 고유 식별자 (예: "AbyssalPlains", "TrenchZone")</summary>
    [Serializable]
    public struct RegionId : IEquatable<RegionId>
    {
        /// <summary>리전 식별 문자열</summary>
        public string Id;

        /// <summary>리전 ID 생성</summary>
        public RegionId(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        /// <summary>문자열로부터 RegionId 생성</summary>
        public static RegionId FromString(string id) => new RegionId(id);

        /// <summary>RegionId를 문자열로 변환</summary>
        public override string ToString() => Id;

        /// <summary>동등성 비교</summary>
        public bool Equals(RegionId other) => Id == other.Id;

        /// <summary>동등성 비교</summary>
        public override bool Equals(object obj) => obj is RegionId other && Equals(other);

        /// <summary>해시 코드 생성</summary>
        public override int GetHashCode() => Id?.GetHashCode() ?? 0;

        /// <summary>동등 연산자</summary>
        public static bool operator ==(RegionId left, RegionId right) => left.Equals(right);

        /// <summary>비동등 연산자</summary>
        public static bool operator !=(RegionId left, RegionId right) => !left.Equals(right);

        /// <summary>암시적 변환: string -> RegionId</summary>
        public static implicit operator RegionId(string id) => new RegionId(id);

        /// <summary>암시적 변환: RegionId -> string</summary>
        public static implicit operator string(RegionId regionId) => regionId.Id;
    }
}
