﻿using CoolEngine.Core;
using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;

namespace CoolEngine.Services.Interfaces;

public interface ICollisionable
{
    public Collision Collision { get; set; }
}