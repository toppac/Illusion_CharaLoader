using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CharaLoader
{
    public delegate void UIEventDel<in T>(T param) where T : BaseEventData;

    internal class ClickListener : MonoBehaviour, IPointerClickHandler
    {
        // Left Click Event.
        public event UIEventDel<PointerEventData> LClick;

        // Right Click Event.
        public event UIEventDel<PointerEventData> RClick;

        // Middle Click Event.
        public event UIEventDel<PointerEventData> MClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (LClick != null && eventData.pointerId == -1)
            {
                LClick(eventData);
            }
            else if (RClick != null && eventData.pointerId == -2)
            {
                RClick(eventData);
            }
            else if (MClick != null && eventData.pointerId == -3)
            {
                MClick(eventData);
            }
        }
    }

    internal class MiniDragListener : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IDragHandler
    {
        //public delegate void UIEventDel<in T>(T param) where T : BaseEventData;

        /*public event UIEventDel<PointerEventData> OnMouseEnter;
        public event UIEventDel<PointerEventData> OnMouseExit;
        public event UIEventDel<PointerEventData> OnMouseUp;*/

        public event UIEventDel<PointerEventData> OnClick;

        public event UIEventDel<PointerEventData> OnMouseDown;

        public event UIEventDel<PointerEventData> Drag;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Time.time - _time < 0.2f)
                OnClick?.Invoke(eventData);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _time = Time.time;
            rectPos = _rectTrans.position;
            mousePos = eventData.position;
            OnMouseDown?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            LoaderTools.OnDragClamp(
                rectPos + (eventData.position - mousePos), _rectTrans);
            Drag?.Invoke(eventData);
        }

        private void Awake()
        {
            _rectTrans = transform as RectTransform;
        }

        private float _time;
        private RectTransform _rectTrans;
        private Vector2 mousePos;
        private Vector2 rectPos;
    }

    internal class UIEventListener : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {

        // Custom Proxy Event.
        //public delegate void UIEventDel(BaseEventData eventData);
        //public delegate void UIEventDel<in T>(T param) where T : BaseEventData;

        // Left Click Event.
        public event UIEventDel<PointerEventData> LClick;

        // Right Click Event.
        public event UIEventDel<PointerEventData> RClick;

        // Middle Click Event.
        public event UIEventDel<PointerEventData> MClick;

        // Cursor Enter.
        public event UIEventDel<PointerEventData> OnMouseEnter;

        // Cursor Exit.
        public event UIEventDel<PointerEventData> OnMouseExit;

        public event UIEventDel<PointerEventData> OnMouseDown;

        public event UIEventDel<PointerEventData> OnMouseUp;

        public event UIEventDel<PointerEventData> BeginDrag;

        public event UIEventDel<PointerEventData> Drag;

        public event UIEventDel<PointerEventData> EndDrag;

        // public event UIEventDel PointerId;

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (LClick != null && eventData.pointerId == -1)
            {
                LClick(eventData);
            }
            else if (RClick != null && eventData.pointerId == -2)
            {
                RClick(eventData);
            }
            else if (MClick != null && eventData.pointerId == -3)
            {
                MClick(eventData);
            }
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            OnMouseEnter?.Invoke(eventData);
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            OnMouseExit?.Invoke(eventData);
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            OnMouseDown?.Invoke(eventData);
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            OnMouseUp?.Invoke(eventData);
        }

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            BeginDrag?.Invoke(eventData);
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            Drag?.Invoke(eventData);
        }

        public virtual void OnEndDrag(PointerEventData eventData)
        {
            EndDrag?.Invoke(eventData);
        }

        public static UIEventListener Get(GameObject gb)
        {
            UIEventListener listener = gb.GetComponent<UIEventListener>();
            if (listener == null)
            {
                listener = gb.AddComponent<UIEventListener>();
            }
            return listener;
        }
    }

    internal class DragEventListener : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IDragHandler
    {
        private RectTransform _rectTrans;
        //private Vector3 _offset;
        private Vector2 mousePos;
        private Vector2 rectPos;

        public event UIEventDel<PointerEventData> OnClick;

        public void Init(RectTransform rectTrans)
        {
            _rectTrans = rectTrans;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClick?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _rectTrans.position = rectPos + (eventData.position - mousePos);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            mousePos = eventData.position;
            rectPos = _rectTrans.position;
        }
    }

    internal class BlockCamCtrl : MonoBehaviour
    {
        private Action _onBlockCtrl;

        private void Update()
        {
            if (Input.GetMouseButton(0))
            {
                if (EventSystem.current.IsPointerOverGameObject())
                {
                    _onBlockCtrl.Invoke();
                }
            }
        }

        public static BlockCamCtrl Get(GameObject gb, Action onBlockCtrl)
        {
            BlockCamCtrl blockCtrl = gb.GetComponent<BlockCamCtrl>();
            if (blockCtrl == null)
            {
                blockCtrl = gb.AddComponent<BlockCamCtrl>();
                blockCtrl._onBlockCtrl = onBlockCtrl;
            }
            return blockCtrl;
        }
    }
}
