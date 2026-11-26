using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ToolbarControl_NS;
using UnityEngine;

using KSP_Log;

namespace KerbalMemorial
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class RegisterToolbar : MonoBehaviour
    {
        public static Log Log;

        public void Start()
        {
            ToolbarControl.RegisterMod("KerbalMemorial", "Kerbal Memorial");

#if DEBUG
            Log = new Log("KerbalMemorial", Log.LEVEL.INFO);
#else
            Log = new Log("KerbalMemorial", Log.LEVEL.ERROR);
#endif
        }
    }
}
