using System;
using System.Reflection;
using InnerNet;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TMPro;

namespace Amethyst;

public sealed class AmethystAutoLobbyReturn : MonoBehaviour
{
    // 第 160 轮性能修复：FindObjectOfType 是全场景遍历，**绝不能在 Update 里每帧调用**。
    // 改成最多每 0.5 秒查一次并缓存实例（这才是大厅/对局里持续掉帧的来源之一）。
    private static EndGameManager _egmCache;
    private static float _nextEgmLookup;
    private const float AutoReturnDelaySeconds = 3f;
    private const float AutoReturnRetrySeconds = 0.4f;
    private const int AutoReturnMaxAttempts = 40;

    private int trackedEndGameId;
    private int exhaustedEndGameId;
    private int attempt;
    private float nextAttemptAt;
    private bool pending;
    private float nextScanAt;

    public void Update()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_AutoLobby);
    try { UpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_AutoLobby); }
    }

    public void UpdateInner()
    {
            if (!ShouldAutoReturn())
            {
                ResetState();
                return;
            }

            // 退出游戏/回到主菜单后 AmongUsClient 会变为未加入状态：立即停止，避免反复建房
            var client = AmongUsClient.Instance;
            if (client == null || client.GameState == InnerNetClient.GameStates.NotJoined)
            {
                ResetState();
                return;
            }

            if (LobbyBehaviour.Instance != null)
            {
                ResetState();
                return;
            }

            if (Time.unscaledTime < nextScanAt)
            {
                return;
            }

            nextScanAt = Time.unscaledTime + 0.4f;

            if (_egmCache == null && Time.unscaledTime >= _nextEgmLookup)
                    {
                        _nextEgmLookup = Time.unscaledTime + 0.5f;
                        _egmCache = Object.FindObjectOfType<EndGameManager>();
                    }
                    EndGameManager manager = _egmCache;
            // 只在结算界面生效；否则（主菜单/其它场景）不动作，避免误触创建房间
            if (manager == null)
            {
                ResetState();
                return;
            }

            int endGameId = manager.GetInstanceID();
            if (trackedEndGameId != endGameId)
            {
                trackedEndGameId = endGameId;
                exhaustedEndGameId = 0;
                attempt = 0;
                nextAttemptAt = Time.unscaledTime + AutoReturnDelaySeconds;
                pending = true;
            }

            if (!pending || exhaustedEndGameId == trackedEndGameId || Time.unscaledTime < nextAttemptAt)
            {
                return;
            }

            // 先走结算界面自己的「再来一局/返回大厅」入口；**成功就不要再点按钮**。
            // 之前两者都会执行：`NextGame()` 之后再点一次按钮 = 第二次 JoinGame，
            // 服务端会判定重复登录（用户看到的官方「重复登录」提示就是这么来的）。
            bool acted = TryInvokeEndGameAction(manager);
            if (!acted) acted = TryClickEndGameButtons(manager);

            if (LobbyBehaviour.Instance != null)
            {
                ResetState();
                return;
            }

            // 已经成功触发“再来一局/返回大厅”后不再重复调用，避免重复 JoinGame。
            if (acted)
            {
                pending = false;
                exhaustedEndGameId = trackedEndGameId;
                return;
            }

            attempt++;
            if (attempt >= AutoReturnMaxAttempts)
            {
                pending = false;
                exhaustedEndGameId = trackedEndGameId;
                return;
            }

            nextAttemptAt = Time.unscaledTime + AutoReturnRetrySeconds;
    
    }

    public void OnDisable()
    {
        ResetState();
    }

    private void ResetState()
    {
        trackedEndGameId = 0;
        exhaustedEndGameId = 0;
        attempt = 0;
        nextAttemptAt = 0f;
        pending = false;
        nextScanAt = 0f;
    }

    private static bool ShouldAutoReturn()
    {
        return AmethystConfig.AutoReturnLobby.Value;
    }

    private static bool TryInvokeEndGameAction(EndGameManager manager)
    {
        if (manager == null)
        {
            return false;
        }

        // 正确入口：结算界面的“再来一局/返回大厅”在 EndGameNavigation.NextGame()，
        // 而不是 EndGameManager 上（原实现反射 EndGameManager 的 Continue/NextGame/PlayAgain 永远找不到）。
        try
        {
            EndGameNavigation nav = manager.Navigation;
            if (nav != null)
            {
                nav.NextGame();
                return true;
            }
        }
        catch
        {
        }

        // 兜底：极少数版本才把方法挂在 EndGameManager 上
        string[] methodNames = { "Continue", "NextGame", "PlayAgain" };
        for (int i = 0; i < methodNames.Length; i++)
        {
            MethodInfo method = FindMethodNoWarn(manager.GetType(), methodNames[i], Type.EmptyTypes);
            if (method == null)
            {
                continue;
            }

            try
            {
                method.Invoke(manager, null);
                return true;
            }
            catch
            {
            }
        }

        return false;
    }

    private static MethodInfo FindMethodNoWarn(Type type, string name, Type[] parameters)
    {
        if (type == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Type[] signature = parameters ?? Type.EmptyTypes;
        for (Type current = type; current != null; current = current.BaseType)
        {
            MethodInfo method = current.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, signature, null);
            if (method != null)
            {
                return method;
            }
        }

        return null;
    }

    private static bool TryClickEndGameButtons(EndGameManager manager)
    {
        if (manager == null)
        {
            return false;
        }

        Component root = manager;
        if (TryClickPassiveButtons(root.GetComponentsInChildren<PassiveButton>(true), onlyActive: true))
        {
            return true;
        }

        return TryClickUnityButtons(root.GetComponentsInChildren<Button>(true), onlyActive: true);
    }

    private static bool TryClickGlobalReturnButtons()
    {
        if (TryClickPassiveButtons(Object.FindObjectsOfType<PassiveButton>(), onlyActive: true))
        {
            return true;
        }

        return TryClickUnityButtons(Object.FindObjectsOfType<Button>(), onlyActive: true);
    }

    private static bool TryClickPassiveButtons(PassiveButton[] buttons, bool onlyActive)
    {
        if (buttons == null)
        {
            return false;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            PassiveButton button = buttons[i];
            if (button == null)
            {
                continue;
            }

            Component component = button;
            if (onlyActive && (!component.gameObject.activeInHierarchy || !button.isActiveAndEnabled))
            {
                continue;
            }

            if (!IsLobbyReturnButton(button.name, component.GetComponentsInChildren<TMP_Text>(true)))
            {
                continue;
            }

            try
            {
                if ((UnityEvent)(object)button.OnClick == null)
                {
                    continue;
                }

                ((UnityEvent)button.OnClick).Invoke();
                return true;
            }
            catch
            {
            }
        }

        return false;
    }

    private static bool TryClickUnityButtons(Button[] buttons, bool onlyActive)
    {
        if (buttons == null)
        {
            return false;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            Component component = button;
            if (onlyActive && (!component.gameObject.activeInHierarchy || !button.isActiveAndEnabled || !button.interactable))
            {
                continue;
            }

            if (!IsLobbyReturnButton(button.name, component.GetComponentsInChildren<TMP_Text>(true)))
            {
                continue;
            }

            try
            {
                ((UnityEvent)button.onClick).Invoke();
                return true;
            }
            catch
            {
            }
        }

        return false;
    }

    private static bool IsLobbyReturnButton(string objectName, TMP_Text[] texts)
    {
        string normalizedName = (objectName ?? string.Empty).ToLowerInvariant();
        if (ContainsAny(normalizedName, "exit", "quit", "menu", "back", "leave", "вых", "выйт", "назад", "退出", "离开"))
        {
            return false;
        }

        if (ContainsAny(normalizedName, "continue", "nextgame", "playagain", "returntolobby", "tolobby", "lobby", "again", "продолж", "занов", "снов", "лобби", "играть", "вернут", "继续", "返回", "大厅", "再来", "下一局"))
        {
            return true;
        }

        if (texts == null)
        {
            return false;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            string normalizedText = StripRichText(text.text).ToLowerInvariant();
            if (ContainsAny(normalizedText, "exit", "quit", "menu", "back", "leave", "вых", "выйт", "назад", "退出", "离开"))
            {
                return false;
            }

            if (ContainsAny(normalizedText, "continue", "next game", "play again", "return to lobby", "lobby", "again", "продолж", "занов", "снов", "лобби", "играть", "вернут", "继续", "返回", "大厅", "再来", "下一局"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAny(string input, params string[] tokens)
    {
        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!string.IsNullOrWhiteSpace(token) && input.Contains(token))
            {
                return true;
            }
        }

        return false;
    }

    private static string StripRichText(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        char[] buffer = new char[input.Length];
        int length = 0;
        bool insideTag = false;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '<')
            {
                insideTag = true;
            }
            else if (c == '>')
            {
                insideTag = false;
            }
            else if (!insideTag)
            {
                buffer[length++] = c;
            }
        }

        return new string(buffer, 0, length);
    }
}
