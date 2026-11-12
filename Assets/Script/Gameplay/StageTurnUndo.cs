using System.Collections.Generic;
using UnityEngine;

// One in-memory checkpoint, captured before consuming the next ball.
internal sealed class StageTurnUndo
{
    private struct BallState
    {
        public int Level;
        public Basket Owner;
        public Vector2 Position, Velocity;
        public float Rotation, AngularVelocity;
        public bool CanMerge;
    }

    private readonly List<BallState> balls = new List<BallState>();
    private List<int> nextLevels;
    private List<float> angles;

    public int FreeUses { get; private set; } = 1;
    public bool CanRestore => FreeUses > 0 && angles != null;

    public void Reset()
    {
        FreeUses = 1;
        angles = null;
        balls.Clear();
    }

    public void Capture(GameObject preview, SpawnManager spawn, ScaleSystem scale)
    {
        if (FreeUses == 0) return;
        balls.Clear();
        foreach (var ball in Object.FindObjectsOfType<BallBehaviour>())
        {
            if (ball.gameObject == preview || ball.isMerged || !ball.TryGetComponent(out Rigidbody2D body) || body.isKinematic) continue;
            balls.Add(new BallState
            {
                Level = ball.level, Owner = ball.owner, Position = body.position, Rotation = body.rotation,
                Velocity = body.velocity, AngularVelocity = body.angularVelocity,
                CanMerge = BallMergeLock.CanMerge(ball.gameObject)
            });
        }
        nextLevels = new List<int>(spawn.nextBallQueue);
        angles = scale.GetAngles();
    }

    public bool Matches(ScaleSystem scale) => CanRestore && angles.Count == scale.AllNodes.Count;

    public void Restore(SpawnManager spawn, ScaleSystem scale, GameManager game)
    {
        scale.ApplyAngles(angles);
        foreach (var state in balls)
        {
            var ball = Object.Instantiate(game.ballList[state.Level], state.Position, Quaternion.Euler(0, 0, state.Rotation));
            spawn.SetupBallProperties(ball, state.Level, false, state.Owner);
            BallMergeLock.SetMergeAllowed(ball, state.CanMerge);
            if (ball.TryGetComponent(out Rigidbody2D body))
            {
                body.velocity = state.Velocity;
                body.angularVelocity = state.AngularVelocity;
            }
        }
        spawn.RestoreQueue(nextLevels);
        Physics2D.SyncTransforms();
        FreeUses--;
        angles = null;
        balls.Clear();
    }
}
