using System;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.DriverSystems.Drivers;
using PEGA.ObjectSystems.DriverSystems.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.DriverSystems
{
    public static class InputDriverFactory
    {
        public static IInputDriver CreateDriver(DriverType driverType, Transform owner)
        {
            IInputDriver driver = driverType switch
            {
                DriverType.Player => new PlayerInputDriver(owner.GetComponent<PlayerInput>()),
                DriverType.AI => new NullInputDriver(owner),
                _ => throw new ArgumentOutOfRangeException(nameof(driverType), driverType, null)
            };
            var driverController = new DriverController(driver);
            return driverController.GetActualDriver();
        }
    }
}