using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;
using Unity.Collections;
using Unity.Jobs;
using Unity.Entities;
using Game.Prefabs;
using Colossal.Serialization.Entities;
using Game.Input;
using Game.Settings;
using Game.UI.InGame;


namespace Fishing
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(Fishing)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            updateSystem.UpdateAt<DansFish>(SystemUpdatePhase.LateUpdate);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
        }
    }

    public partial class DansFish : GameSystemBase
    {
        internal static bool s_clearUsedFishResource = false;

        private NaturalResourceSystem _naturalResourceSystem;

        // Timing logic
        private double _lastClearTime;
        private const double ClearInterval = 300.0; // 5 minutes in seconds

        protected override void OnCreate()
        {
            base.OnCreate();
            _naturalResourceSystem = World.GetOrCreateSystemManaged<NaturalResourceSystem>();
            _lastClearTime = World.Time.ElapsedTime; 
        }

        protected override void OnUpdate()
        {
            double time = World.Time.ElapsedTime;

            // Trigger clear every 5 minutes of real time
            if (time - _lastClearTime >= ClearInterval)
            {
                s_clearUsedFishResource = true;
                _lastClearTime = time;
                Mod.log.Info("Clearing used fish resources");
            }

            if (s_clearUsedFishResource)
            {
                NativeArray<NaturalResourceCell> naturalResourceCells = _naturalResourceSystem.GetData(false, out JobHandle dependencies).m_Buffer;
                JobHandle jobHandle = JobHandle.CombineDependencies(Dependency, dependencies);

                ClearUsedResourceJob clearUsedResourceJob = new()
                {
                    m_Buffer = naturalResourceCells,
                    m_ClearUsedFishResource = true,
                };

                JobHandle clearJobHandle = clearUsedResourceJob.Schedule(naturalResourceCells.Length, 16, jobHandle);
                _naturalResourceSystem.AddWriter(clearJobHandle);
                Dependency = clearJobHandle;

                s_clearUsedFishResource = false;
            }
        }

#if RELEASE
        [Unity.Burst.BurstCompile]
#endif
        private struct ClearUsedResourceJob : IJobParallelFor
        {
            public NativeArray<NaturalResourceCell> m_Buffer;
            public bool m_ClearUsedFishResource;

            public void Execute(int index)
            {
                NaturalResourceCell cell = m_Buffer[index];
                if (m_ClearUsedFishResource)
                {
                    cell.m_Fish.m_Used = 0;
                }
                m_Buffer[index] = cell;
            }
        }
    }
}
