using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace BallGame
{
    /// <summary>
    /// 新しい Input System 用の EventSystem を1つだけ作り、DontDestroyOnLoad で残します。
    /// </summary>
    public static class EventSystemKeeper
    {
        /// <summary>
        /// EventSystem が無ければ作り、シーンをまたいで残るようにします。
        /// </summary>
        /// <returns>使っている EventSystem</returns>
        public static EventSystem Ensure()
        {
            EventSystem existing = Object.FindAnyObjectByType<EventSystem>();
            if (existing != null)
            {
                KeepAlive(existing.gameObject);
                return existing;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
            KeepAlive(eventSystemObject);
            return eventSystem;
        }

        private static void KeepAlive(GameObject target)
        {
            if (target.transform.parent == null)
            {
                Object.DontDestroyOnLoad(target);
            }
        }
    }
}
