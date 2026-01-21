using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;

    private Vector3 lastPosition;

    [SerializeField] LayerMask placementMask;

    private InputSystem_Actions action;

    public event Action OnClicked, OnExit;

    private void Awake()
    {
        action = new InputSystem_Actions();
        action.UI.Enable();
    }

    private void Update()
    {
        if (action.UI.Click.IsPressed())
        {
            OnClicked?.Invoke();
        }
        if (action.UI.Cancel.IsPressed())
        {
            OnExit?.Invoke();
        }
    }

    public bool isPointerOverUI() => EventSystem.current.IsPointerOverGameObject();

    public Vector3 GetPlacementPosition()
    {
        Ray ray = playerCamera.ScreenPointToRay(action.UI.Point.ReadValue<Vector2>());
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, placementMask))
        {
            lastPosition = hit.point;
        }
        return lastPosition;
    }
}
