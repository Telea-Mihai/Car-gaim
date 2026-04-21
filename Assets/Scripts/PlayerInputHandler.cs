using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public Vehicle activeVehicle;
    
    private InputAction throttleAction;
    private InputAction steeringAction;
    private InputAction brakeAction;
    private InputAction gearUpAction;
    private InputAction gearDownAction;
    private InputAction handbrakeAction;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        throttleAction = InputSystem.actions.FindAction("Throttle");
        steeringAction = InputSystem.actions.FindAction("Steer");
        brakeAction = InputSystem.actions.FindAction("Brake");
        gearUpAction = InputSystem.actions.FindAction("ShiftUp");
        gearDownAction = InputSystem.actions.FindAction("ShiftDown");
        handbrakeAction = InputSystem.actions.FindAction("Handbrake");
    }

    // Update is called once per frame
    void Update()
    {
        if (activeVehicle)
        {
            activeVehicle.throttleInput = throttleAction.ReadValue<float>();
            activeVehicle.steeringInput = steeringAction.ReadValue<float>();
            activeVehicle.brakeInput = brakeAction.ReadValue<float>();
            activeVehicle.handbrakeInput = handbrakeAction.ReadValue<float>();
            if(gearUpAction.WasPressedThisFrame())
                activeVehicle.IncreaseGear();
            else if(gearDownAction.WasPressedThisFrame())
                activeVehicle.DecreaseGear();
            
        }
    }
}
