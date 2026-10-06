using System;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace rfmechanics
{
    internal readonly record struct ClimbGrip(float MotionFactor, bool RequiresFreeHands)
    {
        internal static readonly ClimbGrip None = new(0, false);
        internal static readonly ClimbGrip Full = new(1, false);
    }

    internal interface IClimbBlockFilter
    {
        ClimbGrip GetGrip(IWorldAccessor world, Block block, BlockPos pos);
    }

    /// <summary>
    /// Face selection and outside-corner wrapping shared by GoblinClimbingPatch (walls, earth and
    /// trunks) and TreeClimbingPatch (elf trunks). Only the block filter differs between the two,
    /// so everything physics-shaped lives here: the orthogonal scan with face scoring, a grace
    /// window that keeps the grip alive for a few ticks after the held face vanishes, and a
    /// diagonal second pass gated to that window. Each patch owns its own instance so the two
    /// state tables never mix.
    /// Call Update() only from the ApplyTests postfix -- it is the one reached both locally and via
    /// RemoteMotionAndCollision -- and read it from MotionAndCollision with HoldsGrip().
    /// </summary>
    internal sealed class ClimbCornerTraversal
    {
        private sealed class CornerState
        {
            public BlockFacing? HeldFace;
            public int GraceTicksLeft;
            public ClimbGrip Grip = ClimbGrip.None;
        }

        /// <summary>ConditionalWeakTable, not a dictionary keyed by entity id: in singleplayer the
        /// client and server physics run in one process (see RFMechanicsModSystem.cs:58), so an
        /// id key would let the two sides overwrite each other's corner state mid-wrap.</summary>
        private readonly ConditionalWeakTable<Entity, CornerState> states = new();

        internal void Forget(Entity entity) => states.Remove(entity);

        internal void ForgetFreeHandGrip(Entity entity)
        {
            if (states.TryGetValue(entity, out CornerState? state) && state.Grip.RequiresFreeHands)
            {
                states.Remove(entity);
            }
        }

        internal bool HasFreeHandGrip(Entity entity)
            => states.TryGetValue(entity, out CornerState? state) && state.Grip.RequiresFreeHands;

        private BlockFacing? HeldFace(Entity entity)
            => states.TryGetValue(entity, out CornerState? state) ? state.HeldFace : null;

        /// <summary>Pure read for the MotionAndCollision half, which runs first in the same tick
        /// (EntityBehaviorControlledPhysics.cs:567-568) and so sees the previous tick's window.
        /// All bookkeeping stays in Update; moving any of it here would skip remote entities.</summary>
        internal bool TryGetGrip(Entity entity, EntityPos pos, IClimbBlockFilter? filter, bool cornersEnabled, out ClimbGrip grip)
        {
            if (TryFindClimb(entity, pos, filter, cornersEnabled, HeldFace(entity), out BlockFacing? _, out Cuboidf? _, out grip)) return true;
            if (states.TryGetValue(entity, out CornerState? state) && state.GraceTicksLeft > 0)
            {
                grip = state.Grip;
                return true;
            }
            grip = ClimbGrip.None;
            return false;
        }

        internal bool HoldsGrip(Entity entity, EntityPos pos, IClimbBlockFilter? filter, bool cornersEnabled)
            => TryGetGrip(entity, pos, filter, cornersEnabled, out ClimbGrip _);

        internal void Update(Entity entity, EntityPos pos, EntityControls controls, IClimbBlockFilter? filter, bool cornersEnabled, int graceTicks)
        {
            CornerState state = states.GetOrCreateValue(entity);

            // Standing on solid ground is never mid-wrap; without this the window would keep
            // suspending gravity for ~0.2s after merely stepping away from a climbable wall.
            if (entity.OnGround) { state.GraceTicksLeft = 0; state.HeldFace = null; state.Grip = ClimbGrip.None; }

            if (TryFindClimb(entity, pos, filter, cornersEnabled, state.HeldFace, out BlockFacing? face, out Cuboidf? collBox, out ClimbGrip grip))
            {
                Grip(entity, controls, state, face!, collBox!, grip, graceTicks);
                return;
            }

            if (state.GraceTicksLeft <= 0) { state.HeldFace = null; state.Grip = ClimbGrip.None; return; }
            state.GraceTicksLeft--;

            // Only ever grips a face backed by a real collision box -- EntityAgent.cs:665
            // dereferences ClimbingOnCollBox unguarded whenever ClimbingOnFace is set, so
            // holding the vanished face through the window would throw there.
            if (TryFindCornerClimb(entity, pos, state.HeldFace, filter, cornersEnabled, out face, out collBox, out grip))
            {
                Grip(entity, controls, state, face!, collBox!, grip, graceTicks);
            }
        }

        private static void Grip(Entity entity, EntityControls controls, CornerState state, BlockFacing face, Cuboidf collBox, ClimbGrip grip, int graceTicks)
        {
            controls.IsClimbing = true;
            entity.ClimbingOnFace = face;
            entity.ClimbingOnCollBox = collBox;
            state.HeldFace = face;
            state.Grip = grip;
            state.GraceTicksLeft = Math.Max(0, graceTicks);
        }

        private sealed class ScanContext
        {
            public IWorldAccessor World = null!;
            public IBlockAccessor Accessor = null!;
            public IClimbBlockFilter Filter = null!;
            public Cuboidd EntityBox = null!;
            public BlockPos TmpPos = null!;
            public float TouchDistance;
            public int Height;
            public int OriginX, BaseY, OriginZ;
        }

        private static ScanContext? BuildContext(Entity entity, EntityPos pos, IClimbBlockFilter? filter)
        {
            if (filter == null) return null;

            return new ScanContext
            {
                World = entity.World,
                Accessor = entity.World.BlockAccessor,
                Filter = filter,
                EntityBox = new Cuboidd().SetAndTranslate(entity.CollisionBox, pos.X, pos.Y, pos.Z),
                TmpPos = new BlockPos(pos.Dimension),
                TouchDistance = entity.Properties.ClimbTouchDistance,
                Height = (int)Math.Ceiling(entity.CollisionBox.Y2),
                OriginX = (int)pos.X,
                BaseY = (int)pos.Y,
                OriginZ = (int)pos.Z
            };
        }

        /// <summary>Absolute Set() per column rather than vanilla's cumulative IterateHorizontalOffsets
        /// walk, which leaves the cursor on the last offset and so cannot be continued into a diagonal
        /// pass. Offsets are identical to vanilla's for the four horizontals.</summary>
        private static bool ScanColumn(ScanContext c, int dx, int dz, out Cuboidf? collBox, out ClimbGrip grip)
        {
            collBox = null;
            grip = ClimbGrip.None;
            c.TmpPos.Set(c.OriginX + dx, c.BaseY, c.OriginZ + dz);

            for (int dy = 0; dy < c.Height; dy++)
            {
                c.TmpPos.Y = c.BaseY + dy;
                Block inBlock = c.Accessor.GetBlock(c.TmpPos, BlockLayersAccess.Solid);
                ClimbGrip candidateGrip = c.Filter.GetGrip(c.World, inBlock, c.TmpPos);
                if (candidateGrip.MotionFactor <= 0) continue;

                Cuboidf[] collisionBoxes = inBlock.GetCollisionBoxes(c.Accessor, c.TmpPos);
                if (collisionBoxes == null) continue;

                for (int j = 0; j < collisionBoxes.Length; j++)
                {
                    if (c.EntityBox.ShortestDistanceFrom(collisionBoxes[j], c.TmpPos) < c.TouchDistance)
                    {
                        collBox = collisionBoxes[j];
                        grip = candidateGrip;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Normalized horizontal walk input, or false when the entity is not moving --
        /// standing still carries no directional intent, so held-face preference decides alone.</summary>
        private static bool TryGetHeading(Entity entity, out double hx, out double hz)
        {
            hx = 0;
            hz = 0;
            if (entity is not EntityAgent agent) return false;

            Vec3d walk = agent.Controls.WalkVector;
            double length = Math.Sqrt(walk.X * walk.X + walk.Z * walk.Z);
            if (length < 1e-4) return false;

            hx = walk.X / length;
            hz = walk.Z / length;
            return true;
        }

        /// <summary>Same four-orthogonal-neighbor scan shape as vanilla's, but picking the
        /// best-scoring face rather than the first hit, so an inside corner where two faces both
        /// qualify follows the player instead of always snapping north.</summary>
        private static bool TryFindClimb(Entity entity, EntityPos pos, IClimbBlockFilter? filter, bool scoreFaces, BlockFacing? preferred, out BlockFacing? face, out Cuboidf? collBox, out ClimbGrip grip)
        {
            face = null;
            collBox = null;
            grip = ClimbGrip.None;

            ScanContext? c = BuildContext(entity, pos, filter);
            if (c == null) return false;

            double hx = 0, hz = 0;
            bool moving = scoreFaces && TryGetHeading(entity, out hx, out hz);
            double bestScore = double.NegativeInfinity;

            for (int i = 0; i < 4; i++)
            {
                BlockFacing candidate = BlockFacing.HORIZONTALS[i];
                if (!ScanColumn(c, candidate.Normali.X, candidate.Normali.Z, out Cuboidf? box, out ClimbGrip candidateGrip)) continue;

                if (!scoreFaces)
                {
                    face = candidate;
                    collBox = box;
                    grip = candidateGrip;
                    return true;
                }

                // Held face outranks any alignment score, so an established grip is never traded away
                // for a marginally better-aligned neighbor mid-climb.
                double score = preferred != null && candidate == preferred ? 2.0
                    : moving ? hx * candidate.Normali.X + hz * candidate.Normali.Z
                    : 0.0;
                if (score <= bestScore) continue;

                bestScore = score;
                face = candidate;
                collBox = box;
                grip = candidateGrip;
            }

            return face != null;
        }

        /// <summary>Second pass over the four diagonal columns, reached only inside the grace window so
        /// ordinary acquisition keeps vanilla's orthogonal-only shape. This is what carries a climber
        /// around an outside (convex) corner -- a building corner, or the edge of a trunk -- where the
        /// continuing surface is diagonal from its own column and the orthogonal scan finds nothing.</summary>
        private static bool TryFindCornerClimb(Entity entity, EntityPos pos, BlockFacing? held, IClimbBlockFilter? filter, bool cornersEnabled, out BlockFacing? face, out Cuboidf? collBox, out ClimbGrip grip)
        {
            face = null;
            collBox = null;
            grip = ClimbGrip.None;

            if (!cornersEnabled) return false;
            if (held == null) return false;

            ScanContext? c = BuildContext(entity, pos, filter);
            if (c == null) return false;

            bool moving = TryGetHeading(entity, out double hx, out double hz);
            double bestScore = double.NegativeInfinity;

            for (int i = 0; i < 4; i++)
            {
                BlockFacing a = BlockFacing.HORIZONTALS[i];
                BlockFacing b = BlockFacing.HORIZONTALS[(i + 1) % 4];
                int dx = a.Normali.X + b.Normali.X;
                int dz = a.Normali.Z + b.Normali.Z;

                // Only diagonals sharing an edge with the lost face can be a wrap of that same wall;
                // the opposite two would be a jump across open air.
                if (a != held && b != held) continue;
                if (!ScanColumn(c, dx, dz, out Cuboidf? box, out ClimbGrip candidateGrip)) continue;

                // Grip the component the climber is turning ONTO, not the plane it just lost: held
                // NORTH wrapping onto the NW column is the building's east side, so face WEST.
                BlockFacing candidate = a == held ? b : a;

                double score = moving ? hx * dx + hz * dz : 0.0;
                if (score <= bestScore) continue;

                bestScore = score;
                face = candidate;
                collBox = box;
                grip = candidateGrip;
            }

            return face != null;
        }
    }
}
