// Comments are in this file, and should not be removed, even if the style guidelines say to.
// Expanded from https://github.com/bepu/bepuphysics2/blob/master/Demos/Demos/SimpleSelfContainedDemo.cs, look there for help.
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Patchwork.Physics;

public static class PhysicsManager
{
    struct NarrowPhaseCallbacks() : INarrowPhaseCallbacks
    {
        public void Initialize(Simulation simulation) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
        {
            // TODO: Collision groups, reference exposes a BodyHandle so use that.
            return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
        {
            // Not exactly sure what this does, some kind of compound shape stuff. Maybe useful later?
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            pairMaterial.FrictionCoefficient = 1f;
            pairMaterial.MaximumRecoveryVelocity = 2f;
            pairMaterial.SpringSettings = new SpringSettings(30, 1);
            // TODO: Material properties, friction, bounciness, etc. 
            // (Note that there's no 'bounciness' or 'coefficient of restitution' property!
            // Bounciness is handled through the contact spring settings instead. Setting See here for more details: https://github.com/bepu/bepuphysics2/issues/3 and check out the BouncinessDemo for some options.)
            // You can also like prevent the collision here, returning false.
            // This is useful because you can like check the collision normal and make say a one-way wall.
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
        {
            // Compound shape stuff again.
            return true;
        }

        public void Dispose()
        {
        }
    }

    struct PoseIntegratorCallbacks() : IPoseIntegratorCallbacks
    {
        public void Initialize(Simulation simulation) { }

        public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.ConserveMomentumWithGyroscopicTorque; // Idk, this seems correct.

        public readonly bool AllowSubstepsForUnconstrainedBodies => false; // Substeps bodies that aren't colliding, best to leave false.

        public readonly bool IntegrateVelocityForKinematics => false; // Should be left false, this literally defeats the purpose of kinematics.

        // Needs to be wide for how the solver works.
        Vector3Wide gravityWideDt;

        // For precomputing anything expensive that will be used across velocity integration.
        public void PrepareForIntegration(float dt)
        {
            // No reason to recalculate gravity * dt for every body; just cache it ahead of time.
            gravityWideDt = Vector3Wide.Broadcast(Gravity * dt);
        }

        // Apply gravity and forces, this runs for like a bundle of bodies so wide types are needed.
        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
        {
            // For things like position dependent gravity or per-body damping.
            velocity.Linear += gravityWideDt;
        }
    }
    // Exposes an extra unused parameter called initialAllocationSizes, but we don't need it.
    static BufferPool BufferPool = new();
    static Simulation Simulation = Simulation.Create(BufferPool, new NarrowPhaseCallbacks(), new PoseIntegratorCallbacks(), new SolveDescription(8, 1), new DefaultTimestepper());
    static ThreadDispatcher ThreadDispatcher = new(Environment.ProcessorCount);
    public static void Run()
    {
        //Drop a ball on a big static box.
        Sphere sphere = new(1);
        BodyInertia sphereInertia = sphere.ComputeInertia(1);
        BodyHandle bodyHandle = Simulation.Bodies.Add(BodyDescription.CreateDynamic(new Vector3(0, 5, 0), new Vector3(0, 0, 0), sphereInertia, Simulation.Shapes.Add(sphere), 0.01f));

        StaticHandle staticHandle = Simulation.Statics.Add(new StaticDescription(new Vector3(0, 0, 0), Simulation.Shapes.Add(new Box(500, 1, 500))));


        //Now take 100 time steps!
        for (int i = 0; i < 100; ++i)
        {
            //Multithreading is pretty pointless for a simulation of one ball, but passing a IThreadDispatcher instance is all you have to do to enable multithreading.
            //If you don't want to use multithreading, don't pass a IThreadDispatcher.

            //Note that each timestep is 0.01 units in duration, so all 100 time steps will last 1 unit of time.
            //(Usually, units of time are defined to be seconds, but the engine has no preconceived notions about units. All it sees are the numbers.)
            Simulation.Timestep(0.01f, ThreadDispatcher);

            //You can use the body handle to look up information about a body; the bodies collection is indexable:
            BodyReference bodyReference = Simulation.Bodies[bodyHandle];
            //The BodyReference is a convenience wrapper that handles the memory indirections under the hood.
            //You can use it to grab things like the current pose:
            //Note that all the properties that return references are direct references to the body's memory.
            //You can both read from it and write to it. Be advised: the API will let you break stuff!
            //In principle, you could do the exact same lookup using the bodyHandle->index mapping and grab a direct pointer.
            if ((i + 1) % 10 == 0)
                Console.WriteLine($"Body position at timestep {i}: {bodyReference.Pose.Position}");
        }

        //Statics can also be grabbed.
        Console.WriteLine($"Bounding box of the static floor: {Simulation.Statics[staticHandle].BoundingBox}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PositionUpdate(BodyHandle handle, Vector3 position)
    {
        Simulation.Bodies[handle].Pose.Position = position;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OrientationUpdate(BodyHandle handle, Quaternion orientation)
    {
        Simulation.Bodies[handle].Pose.Orientation = orientation;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VelocityUpdate(BodyHandle handle, Vector3 velocity)
    {
        Simulation.Bodies[handle].Velocity.Linear = velocity;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AngularVelocityUpdate(BodyHandle handle, Vector3 angularVelocity)
    {
        Simulation.Bodies[handle].Velocity.Angular = angularVelocity;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MassUpdate(BodyHandle handle, float mass)
    {
        Simulation.Bodies[handle].LocalInertia = (Bodies[handle].Model as Model ?? throw new InvalidDataException("Model not found.")).Hull.ComputeInertia(mass);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DisposeBody(BodyHandle handle)
    {
        Bodies.Remove(handle);
        Simulation.Bodies.Remove(handle);
    }
    public static Dictionary<BodyHandle, Entity> Bodies = new();

    public static void Update()
    {
        Simulation.Timestep(DeltaTime, ThreadDispatcher);
        foreach (Entity entity in Entity.Entities.Values)
            if (entity.Physics && entity.Handle.HasValue)
            {
                BodyReference bodyReference = Simulation.Bodies[entity.Handle.Value];
                entity.PhysicsUpdate(bodyReference.Pose.Position, bodyReference.Pose.Orientation, bodyReference.Velocity.Linear, bodyReference.Velocity.Angular);
            }
    }

    public static void Add(Entity entity)
    {
        if (!entity.Static && entity.Model is Model model)
            Bodies.Add(Simulation.Bodies.Add(new BodyDescription
            {
                Pose = new RigidPose(entity.Position, entity.Rotation),
                Velocity = new BodyVelocity(entity.Velocity, entity.AngularVelocity),
                Collidable = model.Index,
                LocalInertia = model.Hull.ComputeInertia(entity.Mass),
                Activity = 0.01f,
            }), entity);
    }

    public const uint Magic = 0xFF762496;
    public static (ConvexHull hull, TypedIndex index) CreateShape(Model model)
    {
        using BinaryReader reader = model.DataReader;
        uint magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new Exception("Invalid model file");
        }

        int vertexCount = (int)reader.ReadUInt32();

        Vector3[] vertices = new Vector3[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();

            vertices[i] = new Vector3(x, y, z);

            reader.BaseStream.Position += 13 * sizeof(float); // 16 components for each vertex, we read three so skip the 13 remaining
        }
        ConvexHull hull = new ConvexHull(vertices.AsSpan(), BufferPool, out _);
        TypedIndex shape = Simulation.Shapes.Add(hull);
        return (hull, shape);
    }

    public static void Dispose()
    {
        Simulation.Dispose();
        ThreadDispatcher.Dispose();
    }
}
