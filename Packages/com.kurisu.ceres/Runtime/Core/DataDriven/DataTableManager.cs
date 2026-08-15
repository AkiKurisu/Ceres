using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Pool;
using Ceres.Resource;
using Cysharp.Threading.Tasks;

namespace Ceres.DataDriven
{
    public abstract class DataTableManager
    {
        private static bool _isLoaded;

        private static readonly Dictionary<Type, DataTableManager> DataTableManagers = new();
        
        public static void Initialize()
        {
            if (_isLoaded) return;
            _isLoaded = true;
            var managerTypes = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(x => x.GetTypes())
                            .Where(x => typeof(DataTableManager).IsAssignableFrom(x) && !x.IsAbstract)
                            .ToArray();

            var args = new object[] { null };
            foreach (var type in managerTypes)
            {
                var manager = (DataTableManager)Activator.CreateInstance(type, args);
                manager!.Initialize(true).Forget();
            }
        }
        
        /// <summary>
        /// Manual initialization api
        /// </summary>
        /// <returns></returns>
        public static async UniTask InitializeAsync()
        {
            if (_isLoaded) return;
            _isLoaded = true;
            var managerTypes = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(x => x.GetTypes())
                            .Where(x => typeof(DataTableManager).IsAssignableFrom(x) && !x.IsAbstract)
                            .ToArray();

            var args = new object[] { null };
            using var parallel = UniParallel.Get();
            foreach (var type in managerTypes)
            {
                var manager = Activator.CreateInstance(type, args) as DataTableManager;
                parallel.Add(manager!.Initialize(false));
            }
            await parallel;
        }

        protected readonly Dictionary<string, DataTable> DataTables = new();

        protected void RegisterDataTable(string name, DataTable dataTable)
        {
            DataTables[name] = dataTable;
        }
        
        protected void RegisterDataTable(DataTable dataTable)
        {
            DataTables[dataTable.name] = dataTable;
        }
        
        public DataTable GetDataTable(string name)
        {
            return DataTables.GetValueOrDefault(name);
        }

        /// <summary>
        /// Free all <see cref="DataTableManager"/> for clearing cache
        /// </summary>
        public static void ReleaseAll()
        {
            _isLoaded = false;
            DataTableManagers.Clear();
        }
        
        /// <summary>
        /// Async initialize manager at start of game, loading your dataTables in this stage
        /// </summary>
        /// <param name="sync">Whether initialize in sync, useful when need blocking loading</param>
        /// <returns></returns>
        protected abstract UniTask Initialize(bool sync);

        /// <summary>
        /// Initialize with loading a single table
        /// </summary>
        /// <param name="tableKey"></param>
        /// <param name="sync"></param>
        protected async UniTask InitializeSingleTable(string tableKey, bool sync)
        {
            try
            {
                if (sync)
                {
                    if (DataDrivenConfig.ValidateDataTableBeforeLoad)
                    {
                        // ReSharper disable once MethodHasAsyncOverload
                        ResourceSystem.EnsureAssetExists<DataTable>(tableKey);
                    }
                    ResourceSystem.LoadAssetAsync<DataTable>(tableKey, dataTable =>
                    {
                        RegisterDataTable(tableKey, dataTable);
                    }).WaitForCompletion();
                    return;
                }

                if (DataDrivenConfig.ValidateDataTableBeforeLoad)
                {
                    await ResourceSystem.EnsureAssetExistsAsync<DataTable>(tableKey);
                }
                await ResourceSystem.LoadAssetAsync<DataTable>(tableKey, dataTable =>
                {
                    RegisterDataTable(tableKey, dataTable);
                });
            }
            catch (InvalidResourceRequestException)
            {

            }
        }

        public static DataTableManager GetOrCreateDataTableManager(Type type)
        {
            if (DataTableManagers.TryGetValue(type, out var dataTableManager))
            {
                return dataTableManager;
            }

            var getMethod = type.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
            return (DataTableManager)getMethod!.Invoke(null, Array.Empty<object>());
        }
        
        public static bool TryGetDataTableManager(Type type, out DataTableManager dataTableManager)
        {
            return DataTableManagers.TryGetValue(type, out dataTableManager);
        }
        
        public static DataTableManager GetDataTableManager(Type type)
        {
            return DataTableManagers.GetValueOrDefault(type);
        }
        
        protected static void RegisterDataTableManager<TManager>(TManager dataTableManager) where TManager: DataTableManager
        {
            DataTableManagers.TryAdd(typeof(TManager), dataTableManager);
        }
    }
    
    public abstract class DataTableManager<TManager> : DataTableManager where TManager : DataTableManager<TManager>
    {
        private static readonly Type ManagerType;

        static DataTableManager()
        {
            ManagerType = typeof(TManager);
        }
        
        // Force implementation has this constructor
        protected DataTableManager(object _)
        {
            RegisterDataTableManager((TManager)this);
        }
        
        /// <summary>
        /// Get <see cref="DataTableManager{TManager}"/> singleton
        /// </summary>
        /// <returns></returns>
        public static TManager Get()
        {
            if (TryGetDataTableManager(ManagerType, out var dataTableManager))
            {
                return dataTableManager as TManager;
            }
            Initialize(); /* Initialize in blocking mode */
            return GetDataTableManager(ManagerType) as TManager;
        }
    }
}
