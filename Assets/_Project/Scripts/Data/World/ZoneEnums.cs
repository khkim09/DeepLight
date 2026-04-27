using System;

namespace Project.Data.World
{
    /// <summary>월드맵 존 접근 가능성 상태</summary>
    public enum ZoneAccessibility
    {
        /// <summary>완전히 잠겨 있음 - 접근 불가</summary>
        Locked,

        /// <summary>접근 가능하지만 위험 요소 존재</summary>
        AccessibleWithRisk,

        /// <summary>안전하게 접근 가능</summary>
        Accessible
    }

    /// <summary>존 잠금 원인 분류</summary>
    public enum ZoneLockReason
    {
        /// <summary>잠금 없음</summary>
        None,

        /// <summary>탐사 조건 부족 (Traversal)</summary>
        Traversal,

        /// <summary>지식 조건 부족 (Knowledge)</summary>
        Knowledge,

        /// <summary>서사 조건 부족 (Narrative)</summary>
        Narrative,

        /// <summary>위험도 조건 부족 (Risk)</summary>
        Risk,

        /// <summary>다중 조건 부족</summary>
        Multiple
    }

    /// <summary>해금 조건 타입</summary>
    public enum UnlockConditionType
    {
        /// <summary>조건 없음</summary>
        None,

        /// <summary>특정 업그레이드 보유</summary>
        HasUpgrade,

        /// <summary>특정 업그레이드 레벨 보유</summary>
        HasUpgradeLevel,

        /// <summary>특정 로그 보유</summary>
        HasLog,

        /// <summary>최소 로그 수량 보유</summary>
        HasLogCount,

        /// <summary>서사 플래그 달성</summary>
        HasNarrativeFlag,

        /// <summary>대화 상태 달성</summary>
        HasTalkState,

        /// <summary>특정 유물 보유</summary>
        HasRelic,

        /// <summary>최소 선체 티어 달성</summary>
        HasHullTier,

        /// <summary>최소 깊이 레벨 달성</summary>
        HasDepthLevel,

        /// <summary>최소 센서 정확도 달성</summary>
        HasSensorAccuracy,

        /// <summary>특정 존 발견 여부</summary>
        HasZoneDiscovered,

        /// <summary>특정 존 해금 여부</summary>
        HasZoneUnlocked
    }
}
