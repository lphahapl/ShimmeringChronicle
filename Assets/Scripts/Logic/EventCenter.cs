using System;
using System.Collections.Generic;
using UnityEngine;

public static class EventCenter
{
    private static Dictionary<object, Delegate> eventTable = new Dictionary<object, Delegate>();

   
    public static void Subscribe(this object _, EventKey eventKey, Action action)
    {
        Add(eventKey, action);
    }
    public static void Subscribe<T>(this object _, EventKey<T> eventKey, Action<T> action)
    {
        Add(eventKey, action);
    }
    public static void Subscribe<T1, T2>(this object _, EventKey<T1, T2> eventKey, Action<T1, T2> action)
    {
        Add(eventKey, action);
    }
    public static void Subscribe<T1, T2, T3>(this object _, EventKey<T1, T2, T3> eventKey, Action<T1, T2, T3> action)
    {
        Add(eventKey, action);
    }


    public static void UnSubscribe(this object _, EventKey eventKey, Action action)
    {
        Remove(eventKey, action);
    }
    public static void UnSubscribe<T>(this object _, EventKey<T> eventKey, Action<T> action)  
    {
        Remove(eventKey, action);
    }
    public static void UnSubscribe<T1, T2>(this object _, EventKey<T1, T2> eventKey, Action<T1, T2> action) 
    {
        Remove(eventKey, action);
    }
    public static void UnSubscribe<T1, T2, T3>(this object _, EventKey<T1, T2, T3> eventKey, Action<T1, T2, T3> action)
    {
        Remove(eventKey, action);
    }


    public static void Publish(this object _, EventKey eventKey)
    {
        if (eventTable.TryGetValue(eventKey, out Delegate nowDelegate))
        {
            if (nowDelegate is Action callbacks)
            {
                callbacks.Invoke();
            }
        }
    }
    public static void Publish<T>(this object _, EventKey<T> eventKey, T param1)
    {
        if (eventTable.TryGetValue(eventKey, out Delegate nowDelegate))
        {
            if (nowDelegate is Action<T> callbacks)
            {
                callbacks.Invoke(param1);
            }
        }
    }
    public static void Publish<T1, T2>(this object _, EventKey<T1, T2> eventKey, T1 param1, T2 param2) // 修正：键类型
    {
        if (eventTable.TryGetValue(eventKey, out Delegate nowDelegate))
        {
            if (nowDelegate is Action<T1, T2> callbacks)
            {
                callbacks.Invoke(param1, param2);
            }
        }
    }
    public static void Publish<T1, T2, T3>(this object _, EventKey<T1, T2, T3> eventKey, T1 param1, T2 param2, T3 param3) // 修正：键类型
    {
        if (eventTable.TryGetValue(eventKey, out Delegate nowDelegate))
        {
            if (nowDelegate is Action<T1, T2, T3> callbacks)
            {
                callbacks.Invoke(param1, param2, param3);
            }
        }
    }
    public static void Subscribe<T1, T2, T3, T4>(this object _, EventKey<T1, T2, T3, T4> eventKey, Action<T1, T2, T3, T4> action)
    {
        Add(eventKey, action);
    }

    public static void UnSubscribe<T1, T2, T3, T4>(this object _, EventKey<T1, T2, T3, T4> eventKey, Action<T1, T2, T3, T4> action)
    {
        EventCenter.Remove(eventKey, action);
    }

    public static void Publish<T1, T2, T3, T4>(this object _, EventKey<T1, T2, T3, T4> eventKey, T1 param1, T2 param2, T3 param3, T4 param4)
    {
        if (EventCenter.eventTable.TryGetValue(eventKey, out Delegate nowDelegate))
        {
            if (nowDelegate is Action<T1, T2, T3, T4> callbacks)
            {
                callbacks.Invoke(param1, param2, param3, param4);
            }
        }
    }
    public static void Subscribe<T1, T2, T3, T4, T5>(this object _, EventKey<T1, T2, T3, T4, T5> eventKey, Action<T1, T2, T3, T4, T5> action)
    {
        Add(eventKey, action);
    }

    public static void UnSubscribe<T1, T2, T3, T4, T5>(this object _, EventKey<T1, T2, T3, T4, T5> eventKey, Action<T1, T2, T3, T4, T5> action)
    {
        EventCenter.Remove(eventKey, action);
    }

    public static void Publish<T1, T2, T3, T4, T5>(this object _, EventKey<T1, T2, T3, T4, T5> eventKey, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
    {
        if (EventCenter.eventTable.TryGetValue(eventKey, out Delegate nowDelegate))
        {
            if (nowDelegate is Action<T1, T2, T3, T4, T5> callbacks)
            {
                callbacks.Invoke(param1, param2, param3, param4, param5);
            }
        }
    }

    private static void Add(object eventKey, Delegate handler)
    {
        if (eventKey == null || handler == null) return;
        if (eventTable.TryGetValue(eventKey, out Delegate existingDelegate))
        {
            if (existingDelegate.GetType() != handler.GetType())
            {
                throw new Exception(
                    $"类型是{existingDelegate.GetType().Name}，不能存储{handler.GetType().Name}类型"
                );
            }
            eventTable[eventKey] = Delegate.Combine(existingDelegate, handler);
        }
        else
        {
            eventTable.Add(eventKey, handler);
        }
    }

    private static void Remove(object eventKey, Delegate handler)
    {
        if (eventKey == null || handler == null) return;
        if (eventTable.TryGetValue(eventKey, out Delegate existingDelegate))
        {
            if (existingDelegate != null)
            {
                if (existingDelegate.GetType().Name != handler.GetType().Name)
                {
                    throw new Exception(
                        $"尝试移除不匹配的委托类型{handler.GetType().Name}"
                    );
                }
                else
                {
                    Delegate currentDelegate = Delegate.Remove(existingDelegate, handler);
                    if (currentDelegate == null)
                    {
                        eventTable.Remove(eventKey);
                    }
                    else
                    {
                        eventTable[eventKey] = currentDelegate;
                    }
                }
            }
        }
    }
   
}

public sealed class EventKey { }
public sealed class EventKey<T> { }
public sealed class EventKey<T1, T2> { }
public sealed class EventKey<T1, T2, T3> { }
public sealed class EventKey<T1, T2, T3,T4> { }
public sealed class EventKey<T1, T2, T3,T4,T5> { }