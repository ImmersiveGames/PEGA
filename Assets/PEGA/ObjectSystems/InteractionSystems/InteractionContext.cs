﻿using ImmersiveGames.HierarchicalStateMachine;
using PEGA.ObjectSystems.DriverSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.InteractionSystems
{
    [DefaultExecutionOrder(-10)]
    public class InteractionContext : StateContext
    {
        internal IInputDriver InputDriver;
    }
}