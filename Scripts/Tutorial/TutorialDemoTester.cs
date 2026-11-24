using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class TutorialDemoTester : MonoBehaviour
{
    public TutorialDemoController demoController;
    public ToolDemoType testType = ToolDemoType.Sponge;

    private InputDevice rightController;
    private bool aButtonPressedLastFrame;

    void Start()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);
        if (devices.Count > 0)
            rightController = devices[0];
    }

    void Update()
    {
        if (!rightController.isValid)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);
            if (devices.Count > 0)
                rightController = devices[0];
            return;
        }

        // 오른쪽 컨트롤러 A버튼으로 데모 재생
        rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bPressed);
        if (bPressed && !aButtonPressedLastFrame)
            demoController.PlayDemo(testType);

        aButtonPressedLastFrame = bPressed;
    }
}
