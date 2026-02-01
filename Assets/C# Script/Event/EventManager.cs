using System;
using UnityEngine;

public static  class EventManager {


    public static event Action<Transform> OnCheckPoint;

    public static void NotifyCheckPoint(Transform transform)
    {
        OnCheckPoint?.Invoke(transform);
    }
   
}


