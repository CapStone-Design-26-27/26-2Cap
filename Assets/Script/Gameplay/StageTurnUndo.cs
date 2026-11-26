using System.Collections.Generic;
using UnityEngine;

// In-memory turn history captured before each drop.
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

    private sealed class Turn
    {
        public readonly List<BallState> Balls = new List<BallState>();
        public List<int> Queue;
        public List<bool> QueueLocks;
        public List<float> Angles;
    }

    private readonly Stack<Turn> history = new Stack<Turn>();
    public int FreeUses { get; private set; }
    public bool CanRestore => FreeUses != 0 && history.Count > 0;

    public void Reset(int maxUndoCount)
    {
        FreeUses = Mathf.Max(-1, maxUndoCount);
        history.Clear();
    }

    public void Capture(GameObject preview, SpawnManager spawn, ScaleSystem scale)
    {
        if (FreeUses == 0) return;
        var turn = new Turn();
        foreach (var ball in Object.FindObjectsOfType<BallBehaviour>())
        {
            if (ball.gameObject == preview || ball.isMerged || !ball.TryGetComponent(out Rigidbody2D body) || body.isKinematic) continue;
            turn.Balls.Add(new BallState
            {
                Level = ball.level, Owner = ball.owner, Position = body.position, Rotation = body.rotation,
                Velocity = body.velocity, AngularVelocity = body.angularVelocity,
                CanMerge = BallMergeLock.CanMerge(ball.gameObject)
            });
        }
        turn.Queue = new List<int>(spawn.nextBallQueue);
        turn.QueueLocks = spawn.GetRemainingLocks();
        turn.Angles = scale.GetAngles();
        history.Push(turn);
    }

    public bool Matches(ScaleSystem scale) => CanRestore && history.Peek().Angles.Count == scale.AllNodes.Count;

    public void Restore(SpawnManager spawn, ScaleSystem scale, GameManager game)
    {
        Turn turn = history.Pop();
        scale.ApplyAngles(turn.Angles);
        foreach (var state in turn.Balls)
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
        spawn.RestoreQueue(turn.Queue, turn.QueueLocks);
        Physics2D.SyncTransforms();
        if (FreeUses > 0) FreeUses--;
    }
}
