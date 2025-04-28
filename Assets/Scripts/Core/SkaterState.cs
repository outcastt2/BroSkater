using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BroSkater.Core
{
    /// <summary>
    /// Enums and constants for skater state management based on Tony Hawk's Underground source code
    /// </summary>
    
    // Main skater states
    public enum SkaterState
    {
        Ground,
        Air,
        Wall,
        Lip,
        Rail,
        Wallplant
    }

    // Skater flags that can be toggled on and off
    [System.Flags]
    public enum SkaterFlag
    {
        None = 0,
        Tense = 1 << 0,                   // tensing for a jump (X pressed)
        Flipped = 1 << 1,                 // animation is flipped
        VertAir = 1 << 2,                 // going to vert air
        TrackingVert = 1 << 3,            // tracking a vert surface below us
        LastPolyWasVert = 1 << 4,         // last polygon skated on was vert
        CanBreakVert = 1 << 5,            // can break the vert poly we're on
        CanRerail = 1 << 6,               // can get back on rail again
        RailSliding = 1 << 7,             // skater is rail sliding
        CanHitCar = 1 << 8,               // can hit a car
        AutoTurn = 1 << 9,                // can auto turn
        IsBailing = 1 << 10,              // skater is bailing
        SpinePhysics = 1 << 11,           // going over a spine
        InRecovery = 1 << 12,             // recovering from going off vert
        Skitching = 1 << 13,              // being towed by a car
        OverrideCancelGround = 1 << 14,   // ignore "CANCEL_GROUND" flag for gaps
        SnappedOverCurb = 1 << 15,        // snapped up or down a curb this frame
        Snapped = 1 << 16,                // snapped slightly this frame
        InAcidDrop = 1 << 17,             // in an acid drop
        AirAcidDropDisallowed = 1 << 18,  // in-air acid drops not allowed
        CancelWallPush = 1 << 19,         // current wallpush event is canceled
        NoOrientationControl = 1 << 20,   // spins and leans turned off
        NewRail = 1 << 21,                // rail we land on is a "new" rail
        OlliedFromRail = 1 << 22,         // entered current air via ollie from rail
    }

    // Terrain types
    public enum TerrainType
    {
        Default,
        Concrete,
        Wood,
        Metal,
        Grass,
        Gravel,
        Water,
        Snow,
        Sand
    }

    // Physics state types
    public enum PhysicsStateType
    {
        NoState = -1,
        Skating,
        Walking,
        Riding
    }
} 