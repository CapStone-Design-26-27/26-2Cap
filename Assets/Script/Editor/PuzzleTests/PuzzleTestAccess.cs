using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// 기존 게임 API를 테스트 때문에 변경하지 않도록 private 접근은 여기에만 모은다.
internal static class PuzzleTestAccess
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    public static void StartAiming(Vector2 position)
    {
        InvokeSpawnMethod("CreatePreviewBall", position);
    }

    public static void DropAimedBall()
    {
        InvokeSpawnMethod("DropPreviewBall");
    }

    public static bool HasVisibleNextAnimal()
    {
        FieldInfo field = typeof(BallQueueUI).GetField("nextIcon", PrivateInstance);
        if (field == null) throw new MissingFieldException(nameof(BallQueueUI), "nextIcon");
        Image icon = (Image)field.GetValue(BallQueueUI.Inst);
        return icon != null && icon.enabled && icon.sprite != null;
    }

    private static void InvokeSpawnMethod(string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(SpawnManager).GetMethod(methodName, PrivateInstance);
        if (method == null) throw new MissingMethodException(nameof(SpawnManager), methodName);
        try
        {
            method.Invoke(SpawnManager.Inst, arguments);
        }
        catch (TargetInvocationException exception)
        {
            throw new InvalidOperationException($"SpawnManager.{methodName} failed.", exception.InnerException);
        }
    }
}
