﻿using System.Collections;
using System.Collections.Concurrent;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.PhysicEngine;
using OldTanks.Services.Misc;
using OpenTK.Mathematics;

namespace OldTanks.Models;

public class World
{
    private readonly ConcurrentQueue<TransformableAction> m_transformableActions;
    
    private readonly Camera m_defaultCamera;
    private readonly List<WorldObject> m_objects;
    private readonly CamerasCollection m_cameras;
    
    private readonly SkyBox m_skyBox;

    private Camera m_currentCamera;

    public World()
    {
        m_defaultCamera = new Camera();

        m_currentCamera = m_defaultCamera;
        
        m_objects = new List<WorldObject>();

        m_skyBox = new SkyBox();
        
        m_cameras = new CamerasCollection(this);
        m_transformableActions = new ConcurrentQueue<TransformableAction>();
    }

    public List<WorldObject> WorldObjects => m_objects;
    public ConcurrentQueue<TransformableAction> TransformableActions => m_transformableActions;
    public CamerasCollection Cameras => m_cameras;

    public Camera CurrentCamera
    {
        get => m_currentCamera;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            m_currentCamera = value;
        }
    }
    
    public WorldObject? Player { get; set; }

    public SkyBox SkyBox => m_skyBox;

    public void CollideObjects(float timeDelta)
    {
        var haveCollision = false;
        var normal = Vector3.Zero;
        var depth = 0f;

        for (int i = 0; i < WorldObjects.Count; i++)
        {
            WorldObjects[i].Move(timeDelta);
            WorldObjects[i].AcceptTransform();
        }

        for (int i = 0; i < WorldObjects.Count - 1; i++)
        {
            for (int j = i + 1; j < WorldObjects.Count; j++)
            {
                var first = WorldObjects[i];
                var second = WorldObjects[j];
                
                if (first.RigidBody.IsStatic && second.RigidBody.IsStatic)
                    continue;

                haveCollision = first.Collision
                    .CheckCollision(second.Collision, out normal, out depth);

                if (haveCollision)
                {
                    var dot = Vector3.Dot(normal, PhysicsConstants.GravityDirection);
                    if (first.RigidBody.IsStatic)
                    {
                        second.Position += normal * depth;
                        second.RigidBody.OnGround = dot < 0;
                    }
                    else if (second.RigidBody.IsStatic)
                    {
                        first.Position -= normal * depth;
                        first.RigidBody.OnGround = dot < 0;
                    }
                    else
                    {
                        first.Position -= normal * depth / 2;
                        second.Position += normal * depth / 2;
                    }

                    // ResolveColliding(first, second, normal, depth);
                    
                    first.AcceptTransform();
                    second.AcceptTransform();
                }
            }
        }
    }

    private void ResolveColliding(WorldObject o1, WorldObject o2, in Vector3 normal, float depth)
    {
        
    }

    public class CamerasCollection : IEnumerable<Camera>
    {
        private readonly List<Camera> m_cameras;
        private readonly World m_world;
        
        public CamerasCollection(World world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            
            m_cameras = new List<Camera>();
            m_world = world;
        }

        public IEnumerator<Camera> GetEnumerator() => m_cameras.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(Camera camera) => m_cameras.Add(camera);
        public bool Remove(Camera camera) => m_cameras.Remove(camera);
        public bool Contains(Camera camera) => m_cameras.Contains(camera);

        public void Clear()
        {
            m_cameras.Clear();
            m_cameras.Add(m_world.m_defaultCamera);
        }
    }
}