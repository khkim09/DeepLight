using System;

namespace Project.Data.World
{
    /// <summary>월드맵 존의 고유 식별자 (예: "E5", "F6")</summary>
    [Serializable]
    public struct ZoneId : IEquatable<ZoneId>
    {
        /// <summary>존 그리드 열 문자 (A-Z)</summary>
        public char Column;

        /// <summary>존 그리드 행 번호 (1-10)</summary>
        public int Row;

        /// <summary>존 ID 생성</summary>
        public ZoneId(char column, int row)
        {
            Column = char.ToUpper(column);
            Row = row;
        }

        /// <summary>문자열로부터 ZoneId 파싱 (예: "E5")</summary>
        public static ZoneId Parse(string zoneCode)
        {
            if (string.IsNullOrEmpty(zoneCode) || zoneCode.Length < 2)
                throw new ArgumentException($"Invalid zone code: {zoneCode}");

            char column = char.ToUpper(zoneCode[0]);
            if (!char.IsLetter(column))
                throw new ArgumentException($"Invalid column in zone code: {zoneCode}");

            if (!int.TryParse(zoneCode.Substring(1), out int row))
                throw new ArgumentException($"Invalid row in zone code: {zoneCode}");

            return new ZoneId(column, row);
        }

        /// <summary>문자열로부터 ZoneId 파싱 시도 (예: "E5")</summary>
        public static bool TryParse(string zoneCode, out ZoneId zoneId)
        {
            zoneId = default;

            if (string.IsNullOrEmpty(zoneCode) || zoneCode.Length < 2)
                return false;

            char column = char.ToUpper(zoneCode[0]);
            if (!char.IsLetter(column))
                return false;

            if (!int.TryParse(zoneCode.Substring(1), out int row))
                return false;

            zoneId = new ZoneId(column, row);
            return true;
        }

        /// <summary>ZoneId를 문자열로 변환 (예: "E5")</summary>
        public override string ToString() => $"{Column}{Row}";

        /// <summary>동등성 비교</summary>
        public bool Equals(ZoneId other) => Column == other.Column && Row == other.Row;

        /// <summary>동등성 비교</summary>
        public override bool Equals(object obj) => obj is ZoneId other && Equals(other);

        /// <summary>해시 코드 생성</summary>
        public override int GetHashCode() => HashCode.Combine(Column, Row);

        /// <summary>동등 연산자</summary>
        public static bool operator ==(ZoneId left, ZoneId right) => left.Equals(right);

        /// <summary>비동등 연산자</summary>
        public static bool operator !=(ZoneId left, ZoneId right) => !left.Equals(right);
    }
}
