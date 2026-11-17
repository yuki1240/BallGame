using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BallGame
{
    /// <summary>
    /// 画像の押す・ドラッグ・離すを受け取り、画像の中の位置（0〜1、左下が (0,0)）を知らせます。
    /// UI のボタンの上を押したときは届きません。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PointerReceiver : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>押されたとき。引数は画像の中の 0〜1（左下が (0,0)）。</summary>
        public event Action<Vector2> Pressed;

        /// <summary>押したまま動かしたとき。</summary>
        public event Action<Vector2> Dragged;

        /// <summary>離されたとき。</summary>
        public event Action<Vector2> Released;

        /// <summary>
        /// 押されたときに呼ばれます。
        /// </summary>
        /// <param name="eventData">押した情報</param>
        public void OnPointerDown(PointerEventData eventData)
        {
            Notify(Pressed, eventData);
        }

        /// <summary>
        /// 押したまま動かしたときに呼ばれます。
        /// </summary>
        /// <param name="eventData">動かした情報</param>
        public void OnDrag(PointerEventData eventData)
        {
            Notify(Dragged, eventData);
        }

        /// <summary>
        /// 離されたときに呼ばれます。
        /// </summary>
        /// <param name="eventData">離した情報</param>
        public void OnPointerUp(PointerEventData eventData)
        {
            Notify(Released, eventData);
        }

        private void Notify(Action<Vector2> handler, PointerEventData eventData)
        {
            if (handler == null)
            {
                return;
            }

            RectTransform rect = (RectTransform)transform;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out local))
            {
                handler(Rect.PointToNormalized(rect.rect, local));
            }
        }
    }
}
