using System;
using System.Collections.Generic;

namespace Project.Core.Events
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> subscribers = new(); // 이벤트별 구독자 목록

        /// <summary>이벤트 구독 (수신 대기)</summary>
        public static void Subscribe<T>(Action<T> callback) where T : struct, IEvent
        {
            Type eventType = typeof(T);

            if (subscribers.TryGetValue(eventType, out Delegate existingDelegate))
            {
                subscribers[eventType] = Delegate.Combine(existingDelegate, callback);
                return;
            }

            subscribers.Add(eventType, callback);
        }

        /// <summary>이벤트 구독 해제</summary>
        public static void Unsubscribe<T>(Action<T> callback) where T : struct, IEvent
        {
            Type eventType = typeof(T);

            if (!subscribers.TryGetValue(eventType, out Delegate existingDelegate))
                return;

            Delegate newDelegate = Delegate.Remove(existingDelegate, callback);

            if (newDelegate == null)
            {
                subscribers.Remove(eventType);
                return;
            }

            subscribers[eventType] = newDelegate;
        }

        /// <summary>이벤트 발행</summary>
        public static void Publish<T>(T publishedEvent) where T : struct, IEvent
        {
            Type eventType = typeof(T);

            if (!subscribers.TryGetValue(eventType, out Delegate existingDelegate))
                return;

            if (existingDelegate is Action<T> callback)
                callback.Invoke(publishedEvent);
        }

        /// <summary>모든 이벤트 구독 초기화</summary>
        public static void Clear()
        {
            subscribers.Clear();
        }
    }
}
