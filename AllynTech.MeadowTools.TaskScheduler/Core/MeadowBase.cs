using Meadow;
using Meadow.Logging;

namespace AllynTech.MeadowTools.TaskScheduler
{
    public abstract class MeadowBase
    {
        protected static IMeadowDevice MeadowCpu => Resolver.Device;
        protected static Logger Log => Resolver.Log;
    }
}
