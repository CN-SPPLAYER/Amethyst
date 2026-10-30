namespace Amethyst.Patches;

// 驱逐（放逐结算）画面：**保持游戏原生**，不做任何名字着色。
//
// 历史：这里原本会在中文文本里把被放逐者名字替换成带 <color> 的富文本。
// 现按需求移除该行为——驱逐画面的名字颜色一律交给原版处理。
//
// 保留本类型与 Tick() 方法（空实现）是为了不改动 Menu\AmethystHud.cs 的调用点；
// 若后续不再需要，可连同调用点一起删除。
internal static class AmethystExileName
{
    internal static void Tick()
    {
        // 故意留空：驱逐阶段交由游戏原生渲染。
    }
}
