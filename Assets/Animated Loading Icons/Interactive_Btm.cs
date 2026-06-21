using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Interactive_Btm : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public UnityEvent _Event;

    public float _delay;

    public bool _isTrigger;
    public bool _isTrick;

    public string _Tag;

    private float _TimeCount;

    public GameObject _Loading;
    public Image _loadingFill;

    public void OnEnable()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        _isTrigger = false;
        _isTrick = false;
        _TimeCount = 0;
    }

    public void OnTriggerStay(Collider other)
    {
        if (other.gameObject.tag == _Tag || other.gameObject.tag == "SnapHand_R" || other.gameObject.tag == "SnapHand_L" || other.gameObject.tag == "Hand")
        {
            _isTrigger = true;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == _Tag || other.gameObject.tag == "SnapHand_R" || other.gameObject.tag == "SnapHand_L" || other.gameObject.tag == "Hand")
        {
            _isTrigger = true;
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == _Tag || other.gameObject.tag == "SnapHand_R" || other.gameObject.tag == "SnapHand_L" || other.gameObject.tag == "Hand")
        {
            StopAllCoroutines();
            StartCoroutine(_EnableTrick());
            Debug.Log("Trigger Exit");
            _isTrigger = false;
        }
    }

    // =========================
    // UI Canvas Support
    // =========================
    public void OnPointerDown(PointerEventData eventData)
    {
        _isTrigger = true;
        Debug.Log("Pointer Down");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopAllCoroutines();
        StartCoroutine(_EnableTrick());
        _isTrigger = false;
        Debug.Log("Pointer Up");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopAllCoroutines();
        StartCoroutine(_EnableTrick());
        _isTrigger = false;
        Debug.Log("Pointer Exit");
    }

    // =========================
    // PC Mouse Support for 3D Object
    // =========================
    private void OnMouseDown()
    {
        _isTrigger = true;
        Debug.Log("Mouse Down");
    }

    private void OnMouseUp()
    {
        StopAllCoroutines();
        StartCoroutine(_EnableTrick());
        _isTrigger = false;
        Debug.Log("Mouse Up");
    }

    private void OnMouseExit()
    {
        StopAllCoroutines();
        StartCoroutine(_EnableTrick());
        _isTrigger = false;
        Debug.Log("Mouse Exit");
    }

    public void FixedUpdate()
    {
        if (!_isTrick)
        {
            if (_isTrigger)
            {
                _TimeCount += Time.deltaTime;

                if (_Loading)
                {
                    _Loading.SetActive(true);
                }

                if (_loadingFill)
                {
                    _loadingFill.gameObject.SetActive(true);
                    float progress = (_delay > 0f) ? _TimeCount / _delay : 1f;
                    _loadingFill.fillAmount = Mathf.Clamp01(progress);
                }

                if (_TimeCount >= _delay)
                {
                    _InterAct();
                }
            }
            else
            {
                if (_Loading)
                {
                    _Loading.SetActive(false);
                }

                if (_loadingFill)
                {
                    _loadingFill.fillAmount = 0;
                    _loadingFill.gameObject.SetActive(false);
                }

                _ResetBTM();
            }
        }
        else
        {
            if (_Loading)
            {
                _Loading.SetActive(false);
            }

            // _ResetBTM();
        }
    }

    IEnumerator _EnableTrick()
    {
        Debug.Log("_EnableTrick A");

        yield return new WaitForSeconds(1);

        if (!_isTrigger)
        {
            Debug.Log("_EnableTrick A-1");

            if (_Loading)
            {
                _Loading.SetActive(false);
            }

            _isTrick = false;

            Debug.Log("_EnableTrick B");
        }
    }

    public void _InterAct()
    {
        _TimeCount = 0;
        _isTrick = true;
        _Event.Invoke();
        Debug.Log("ACT");
    }

    public void _ResetBTM()
    {
        _isTrick = false;
        _TimeCount = 0;

        if (_Loading)
        {
            _Loading.SetActive(false);
        }
    }

    public void _Check()
    {
        Debug.Log("CHECK");
    }

    public void OnDisable()
    {
        Debug.Log("STOP");
        StopAllCoroutines();
    }

    public void _Reset()
    {
        Application.LoadLevel(0);
    }
}