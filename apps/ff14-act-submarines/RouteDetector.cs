using System;

namespace FF14SubmarinesAct
{
    // Placeholder for future network-based route extraction.
    // Next step: implement TryExtractRouteKey using FFXIV_ACT_Plugin network events or parsed payloads.
    public static class RouteDetector
    {
        // Returns true when a route key is detected in the given payload/event args, along with submarine name.
        // For now this is a stub that always returns false.
        public static bool TryExtractRouteKey(object eventArgs, out string submarineName, out string routeKey)
        {
            submarineName = null;
            routeKey = null;
            return false;
        }
    }
}

