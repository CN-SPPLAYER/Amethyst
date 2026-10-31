using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using UnityEngine;

namespace Amethyst;

internal sealed class AccessEntry
{
    internal string Name;
    internal string Code;
    internal string Puid;
}
